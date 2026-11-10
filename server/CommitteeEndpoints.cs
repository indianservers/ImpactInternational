using Microsoft.EntityFrameworkCore;
using static Clubs.Access;

namespace Clubs;

public static class CommitteeEndpoints
{
    public static void MapCommittees(this RouteGroupBuilder api)
    {
        api.MapGet(
            "/committees",
            async (
                GovernanceService s,
                bool mine = false,
                string? search = null,
                Guid? orgId = null,
                int page = 1
            ) =>
            {
                var u = await s.A.User();
                var q = s.Db.Committees.AsQueryable();
                if (mine)
                    q = q.Where(x =>
                        x.Status == "Active"
                        && x.StartsAt <= DateTimeOffset.UtcNow
                        && x.EndsAt > DateTimeOffset.UtcNow
                        && s.Db.CommitteeMemberships.Any(m =>
                            m.CommitteeId == x.Id
                            && m.Status == "Active"
                            && m.StartsAt <= DateTimeOffset.UtcNow
                            && m.EndsAt > DateTimeOffset.UtcNow
                            && s.Db.Persons.Any(p => p.Id == m.PersonId && p.UserId == u.Id)
                        )
                    );
                else
                {
                    var w = await s.A.Workspace("committees.view");
                    q = q.Where(x =>
                        x.TenantId == w.TenantId && s.A.VisibleIds(w).Contains(x.OrgId)
                    );
                }
                if (orgId != null)
                    q = q.Where(x => x.OrgId == orgId);
                if (search != null)
                {
                    Check(search.Length <= 100, "Search too long.");
                    q = q.Where(x =>
                        EF.Functions.ILike(x.Name, "%" + search + "%")
                        || EF.Functions.ILike(x.Code, "%" + search + "%")
                    );
                }
                return new
                {
                    total = await q.CountAsync(),
                    items = await q.OrderBy(x => x.Name)
                        .ThenBy(x => x.Id)
                        .Skip((Math.Clamp(page, 1, 10000) - 1) * 20)
                        .Take(20)
                        .Select(x => new
                        {
                            x.Id,
                            x.OrgId,
                            x.Name,
                            x.Code,
                            x.Purpose,
                            x.Category,
                            x.Status,
                            x.StartsAt,
                            x.EndsAt,
                            x.Version,
                            Organization = s
                                .Db.Orgs.Where(o => o.Id == x.OrgId)
                                .Select(o => o.Name)
                                .First(),
                            Members = s.Db.CommitteeMemberships.Count(m =>
                                m.CommitteeId == x.Id
                                && m.Status == "Active"
                                && m.StartsAt <= DateTimeOffset.UtcNow
                                && m.EndsAt > DateTimeOffset.UtcNow
                            ),
                        })
                        .ToListAsync(),
                };
            }
        );
        api.MapPost(
            "/committees",
            async (CommitteeInput r, GovernanceService s) =>
            {
                var w = await s.A.Workspace("committees.manage");
                await using var tx = await s.A.Mutation(w);
                await s.Scope(w, r.OrgId);
                Validate(r);
                Check(
                    !await s.Db.Committees.AnyAsync(x =>
                        x.TenantId == w.TenantId && x.OrgId == r.OrgId && x.Code == r.Code.ToUpper()
                    ),
                    "Committee code already exists.",
                    409
                );
                var c = new Committee
                {
                    TenantId = w.TenantId,
                    OrgId = r.OrgId,
                    Name = r.Name,
                    Code = r.Code.ToUpper(),
                    Purpose = r.Purpose,
                    Category = r.Category,
                    Description = r.Description,
                    Notes = r.Notes,
                    StartsAt = r.StartsAt.ToUniversalTime(),
                    EndsAt = r.EndsAt.ToUniversalTime(),
                };
                s.Db.Add(c);
                var request = await s.Request(
                    w,
                    c.OrgId,
                    "CommitteeFormation",
                    "Form " + c.Name,
                    GovernanceService.Pack(r),
                    committee: c.Id
                );
                await s.Db.SaveChangesAsync();
                await tx.CommitAsync();
                return Results.Ok(
                    new
                    {
                        c.Id,
                        c.Status,
                        c.Version,
                        request,
                    }
                );
            }
        );
        api.MapGet(
            "/committees/{id:guid}",
            async (Guid id, GovernanceService s) =>
            {
                var c = await s.CommitteeAccess(id);
                bool canManage;
                try
                {
                    await s.CommitteeAccess(id, true);
                    canManage = true;
                }
                catch (ApiError)
                {
                    canManage = false;
                }
                return new
                {
                    committee = c,
                    canManage,
                    members = await (
                        from m in s.Db.CommitteeMemberships
                        join p in s.Db.Persons on m.PersonId equals p.Id
                        join role in s.Db.CommitteeRoles on m.RoleId equals role.Id
                        where m.CommitteeId == id
                        orderby m.StartsAt descending
                        select new
                        {
                            m.Id,
                            m.PersonId,
                            Name = p.DisplayName,
                            m.RoleId,
                            Role = role.Name,
                            role.CanManage,
                            m.Responsibilities,
                            m.StartsAt,
                            m.EndsAt,
                            m.Status,
                            m.Version,
                        }
                    )
                        .Take(100)
                        .ToListAsync(),
                    activity = await s
                        .Db.CommitteeActivities.Where(x => x.CommitteeId == id)
                        .OrderByDescending(x => x.At)
                        .Take(100)
                        .Select(x => new
                        {
                            x.Id,
                            x.Action,
                            x.Detail,
                            x.At,
                        })
                        .ToListAsync(),
                    pending = await s.Db.GovernanceRequests.CountAsync(x =>
                        x.CommitteeId == id
                        && new[] { "Submitted", "Under Review" }.Contains(x.Status)
                    ),
                };
            }
        );
        api.MapPut(
            "/committees/{id:guid}",
            async (Guid id, CommitteeInput r, GovernanceService s) =>
            {
                await using var tx = await s.Db.Database.BeginTransactionAsync();
                var c = await s.CommitteeAccess(id, true);
                await s.Db.Database.ExecuteSqlInterpolatedAsync(
                    $"SELECT pg_advisory_xact_lock(hashtext({c.TenantId.ToString()}))"
                );
                await s.Db.Entry(c).ReloadAsync();
                Validate(r);
                Check(
                    c.Version == r.Version && c.OrgId == r.OrgId,
                    "Committee changed or organization is immutable.",
                    409
                );
                Check(
                    c.StartsAt == r.StartsAt && c.EndsAt == r.EndsAt && c.Code == r.Code.ToUpper(),
                    "Committee identity and approved term are immutable. Submit a replacement formation."
                );
                c.Name = r.Name;
                c.Purpose = r.Purpose;
                c.Description = r.Description;
                c.Category = r.Category;
                c.Notes = r.Notes;
                c.Version++;
                s.Db.CommitteeActivities.Add(
                    new()
                    {
                        TenantId = c.TenantId,
                        CommitteeId = id,
                        ActorId = (await s.A.User()).Id,
                        Action = "committee.updated",
                        Detail = GovernanceService.Pack(r),
                    }
                );
                await s.Db.SaveChangesAsync();
                await tx.CommitAsync();
                return Results.Ok(new { c.Id, c.Version });
            }
        );
        api.MapPost(
            "/committees/{id:guid}/members",
            async (Guid id, CommitteeMemberInput r, GovernanceService s) =>
            {
                await using var tx = await s.Db.Database.BeginTransactionAsync();
                var c = await s.CommitteeAccess(id, true);
                await s.Db.Database.ExecuteSqlInterpolatedAsync(
                    $"SELECT pg_advisory_xact_lock(hashtext({c.TenantId.ToString()}))"
                );
                await s.CommitteeAccess(id, true);
                await s.AddCommitteeMember(id, r, null);
                await s.Db.SaveChangesAsync();
                await tx.CommitAsync();
                return Results.Ok(new { status = "Assigned" });
            }
        );
        api.MapPost(
            "/committees/{id:guid}/chair",
            async (Guid id, CommitteeMemberInput r, GovernanceService s) =>
            {
                var w = await s.A.Workspace("committees.manage");
                await using var tx = await s.A.Mutation(w);
                var c = await s.CommitteeAccess(id, true);
                await s.Scope(w, c.OrgId);
                Check(
                    await s.Db.CommitteeRoles.AnyAsync(x =>
                        x.Id == r.RoleId && x.TenantId == w.TenantId && x.CanManage
                    ),
                    "Select a committee leadership role."
                );
                var person = await s.Db.Persons.SingleOrDefaultAsync(x => x.Id == r.PersonId);
                Check(
                    person != null && person.UserId != w.UserId,
                    "You cannot appoint yourself committee leader.",
                    403
                );
                var request = await s.Request(
                    w,
                    c.OrgId,
                    "CommitteeChair",
                    "Appoint committee leadership: " + c.Name,
                    GovernanceService.Pack(r),
                    committee: id,
                    subject: person.UserId
                );
                await s.Db.SaveChangesAsync();
                await tx.CommitAsync();
                return Results.Ok(request);
            }
        );
        api.MapPost(
            "/committees/{id:guid}/members/{memberId:guid}/remove",
            async (Guid id, Guid memberId, ReasonInput r, GovernanceService s) =>
            {
                await using var tx = await s.Db.Database.BeginTransactionAsync();
                var c = await s.CommitteeAccess(id, true);
                await s.Db.Database.ExecuteSqlInterpolatedAsync(
                    $"SELECT pg_advisory_xact_lock(hashtext({c.TenantId.ToString()}))"
                );
                await s.CommitteeAccess(id, true);
                var m = await s.Db.CommitteeMemberships.SingleOrDefaultAsync(x =>
                    x.Id == memberId && x.CommitteeId == id
                );
                Check(
                    m != null
                        && m.Version == r.Version
                        && m.Status == "Active"
                        && r.Reason.Length >= 5,
                    "Invalid member removal or version.",
                    409
                );
                var role = await s.Db.CommitteeRoles.SingleAsync(x => x.Id == m.RoleId);
                if (role.CanManage)
                {
                    var w = await s.A.Workspace("committees.manage");
                    await s.Scope(w, c.OrgId);
                    var req = await s.Request(
                        w,
                        c.OrgId,
                        "CommitteeRemoval",
                        "Remove committee leadership: " + c.Name,
                        GovernanceService.Pack(new CommitteeRemoveInput(m.Id, r.Reason, r.Version)),
                        committee: id,
                        subject: await s
                            .Db.Persons.Where(x => x.Id == m.PersonId)
                            .Select(x => x.UserId)
                            .SingleAsync()
                    );
                    await s.Db.SaveChangesAsync();
                    await tx.CommitAsync();
                    return Results.Ok(new { request = req });
                }
                m.Status = "Removed";
                m.Version++;
                s.Db.CommitteeActivities.Add(
                    new()
                    {
                        TenantId = c.TenantId,
                        CommitteeId = id,
                        ActorId = (await s.A.User()).Id,
                        Action = "member.removed",
                        Detail = GovernanceService.Pack(new { m.PersonId, r.Reason }),
                    }
                );
                await s.Db.SaveChangesAsync();
                await tx.CommitAsync();
                return Results.Ok(new { m.Id, m.Status });
            }
        );
        api.MapPost(
            "/committees/{id:guid}/close",
            async (Guid id, ReasonInput r, GovernanceService s) =>
            {
                var w = await s.A.Workspace("committees.manage");
                await using var tx = await s.A.Mutation(w);
                var c = await s.CommitteeAccess(id, true);
                await s.Scope(w, c.OrgId);
                Check(
                    c.Version == r.Version && r.Reason.Length >= 5,
                    "Committee changed or reason missing.",
                    409
                );
                var request = await s.Request(
                    w,
                    c.OrgId,
                    "CommitteeClose",
                    "Close " + c.Name,
                    GovernanceService.Pack(r),
                    committee: id
                );
                await s.Db.SaveChangesAsync();
                await tx.CommitAsync();
                return Results.Ok(request);
            }
        );
        api.MapGet(
            "/committees/{id:guid}/eligible",
            async (Guid id, GovernanceService s, string search = "") =>
            {
                var c = await s.CommitteeAccess(id, true);
                Check(search.Length <= 100, "Search too long.");
                return await s
                    .Db.Persons.Where(p =>
                        s.Db.Memberships.Any(m =>
                            m.PersonId == p.Id
                            && m.TenantId == c.TenantId
                            && m.Status == "Active"
                            && (m.EndsAt == null || m.EndsAt > DateTimeOffset.UtcNow)
                            && s.A.Tree(c.TenantId, c.OrgId).Any(o => o.Id == m.ClubId)
                        ) && EF.Functions.ILike(p.SearchName, "%" + search.ToLower() + "%")
                    )
                    .OrderBy(x => x.DisplayName)
                    .Take(20)
                    .Select(p => new { p.Id, p.DisplayName })
                    .ToListAsync();
            }
        );
        api.MapGet(
            "/committees/{id:guid}/roles",
            async (Guid id, GovernanceService s) =>
            {
                var c = await s.CommitteeAccess(id);
                return await s
                    .Db.CommitteeRoles.Where(x => x.TenantId == c.TenantId && x.Active)
                    .Select(x => new
                    {
                        x.Id,
                        x.Name,
                        x.CanManage,
                    })
                    .ToListAsync();
            }
        );
        api.MapPost(
            "/committee-roles",
            async (CommitteeRoleInput r, GovernanceService s) =>
            {
                var w = await s.A.TenantWorkspace("governance.configure");
                Check(r.Name.Length is > 0 and <= 100, "Role name required.");
                var role = new CommitteeRole
                {
                    TenantId = w.TenantId,
                    Name = r.Name,
                    CanManage = r.CanManage,
                };
                s.Db.Add(role);
                await s.A.Log(w, "committee.role.created", role.Id.ToString());
                return Results.Ok(role);
            }
        );
    }

    private static void Validate(CommitteeInput r) =>
        Check(
            r.Name.Length is > 0 and <= 150
                && r.Code.Length is > 0 and <= 40
                && r.Purpose.Length is > 0 and <= 4000
                && r.Description.Length <= 4000
                && r.Notes.Length <= 4000
                && r.StartsAt < r.EndsAt
                && r.EndsAt <= r.StartsAt.AddYears(10),
            "Invalid committee identity, purpose or term."
        );
}

public record CommitteeRemoveInput(Guid MembershipId, string Reason, long Version);

public record CommitteeRoleInput(string Name, bool CanManage);
