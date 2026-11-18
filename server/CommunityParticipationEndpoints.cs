using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using static Clubs.Access;
namespace Clubs;
public record ParticipationConsent(string Decision,bool PublicName=false,long Version=1);
public record AttendanceVerification(bool Verified,string Evidence,string Role="Participant",decimal Hours=0,long Version=1);
public record CertificateRevokeInput(string Reason);
public static partial class CommunityGrowthEndpoints
{
    static void MapParticipation(RouteGroupBuilder api)
    {
        api.MapGet("/community/participation/me",async(Access a,IDataProtectionProvider protection)=>{
            var s=await CommunityEndpoints.GetScope(a);var person=await a.Db.Persons.Where(p=>p.UserId==s.W.UserId).Select(p=>(Guid?)p.Id).SingleOrDefaultAsync();
            var rows=await a.Db.ActivityParticipations.Where(t=>t.TenantId==s.W.TenantId&&t.PersonId==person&&t.Consent!="Removed"&&a.Db.CommunityPosts.Any(p=>p.Id==t.PostId&&p.Status!="Archived")).OrderByDescending(t=>t.CreatedAt).Select(t=>new{t.Id,t.PostId,t.Consent,t.PublicName,t.Verified,t.Role,t.Hours,t.Version,title=a.Db.CommunityPosts.Where(p=>p.Id==t.PostId).Select(p=>p.Title).First(),visibility=a.Db.CommunityPosts.Where(p=>p.Id==t.PostId).Select(p=>p.Visibility).First(),status=a.Db.CommunityPosts.Where(p=>p.Id==t.PostId).Select(p=>p.Status).First()}).ToListAsync();
            var eligible=rows.Where(t=>t.Verified&&t.Consent=="Accepted"&&t.status=="Published").ToList();
            var certificates=await a.Db.ActivityCertificates.Where(c=>c.TenantId==s.W.TenantId&&c.PersonId==person).OrderByDescending(c=>c.IssuedAt).ToListAsync();
            var protect=protection.CreateProtector("Impact.ActivityCertificate.v1");
            return new{items=rows,milestones=new[]{new{title="First contribution",target=1,current=eligible.Count,earned=eligible.Count>=1},new{title="Community contributor",target=5,current=eligible.Count,earned=eligible.Count>=5},new{title="Impact champion",target=10,current=eligible.Count,earned=eligible.Count>=10}},verifiedHours=eligible.Sum(t=>t.Hours),certificates=certificates.Select(c=>new{c.Id,c.Number,c.ActivitySnapshot,c.Status,c.IssuedAt,verificationUrl="/activity-certificate/"+protect.Unprotect(c.ProtectedToken)})};
        });
        api.MapPost("/community/participation/{id:guid}/consent",async(Guid id,ParticipationConsent i,Access a)=>{
            var s=await CommunityEndpoints.GetScope(a);await using var tx=await a.Mutation(s.W);var t=await a.Db.ActivityParticipations.SingleOrDefaultAsync(t=>t.Id==id&&t.TenantId==s.W.TenantId&&t.Consent!="Removed"&&a.Db.Persons.Any(p=>p.Id==t.PersonId&&p.UserId==s.W.UserId));
            Check(t!=null,"Participation invitation unavailable.",404);Check(t.Version==i.Version,"Participation changed. Refresh.",409);Check(i.Decision is "Accepted" or "Declined","Accept or decline this participation tag.");
            t.Consent=i.Decision;t.PublicName=i.Decision=="Accepted"&&i.PublicName;t.Version++;
            if(i.Decision=="Declined")await a.Db.ActivityCertificates.Where(c=>c.ParticipationId==id&&c.Status=="Issued").ExecuteUpdateAsync(x=>x.SetProperty(c=>c.Status,"Revoked").SetProperty(c=>c.RevocationReason,"Member withdrew participation consent"));
            await a.Log(s.W,"community.participation.consent",id.ToString());await tx.CommitAsync();return new{t.Version};
        });
        api.MapGet("/community/posts/{id:guid}/participants",async(Guid id,Access a)=>{
            var s=await CommunityEndpoints.GetScope(a);await CommunityGrowthService.Editable(id,a,s);
            return await a.Db.ActivityParticipations.Where(t=>t.PostId==id&&t.Consent!="Removed").OrderBy(t=>t.CreatedAt).Select(t=>new{t.Id,t.PersonId,t.Consent,t.Verified,t.Evidence,t.Role,t.Hours,t.Version,name=a.Db.Persons.Where(p=>p.Id==t.PersonId).Select(p=>p.DisplayName).First(),certificateStatus=a.Db.ActivityCertificates.Where(c=>c.ParticipationId==t.Id).Select(c=>c.Status).SingleOrDefault(),certificateId=a.Db.ActivityCertificates.Where(c=>c.ParticipationId==t.Id).Select(c=>(Guid?)c.Id).SingleOrDefault()}).ToListAsync();
        });
        api.MapPost("/community/participation/{id:guid}/verify",async(Guid id,AttendanceVerification i,Access a)=>{
            var s=await CommunityEndpoints.GetScope(a);await using var tx=await a.Mutation(s.W);var t=await a.Db.ActivityParticipations.SingleOrDefaultAsync(t=>t.Id==id&&t.TenantId==s.W.TenantId);Check(t!=null,"Participation unavailable.",404);var post=await CommunityGrowthService.Editable(t.PostId,a,s);
            Check(t.Version==i.Version,"Participation changed. Refresh.",409);Check(post.ActivityOn<=await CommunityGrowthService.Today(a.Db,s.W.TenantId),"Attendance cannot be verified for a future activity.");
            Check(i.Evidence?.Trim().Length is >=5 and <=1000&&new[]{"Participant","Volunteer","Speaker","Organizer"}.Contains(i.Role)&&i.Hours is >=0 and <=1000&&decimal.Round(i.Hours,2)==i.Hours,"Record attendance evidence, a supported role and valid hours.");
            t.Verified=i.Verified;t.VerifiedBy=s.W.UserId;t.Evidence=i.Evidence.Trim();t.Role=i.Role;t.Hours=i.Hours;t.Version++;
            if(!i.Verified)await a.Db.ActivityCertificates.Where(c=>c.ParticipationId==id&&c.Status=="Issued").ExecuteUpdateAsync(x=>x.SetProperty(c=>c.Status,"Revoked").SetProperty(c=>c.RevocationReason,"Attendance verification withdrawn"));
            await a.Log(s.W,"community.attendance.verified",id.ToString(),t.Evidence);await tx.CommitAsync();return new{t.Version};
        });
        api.MapPost("/community/participation/{id:guid}/certificate",async(Guid id,Access a,IDataProtectionProvider protection)=>{
            var s=await CommunityEndpoints.GetScope(a);await using var tx=await a.Mutation(s.W);var t=await a.Db.ActivityParticipations.SingleOrDefaultAsync(t=>t.Id==id&&t.TenantId==s.W.TenantId);Check(t!=null,"Participation unavailable.",404);var post=await CommunityGrowthService.Editable(t.PostId,a,s);
            Check(post.Status=="Published"&&t.Verified&&t.Consent=="Accepted"&&post.ActivityOn<=await CommunityGrowthService.Today(a.Db,s.W.TenantId),"Certificates require a published activity, member consent and verified attendance.",409);
            var c=await a.Db.ActivityCertificates.SingleOrDefaultAsync(c=>c.ParticipationId==id);if(c?.Status=="Issued")return Results.Ok(new{c.Id,c.Number});
            if(c==null){c=new(){TenantId=s.W.TenantId,ParticipationId=id,PersonId=t.PersonId};a.Db.ActivityCertificates.Add(c);}
            var token=Token();c.Number="IMPACT-"+DateTimeOffset.UtcNow.ToString("yyyy")+"-"+c.Id.ToString("N")[..12].ToUpperInvariant();c.TokenHash=Hash(token);c.ProtectedToken=protection.CreateProtector("Impact.ActivityCertificate.v1").Protect(token);c.NameSnapshot=await a.Db.Persons.Where(p=>p.Id==t.PersonId).Select(p=>p.DisplayName).SingleAsync();c.ActivitySnapshot=post.Title;c.ActivityOn=post.ActivityOn;c.OrganizationSnapshot=await a.Db.Orgs.Where(o=>o.Id==post.OrgId).Select(o=>o.Name).SingleAsync();c.IssuedBy=s.W.UserId;c.Status="Issued";c.RevocationReason="";c.IssuedAt=DateTimeOffset.UtcNow;
            await a.Log(s.W,"community.certificate.issued",c.Id.ToString());await tx.CommitAsync();return Results.Ok(new{c.Id,c.Number});
        });
        api.MapPost("/community/certificates/{id:guid}/revoke",async(Guid id,CertificateRevokeInput i,Access a)=>{
            var s=await CommunityEndpoints.GetScope(a);await using var tx=await a.Mutation(s.W);var c=await a.Db.ActivityCertificates.SingleOrDefaultAsync(c=>c.Id==id&&c.TenantId==s.W.TenantId);Check(c!=null,"Certificate unavailable.",404);var t=await a.Db.ActivityParticipations.SingleAsync(t=>t.Id==c.ParticipationId);await CommunityGrowthService.Editable(t.PostId,a,s);Check(i.Reason?.Trim().Length is >=5 and <=1000,"Explain the revocation.");c.Status="Revoked";c.RevocationReason=i.Reason.Trim();await a.Log(s.W,"community.certificate.revoked",id.ToString(),c.RevocationReason);await tx.CommitAsync();return Results.NoContent();
        });
        api.MapGet("/community/certificates/{id:guid}/download",async(Guid id,Access a,IDataProtectionProvider protection,IConfiguration config)=>{
            var s=await CommunityEndpoints.GetScope(a);var c=await a.Db.ActivityCertificates.SingleOrDefaultAsync(c=>c.Id==id&&c.TenantId==s.W.TenantId&&c.Status=="Issued");Check(c!=null,"Issued certificate unavailable.",404);var t=await a.Db.ActivityParticipations.SingleAsync(t=>t.Id==c.ParticipationId);var post=await a.Db.CommunityPosts.SingleAsync(p=>p.Id==t.PostId);
            Check(post.Status=="Published"&&t.Verified&&t.Consent=="Accepted","Certificate is no longer eligible.",409);Check(await a.Db.Persons.AnyAsync(p=>p.Id==c.PersonId&&p.UserId==s.W.UserId)||CommunityEndpoints.CanModerate(s,post.OrgId),"Certificate belongs to another member.",403);
            string Short(string value)=>value.Length>75?value[..72]+"…":value;
            var layout=new CertificateLayout(Elements:[new("IMPACT INTERNATIONAL",50,13,24,"Center","#123747"),new("CERTIFICATE OF PARTICIPATION",50,27,28,"Center","#123747"),new("Presented to",50,40,14,"Center","#607080"),new(Short(c.NameSnapshot),50,48,c.NameSnapshot.Length>55?17:c.NameSnapshot.Length>38?22:27,"Center","#123747"),new(Short(c.ActivitySnapshot),50,61,17,"Center","#123747"),new(Short(c.OrganizationSnapshot)+" · "+c.ActivityOn,50,70,13,"Center","#607080"),new(c.Number,50,82,11,"Center","#607080")]);
            var token=protection.CreateProtector("Impact.ActivityCertificate.v1").Unprotect(c.ProtectedToken);var origin=(config["PublicOrigin"]??"").TrimEnd('/');var bytes=OperationsEndpoints.CertificatePdf(layout,new(),origin+"/activity-certificate/"+token);
            return Results.File(bytes,"application/pdf",c.Number+".pdf");
        });
        api.MapGet("/public/activity-certificates/{token}",async(string token,Access a)=>{
            Check(token.Length is >=32 and <=200,"Certificate unavailable.",404);var c=await a.Db.ActivityCertificates.AsNoTracking().SingleOrDefaultAsync(c=>c.TokenHash==Hash(token));Check(c!=null&&(a.BoundTenant==null||a.BoundTenant==c.TenantId),"Certificate unavailable.",404);
            var t=await a.Db.ActivityParticipations.SingleAsync(t=>t.Id==c.ParticipationId);var p=await a.Db.CommunityPosts.SingleAsync(p=>p.Id==t.PostId);var valid=c.Status=="Issued"&&t.Verified&&t.Consent=="Accepted"&&p.Status=="Published";
            return new{c.Number,status=c.Status=="Revoked"?"Revoked":valid?"Valid":"Unavailable",c.ActivityOn,c.IssuedAt,organization=c.OrganizationSnapshot,activity=p.Visibility=="Public"?c.ActivitySnapshot:"Private activity",recipient=t.PublicName&&t.Consent=="Accepted"?c.NameSnapshot:null};
        });
        api.MapGet("/community/people/{personId:guid}/participation",async(Guid personId,Access a)=>{
            var s=await CommunityEndpoints.GetScope(a);Check(await a.Db.Persons.AnyAsync(p=>p.Id==personId&&(p.UserId==s.W.UserId||p.Privacy.DirectoryVisible)&&NetworkingEndpoints.Active(a.Db,s.W.TenantId).Any(m=>m.PersonId==p.Id)),"Member participation unavailable.",404);
            var q=CommunityEndpoints.Posts(a.Db,s);return await a.Db.ActivityParticipations.Where(t=>t.PersonId==personId&&t.TenantId==s.W.TenantId&&t.Consent=="Accepted"&&q.Any(p=>p.Id==t.PostId)).OrderByDescending(t=>t.CreatedAt).Select(t=>new{t.PostId,t.Role,t.Hours,t.Verified,title=a.Db.CommunityPosts.Where(p=>p.Id==t.PostId).Select(p=>p.Title).First(),date=a.Db.CommunityPosts.Where(p=>p.Id==t.PostId).Select(p=>p.ActivityOn).First()}).Take(100).ToListAsync();
        });
    }
}
