using Microsoft.EntityFrameworkCore;
using static Clubs.Access;

namespace Clubs;

public record DelegatedPositionInput(string Role, string Portfolio, Guid? MembershipId, Guid? CurrentAssignmentId);

public static class DelegatedPositions
{
    private static readonly string[] ClubRoles = ["Club President", "Club Secretary", "Club Treasurer"];
    private static readonly string[] RegionRoles = ["Regional President", "Regional Director"];

    public static bool IsClubRole(string role) => ClubRoles.Contains(role);

    private static async Task<(Assignment Assignment, bool CanManageRegions)> Workspace(Access access)
    {
        var assignment = await access.Workspace("ici.view");
        var role = await IciPolicy.Role(access, assignment);
        Check(role is "National President" or "Regional President", "Only National and Regional Presidents can manage delegated positions.", 403);
        return (assignment, role == "National President");
    }

    private static IQueryable<Org> Units(Access access, Assignment assignment, bool canManageRegions) =>
        access.Tree(assignment.TenantId, assignment.OrgId).Where(o => o.Status == "Active" && o.Id != assignment.OrgId
            && access.Db.Levels.Any(l => l.Id == o.LevelId && (l.IsClub || canManageRegions && l.Name == "Region")));

    private static IQueryable<Membership> Candidates(Access access, Assignment assignment, Org unit, bool isRegion)
    {
        var subtree = access.Tree(assignment.TenantId, unit.Id).Select(o => o.Id);
        var now = DateTimeOffset.UtcNow;
        return access.Db.Memberships.Where(m => m.TenantId == assignment.TenantId && m.Status == "Active" && m.Club.Status == "Active"
            && (m.EndsAt == null || m.EndsAt > now)
            && (isRegion ? subtree.Contains(m.ClubId) : m.ClubId == unit.Id)
            && m.Person.UserId != assignment.UserId);
    }

    public static void MapDelegatedPositions(this RouteGroupBuilder root)
    {
        var api = root.MapGroup("/ici/delegated-positions");
        api.MapGet("/units", async (Access access) =>
        {
            var (assignment, canManageRegions) = await Workspace(access);
            return await Units(access, assignment, canManageRegions).OrderBy(o => o.Name)
                .Select(o => new { o.Id, o.Name, o.Code, o.ParentId, Level = access.Db.Levels.Where(l => l.Id == o.LevelId).Select(l => l.Name).First() })
                .ToListAsync();
        });

        api.MapGet("/{orgId:guid}", async (Guid orgId, Access access) =>
        {
            var (assignment, canManageRegions) = await Workspace(access);
            var (unit, isRegion) = await Unit(access, assignment, canManageRegions, orgId);
            var allowedRoles = isRegion ? RegionRoles : ClubRoles;
            var holders = await (from holder in access.Db.Assignments
                                 join role in access.Db.Roles on holder.RoleId equals role.Id
                                 join user in access.Db.Users on holder.UserId equals user.Id
                                 where holder.TenantId == assignment.TenantId && holder.OrgId == unit.Id
                                     && allowedRoles.Contains(role.Name)
                                 orderby user.Name
                                 select new { holder.Id, Role = role.Name, Portfolio = holder.IciPortfolio, user.Name }).ToListAsync();
            return new { roles = allowedRoles, portfolios = isRegion ? IciPolicy.ActivityPortfolios : [], holders };
        });

        api.MapGet("/{orgId:guid}/members", async (Guid orgId, string? search, Access access, int page = 1) =>
        {
            var (assignment, canManageRegions) = await Workspace(access);
            var (unit, isRegion) = await Unit(access, assignment, canManageRegions, orgId);
            var members = Candidates(access, assignment, unit, isRegion);
            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim().ToLowerInvariant();
                members = members.Where(m => m.Person.SearchName.Contains(term) || m.Number.ToLower().Contains(term));
            }
            const int size = 50;
            page = Math.Clamp(page, 1, 10000);
            return new
            {
                total = await members.CountAsync(), page, size,
                items = await members.OrderBy(m => m.Person.DisplayName).ThenBy(m => m.Id)
                    .Skip((page - 1) * size).Take(size)
                    .Select(m => new { m.Id, m.Number, Name = m.Person.DisplayName, Club = m.Club.Name, AccountLinked = m.Person.UserId != null })
                    .ToListAsync(),
            };
        });

        api.MapPut("/{orgId:guid}", async (Guid orgId, DelegatedPositionInput input, Access access) =>
        {
            var (assignment, canManageRegions) = await Workspace(access);
            await using var transaction = await access.Mutation(assignment);
            var (unit, isRegion) = await Unit(access, assignment, canManageRegions, orgId);
            Check((isRegion ? RegionRoles : ClubRoles).Contains(input.Role), "Choose a position for this unit.", 403);
            Check(input.Role == "Regional Director" ? IciPolicy.ActivityPortfolios.Contains(input.Portfolio) : input.Portfolio == "",
                "Choose a valid portfolio for this position.");
            var role = await access.Db.Roles.SingleAsync(r => r.TenantId == assignment.TenantId && r.Name == input.Role);
            var occupants = await access.Db.Assignments.Where(a => a.TenantId == assignment.TenantId && a.OrgId == unit.Id
                && a.RoleId == role.Id && a.IciPortfolio == input.Portfolio).Take(2).ToListAsync();
            Check(occupants.Count < 2, "Multiple holders exist for this position. Resolve the duplicate assignments in administration.", 409);
            var current = occupants.SingleOrDefault();
            Check(current?.Id == input.CurrentAssignmentId, "The position changed. Refresh and try again.", 409);

            Membership? member = null;
            if (input.MembershipId.HasValue)
            {
                member = await Candidates(access, assignment, unit, isRegion).Include(m => m.Person).SingleOrDefaultAsync(m => m.Id == input.MembershipId && m.Person.UserId != null);
                Check(member != null, "Choose an active member with a linked account in this unit.", 404);
                if (current?.UserId == member!.Person.UserId)
                    return Results.NoContent();
            }

            if (current != null)
                await IciWorkflow.RemoveAssignment(access, assignment, current, role.Name);
            if (member != null)
            {
                var email = await access.Db.Users.Where(u => u.Id == member.Person.UserId).Select(u => u.Email).SingleAsync();
                await IciWorkflow.InstallAssignment(access, assignment, new(email, role.Id, unit.Id, input.Portfolio), member.Id);
            }
            await access.Db.SaveChangesAsync();
            await transaction.CommitAsync();
            return Results.NoContent();
        });
    }

    private static async Task<(Org Unit, bool IsRegion)> Unit(Access access, Assignment assignment, bool canManageRegions, Guid id)
    {
        var unit = await Units(access, assignment, canManageRegions).SingleOrDefaultAsync(o => o.Id == id);
        Check(unit != null, "Unit is outside your authority.", 404);
        var isRegion = await access.Db.Levels.AnyAsync(l => l.Id == unit!.LevelId && l.Name == "Region");
        return (unit!, isRegion);
    }
}
