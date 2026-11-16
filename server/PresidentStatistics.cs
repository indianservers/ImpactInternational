using Microsoft.EntityFrameworkCore;
using static Clubs.Access;
namespace Clubs;

public static class PresidentStatistics
{
    public static void MapPresidentStatistics(this RouteGroupBuilder api)
    {
        api.MapGet("/leadership/my-statistics", async (MembershipService s, string search = "", int page = 1) => {
            var w = await s.Access.Workspace("members.view");
            var role = await s.Db.Roles.Where(r => r.Id == w.RoleId).Select(r => r.Name).SingleAsync();
            Check(role is "Club President" or "Regional President" || (await s.Access.User()).PlatformAdmin, "President statistics require a club or region president workspace.", 403);
            Check(page > 0 && search.Length <= 100, "Invalid member search.");
            var members = s.Scoped(w); var now = DateTimeOffset.UtcNow;
            var visible = s.Access.Visible(w).Select(o => o.Id);
            var filtered = members.Where(m => search == "" || EF.Functions.ILike(m.Person.DisplayName, "%" + search + "%") || EF.Functions.ILike(m.Number, "%" + search + "%"));
            return new {
                organization = await s.Db.Orgs.Where(o => o.Id == w.OrgId).Select(o => new {o.Id,o.Name}).SingleAsync(),
                total = await members.CountAsync(), active = await members.CountAsync(m => m.Status == "Active" && (m.EndsAt == null || m.EndsAt > now)),
                expired = await members.CountAsync(m => m.Status == "Expired" || m.Status == "Active" && m.EndsAt <= now),
                clubs = await s.Access.Visible(w).CountAsync(o => s.Db.Levels.Any(l => l.Id == o.LevelId && l.IsClub)),
                certifiedMembers = await members.Select(m => m.PersonId).Distinct().CountAsync(id => s.Db.MemberCertifications.Any(c => c.PersonId == id && c.TenantId == w.TenantId)),
                activities = await s.Db.Events.CountAsync(e => e.TenantId == w.TenantId && visible.Contains(e.OrgId)),
                matches = await filtered.CountAsync(), page, pageSize = 25,
                members = await filtered.OrderBy(m => m.Person.DisplayName).ThenBy(m => m.Id).Skip((page - 1) * 25).Take(25)
                    .Select(m => new {m.Id,m.PersonId,m.Number,name=m.Person.DisplayName,club=m.Club.Name,m.Status,m.JoinedAt,m.EndsAt}).ToListAsync()
            };
        });
    }
}
