using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using static Clubs.Access;

namespace Clubs;

public static class ImpactSetup
{
    public const string Name = "Impact International";
    public const string Color = "#302b78";
    public static readonly string[] Roles =
    [
        "Association Super Admin", "International Administrator", "National Administrator",
        "District Administrator", "Regional Administrator", "Zone Administrator",
        "Club President", "Club Secretary", "Club Treasurer", "Club Manager",
        "Club Member", "Guest / Applicant",
        "International President", "National President", "Regional President", "District President", "Zonal President",
    ];

    // Explicit first-install command. Never resets an existing installation.
    public static async Task Initialize(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<Db>();
        Check(!await db.Users.AnyAsync() && !await db.Tenants.AnyAsync(),
            "Initialize Impact International only in an empty, migrated database.", 409);
        var password = Environment.GetEnvironmentVariable("CLUBS_ADMIN_PASSWORD");
        Check(password is { Length: >= 12 }, "Set CLUBS_ADMIN_PASSWORD to at least 12 characters.");
        await using var tx = await db.Database.BeginTransactionAsync();
        var user = new User
        {
            Email = "admin@example.test", Name = "Impact International Administrator",
            Verified = true, PlatformAdmin = true, TimeZone = "Asia/Kolkata",
        };
        user.PasswordHash = scope.ServiceProvider.GetRequiredService<IPasswordHasher<User>>()
            .HashPassword(user, password!);
        var tenant = new Tenant
        {
            Name = Name, Currency = "INR", TimeZone = "Asia/Kolkata", Color = Color,
        };
        Level? parent = null;
        var levels = new List<Level>();
        foreach (var name in new[] { "International", "National", "Region", "District", "Zone", "Club" })
        {
            var level = new Level { TenantId = tenant.Id, Name = name, ParentLevelId = parent?.Id, IsClub = name == "Club" };
            levels.Add(level);
            parent = level;
        }
        var root = new Org
        {
            TenantId = tenant.Id, LevelId = levels[0].Id, Name = Name, Code = "IMPACT-HQ",
            Country = "India", State = "Telangana", City = "Hyderabad",
            Website = "https://impactinternational.in/", TimeZone = "Asia/Kolkata", Currency = "INR",
        };
        var roles = Roles.Select(name =>
        {
            var role = new Role { TenantId = tenant.Id, Name = name, Permissions = name == "Association Super Admin" ? All : LeaderCommunications.PresidentLevels.ContainsKey(name) && name != "Club President" ? ["organization.view", "members.view", "directory.view"] : name is "Club Member" or "Guest / Applicant" ? ["organization.view"] : ["organization.view", "organization.create", "organization.update", "users.view", "reports.view"] };
            MembershipSetup.Template(role);
            return role;
        }).ToArray();
        db.AddRange(user, tenant, root);
        db.Levels.AddRange(levels);
        db.Roles.AddRange(roles);
        db.Assignments.Add(new Assignment { UserId = user.Id, TenantId = tenant.Id, OrgId = root.Id, RoleId = roles[0].Id });
        await db.SaveChangesAsync();
        await MembershipSetup.Tenant(db, tenant.Id);
        await GovernanceSetup.Tenant(db, tenant.Id);
        await tx.CommitAsync();
    }
}
