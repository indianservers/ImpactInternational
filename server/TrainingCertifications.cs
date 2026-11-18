using Microsoft.EntityFrameworkCore;
using static Clubs.Access;
namespace Clubs;

public class TrainingProgram : ITenantRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public bool Active { get; set; } = true;
    public long Version { get; set; } = 1;
}
public class MemberCertification : ITenantRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid PersonId { get; set; }
    public Guid ProgramId { get; set; }
    public Guid? VerifiedBy { get; set; }
    public DateTimeOffset? VerifiedAt { get; set; }
    public string CertificateId { get; set; } = "";
    public string Batch { get; set; } = "";
    public string Issuer { get; set; } = "";
    public string TrainingCenter { get; set; } = "";
    public DateOnly? IssuedOn { get; set; }
    public DateOnly? ValidUntil { get; set; }
    public string Notes { get; set; } = "";
    public string VerificationUrl { get; set; } = "";
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public long Version { get; set; } = 1;
}
public record TrainingProgramInput(string Name, string Description = "", bool Active = true, long Version = 1);
public record CertificationInput(Guid ProgramId, string CertificateId, string Batch = "", string Issuer = "",
    string TrainingCenter = "", DateOnly? IssuedOn = null, DateOnly? ValidUntil = null,
    string Notes = "", string VerificationUrl = "", long Version = 1);

