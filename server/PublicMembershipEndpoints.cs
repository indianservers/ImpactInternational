using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using static Clubs.Access;

namespace Clubs;

public static class PublicMembershipEndpoints
{
    public const string WorkflowKind = "PublicSignup";
    public record SignupInput(string Name, string Email, string Phone, string Password,
        string Confirmation, bool Agreed, Guid RegionId, Guid ClubId, Guid NationalId, string Statement = "");
    sealed record PublicClub(Guid Id, string Name, string Code, string City, string Country,
        Guid RegionId, string Region, Guid NationalId, string National);

    static async Task<(Guid Tenant, List<Org> Units, Dictionary<Guid, Level> Levels)> Structure(Access a)
    {
        var tenant = a.BoundTenant ?? await a.Db.Tenants.Where(t => t.Name == ImpactSetup.Name)
            .Select(t => (Guid?)t.Id).SingleOrDefaultAsync();
        Check(tenant != null, "Public membership signup is unavailable for this association.", 404);
        var units = await a.Db.Orgs.AsNoTracking().Where(o => o.TenantId == tenant).Take(15001).ToListAsync();
        Check(units.Count <= 15000, "Public membership directory is temporarily unavailable.", 503);
        var levels = await a.Db.Levels.AsNoTracking().Where(l => l.TenantId == tenant).ToDictionaryAsync(l => l.Id);
        return (tenant.Value, units, levels);
    }

    static List<PublicClub> Clubs(List<Org> units, Dictionary<Guid, Level> levels)
    {
        var index = units.ToDictionary(o => o.Id); var result = new List<PublicClub>();
        foreach (var club in units.Where(o => levels[o.LevelId].IsClub && o.Status == "Active"))
        {
            var chain = new List<Org>(); var seen = new HashSet<Guid>(); Org? cursor = club;
            while (cursor != null && cursor.Status == "Active" && seen.Add(cursor.Id))
            {
                chain.Add(cursor);
                cursor = cursor.ParentId == null ? null : index.GetValueOrDefault(cursor.ParentId.Value);
            }
            // An inactive, missing or cyclic ancestor must never make a club publicly selectable.
            if (cursor != null || chain[^1].ParentId != null) continue;
            var region = chain.FirstOrDefault(o => levels[o.LevelId].Name == "Region");
            var national = chain.FirstOrDefault(o => levels[o.LevelId].Name == "National");
            if (region == null || national == null) continue;
            result.Add(new(club.Id, club.Name, club.Code, club.City, club.Country, region.Id, region.Name, national.Id, national.Name));
        }
        return result;
    }

