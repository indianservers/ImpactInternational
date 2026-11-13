using Microsoft.EntityFrameworkCore;
using static Clubs.Access;

namespace Clubs;

public static class MembershipHierarchy
{
    public static void MapMembershipHierarchy(this RouteGroupBuilder api)
    {
        api.MapGet("/membership/hierarchy", async (Access a, string kind = "National", Guid? parentId = null,
            Guid? tenantId = null, bool publicCatalog = false, string? search = null, int page = 1, Guid? clubId = null) =>
        {
            await a.User();
            Check(ImpactStructure.Kinds.Contains(kind), "Choose a National unit, Region, District, Zone, or Club.");
            Guid tenant, scope;
            if (publicCatalog)
            {
                var organization = await a.Db.Tenants.SingleAsync(t => t.Name == ImpactSetup.Name);
                tenant = organization.Id;
                Check(tenantId == null || tenantId == tenant, "Organization unavailable.", 404);
                Check(a.BoundTenant == null || a.BoundTenant == tenant, "Organization unavailable.", 404);
                scope = await a.Db.Orgs.Where(o => o.TenantId == tenant && o.ParentId == null).Select(o => o.Id).SingleAsync();
            }
            else
            {
                var w = await a.WorkspaceAny("members.view", "members.manage", "members.import", "members.invite",
                    "members.transfer.approve", "members.workflow.manage", "directory.view");
                tenant = w.TenantId;
                scope = (await a.User()).PlatformAdmin
                    ? await a.Db.Orgs.Where(o => o.TenantId == tenant && o.ParentId == null).Select(o => o.Id).SingleAsync()
                    : w.OrgId;
            }
            // Include the authorized subtree and its ancestor names, so a club- or
            // zone-scoped user can select the full path without seeing other branches.
            var available = a.Db.Orgs.FromSqlInterpolated($"""
                WITH RECURSIVE scoped AS (
                  SELECT * FROM "Orgs" WHERE "Id"={scope} AND "TenantId"={tenant} AND "Status"='Active'
                  UNION ALL SELECT o.* FROM "Orgs" o JOIN scoped s ON o."ParentId"=s."Id"
                    WHERE o."TenantId"={tenant} AND o."Status"='Active'
                ), ancestry AS (
                  SELECT * FROM scoped
                  UNION SELECT p.* FROM "Orgs" p JOIN ancestry c ON p."Id"=c."ParentId"
                    WHERE p."TenantId"={tenant} AND p."Status"='Active'
                ) SELECT * FROM ancestry
                """).AsNoTracking();
            var levels = await a.Db.Levels.Where(l => l.TenantId == tenant).ToDictionaryAsync(l => l.Id, l => l.Name);
            var selectedPath = new List<object>();
            if (clubId != null)
            {
                var selected = await available.SingleOrDefaultAsync(o => o.Id == clubId);
                Check(selected != null && levels[selected.LevelId] == "Club", "Club unavailable in this scope.", 404);
                for (var node = selected; node != null; node = node.ParentId == null ? null : await available.SingleOrDefaultAsync(o => o.Id == node.ParentId))
                    selectedPath.Add(new { node.Id, node.Name, Kind = levels[node.LevelId], node.ParentId });
            }
            var options = available.Where(o => a.Db.Levels.Any(l => l.Id == o.LevelId && l.Name == kind));
            if (parentId != null) options = options.Where(o => o.ParentId == parentId);
            if (!string.IsNullOrWhiteSpace(search)) options = options.Where(o => EF.Functions.ILike(o.Name, "%" + search + "%") || EF.Functions.ILike(o.Code, "%" + search + "%"));
            page = Math.Max(1, page);
            return new
            {
                total = await options.CountAsync(), page, size = 100, selectedPath,
                items = await options.OrderBy(o => o.Name).ThenBy(o => o.Id).Skip((page - 1) * 100).Take(100)
                    .Select(o => new { o.Id, o.Name, o.Code, o.ParentId }).ToListAsync(),
            };
        });
    }
}