public partial class Db
{
    public DbSet<TrainingProgram> TrainingPrograms => Set<TrainingProgram>();
    public DbSet<MemberCertification> MemberCertifications => Set<MemberCertification>();
    void ConfigureTraining(ModelBuilder b)
    {
        b.Entity<TrainingProgram>().HasIndex(x => new { x.TenantId, x.Name }).IsUnique();
        b.Entity<TrainingProgram>().Property(x => x.Version).IsConcurrencyToken();
        b.Entity<MemberCertification>().HasIndex(x => new { x.TenantId, x.ProgramId, x.CertificateId }).IsUnique();
        b.Entity<MemberCertification>().HasIndex(x => new { x.TenantId, x.PersonId });
        b.Entity<MemberCertification>().Property(x => x.Version).IsConcurrencyToken();
        b.Entity<MemberCertification>().HasOne<TrainingProgram>().WithMany()
            .HasForeignKey(x => new { x.TenantId, x.ProgramId }).HasPrincipalKey(x => new { x.TenantId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.Entity<MemberCertification>().HasOne<Person>().WithMany().HasForeignKey(x => x.PersonId).OnDelete(DeleteBehavior.Restrict);
    }
}
public static class TrainingCertifications
{
    public static async Task Apply(Db db, Guid tenant, Guid person, CertificationInput input, MemberCertification row)
    {
        row.VerifiedBy = null; row.VerifiedAt = null;
        Check(input.CertificateId is { Length: > 0 and <= 120 } && input.CertificateId.Trim().Length > 0
            && input.Batch is { Length: <= 120 } && input.Issuer is { Length: <= 200 }
            && input.TrainingCenter is { Length: <= 200 } && input.Notes is { Length: <= 4000 }
            && input.VerificationUrl is { Length: <= 1000 }, "Enter a certificate ID and valid certification details.");
        Check(input.ValidUntil == null || input.IssuedOn == null || input.ValidUntil >= input.IssuedOn, "Valid till cannot precede the issue date.");
        Check(input.VerificationUrl == "" || Uri.TryCreate(input.VerificationUrl, UriKind.Absolute, out var url) && url.Scheme == "https", "Use an HTTPS verification URL.");
        Check(await db.TrainingPrograms.AnyAsync(p => p.Id == input.ProgramId && p.TenantId == tenant && (p.Active || p.Id == row.ProgramId)), "Choose an active certification program.");
        var number = input.CertificateId.Trim().ToUpperInvariant();
        Check(!await db.MemberCertifications.AnyAsync(c => c.TenantId == tenant && c.ProgramId == input.ProgramId && c.CertificateId == number && c.Id != row.Id), "This certificate ID is already recorded for this program.", 409);
        row.TenantId = tenant; row.PersonId = person; row.ProgramId = input.ProgramId;
        row.CertificateId = number; row.Batch = input.Batch.Trim(); row.Issuer = input.Issuer.Trim();
        row.TrainingCenter = input.TrainingCenter.Trim(); row.IssuedOn = input.IssuedOn; row.ValidUntil = input.ValidUntil;
        row.Notes = input.Notes; row.VerificationUrl = input.VerificationUrl; row.Version++; row.UpdatedAt = DateTimeOffset.UtcNow;
    }
    static async Task<(Guid TenantId, Guid PersonId, Assignment? Workspace)> Scope(MembershipService s, Guid? personId, bool write)
    {
        var user = await s.Access.User();
        var tenant = await PortalParticipants.Tenant(s.Access);
        var own = await s.Db.Persons.SingleOrDefaultAsync(p => p.UserId == user.Id);
        if (own != null && (personId == null || personId == own.Id))
        {
            Check(await s.Db.PortalParticipants.AnyAsync(p => p.TenantId == tenant && p.PersonId == own.Id)
                || await s.Db.Memberships.AnyAsync(m => m.PersonId == own.Id && m.TenantId == tenant), "Join the certification portal first.", 403);
            return (tenant, own.Id, null);
        }
        var w = await s.Access.Workspace(write ? "members.manage" : "members.view");
        Check(personId != null && (await s.Scoped(w).AnyAsync(m => m.PersonId == personId)
            || user.PlatformAdmin && await s.Db.PortalParticipants.AnyAsync(p => p.TenantId == w.TenantId && p.PersonId == personId)), "Person is outside your scope.", 404);
        return (w.TenantId, personId!.Value, w);
    }
    static async Task Audit(MembershipService s, Guid tenant, Assignment? w, string action, Guid id)
    {
        s.Db.Audits.Add(new() { TenantId = tenant, OrgId = w?.OrgId, ActorId = (await s.Access.User()).Id, Action = action, Resource = id.ToString() });
        await s.Db.SaveChangesAsync();
    }
    public static void MapTrainingCertifications(this RouteGroupBuilder api)
    {
        api.MapGet("/certifications", async (Access a) => {
            await a.User();
            var tenant = await PortalParticipants.Tenant(a);
            return await a.Db.TrainingPrograms.Where(p => p.TenantId == tenant).OrderBy(p => p.Name).ToListAsync();
        });
        api.MapPost("/certifications", async (TrainingProgramInput input, Access a) => await Program(null, input, a));
        api.MapPut("/certifications/{id:guid}", async (Guid id, TrainingProgramInput input, Access a) => await Program(id, input, a));
        api.MapGet("/member-certifications", async (MembershipService s, Guid? personId = null) => {
            var (tenant, person, w) = await Scope(s, personId, false);
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            return await (from c in s.Db.MemberCertifications join p in s.Db.TrainingPrograms on c.ProgramId equals p.Id
                where c.TenantId == tenant && c.PersonId == person orderby c.UpdatedAt descending
                select new { certification = c, program = p.Name, status = c.ValidUntil != null && c.ValidUntil < today ? "Expired" : "Current", verification = c.VerifiedAt != null ? "Platform verified" : "Self reported" }).ToListAsync();
        });
        api.MapPost("/member-certifications", async (CertificationInput input, MembershipService s, Guid? personId = null) => await Save(null, input, s, personId));
        api.MapPut("/member-certifications/{id:guid}", async (Guid id, CertificationInput input, MembershipService s, Guid? personId = null) => await Save(id, input, s, personId));
        api.MapDelete("/member-certifications/{id:guid}", async (Guid id, MembershipService s, Guid? personId = null) => {
            var (tenant, person, w) = await Scope(s, personId, true);
            var row = await s.Db.MemberCertifications.SingleOrDefaultAsync(c => c.Id == id && c.TenantId == tenant && c.PersonId == person);
            Check(row != null, "Certification not found.", 404);
            s.Db.MemberCertifications.Remove(row!); await Audit(s, tenant, w, "member.certification.delete", id); await s.Db.SaveChangesAsync(); return Results.NoContent();
        });
    }
    static async Task<TrainingProgram> Program(Guid? id, TrainingProgramInput input, Access a)
    {
        var w = await a.Workspace("organization.view");
        Check((await a.User()).PlatformAdmin, "Only Platform Admin can manage certification programs.", 403);
        Check(input.Name is { Length: >= 2 and <= 160 } && input.Description is { Length: <= 4000 }, "Enter a valid program name and description.");
        var row = id == null ? new TrainingProgram { TenantId = w.TenantId } : await a.Db.TrainingPrograms.SingleOrDefaultAsync(p => p.Id == id && p.TenantId == w.TenantId);
        Check(row != null, "Program not found.", 404); Check(id == null || row!.Version == input.Version, "Program changed. Reload and retry.", 409);
        Check(!await a.Db.TrainingPrograms.AnyAsync(p => p.TenantId == w.TenantId && p.Id != row!.Id && p.Name.ToLower() == input.Name.Trim().ToLower()), "Program name already exists.", 409);
        row!.Name = input.Name.Trim(); row.Description = input.Description; row.Active = input.Active; row.Version++;
        if (id == null) a.Db.TrainingPrograms.Add(row);
        await a.Log(w, "certification.program.save", row.Id.ToString()); await a.Db.SaveChangesAsync(); return row;
    }
    static async Task<MemberCertification> Save(Guid? id, CertificationInput input, MembershipService s, Guid? personId)
    {
        var (tenant, person, w) = await Scope(s, personId, true);
        var row = id == null ? new MemberCertification() : await s.Db.MemberCertifications.SingleOrDefaultAsync(c => c.Id == id && c.PersonId == person && c.TenantId == tenant);
        Check(row != null, "Certification not found.", 404); Check(id == null || row!.Version == input.Version, "Certification changed. Reload and retry.", 409);
        await Apply(s.Db, tenant, person, input, row!); if (id == null) s.Db.MemberCertifications.Add(row!);
        await Audit(s, tenant, w, "member.certification.save", row!.Id); await s.Db.SaveChangesAsync(); return row;
    }
}