    public static void MapPublicMembership(this RouteGroupBuilder api)
    {
        api.MapGet("/public/membership-options", async (Access a, Guid? regionId = null, Guid? nationalId = null, string search = "", int page = 1) =>
        {
            Check(search.Length <= 100 && page is >= 1 and <= 10000, "Invalid directory search.");
            var (tenant, units, levels) = await Structure(a);
            var clubs = Clubs(units, levels);
            var nationals = clubs.GroupBy(c => c.NationalId).Select(g => new { id = g.Key, name = g.First().National, regionCount = g.Select(c => c.RegionId).Distinct().Count(), clubCount = g.Count() }).OrderBy(x => x.name).ToList();
            if (nationalId != null) Check(nationals.Any(x => x.id == nationalId), "Choose an available national unit.", 404);
            var regions = clubs.Where(c => nationalId == null || c.NationalId == nationalId).GroupBy(c => c.RegionId)
                .Select(g => new { id = g.Key, name = g.First().Region, nationalId = g.First().NationalId, clubCount = g.Count() }).OrderBy(x => x.name).ToList();
            if (regionId != null) Check(regions.Any(x => x.id == regionId), "Choose an available region.", 404);
            var filtered = clubs.Where(c => (regionId == null || c.RegionId == regionId) && (nationalId == null || c.NationalId == nationalId));
            if (!string.IsNullOrWhiteSpace(search)) filtered = filtered.Where(c => new[] { c.Name, c.Code, c.City, c.Region, c.National }.Any(s => s.Contains(search.Trim(), StringComparison.OrdinalIgnoreCase)));
            var category = await a.Db.MembershipCategories.Where(c => c.TenantId == tenant && c.Active).OrderBy(c => c.Code == "REG" ? 0 : 1).ThenBy(c => c.Name).Select(c => new { c.Name }).FirstOrDefaultAsync();
            return Results.Ok(new { nationals, regions, availableRegions = regions.Count, total = filtered.Count(), availableClubs = clubs.Count, page, pageSize = 50,
                clubs = filtered.OrderBy(c => c.Name).ThenBy(c => c.Id).Skip((page - 1) * 50).Take(50), category = category?.Name, signupAvailable = category != null && clubs.Count > 0 });
        });

        api.MapPost("/public/membership-signup", async (SignupInput input, MembershipService s,
            IPasswordHasher<User> passwords, NotificationService notifications) =>
        {
            Check(input.Agreed && (input.Name?.Trim().Length ?? 0) is >= 2 and <= 200
                && (input.Email?.Length ?? 0) is > 0 and <= 200
                && System.Net.Mail.MailAddress.TryCreate(input.Email?.Trim(), out var address)
                && address.Address.Equals(input.Email!.Trim(), StringComparison.OrdinalIgnoreCase)
                && (input.Password?.Length ?? 0) is >= 12 and <= 200 && input.Password == input.Confirmation
                && input.Phone is { Length: >= 7 and <= 30 } && input.Statement is { Length: <= 4000 },
                "Enter your name, valid email, mobile number, matching 12-character passwords and consent.");
            var email = input.Email!.Trim().ToLowerInvariant();
            var personInput = new PersonInput(input.Name!.Trim(), input.Name.Trim(), Contacts: [new("Email", "Personal", email), new("Phone", "Mobile", input.Phone.Trim())]);
            s.Validate(personInput);
            var (tenant, units, levels) = await Structure(s.Access);
            var club = Clubs(units, levels).SingleOrDefault(c => c.Id == input.ClubId && c.RegionId == input.RegionId && c.NationalId == input.NationalId);
            Check(club != null, "Choose an active club within the selected national unit and region.");
            var category = await s.Db.MembershipCategories.Where(c => c.TenantId == tenant && c.Active)
                .OrderBy(c => c.Code == "REG" ? 0 : 1).ThenBy(c => c.Name).FirstOrDefaultAsync();
            Check(category != null, "The association must configure a membership category before signup.", 409);
            var accepted = new { status = "Pending", message = "Your signup request has been received. New applications remain pending until a club or region president approves them. Check your email for account verification, then sign in to track your application. If you already have an account, sign in instead of signing up again." };
            await using var tx = await s.Db.Database.BeginTransactionAsync();
            await s.Lock(tenant);
            await s.Db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtext({"signup:" + email}))");
            // Do not change or expose another account, its password, or its applications.
            if (await s.Db.Users.AnyAsync(u => u.Email == email)) return Results.Accepted(value: accepted);
            var latest = await Structure(s.Access);
            Check(Clubs(latest.Units, latest.Levels).Any(c => c.Id == club.Id && c.RegionId == input.RegionId && c.NationalId == input.NationalId)
                && await s.Db.MembershipCategories.AnyAsync(c => c.Id == category.Id && c.Active), "The selected zone, club or category is no longer available.", 409);
            var workflow = await s.Db.MembershipWorkflows.Include(w => w.Stages).ThenInclude(st => st.Roles)
                .SingleOrDefaultAsync(w => w.TenantId == tenant && w.Kind == WorkflowKind && w.Active);
            if (workflow == null)
            {
                var roles = await s.Db.Roles.Where(r => r.TenantId == tenant && new[] { "Club President", "Regional President" }.Contains(r.Name)).ToListAsync();
                Check(roles.Any(r => r.Name == "Club President") && roles.Any(r => r.Name == "Regional President"), "Configure club and region president roles before accepting public applications.", 409);
                workflow = new MembershipWorkflow { TenantId = tenant, Kind = WorkflowKind, Stages = [new() {
                    TenantId = tenant, Name = "Club or region president approval", Sequence = 1, RequiredApprovals = 1,
                    Roles = roles.Select(r => new MembershipStageRole { TenantId = tenant, RoleId = r.Id }).ToList() }] };
                s.Db.MembershipWorkflows.Add(workflow);
            }
            var user = new User { Name = input.Name.Trim(), Email = email, Phone = input.Phone.Trim(), Verified = false };
            user.PasswordHash = passwords.HashPassword(user, input.Password!);
            s.Db.Users.Add(user);
            var person = s.NewPerson(personInput); person.UserId = user.Id;
            // Email/contact ownership is not asserted until verification. No membership or role is granted here.
            var application = new MembershipApplication { TenantId = tenant, PersonId = person.Id, ApplicantUserId = user.Id,
                ClubId = club.Id, CategoryId = category.Id, WorkflowId = workflow.Id, Statement = input.Statement.Trim(),
                Status = "Submitted", Revision = 1, CurrentSequence = 1, SubmittedAt = DateTimeOffset.UtcNow };
            s.Db.MembershipApplications.Add(application);
            Queue(s.Db, user, "verify");
            s.Db.Audits.Add(new Audit { TenantId = tenant, OrgId = club.Id, ActorId = user.Id,
                Action = "membership.public-signup", Resource = application.Id.ToString(),
                Changes = GovernanceService.Pack(new { input.NationalId, input.RegionId, input.ClubId, state = "Pending", approval = "Club or region president", membershipGranted = false, consentRecorded = true }) });
            s.Db.AuthenticationAudits.Add(new() { ActorId = user.Id, Action = "auth.public-signup" });
            await s.Db.SaveChangesAsync();
            await MembershipWorkflowEndpoints.NotifyReviewers(s, notifications, application);
            await s.Db.SaveChangesAsync();
            await tx.CommitAsync();
            return Results.Accepted(value: accepted);
        }).RequireRateLimiting("auth");
    }
}
