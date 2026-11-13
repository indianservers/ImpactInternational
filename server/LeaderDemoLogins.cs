using Microsoft.EntityFrameworkCore;
using static Clubs.Access;

namespace Clubs;

public static class LeaderDemoLogins
{
    public static async Task EnablePasswordTesting(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        Check(scope.ServiceProvider.GetRequiredService<IHostEnvironment>().IsDevelopment(), "Local password testing requires Development.");
        var db = scope.ServiceProvider.GetRequiredService<Db>();
        var password = Environment.GetEnvironmentVariable("CLUBS_LOCAL_TEST_PASSWORD");
        Check(password is { Length: >= 12 }, "Set CLUBS_LOCAL_TEST_PASSWORD to at least 12 characters.");
        var tenant = await db.Tenants.SingleAsync(t => t.Name == ImpactSetup.Name);
        var headquarters = await db.Orgs.SingleAsync(o => o.TenantId == tenant.Id && o.Code == "IMPACT-HQ");
        var role = await db.Roles.SingleAsync(r => r.TenantId == tenant.Id && r.Name == "Association Super Admin");
        var hasher = scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.IPasswordHasher<User>>();
        await using var tx = await db.Database.BeginTransactionAsync();
        foreach (var email in new[] { "saisatish@indianservers.com", "admin@example.test" })
        {
            var user = await db.Users.SingleOrDefaultAsync(u => u.Email == email);
            if (user == null)
            {
                user = new User { Email = email, Name = email.StartsWith("saisatish@") ? "Sai Satish" : "Impact International Administrator", TimeZone = "Asia/Kolkata" };
                db.Users.Add(user);
            }
            user.PlatformAdmin = true; user.Verified = true;
            if (!await db.Assignments.AnyAsync(a => a.UserId == user.Id && a.OrgId == headquarters.Id && a.RoleId == role.Id))
                db.Assignments.Add(new Assignment { UserId = user.Id, TenantId = tenant.Id, OrgId = headquarters.Id, RoleId = role.Id });
            db.AuthenticationAudits.Add(new() { ActorId = user.Id, Action = "operator.platform-admin.grant" });
        }
        await db.SaveChangesAsync();
        var emails = new[] { "saisatish@indianservers.com", "admin@example.test", "international.president@example.test", "national.president@example.test", "regional.president@example.test", "district.president@example.test", "zonal.president@example.test", "club.president@example.test", "member.club1@example.test", "member.club2@example.test", "member.zone2@example.test", "member.district2@example.test", "member.region2@example.test" };
        var users = await db.Users.Where(u => emails.Contains(u.Email)).ToListAsync();
        Check(users.Count == emails.Length, "Create all demo leader/member logins before enabling password testing.");
        foreach (var user in users)
        {
            user.PasswordHash = hasher.HashPassword(user, password!); user.Verified = true; user.FailedAttempts = 0; user.LockedUntil = null;
            db.AuthenticationAudits.Add(new() { ActorId = user.Id, Action = "operator.local-password-testing" });
        }
        await db.LoginChallenges.Where(c => !c.Used).ExecuteUpdateAsync(s => s.SetProperty(c => c.Used, true).SetProperty(c => c.CodeProtected, ""));
        await db.Mails.Where(m => m.Kind == "login-otp" && !m.Used).ExecuteUpdateAsync(s => s.SetProperty(m => m.Used, true).SetProperty(m => m.Body, ""));
        await db.SaveChangesAsync(); await tx.CommitAsync();
        Console.WriteLine("Both Platform Admin accounts and eleven leader/member accounts are ready for local password testing. No passwords were printed.");
    }

    public static async Task Run(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        Check(scope.ServiceProvider.GetRequiredService<IHostEnvironment>().IsDevelopment(), "Demo logins require Development.");
        var db = scope.ServiceProvider.GetRequiredService<Db>();
        await LeaderCommunications.Configure(db);
        var tenant = await db.Tenants.SingleAsync(t => t.Name == ImpactSetup.Name);
        var admin = await db.Users.SingleAsync(u => u.Email == "admin@example.test" && u.PlatformAdmin);
        var roles = await db.Roles.Where(r => r.TenantId == tenant.Id).ToDictionaryAsync(r => r.Name);
        var orgs = await db.Orgs.Where(o => o.TenantId == tenant.Id).ToDictionaryAsync(o => o.Code);
        await using var tx = await db.Database.BeginTransactionAsync();
        async Task<User> User(string email, string name)
        {
            var user = await db.Users.SingleOrDefaultAsync(u => u.Email == email);
            if (user != null) return user;
            user = new User { Email = email, Name = "Demo " + name, Verified = true, PasswordHash = admin.PasswordHash, TimeZone = "Asia/Kolkata" };
            db.Users.Add(user); return user;
        }
        async Task Assign(User user, string role, Org org)
        {
            if (!await db.Assignments.AnyAsync(a => a.UserId == user.Id && a.RoleId == roles[role].Id && a.OrgId == org.Id))
                db.Assignments.Add(new Assignment { TenantId = tenant.Id, UserId = user.Id, OrgId = org.Id, RoleId = roles[role].Id });
        }
        var leaders = new[]
        {
            ("international.president@example.test", "International President", "IMPACT-HQ"),
            ("national.president@example.test", "National President", "DEMO-NAT-01"),
            ("regional.president@example.test", "Regional President", "DEMO-R01"),
            ("district.president@example.test", "District President", "DEMO-R01-D01"),
            ("zonal.president@example.test", "Zonal President", "DEMO-R01-D01-Z01"),
            ("club.president@example.test", "Club President", "DEMO-R01-D01-Z01-C01"),
        };
        foreach (var (email, role, code) in leaders)
        {
            Check(orgs.ContainsKey(code), "Create the demo hierarchy before demo logins.");
            await Assign(await User(email, role), role, orgs[code]);
        }
        var members = new[]
        {
            ("member.club1@example.test", "DEMO-R01-D01-Z01-C01"),
            ("member.club2@example.test", "DEMO-R01-D01-Z01-C02"),
            ("member.zone2@example.test", "DEMO-R01-D01-Z02-C01"),
            ("member.district2@example.test", "DEMO-R01-D02-Z01-C01"),
            ("member.region2@example.test", "DEMO-R02-D01-Z01-C01"),
        };
        foreach (var (email, code) in members)
        {
            var user = await User(email, "Member " + email.Split('@')[0]);
            var club = orgs[code];
            var person = await db.Memberships.Where(m => m.ClubId == club.Id).Select(m => m.Person).OrderBy(p => p.DisplayName).FirstAsync();
            Check(person.UserId == null || person.UserId == user.Id, "This demo member is already linked to another account.");
            person.UserId = user.Id;
            await Assign(user, "Club Member", club);
        }
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        Console.WriteLine("Six president and five member demo logins are ready. Newly created logins use the current administrator demo password. Existing passwords are preserved.");
    }
}
