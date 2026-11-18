using Microsoft.EntityFrameworkCore;
using static Clubs.Access;
namespace Clubs;
public record OpportunityInput(string Title,string Body,string Category,string Subcategory,string[] Tags,string City,DateOnly ClosesOn,string Status="Open",long Version=1);
public record OpportunityReplyInput(string Message);
public static class BusinessOpportunityEndpoints
{
    static async Task<Person> Own(Access a,Assignment w)=>await a.Db.Persons.SingleOrDefaultAsync(p=>p.UserId==w.UserId&&NetworkingEndpoints.Active(a.Db,w.TenantId).Any(m=>m.PersonId==p.Id))??throw new ApiError(403,"An active personal membership is required.");
    static IQueryable<BusinessOpportunity> Visible(Db db,Guid tenant)=>db.BusinessOpportunities.Where(o=>o.TenantId==tenant&&o.Status!="Removed"&&NetworkingEndpoints.Active(db,tenant).Any(m=>m.PersonId==o.PersonId));
    static async Task Validate(OpportunityInput i,Db db,Guid tenant){var today=await CommunityGrowthService.Today(db,tenant);Check(i.Title?.Trim().Length is >=5 and <=180&&i.Body?.Trim().Length is >=10 and <=5000&&i.Category?.Trim().Length is >0 and <=200&&i.Subcategory is{Length:<=200}&&i.City is{Length:<=200}&&i.Tags!=null&&i.Tags.Length<=30&&i.Tags.All(t=>t?.Trim().Length is >0 and <=100)&&(i.Status=="Closed"?i.ClosesOn>=today.AddYears(-5):i.ClosesOn>=today)&&i.ClosesOn<=today.AddDays(366)&&i.Status is "Open" or "Closed","Enter a title, requirement, category, up to 30 tags and a closing date within the next year.");}
    public static void MapBusinessOpportunities(this RouteGroupBuilder api)
    {
        api.MapGet("/opportunities",async(Access a,string search="",string category="",bool mine=false,bool matches=false,int page=1)=>{
            var w=await NetworkingEndpoints.Workspace(a);Check(search.Length<=100&&category.Length<=200&&page is >=1 and <=10000,"Invalid opportunity filters.");var today=await CommunityGrowthService.Today(a.Db,w.TenantId);var q=Visible(a.Db,w.TenantId);
            var own=await a.Db.Persons.Include(p=>p.Tags).SingleOrDefaultAsync(p=>p.UserId==w.UserId);var tags=own?.Tags.Where(t=>t.Kind=="Business").Select(t=>t.Value.ToLower()).ToArray()??[];var cat=own?.BusinessCategory.ToLower()??"";
            if(mine)q=q.Where(o=>o.UserId==w.UserId);else q=q.Where(o=>o.Status=="Open"&&o.ClosesOn>=today);
            if(category!="")q=q.Where(o=>o.Category.ToLower()==category.ToLower());if(search.Trim()!=""){var term=search.Trim().ToLower();q=q.Where(o=>o.Title.ToLower().Contains(term)||o.Body.ToLower().Contains(term)||o.Category.ToLower().Contains(term)||o.Tags.Any(t=>t.ToLower().Contains(term)));}
            if(matches)q=q.Where(o=>o.UserId!=w.UserId&&(cat!=""&&o.Category.ToLower()==cat||o.Tags.Any(t=>tags.Contains(t.ToLower()))));
            return new{page,pageSize=20,total=await q.CountAsync(),items=await q.OrderByDescending(o=>o.CreatedAt).ThenBy(o=>o.Id).Skip((page-1)*20).Take(20).Select(o=>new{o.Id,o.Title,o.Body,o.Category,o.Subcategory,o.Tags,o.City,o.ClosesOn,o.Status,o.Version,o.CreatedAt,o.PersonId,author=a.Db.Persons.Where(p=>p.Id==o.PersonId).Select(p=>p.DisplayName).First(),canManage=o.UserId==w.UserId,responded=a.Db.OpportunityResponses.Any(r=>r.OpportunityId==o.Id&&r.UserId==w.UserId),responses=a.Db.OpportunityResponses.Count(r=>r.OpportunityId==o.Id)}).ToListAsync()};
        });
        api.MapPost("/opportunities",async(OpportunityInput i,Access a)=>{
            var w=await NetworkingEndpoints.Workspace(a);var person=await Own(a,w);await Validate(i,a.Db,w.TenantId);await using var tx=await a.Mutation(w);
            Check(await a.Db.BusinessOpportunities.CountAsync(o=>o.UserId==w.UserId&&o.CreatedAt>DateTimeOffset.UtcNow.AddHours(-1))<5,"Post at most five opportunities per hour.",429);
            var o=new BusinessOpportunity{TenantId=w.TenantId,UserId=w.UserId,PersonId=person.Id,OrgId=w.OrgId};Apply(o,i);a.Db.BusinessOpportunities.Add(o);await a.Log(w,"opportunity.created",o.Id.ToString());await tx.CommitAsync();return new{o.Id,o.Version};
        });
        api.MapPut("/opportunities/{id:guid}",async(Guid id,OpportunityInput i,Access a)=>{
            var s=await CommunityEndpoints.GetScope(a);await using var tx=await a.Mutation(s.W);var o=await a.Db.BusinessOpportunities.SingleOrDefaultAsync(o=>o.Id==id&&o.TenantId==s.W.TenantId);Check(o!=null&&(o.UserId==s.W.UserId||CommunityEndpoints.CanModerate(s,o.OrgId)),"Opportunity management is unavailable.",403);Check(o.Version==i.Version,"Opportunity changed. Refresh.",409);await Validate(i,a.Db,s.W.TenantId);Apply(o,i);o.Version++;await a.Log(s.W,"opportunity.updated",id.ToString());await tx.CommitAsync();return new{o.Version};
        });
        api.MapDelete("/opportunities/{id:guid}",async(Guid id,Access a)=>{
            var s=await CommunityEndpoints.GetScope(a);await using var tx=await a.Mutation(s.W);var o=await a.Db.BusinessOpportunities.SingleOrDefaultAsync(o=>o.Id==id&&o.TenantId==s.W.TenantId);Check(o!=null&&(o.UserId==s.W.UserId||CommunityEndpoints.CanModerate(s,o.OrgId)),"Opportunity management is unavailable.",403);o.Status="Removed";o.Version++;await a.Log(s.W,"opportunity.removed",id.ToString());await tx.CommitAsync();return Results.NoContent();
        });
        api.MapPost("/opportunities/{id:guid}/respond",async(Guid id,OpportunityReplyInput i,Access a)=>{
            var w=await NetworkingEndpoints.Workspace(a);var person=await Own(a,w);Check(i.Message?.Trim().Length is >=5 and <=1000,"Introduce yourself in 5–1,000 characters.");var today=await CommunityGrowthService.Today(a.Db,w.TenantId);await using var tx=await a.Mutation(w);
            var o=await Visible(a.Db,w.TenantId).SingleOrDefaultAsync(o=>o.Id==id&&o.Status=="Open"&&o.ClosesOn>=today);Check(o!=null&&o.UserId!=w.UserId,"Open opportunity unavailable.",404);Check(!await a.Db.OpportunityResponses.AnyAsync(r=>r.OpportunityId==id&&r.UserId==w.UserId),"You already responded to this opportunity.",409);
            a.Db.OpportunityResponses.Add(new(){TenantId=w.TenantId,OpportunityId=id,UserId=w.UserId,PersonId=person.Id,Message=i.Message.Trim()});await a.Log(w,"opportunity.response.sent",id.ToString());await tx.CommitAsync();return Results.NoContent();
        });
        api.MapGet("/opportunities/{id:guid}/responses",async(Guid id,Access a)=>{
            var w=await NetworkingEndpoints.Workspace(a);Check(await a.Db.BusinessOpportunities.AnyAsync(o=>o.Id==id&&o.TenantId==w.TenantId&&o.UserId==w.UserId),"Only the opportunity owner can read responses.",403);
            return await a.Db.OpportunityResponses.Where(r=>r.OpportunityId==id).OrderByDescending(r=>r.CreatedAt).Take(200).Select(r=>new{r.Id,r.PersonId,r.Message,r.CreatedAt,name=a.Db.Persons.Where(p=>p.Id==r.PersonId).Select(p=>p.DisplayName).First()}).ToListAsync();
        });
        api.MapGet("/community/recommendations",async(Access a,Guid? opportunityId=null)=>{
            var w=await NetworkingEndpoints.Workspace(a);var own=await a.Db.Persons.Include(p=>p.Tags).Include(p=>p.Addresses).SingleOrDefaultAsync(p=>p.UserId==w.UserId);
            var business=own?.Tags.Where(t=>t.Kind=="Business").Select(t=>t.Value.ToLower()).ToArray()??[];var interests=own?.Tags.Where(t=>t.Kind is "Skill" or "Interest" or "Expertise").Select(t=>t.Value.ToLower()).ToArray()??[];
            var category=own?.BusinessCategory.ToLower()??"";var subcategory=own?.BusinessSubcategory.ToLower()??"";var city=own?.Addresses.OrderByDescending(a=>a.Type=="Office").Select(a=>a.City.ToLower()).FirstOrDefault()??"";
            if(opportunityId!=null){var o=await Visible(a.Db,w.TenantId).SingleOrDefaultAsync(o=>o.Id==opportunityId);Check(o!=null,"Opportunity unavailable.",404);category=o.Category.ToLower();subcategory=o.Subcategory.ToLower();city=o.City.ToLower();business=o.Tags.Select(t=>t.ToLower()).ToArray();interests=[];}
            var candidates=CommunityGrowthService.Discoverable(a.Db,w.TenantId).Where(p=>p.UserId!=w.UserId).Select(p=>new{p.Id,p.DisplayName,p.Company,p.BusinessCategory,p.BusinessSubcategory,p.WorkLocation,category=category!=""&&p.BusinessCategory.ToLower()==category,subcategory=subcategory!=""&&p.BusinessSubcategory.ToLower()==subcategory,business=p.Tags.Count(t=>t.Kind=="Business"&&business.Contains(t.Value.ToLower())),shared=p.Tags.Count(t=>new[]{"Skill","Interest","Expertise"}.Contains(t.Kind)&&interests.Contains(t.Value.ToLower())),city=city!=""&&p.Addresses.Any(a=>a.City.ToLower()==city)});
            var result=await candidates.OrderByDescending(p=>(p.category?6:0)+(p.subcategory?4:0)+p.business*2+p.shared+(p.city?1:0)).ThenBy(p=>p.DisplayName).ThenBy(p=>p.Id).Take(24).ToListAsync();
            return result.Select(p=>new{p.Id,p.DisplayName,p.Company,p.BusinessCategory,p.BusinessSubcategory,p.WorkLocation,score=(p.category?6:0)+(p.subcategory?4:0)+p.business*2+p.shared+(p.city?1:0),reasons=new[]{p.category?"Matching business category":null,p.subcategory?"Matching specialty":null,p.business>0?p.business+" matching business tags":null,p.shared>0?p.shared+" shared skills or interests":null,p.city?"Same city":null}.Where(r=>r!=null).ToArray()}).Where(p=>p.score>0).ToArray();
        });
    }
    static void Apply(BusinessOpportunity o,OpportunityInput i){o.Title=i.Title.Trim();o.Body=i.Body.Trim();o.Category=i.Category.Trim();o.Subcategory=i.Subcategory.Trim();o.Tags=i.Tags.Select(t=>t.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();o.City=i.City.Trim();o.ClosesOn=i.ClosesOn;o.Status=i.Status;}
}
