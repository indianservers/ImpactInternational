using Microsoft.EntityFrameworkCore;
using static Clubs.Access;

namespace Clubs;

public static class ImpactStructure
{
    public static readonly string[] Kinds = ["National", "Region", "District", "Zone", "Club"];
    const string GuideKey = "impact.setup.completed";

    public static async Task Configure(Db db)
    {
        var tenant = await db.Tenants.SingleOrDefaultAsync(t => t.Name == ImpactSetup.Name);
        if (tenant == null) return;
        var root = await db.Orgs.SingleAsync(o => o.TenantId == tenant.Id && o.ParentId == null);
        var levels = await db.Levels.Where(l => l.TenantId == tenant.Id).ToListAsync();
        var parent = root.LevelId;
        foreach (var kind in Kinds)
        {
            var level = levels.SingleOrDefault(l => l.Name == kind);
            if (level == null)
            {
                level = new Level { TenantId = tenant.Id, Name = kind };
                db.Levels.Add(level);
            }
            level.ParentLevelId = parent;
            level.IsClub = kind == "Club";
            parent = level.Id;
        }
        await db.SaveChangesAsync();
    }

    public static void MapImpactStructure(this RouteGroupBuilder api)
    {
        api.MapGet("/structure", async (Access a) =>
        {
            var w = await a.Workspace("organization.view");
            var root = await a.Db.Orgs.SingleAsync(o => o.TenantId == w.TenantId && o.ParentId == null);
            var levels = await a.Db.Levels.Where(l => l.TenantId == w.TenantId && (Kinds.Contains(l.Name) || l.Id == root.LevelId)).ToListAsync();
            var visible = a.Visible(w);
            var units = await visible.Where(o => o.Status != "Archived").GroupBy(o => o.LevelId)
                .Select(g => new { LevelId = g.Key, Count = g.Count() }).ToListAsync();
            var counts = Kinds.ToDictionary(kind => kind, kind => units.Where(c => levels.Any(l => l.Id == c.LevelId && l.Name == kind)).Sum(c => c.Count));
            var completed = await a.Db.TenantSettings.AnyAsync(s => s.TenantId == w.TenantId && s.Key == GuideKey && s.Value == "true");
            return new { root, levels, counts, setupReady = counts.Values.All(c => c > 0), setupCompleted = completed };
        });
        api.MapPost("/setup-guide/complete", async (Access a) =>
        {
            var w = await a.TenantWorkspace("settings.manage");
            await using var tx = await a.Mutation(w);
            foreach (var kind in Kinds)
                Check(await a.Db.Orgs.AnyAsync(o => o.TenantId == w.TenantId && o.Status != "Archived" && a.Db.Levels.Any(l => l.Id == o.LevelId && l.Name == kind)), "Create your first national unit, region, district, zone, and club before completing setup.");
            var row = await a.Db.TenantSettings.SingleOrDefaultAsync(s => s.TenantId == w.TenantId && s.Key == GuideKey);
            if (row == null) { row = new TenantSetting { TenantId = w.TenantId, Key = GuideKey }; a.Db.TenantSettings.Add(row); }
            row.Value = "true";
            await a.Db.SaveChangesAsync();
            await tx.CommitAsync();
            return Results.NoContent();
        });
    }
}
