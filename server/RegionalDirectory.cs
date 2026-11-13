using Microsoft.EntityFrameworkCore;
using static Clubs.Access;
namespace Clubs;

public static class RegionalDirectory
{
    public static void MapRegionalDirectory(this RouteGroupBuilder api) => api.MapGet("/directory/regions", async (MembershipService s) =>
    {
        var w = await s.Access.WorkspaceAny("directory.view", "members.view");
        var ancestors = new List<Guid>(); Guid? parent = w.OrgId;
        for (var depth = 0; parent != null && depth < 16 && !ancestors.Contains(parent.Value); depth++)
        {
            ancestors.Add(parent.Value);
            parent = await s.Db.Orgs.Where(o => o.Id == parent && o.TenantId == w.TenantId).Select(o => o.ParentId).SingleOrDefaultAsync();
        }
        return await s.Db.Orgs.Where(o => o.TenantId == w.TenantId && o.Status == "Active"
            && s.Db.Levels.Any(l => l.Id == o.LevelId && l.Name == "Region"))
            .Where(o => s.Access.Visible(w).Any(v => v.Id == o.Id)
                || ancestors.Contains(o.Id))
            .OrderBy(o => o.Name).Select(o => new { o.Id, o.Name, o.Code }).ToListAsync();
    });

    public static async Task<IQueryable<Membership>> Filter(MembershipService s, Assignment w, IQueryable<Membership> q, Guid? regionId)
    {
        if (regionId == null) return q;
        Check(await s.Db.Orgs.AnyAsync(o => o.Id == regionId && o.TenantId == w.TenantId && o.Status == "Active"
            && s.Db.Levels.Any(l => l.Id == o.LevelId && l.Name == "Region"))
            && (await s.Access.Visible(w).AnyAsync(o => o.Id == regionId)
                || await s.Access.Tree(w.TenantId, regionId.Value).AnyAsync(o => o.Id == w.OrgId)), "Region is outside your authorized scope.", 404);
        return q.Where(m => s.Access.Tree(w.TenantId, regionId.Value).Any(o => o.Id == m.ClubId));
    }

    public record Place(string Region = "", string District = "", string Zone = "");
    public static async Task<Dictionary<Guid, Place>> Places(MembershipService s, Guid tenant, Guid[] clubs)
    {
        var nodes = new Dictionary<Guid, (Guid? Parent, string Name, string Level)>();
        var pending = clubs.Distinct().ToArray();
        for (var depth = 0; pending.Length > 0 && depth < 16; depth++)
        {
            var batch = await (from o in s.Db.Orgs join l in s.Db.Levels on o.LevelId equals l.Id
                where o.TenantId == tenant && pending.Contains(o.Id) select new { o.Id, o.ParentId, o.Name, Level = l.Name }).ToListAsync();
            foreach (var n in batch) nodes[n.Id] = (n.ParentId, n.Name, n.Level);
            pending = batch.Where(n => n.ParentId != null && !nodes.ContainsKey(n.ParentId.Value)).Select(n => n.ParentId!.Value).Distinct().ToArray();
        }
        return clubs.Distinct().ToDictionary(club => club, club =>
        {
            string region = "", district = "", zone = ""; Guid? id = club;
            for (var depth = 0; id != null && depth < 16 && nodes.TryGetValue(id.Value, out var node); depth++, id = node.Parent)
            {
                if (node.Level == "Region") region = node.Name;
                if (node.Level == "District") district = node.Name;
                if (node.Level == "Zone") zone = node.Name;
            }
            return new Place(region, district, zone);
        });
    }
}
