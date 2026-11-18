using Microsoft.EntityFrameworkCore;
using static Clubs.Access;
namespace Clubs;

public class PortalParticipant : ITenantRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid PersonId { get; set; }
    public DateTimeOffset JoinedAt { get; set; } = DateTimeOffset.UtcNow;
}
public record PortalPersonInput(string Name, string Email);
public record CertificationVerificationInput(long Version);
public partial class Db
{
    public DbSet<PortalParticipant> PortalParticipants => Set<PortalParticipant>();
    void ConfigurePortalParticipants(ModelBuilder b)
    {
        b.Entity<PortalParticipant>().HasIndex(p => new { p.TenantId, p.PersonId }).IsUnique();
        b.Entity<PortalParticipant>().HasOne<Person>().WithMany().HasForeignKey(p => p.PersonId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<PortalParticipant>().HasOne<Tenant>().WithMany().HasForeignKey(p => p.TenantId).OnDelete(DeleteBehavior.Restrict);
    }
}
public static class PortalParticipants
{
    public static async Task<Guid> Tenant(Access a)
    {
        var id = await a.Db.Tenants.Where(t => t.Name == ImpactSetup.Name).Select(t => t.Id).SingleAsync();
        Check(a.BoundTenant == null || a.BoundTenant == id, "Portal unavailable on this domain.", 404);
        return id;
    }
    static async Task<Assignment> Admin(Access a)
    {
        Check((await a.User()).PlatformAdmin, "Platform administrator required.", 403);
        return await a.Workspace("members.view");
    }
    static async Task<Person> Enroll(Access a, Guid tenant, string name, string email, Guid? userId)
    {
        var person = userId == null ? null : await a.Db.Persons.SingleOrDefaultAsync(p => p.UserId == userId);
        if (person == null)
        {
            var matches = await a.Db.Persons.Where(p => p.Contacts.Any(c => c.Kind == "Email" && c.Value.ToLower() == email)).Take(2).ToListAsync();
            Check(matches.Count < 2, "Multiple profiles use this email. Resolve the duplicate profiles before enrolling.", 409);
            person = matches.SingleOrDefault();
            Check(person == null || person.UserId == null || person.UserId == userId, "Email is already associated with another profile.", 409);
        }
        if (person == null)
        {
            person = new() { LegalName = name, DisplayName = name, SearchName = name.ToUpperInvariant(), UserId = userId };
            person.Contacts.Add(new() { Kind = "Email", Label = "Personal", Value = email, NormalizedValue = email, Verified = userId != null });
            a.Db.Persons.Add(person);
        }
        else if (userId != null && person.UserId == null) { person.UserId = userId; person.Version++; }
        if (!await a.Db.PortalParticipants.AnyAsync(p => p.TenantId == tenant && p.PersonId == person.Id))
            a.Db.PortalParticipants.Add(new() { TenantId = tenant, PersonId = person.Id });
        return person;
    }
    public static void MapPortalParticipants(this RouteGroupBuilder api)
    {
        api.MapGet("/portal/me", async (Access a) => {
            var u = await a.User(); var tenant = await Tenant(a);
            var p = await a.Db.Persons.Where(p => p.UserId == u.Id).Select(p => new { p.Id, p.DisplayName }).SingleOrDefaultAsync();
            return Results.Ok(new { person = p, enrolled = p != null && await a.Db.PortalParticipants.AnyAsync(x => x.TenantId == tenant && x.PersonId == p.Id), clubMember = p != null && await a.Db.Memberships.AnyAsync(m => m.PersonId == p.Id && m.TenantId == tenant && m.Status == "Active") });
        });
        api.MapPost("/portal/enroll", async (Access a) => {
            var u = await a.User(); var tenant = await Tenant(a);
            await using var tx = await a.Db.Database.BeginTransactionAsync();
            await a.Db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtext({tenant.ToString()}))");
            var p = await Enroll(a, tenant, u.Name, u.Email, u.Id);
            a.Db.Audits.Add(new() { TenantId = tenant, ActorId = u.Id, Action = "portal.enroll", Resource = p.Id.ToString() });
            await a.Db.SaveChangesAsync(); await tx.CommitAsync(); return Results.Ok(new { p.Id });
        });
        api.MapGet("/platform/participants", async (Access a, string? search = null, Guid? programId = null, string kind = "All", int page = 1) => {
            var w = await Admin(a); var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var q = a.Db.Persons.Where(p => a.Db.PortalParticipants.Any(x => x.TenantId == w.TenantId && x.PersonId == p.Id) || a.Db.MemberCertifications.Any(c => c.TenantId == w.TenantId && c.PersonId == p.Id));
            if (!string.IsNullOrWhiteSpace(search)) { var term = "%" + search.Trim() + "%"; q = q.Where(p => EF.Functions.ILike(p.DisplayName, term) || p.Contacts.Any(c => c.Kind == "Email" && EF.Functions.ILike(c.Value, term))); }
            if (programId != null) q = q.Where(p => a.Db.MemberCertifications.Any(c => c.TenantId == w.TenantId && c.PersonId == p.Id && c.ProgramId == programId));
            Check(kind is "All" or "Club" or "Independent", "Choose a participant type.");
            if (kind != "All") q = q.Where(p => a.Db.Memberships.Any(m => m.PersonId == p.Id && m.TenantId == w.TenantId && m.Status == "Active") == (kind == "Club"));
            var total = await q.CountAsync(); page = Math.Max(1, page);
            var items = await q.OrderBy(p => p.DisplayName).ThenBy(p => p.Id).Skip((page-1)*30).Take(30).Select(p => new {
                p.Id, name = p.DisplayName, email = p.Contacts.Where(c => c.Kind == "Email").Select(c => c.Value).FirstOrDefault(), hasAccount = p.UserId != null,
                clubs = a.Db.Memberships.Where(m => m.PersonId == p.Id && m.TenantId == w.TenantId && m.Status == "Active").Select(m => m.Club.Name).ToArray(),
                certificates = (from c in a.Db.MemberCertifications join t in a.Db.TrainingPrograms on c.ProgramId equals t.Id where c.TenantId == w.TenantId && c.PersonId == p.Id select new { c.Id, program = t.Name, c.CertificateId, c.Batch, c.ValidUntil, c.Version, verified = c.VerifiedAt != null, expired = c.ValidUntil != null && c.ValidUntil < today }).ToArray()
            }).ToListAsync();
            return Results.Ok(new { items, total, page });
        });
        api.MapPost("/platform/participants", async (PortalPersonInput i, Access a) => {
            var w = await Admin(a); Check(i.Name?.Trim().Length is >= 2 and <= 150 && i.Email?.Length <= 254 && System.Net.Mail.MailAddress.TryCreate(i.Email, out _), "Enter a name and valid email.");
            var email = i.Email!.Trim().ToLowerInvariant(); await using var tx = await a.Mutation(w);
            var uid = await a.Db.Users.Where(u => u.Email == email && u.Verified).Select(u => (Guid?)u.Id).SingleOrDefaultAsync();
            var p = await Enroll(a, w.TenantId, i.Name!.Trim(), email, uid);
            await a.Log(w, "portal.participant.add", p.Id.ToString()); await tx.CommitAsync(); return Results.Ok(new { p.Id });
        });
        api.MapPost("/platform/participants/certifications/{id:guid}/verify", async (Guid id, CertificationVerificationInput input, Access a) => {
            var w = await Admin(a); await using var tx = await a.Mutation(w);
            var c = await a.Db.MemberCertifications.SingleOrDefaultAsync(c => c.Id == id && c.TenantId == w.TenantId);
            Check(c != null, "Certificate unavailable.", 404); Check(c.Version == input.Version, "Certificate changed. Refresh before verifying.", 409); c.VerifiedBy = w.UserId; c.VerifiedAt = DateTimeOffset.UtcNow; c.Version++;
            await a.Log(w, "portal.certificate.verify", id.ToString()); await tx.CommitAsync(); return Results.NoContent();
        });
    }
}
