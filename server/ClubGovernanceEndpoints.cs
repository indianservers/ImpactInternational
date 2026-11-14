using Microsoft.EntityFrameworkCore;
using static Clubs.Access;

namespace Clubs;

public static class ClubGovernanceEndpoints
{
    public static void MapClubGovernance(this RouteGroupBuilder api)
    {
        api.MapGet(
            "/clubs",
            async (
                GovernanceService s,
                string? search = null,
                string? status = null,
                string? country = null,
                Guid? ancestorId = null,
                string sort = "name",
                int page = 1,
                int size = 20
            ) =>
            {
                var w = await s.A.Workspace("clubs.view");
                var q = s
                    .A.Visible(w)
                    .Where(o => s.Db.Levels.Any(l => l.Id == o.LevelId && l.IsClub));
                if (search != null)
                {
                    Check(search.Length <= 100, "Search too long.");
                    q = q.Where(x =>
                        EF.Functions.ILike(x.Name, "%" + search + "%")
                        || EF.Functions.ILike(x.Code, "%" + search + "%")
                    );
                }
                if (status != null)
                    q = q.Where(x => x.Status == status);
                if (country != null)
                    q = q.Where(x => x.Country == country);
                if (ancestorId != null)
                {
                    await s.Scope(w, ancestorId.Value);
                    q = q.Where(x => s.A.Tree(w.TenantId, ancestorId.Value).Any(o => o.Id == x.Id));
                }
                var ordered =
                    sort == "charter" ? q.OrderByDescending(x => x.CharterDate)
                    : sort == "members"
                        ? q.OrderByDescending(x =>
                            s.Db.Memberships.Count(m =>
                                m.TenantId == w.TenantId
                                && m.ClubId == x.Id
                                && m.Status == "Active"
                                && (m.EndsAt == null || m.EndsAt > DateTimeOffset.UtcNow)
                            )
                        )
                    : q.OrderBy(x => x.Name);
                size = Math.Clamp(size, 1, 100);
                return new
                {
                    total = await q.CountAsync(),
                    page,
                    size,
                    items = await ordered
                        .ThenBy(x => x.Id)
                        .Skip((Math.Clamp(page, 1, 10000) - 1) * size)
                        .Take(size)
                        .Select(x => new
                        {
                            x.Id,
                            x.Name,
                            x.Code,
                            x.Logo,
                            x.Country,
                            x.State,
                            x.City,
                            x.CharterDate,
                            x.Status,
                            x.ParentId,
                            x.LevelId,
                            x.Version,
                            Parent = s
                                .Db.Orgs.Where(o => o.Id == x.ParentId)
                                .Select(o => o.Name)
                                .FirstOrDefault(),
                            Members = s
                                .Db.Memberships.Where(m =>
                                    m.TenantId == w.TenantId
                                    && m.ClubId == x.Id
                                    && m.Status == "Active"
                                    && (m.EndsAt == null || m.EndsAt > DateTimeOffset.UtcNow)
                                )
                                .Select(m => m.PersonId)
                                .Distinct()
                                .Count(),
                        })
                        .ToListAsync(),
                };
            }
        );
        api.MapGet(
            "/clubs/{id:guid}",
            async (Guid id, GovernanceService s) =>
            {
                var o = await s.Org(id, "clubs.view", true);
                var w = await s.A.Workspace("clubs.view");
                return new
                {
                    club = o,
                    parent = await s
                        .Db.Orgs.Where(x => x.Id == o.ParentId)
                        .Select(x => new { x.Id, x.Name })
                        .SingleOrDefaultAsync(),
                    leadership = await (
                        from a in GovernanceService.Current(s.Db)
                        join p in s.Db.Persons on a.PersonId equals p.Id
                        join pos in s.Db.OfficerPositions on a.PositionId equals pos.Id
                        where a.OrgId == id
                        select new
                        {
                            a.Id,
                            Name = p.DisplayName,
                            Position = pos.Title,
                            a.StartsAt,
                            a.EndsAt,
                        }
                    ).ToListAsync(),
                    committees = await s
                        .Db.Committees.Where(x => x.OrgId == id)
                        .Select(x => new
                        {
                            x.Id,
                            x.Name,
                            x.Purpose,
                            x.Status,
                            x.StartsAt,
                            x.EndsAt,
                        })
                        .ToListAsync(),
                    health = await Health(id, s, w),
                    canManage = (await s.A.Grants(w)).Contains("clubs.manage"),
                };
            }
        );
        api.MapGet(
            "/clubs/{id:guid}/history",
            async (Guid id, GovernanceService s, int page = 1) =>
            {
                await s.Org(id, "clubs.view", true);
                var q = s.Db.OrganizationHistory.Where(x => x.OrgId == id);
                return new
                {
                    total = await q.CountAsync(),
                    items = await q.OrderByDescending(x => x.At)
                        .Skip((Math.Clamp(page, 1, 10000) - 1) * 20)
                        .Take(20)
                        .Select(x => new
                        {
                            x.Id,
                            x.Before,
                            x.After,
                            x.At,
                            x.ActorId,
                        })
                        .ToListAsync(),
                };
            }
        );
        api.MapPost(
            "/clubs",
            async (ClubInput input, GovernanceService s) =>
            {
                var w = await s.A.Workspace("clubs.manage");
                await using var tx = await s.A.Mutation(w);
                await s.Parent(input.LevelId, input.ParentId, w);
                Check(
                    input.Name.Length is >= 2 and <= 200
                        && await s.Db.Levels.AnyAsync(x => x.Id == input.LevelId && x.IsClub),
                    "Choose a valid club identity/level."
                );
                Check(
                    !await s.Db.Orgs.AnyAsync(x =>
                        x.TenantId == w.TenantId && x.Code == input.Code.Trim().ToUpper()
                    ),
                    "Duplicate club code.",
                    409
                );
                var r = await s.Request(
                    w,
                    input.ParentId,
                    "ClubCreate",
                    "Create " + input.Name,
                    GovernanceService.Pack(input)
                );
                await s.Db.SaveChangesAsync();
                await tx.CommitAsync();
                return Results.Ok(r);
            }
        );
        api.MapPut(
            "/clubs/{id:guid}",
            async (Guid id, ClubInput input, GovernanceService s) =>
            {
                var w = await s.A.Workspace("clubs.manage");
                await using var tx = await s.A.Mutation(w);
                var o = await s.Org(id, "clubs.manage", true);
                Check(o.Version == input.Version, "Club changed.", 409);
                Check(
                    input.ParentId == o.ParentId,
                    "Use the controlled restructuring workflow to change parents."
                );
                Check(input.LevelId == o.LevelId, "Existing club level is immutable.");
                var r = await s.Request(
                    w,
                    id,
                    "ClubUpdate",
                    "Update " + o.Name,
                    GovernanceService.Pack(input)
                );
                await s.Db.SaveChangesAsync();
                await tx.CommitAsync();
                return Results.Ok(r);
            }
        );
        api.MapPost(
            "/clubs/{id:guid}/status",
            async (Guid id, ClubStatusInput input, GovernanceService s) =>
            {
                var w = await s.A.Workspace("clubs.manage");
                await using var tx = await s.A.Mutation(w);
                var o = await s.Org(id, "clubs.manage", true);
                Check(
                    o.Version == input.Version && input.Reason.Length >= 5,
                    "Club changed or reason missing.",
                    409
                );
                var r = await s.Request(
                    w,
                    id,
                    "ClubStatus",
                    input.Status + ": " + o.Name,
                    GovernanceService.Pack(input)
                );
                await s.Db.SaveChangesAsync();
                await tx.CommitAsync();
                return Results.Ok(r);
            }
        );
        api.MapPost(
            "/organizations/{id:guid}/restructure",
            async (Guid id, ReparentInput input, GovernanceService s) =>
            {
                var w = await s.A.Workspace("governance.restructure");
                await using var tx = await s.A.Mutation(w);
                var o = await s.Org(id, "governance.restructure");
                Check(
                    o.Version == input.Version && o.Id != w.OrgId && input.Reason.Length >= 5,
                    "Invalid restructuring or stale record.",
                    409
                );
                await s.Parent(o.LevelId, input.ParentId, w, id);
                var r = await s.Request(
                    w,
                    id,
                    "Restructure",
                    "Restructure " + o.Name,
                    GovernanceService.Pack(input)
                );
                await s.Db.SaveChangesAsync();
                await tx.CommitAsync();
                return Results.Ok(r);
            }
        );
        api.MapGet(
            "/clubs/{id:guid}/health",
            async (Guid id, GovernanceService s) =>
            {
                var w = await s.A.Workspace("governance.reports");
                await s.Scope(w, id);
                return await Health(id, s, w);
            }
        );
        api.MapGet(
            "/administration/dashboard",
            async (GovernanceService s) =>
            {
                var w = await s.A.Workspace("governance.reports");
                var ids = (await s.Organizations(w)).Select(x => x.Id);
                var clubs = s.Db.Orgs.Where(x =>
                    x.TenantId == w.TenantId
                    && ids.Contains(x.Id)
                    && s.Db.Levels.Any(l => l.Id == x.LevelId && l.IsClub)
                );
                var members = await s.Members(w);
                var current = GovernanceService
                    .Current(s.Db)
                    .Where(x => x.TenantId == w.TenantId && ids.Contains(x.OrgId));
                var now = DateTimeOffset.UtcNow;
                var month = new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, TimeSpan.Zero);
                var memberStats = await members
                    .GroupBy(x => x.TenantId)
                    .Select(g => new
                    {
                        Active = g.Where(x =>
                                x.Status == "Active" && (x.EndsAt == null || x.EndsAt > now)
                            )
                            .Select(x => x.PersonId)
                            .Distinct()
                            .Count(),
                        Joined = g.Where(x => x.JoinedAt >= month)
                            .Select(x => x.PersonId)
                            .Distinct()
                            .Count(),
                    })
                    .SingleOrDefaultAsync();
                var clubStatuses = await clubs
                    .GroupBy(x => x.Status)
                    .Select(g => new { status = g.Key, count = g.Count() })
                    .ToListAsync();
                var officerStats = await current
                    .GroupBy(x => x.TenantId)
                    .Select(g => new
                    {
                        Total = g.Count(),
                        Expiring = g.Count(x => x.EndsAt <= now.AddDays(30)),
                    })
                    .SingleOrDefaultAsync();
                return new
                {
                    scope = await s
                        .Db.Orgs.Where(x => x.Id == w.OrgId)
                        .Select(x => new
                        {
                            x.Id,
                            x.Name,
                            x.LevelId,
                        })
                        .SingleAsync(),
                    role = w.RoleId,
                    clubs = clubStatuses.Sum(x => x.count),
                    clubStatuses,
                    activeMembers = memberStats?.Active ?? 0,
                    joinedThisMonth = memberStats?.Joined ?? 0,
                    pendingApplications = await s.Db.MembershipApplications.CountAsync(x =>
                        x.TenantId == w.TenantId
                        && ids.Contains(x.ClubId)
                        && MembershipStatistics.PendingApplicationStates.Contains(x.Status)
                    ),
                    currentOfficers = officerStats?.Total ?? 0,
                    expiringOfficers = officerStats?.Expiring ?? 0,
                    scheduledSuccessors = await s.Db.OfficerAppointments.CountAsync(x =>
                        x.TenantId == w.TenantId
                        && ids.Contains(x.OrgId)
                        && x.Status == "Scheduled"
                        && x.EndsAt > now
                    ),
                    pendingGovernance = await s.Db.GovernanceRequests.CountAsync(x =>
                        x.TenantId == w.TenantId
                        && ids.Contains(x.OrgId)
                        && new[] { "Submitted", "Under Review" }.Contains(x.Status)
                    ),
                    activeCommittees = await s.Db.Committees.CountAsync(x =>
                        x.TenantId == w.TenantId
                        && ids.Contains(x.OrgId)
                        && x.Status == "Active"
                        && x.StartsAt <= now
                        && x.EndsAt > now
                    ),
                    mandatoryVacancies = await (
                        from o in clubs.Where(o => o.Status == "Active")
                        from p in s.Db.OfficerPositions
                        where
                            p.TenantId == w.TenantId
                            && p.LevelId == o.LevelId
                            && p.Mandatory
                            && p.Active
                            && !current.Any(a => a.OrgId == o.Id && a.PositionId == p.Id)
                        select p.Id
                    ).CountAsync(),
                    countries = await clubs
                        .GroupBy(x => x.Country)
                        .Select(g => new { name = g.Key, count = g.Count() })
                        .ToListAsync(),
                    distribution = await clubs
                        .OrderBy(x => x.Name)
                        .Take(20)
                        .Select(x => new
                        {
                            x.Id,
                            x.Name,
                            x.Status,
                            Members = members
                                .Where(m =>
                                    m.TenantId == w.TenantId
                                    && m.ClubId == x.Id
                                    && m.Status == "Active"
                                    && (m.EndsAt == null || m.EndsAt > now)
                                )
                                .Select(m => m.PersonId)
                                .Distinct()
                                .Count(),
                            Officers = current.Count(a => a.OrgId == x.Id),
                        })
                        .ToListAsync(),
                };
            }
        );
        api.MapGet(
            "/leadership/me",
            async (GovernanceService s) =>
            {
                var u = await s.A.User();
                return await (
                    from a in s.Db.OfficerAppointments
                    join p in s.Db.Persons on a.PersonId equals p.Id
                    join pos in s.Db.OfficerPositions on a.PositionId equals pos.Id
                    join o in s.Db.Orgs on a.OrgId equals o.Id
                    where p.UserId == u.Id
                    orderby a.StartsAt descending
                    select new
                    {
                        a.Id,
                        Title = a.PositionTitle != "" ? a.PositionTitle : pos.Title,
                        Organization = a.OrganizationName != "" ? a.OrganizationName : o.Name,
                        a.Status,
                        a.StartsAt,
                        a.EndsAt,
                        Effective = GovernanceService.Current(s.Db).Any(x => x.Id == a.Id),
                    }
                )
                    .Take(100)
                    .ToListAsync();
            }
        );
    }

    public static async Task<object> Health(Guid id, GovernanceService s, Assignment w)
    {
        var o = await s.Db.Orgs.SingleAsync(x => x.Id == id && x.TenantId == w.TenantId);
        var config = await s.Db.ClubHealthConfigurations.SingleAsync(x => x.TenantId == w.TenantId);
        var now = DateTimeOffset.UtcNow;
        var active = await s
            .Db.Memberships.Where(x =>
                x.TenantId == w.TenantId
                && x.ClubId == id
                && x.Status == "Active"
                && (x.EndsAt == null || x.EndsAt > now)
            )
            .Select(x => x.PersonId)
            .Distinct()
            .CountAsync();
        var mandatory = await s
            .Db.OfficerPositions.Where(x =>
                x.TenantId == w.TenantId && x.LevelId == o.LevelId && x.Mandatory && x.Active
            )
            .Select(x => new { x.Id, x.Title })
            .ToListAsync();
        var covered = await GovernanceService
            .Current(s.Db)
            .Where(x => x.OrgId == id)
            .Select(x => x.PositionId)
            .Distinct()
            .ToArrayAsync();
        var pending = await s.Db.GovernanceRequests.CountAsync(x =>
            x.OrgId == id && new[] { "Submitted", "Under Review" }.Contains(x.Status)
        );
        var fields = new[]
        {
            o.Name,
            o.Country,
            o.City,
            o.Email,
            o.MeetingSchedule,
            o.MeetingVenue,
        };
        int complete = fields.Count(x => !string.IsNullOrWhiteSpace(x));
        var leadership =
            mandatory.Count == 0
                ? 1d
                : (double)mandatory.Count(x => covered.Contains(x.Id)) / mandatory.Count;
        var membership = Math.Min(1d, (double)active / config.MinimumActiveMembers);
        var governance = pending == 0 ? 1d : 1d / (1 + pending);
        var data = (double)complete / fields.Length;
        return new
        {
            score = Math.Round(
                leadership * config.LeadershipWeight
                    + membership * config.MembershipWeight
                    + governance * config.GovernanceWeight
                    + data * config.DataWeight,
                1
            ),
            activeMembers = active,
            operationalStatus = o.Status,
            leadershipCoverage = Math.Round(leadership * 100, 1),
            vacancies = mandatory.Where(x => !covered.Contains(x.Id)),
            pendingGovernance = pending,
            dataFieldsComplete = complete,
            dataFieldsTotal = fields.Length,
            expiringOfficers = await GovernanceService
                .Current(s.Db)
                .CountAsync(x => x.OrgId == id && x.EndsAt <= now.AddDays(30)),
            activeCommittees = await s.Db.Committees.CountAsync(x =>
                x.OrgId == id && x.Status == "Active" && x.StartsAt <= now && x.EndsAt > now
            ),
            configuration = config,
            formula = "Sum of configured weights × normalized components: mandatory leadership filled/required (1 when none required); min(active distinct people/member target,1); governance=1/(1+pending requests); populated official data fields/6. No finance, events or attendance data.",
        };
    }
}
