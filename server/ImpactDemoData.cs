using Microsoft.EntityFrameworkCore;
using static Clubs.Access;

namespace Clubs;

public static class ImpactDemoData
{
    // Explicit Development-only sample installation; never runs during startup.
    public static async Task Run(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        Check(scope.ServiceProvider.GetRequiredService<IHostEnvironment>().IsDevelopment(), "Demo setup requires Development.");
        var db = scope.ServiceProvider.GetRequiredService<Db>();
        var membership = scope.ServiceProvider.GetRequiredService<MembershipService>();
        var tenant = await db.Tenants.SingleAsync(t => t.Name == ImpactSetup.Name);
        var root = await db.Orgs.SingleAsync(o => o.TenantId == tenant.Id && o.ParentId == null);
        var admin = await db.Users.Where(u => u.PlatformAdmin).Select(u => u.Id).FirstAsync();
        var levels = await db.Levels.Where(l => l.TenantId == tenant.Id).ToDictionaryAsync(l => l.Name);
        var category = await db.MembershipCategories.FirstAsync(c => c.TenantId == tenant.Id && c.Active);
        var existing = await db.Orgs.Where(o => o.TenantId == tenant.Id && o.Code.StartsWith("DEMO-")).ToDictionaryAsync(o => o.Code);
        await using var tx = await db.Database.BeginTransactionAsync();
        Org Node(string kind, string code, string name, Org parent)
        {
            if (existing.TryGetValue(code, out var found)) return found;
            var node = new Org { TenantId = tenant.Id, LevelId = levels[kind].Id, ParentId = parent.Id,
                Name = "Demo " + name, Code = code, Country = "India", City = "Demo City", Status = "Active" };
            db.Orgs.Add(node); existing[code] = node; return node;
        }
        var national = Node("National", "DEMO-NAT-01", "India National Unit", root);
        await db.SaveChangesAsync();
        var regions = Enumerable.Range(1, 5).Select(r => Node("Region", $"DEMO-R{r:00}", $"Region {r:00}", national)).ToArray();
        await db.SaveChangesAsync();
        var clubs = new List<Org>();
        foreach (var region in regions) for (int c = 1; c <= 125; c++)
            clubs.Add(Node("Club", region.Code + $"-C{c:000}", region.Name[5..] + $" Club {c:000}", region));
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        var createdMembers = 0;
        for (var index = 0; index < clubs.Count; index++)
        {
            var club = clubs[index];
            var count = await db.Memberships.CountAsync(m => m.ClubId == club.Id);
            for (int member = count + 1; member <= 5; member++)
            {
                var person = membership.NewPerson(new PersonInput(
                    $"Demo Member {index + 1:0000}-{member:00}", $"Demo Member {index + 1:0000}-{member:00}",
                    AdditionalInformation: "Fictional sample record for Impact International testing."
                ));
                await membership.Activate(person, club, category, admin);
                createdMembers++;
            }
            await db.SaveChangesAsync();
            db.ChangeTracker.Clear();
            if ((index + 1) % 50 == 0) Console.WriteLine($"Demo clubs prepared: {index + 1}/{clubs.Count}");
        }
        var completed = await db.TenantSettings.SingleOrDefaultAsync(s => s.TenantId == tenant.Id && s.Key == "impact.setup.completed");
        if (completed == null) { completed = new TenantSetting { TenantId = tenant.Id, Key = "impact.setup.completed" }; db.TenantSettings.Add(completed); }
        completed.Value = "true";
        db.Audits.Add(new Audit { TenantId = tenant.Id, OrgId = root.Id, ActorId = admin, Action = "demo.setup", Resource = "625 demo clubs with at least 5 fictional members each" });
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        Console.WriteLine($"Demo setup complete: 1 national unit, 5 regions, 625 clubs; {createdMembers} members added.");
    }
}
