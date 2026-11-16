using Microsoft.EntityFrameworkCore;
using static Clubs.Access;
namespace Clubs;

public record IciRoleDefinition(string Name, string Level, string[] Features, string[] Reports, string[] Portfolios);
public static class IciPolicy
{
    public static readonly string[] ActivityPortfolios = ["Business", "Executive Leadership", "Service", "Training", "Nari Cell"];
    public static readonly string[] CoordinatorPortfolios = ["Mini & Mega Impact", "EPS", "Young Minds", "Skill Proficiency", "Social Media"];
    public static readonly string[] Programs = [.. CoordinatorPortfolios, "National pro"];
    public static readonly IciRoleDefinition[] Roles = [
        new("Executive Board","International",["Title clearance","Final member transfer approval","Club creation and regional mapping","Region-to-NVP mapping","Regional event bid and report approval","Leadership handovers including National President","Confidential legal case tracking"],["Members and activities across authorized regions and clubs"],[]),
        new("Office Bearers","National",["Own profile and career updates"],["Member and activity reports in assigned regions"],[]),
        new("National President","National",["Own profile and career updates","Title clearance","Final member transfer approval","Club creation and regional mapping","Region-to-NVP mapping","Club event bid approval","Leadership handovers excluding National President"],["Members and activities within national scope"],[]),
        new("National Secretary","National",["Own profile and career updates"],["Assigned-region reporting; no additional approval powers specified by the source"],[]),
        new("National Treasurer","National",["Own profile and career updates"],["Event and project submissions requesting financial aid"],[]),
        new("National Vice President","National",["Own profile and career updates","Inward and outward member transfer review"],["Members and activities in assigned regions"],[]),
        new("National Director","National",["Own profile and career updates","Final club event approval after Regional President","Regional event pre-approval before Executive Board"],["Activities in assigned portfolio across national, region and club scopes"],[..ActivityPortfolios,"National pro"]),
        new("National Coordinator","National",["Own profile and career updates"],["Activities in assigned initiative across national, region and club scopes"],CoordinatorPortfolios),
        new("Regional President","Region",["Own profile, bank and career updates","Inward and outward member transfer review","Event bidding, reporting and creation","Club event and report review"],["Region and club members","Region and club activities"],[]),
        new("Regional Director","Region",["Own profile and career updates"],["Region and club activities in assigned portfolio"],ActivityPortfolios),
        new("Club President","Club",["Own profile, bank and career updates","Subscription payment records: INR 365/member; initial minimum 20","Add new and existing members","Member transfer requests","New LO title clearance: INR 100","Event bidding, reporting and creation"],["Club member details","Extended LOs and title-clearance status","Event calendar"],[])
    ];
    public static readonly string[] RequestKinds = ["TitleClearance", "Subscription", "EventBid", "EventReport", "FinancialAid", "ClubCreation", "ClubMapping", "RegionMapping", "TeamHandover", "LegalAction"];
    public static bool IsRole(string role) => Roles.Any(r => r.Name == role);
    public static bool CoverageRequired(string role) => role is "National Vice President" or "Office Bearers" or "National Secretary";
    public static bool PortfolioRequired(string role) => role is "National Director" or "National Coordinator" or "Regional Director";
    public static bool OwnBank(string role) => role is "Club President" or "Regional President";
    public static Task<string> Role(Access a, Assignment w) => a.Db.Roles.Where(r => r.Id == w.RoleId && r.TenantId == w.TenantId).Select(r => r.Name).SingleAsync();
    public static async Task<bool> Administrator(Access a, Assignment w) => (await a.User()).PlatformAdmin || (await Role(a, w)) is "Association Super Admin" or "Main Admin";
    public static string[] Permissions(string role)
    {
        if (!IsRole(role)) return [];
        var p = new List<string> { "organization.view", "ici.view", "events.view", "events.reports", "finance.self" };
        if (role is "Club President") p.AddRange(["members.view", "members.manage", "members.personal.view", "members.contacts.view", "members.export", "members.import", "members.invite", "members.approve", "directory.view", "leadership.view", "events.manage", "ici.submit"]);
        if (role is "Regional President") p.AddRange(["members.view", "members.personal.view", "members.contacts.view", "members.approve", "members.transfer.approve", "directory.view", "leadership.view", "events.manage", "governance.approve", "ici.submit", "ici.review"]);
        if (role is "National Vice President") p.AddRange(["members.view", "members.contacts.view", "directory.view", "members.transfer.approve", "ici.review"]);
        if (role is "Office Bearers" or "National Secretary") p.AddRange(["members.view", "directory.view"]);
        if (role is "Executive Board") p.Add("governance.approve");
        if (role is "National Director") p.AddRange(["governance.approve", "ici.review"]);
        if (role is "National President" or "Executive Board") p.AddRange(["members.view", "members.personal.view", "members.contacts.view", "directory.view", "leadership.view", "members.transfer.approve", "ici.submit", "ici.review", "ici.team"]);
        return p.Distinct().ToArray();
    }
    public static async Task Configure(Db db)
    {
        foreach (var tenant in await db.Tenants.Where(t => t.Name == ImpactSetup.Name).ToListAsync())
        {
            var roles = await db.Roles.Include(r => r.Grants).Where(r => r.TenantId == tenant.Id).ToListAsync();
            foreach (var administrator in roles.Where(r => r.Name is "Association Super Admin" or "Main Admin"))
                foreach (var permission in new[] { "ici.view", "ici.submit", "ici.review", "ici.team" })
                    if (!administrator.Grants.Any(g => g.PermissionId == permission)) db.RolePermissions.Add(new RolePermission { RoleId = administrator.Id, PermissionId = permission });
            var levels = await db.Levels.Where(l => l.TenantId == tenant.Id).ToDictionaryAsync(l => l.Name);
            foreach (var definition in Roles)
            {
                var role = roles.SingleOrDefault(r => r.Name == definition.Name);
                if (role == null) { role = new Role { TenantId = tenant.Id, Name = definition.Name }; db.Roles.Add(role); roles.Add(role); }
                var desired = Permissions(role.Name);
                foreach (var old in role.Grants.Where(g => !desired.Contains(g.PermissionId)).ToList()) { db.RolePermissions.Remove(old); role.Grants.Remove(old); }
                foreach (var permission in desired.Where(p => !role.Grants.Any(g => g.PermissionId == p))) db.RolePermissions.Add(new RolePermission { RoleId = role.Id, PermissionId = permission });
                if (!levels.TryGetValue(definition.Level, out var level)) continue;
                var titles = definition.Name == "National Vice President" ? new[] { "NVP 1", "NVP 2", "NVP 3", "NVP 4" } : new[] { definition.Name };
                foreach (var title in titles)
                    if (!await db.OfficerPositions.AnyAsync(p => p.TenantId == tenant.Id && p.LevelId == level.Id && p.Title == title))
                        db.OfficerPositions.Add(new OfficerPosition { TenantId = tenant.Id, LevelId = level.Id, Title = title, Code = "ICI-" + Guid.NewGuid().ToString("N")[..12], GrantRoleId = role.Id, RequireActiveMembership = true, MaximumHolders = definition.Name is "Regional Director" or "National Director" or "National Coordinator" or "Office Bearers" or "Executive Board" ? null : 1 });
            }
            await db.SaveChangesAsync();
        }
    }
    public static async Task ValidateAssignment(Access a, Guid tenant, Guid orgId, string role, string portfolio, string slot, Guid[] regions, Guid? except = null)
    {
        if (!IsRole(role)) return;
        var definition = Roles.Single(r => r.Name == role);
        Check(await a.Db.Orgs.AnyAsync(o => o.Id == orgId && o.TenantId == tenant && o.Status == "Active" && a.Db.Levels.Any(l => l.Id == o.LevelId && l.Name == definition.Level)), "Assign this role at the " + definition.Level + " level.");
        Check(!PortfolioRequired(role) || definition.Portfolios.Contains(portfolio), "Choose the role's assigned portfolio.");
        Check(PortfolioRequired(role) || portfolio == "", "This role does not use a portfolio.");
        Check(role != "National Vice President" || new[] { "NVP 1", "NVP 2", "NVP 3", "NVP 4" }.Contains(slot), "Choose NVP 1, 2, 3 or 4.");
        Check(role == "National Vice President" || slot == "", "Slots are only used for National Vice Presidents.");
        Check(regions.Length <= 1000 && regions.Distinct().Count() == regions.Length, "Invalid region coverage.");
        Check(!CoverageRequired(role) || regions.Length > 0, "Assign at least one region before activating this role.");
        Check(CoverageRequired(role) || regions.Length == 0, "This role uses its organizational branch, not separate region coverage.");
        foreach (var region in regions)
            Check(await a.Tree(tenant, orgId).AnyAsync(o => o.Id == region && o.Status == "Active" && a.Db.Levels.Any(l => l.Id == o.LevelId && l.Name == "Region")), "Region coverage must be within the assigned national unit.");
        if (role is "Club President" or "Regional President" or "National President" or "National Secretary" or "National Treasurer")
            Check(!await a.Db.Assignments.AnyAsync(w => w.TenantId == tenant && w.OrgId == orgId && w.Id != except && a.Db.Roles.Any(r => r.Id == w.RoleId && r.Name == role)), "This office is occupied. Use a team handover to replace its holder.", 409);
        if (PortfolioRequired(role))
            Check(!await a.Db.Assignments.AnyAsync(w => w.TenantId == tenant && w.OrgId == orgId && w.Id != except && w.IciPortfolio == portfolio && a.Db.Roles.Any(r => r.Id == w.RoleId && r.Name == role)), "This portfolio already has an assigned official.", 409);
        if (role == "National Vice President")
        {
            Check(!await a.Db.Assignments.AnyAsync(w => w.TenantId == tenant && w.OrgId == orgId && w.Id != except && w.IciSlot == slot && a.Db.Roles.Any(r => r.Id == w.RoleId && r.Name == role)), "This NVP slot is occupied.", 409);
            Check(!await a.Db.IciRegionCoverages.AnyAsync(c => regions.Contains(c.RegionId) && c.AssignmentId != except && a.Db.Assignments.Any(w => w.Id == c.AssignmentId && a.Db.Roles.Any(r => r.Id == w.RoleId && r.Name == role))), "A region is already mapped to another National Vice President.", 409);
        }
    }
    public static IQueryable<ClubEvent> Events(Access a, Assignment w, IQueryable<ClubEvent> q)
    {
        var role = a.Db.Roles.Where(r => r.Id == w.RoleId).Select(r => r.Name);
        var source = !a.Db.Users.Any(u => u.Id == w.UserId && u.PlatformAdmin) && role.Any(r => Roles.Select(d => d.Name).Contains(r));
        var portfolio = role.Any(r => r == "Regional Director" || r == "National Director" || r == "National Coordinator");
        var treasurer = role.Any(r => r == "National Treasurer");
        var type = w.IciPortfolio == "Executive Leadership" ? "Leadership" : w.IciPortfolio;
        return q.Where(e => e.TenantId == w.TenantId && (!source || a.Visible(w).Any(o => o.Id == e.OrgId) && (!portfolio || w.IciPortfolio != "" && (e.ActivityType == type || e.ActivityProgram == w.IciPortfolio)) && (!treasurer || a.Db.IciRequests.Any(r => r.EventId == e.Id && r.Kind == "FinancialAid" && r.Status != "Draft"))));
    }
}
