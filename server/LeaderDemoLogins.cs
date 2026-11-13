using Microsoft.EntityFrameworkCore;
using static Clubs.Access;

namespace Clubs;

public static class LeaderDemoLogins
{
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
