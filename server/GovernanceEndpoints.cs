using Microsoft.EntityFrameworkCore;
using static Clubs.Access;

namespace Clubs;

public static class GovernanceEndpoints
{
    public static void MapGovernance(this RouteGroupBuilder api)
    {
        api.MapGet(
            "/members/{id:guid}/leadership",
            async (Guid id, MembershipService members) =>
            {
                var m = await members.Find(id);
                var u = await members.Access.User();
                var own = m.Person.UserId == u.Id;
                var q = members.Db.OfficerAppointments.Where(x => x.PersonId == m.PersonId);
                if (!own)
                {
                    var w = await members.Access.Workspace("members.view");
                    q = q.Where(x =>
                        x.TenantId == w.TenantId && members.Access.VisibleIds(w).Contains(x.OrgId)
                    );
                }
                return await q.OrderByDescending(x => x.StartsAt)
                    .Select(x => new
                    {
                        x.Id,
                        x.Status,
                        x.StartsAt,
                        x.EndsAt,
                        Title = x.PositionTitle != ""
                            ? x.PositionTitle
                            : members
                                .Db.OfficerPositions.Where(p => p.Id == x.PositionId)
                                .Select(p => p.Title)
                                .First(),
                        Organization = x.OrganizationName != ""
                            ? x.OrganizationName
                            : members
                                .Db.Orgs.Where(o => o.Id == x.OrgId)
                                .Select(o => o.Name)
                                .First(),
                    })
                    .Take(100)
                    .ToListAsync();
            }
        );
        api.MapGet(
            "/governance/organizations",
            async (GovernanceService s, string? search = null, Guid? id = null, int page = 1) =>
            {
                var w = await s.A.WorkspaceAny(GovernanceService.Permissions);
                var q = await s.Organizations(w);
                if (id != null)
                    q = q.Where(x => x.Id == id);
                if (!string.IsNullOrWhiteSpace(search))
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
                            x.Name,
                            x.LevelId,
                            x.ParentId,
                            x.Status,
                            x.Code,
                        })
                        .ToListAsync(),
                };
            }
        );
        api.MapGet(
            "/governance/options",
            async (GovernanceService s) =>
            {
                var w = await s.A.WorkspaceAny(
                    "clubs.view",
                    "clubs.manage",
                    "leadership.view",
                    "leadership.manage",
                    "governance.configure",
                    "governance.approve",
                    "committees.view",
                    "committees.manage",
                    "governance.documents",
                    "governance.reports"
                );
                return Results.Ok(
                    new
                    {
                        levels = await s
                            .Db.Levels.Where(x => x.TenantId == w.TenantId)
                            .Select(x => new
                            {
                                x.Id,
                                x.Name,
                                x.IsClub,
                                x.ParentLevelId,
                            })
                            .ToListAsync(),
                        positions = await s
                            .Db.OfficerPositions.Where(x => x.TenantId == w.TenantId)
                            .OrderBy(x => x.Title)
                            .ToListAsync(),
                        committeeRoles = await s
                            .Db.CommitteeRoles.Where(x => x.TenantId == w.TenantId)
                            .ToListAsync(),
                        health = await s.Db.ClubHealthConfigurations.SingleOrDefaultAsync(x =>
                            x.TenantId == w.TenantId
                        ),
                        roles = (await s.A.Grants(w)).Contains("governance.configure")
                            ? await s
                                .Db.Roles.Where(x => x.TenantId == w.TenantId)
                                .Select(x => new
                                {
                                    x.Id,
                                    x.Name,
                                    x.Permissions,
                                })
                                .ToListAsync()
                            : null,
                        workflow = await s
                            .Db.MembershipWorkflows.Include(x => x.Stages)
                                .ThenInclude(x => x.Roles)
                            .SingleOrDefaultAsync(x =>
                                x.TenantId == w.TenantId && x.Kind == "Governance" && x.Active
                            ),
                    }
                );
            }
        );
        api.MapGet(
            "/leadership/eligible",
            async (Guid orgId, GovernanceService s, string? search = null, int page = 1) =>
            {
                var w = await s.A.WorkspaceAny("leadership.manage", "committees.manage");
                await s.Scope(w, orgId);
                var ids = s.A.Tree(w.TenantId, orgId).Select(x => x.Id);
                var people = s.Db.Persons.Where(p =>
                    s.Db.Memberships.Any(m =>
                        m.PersonId == p.Id
                        && m.TenantId == w.TenantId
                        && ids.Contains(m.ClubId)
                        && m.Status == "Active"
                        && (m.EndsAt == null || m.EndsAt > DateTimeOffset.UtcNow)
                    )
                );
                if (!string.IsNullOrWhiteSpace(search))
                {
                    Check(search.Length <= 100, "Search too long.");
                    people = people.Where(p =>
                        EF.Functions.ILike(p.SearchName, "%" + search.ToLower() + "%")
                    );
                }
                return new
                {
                    total = await people.CountAsync(),
                    items = await people
                        .OrderBy(p => p.DisplayName)
                        .ThenBy(p => p.Id)
                        .Skip((Math.Clamp(page, 1, 10000) - 1) * 20)
                        .Take(20)
                        .Select(p => new
                        {
                            p.Id,
                            p.DisplayName,
                            HasAccount = p.UserId != null,
                        })
                        .ToListAsync(),
                };
            }
        );
        api.MapPost(
            "/officer-positions",
            async (PositionInput r, GovernanceService s) => await Position(null, r, s)
        );
        api.MapPut(
            "/officer-positions/{id:guid}",
            async (Guid id, PositionInput r, GovernanceService s) => await Position(id, r, s)
        );
        api.MapGet(
            "/officer-positions",
            async (GovernanceService s) =>
            {
                var w = await s.A.Workspace("leadership.view");
                return await s
                    .Db.OfficerPositions.Where(x => x.TenantId == w.TenantId)
                    .OrderBy(x => x.Title)
                    .ToListAsync();
            }
        );
        api.MapPost(
            "/leadership-terms",
            async (LeadershipTermInput r, GovernanceService s) =>
            {
                var w = await s.A.Workspace("leadership.manage");
                await using var tx = await s.A.Mutation(w);
                await s.Scope(w, r.OrgId);
                Check(
                    r.Name.Length is > 0 and <= 100
                        && new[] { "Calendar", "Fiscal", "Custom" }.Contains(r.Calendar)
                        && r.StartsAt < r.EndsAt
                        && r.EndsAt <= r.StartsAt.AddYears(5),
                    "Invalid leadership term."
                );
                try
                {
                    TimeZoneInfo.FindSystemTimeZoneById(r.TimeZone);
                }
                catch
                {
                    throw new ApiError(400, "Invalid time zone.");
                }
                var t = new LeadershipTerm
                {
                    TenantId = w.TenantId,
                    OrgId = r.OrgId,
                    Name = r.Name,
                    Calendar = r.Calendar,
                    TimeZone = r.TimeZone,
                    StartsAt = r.StartsAt.ToUniversalTime(),
                    EndsAt = r.EndsAt.ToUniversalTime(),
                };
                s.Db.Add(t);
                s.Audit(w, t.OrgId, "leadership.term.created", t.Id);
                await s.Db.SaveChangesAsync();
                await tx.CommitAsync();
                return Results.Ok(t);
            }
        );
        api.MapGet(
            "/leadership-terms",
            async (
                GovernanceService s,
                Guid? orgId = null,
                int page = 1,
                Guid? applicableToOrgId = null
            ) =>
            {
                var w = await s.A.Workspace("leadership.view");
                var q = s.Db.LeadershipTerms.Where(x => x.TenantId == w.TenantId);
                if (applicableToOrgId != null)
                {
                    await s.Scope(w, applicableToOrgId.Value);
                    var ancestors = s.Db.Database.SqlQuery<Guid>($"""
                        WITH RECURSIVE ancestors AS (
                          SELECT "Id","ParentId" FROM "Orgs" WHERE "TenantId"={w.TenantId} AND "Id"={applicableToOrgId.Value}
                          UNION ALL SELECT o."Id",o."ParentId" FROM "Orgs" o JOIN ancestors a ON o."Id"=a."ParentId" WHERE o."TenantId"={w.TenantId}
                        ) SELECT "Id" AS "Value" FROM ancestors
                        """);
                    q = q.Where(x => ancestors.Contains(x.OrgId));
                }
                else
                    q = q.Where(x => s.A.VisibleIds(w).Contains(x.OrgId));
                if (orgId != null)
                    q = q.Where(x => x.OrgId == orgId);
                return new
                {
                    total = await q.CountAsync(),
                    items = await q.OrderByDescending(x => x.StartsAt)
                        .Skip((Math.Clamp(page, 1, 10000) - 1) * 20)
                        .Take(20)
                        .Select(x => new
                        {
                            x.Id,
                            x.OrgId,
                            x.Name,
                            x.Calendar,
                            x.TimeZone,
                            x.StartsAt,
                            x.EndsAt,
                            x.Version,
                            Period = x.StartsAt > DateTimeOffset.UtcNow ? "Upcoming"
                            : x.EndsAt <= DateTimeOffset.UtcNow ? "Previous"
                            : "Current",
                        })
                        .ToListAsync(),
                };
            }
        );
        api.MapPost(
            "/officer-appointments",
            async (AppointmentInput r, GovernanceService s) =>
            {
                var w = await s.A.Workspace("leadership.manage");
                await using var tx = await s.A.Mutation(w);
                var p = await s.Propose(r, w);
                await s.Db.SaveChangesAsync();
                await tx.CommitAsync();
                return Results.Ok(
                    new
                    {
                        p.Id,
                        p.Status,
                        p.Version,
                        Request = await s
                            .Db.GovernanceRequests.Where(x =>
                                x.AppointmentId == p.Id && x.Kind == "OfficerAppointment"
                            )
                            .Select(x => new
                            {
                                x.Id,
                                x.Status,
                                x.Version,
                            })
                            .SingleOrDefaultAsync(),
                    }
                );
            }
        );
        api.MapGet("/officer-appointments", Appointments);
        api.MapGet(
            "/officer-appointments/{id:guid}",
            async (Guid id, GovernanceService s) =>
            {
                var w = await s.A.Workspace("leadership.view");
                var p = await s.Db.OfficerAppointments.SingleOrDefaultAsync(x =>
                    x.Id == id && x.TenantId == w.TenantId && s.A.VisibleIds(w).Contains(x.OrgId)
                );
                Check(p != null, "Appointment outside scope.", 404);
                return new
                {
                    appointment = p,
                    person = await s
                        .Db.Persons.Where(x => x.Id == p.PersonId)
                        .Select(x => new { x.Id, x.DisplayName })
                        .SingleAsync(),
                    position = await s
                        .Db.OfficerPositions.Where(x => x.Id == p.PositionId)
                        .Select(x => new { x.Title, x.Code })
                        .SingleAsync(),
                    history = await s
                        .Db.OfficerAppointmentHistory.Where(x => x.AppointmentId == id)
                        .OrderByDescending(x => x.At)
                        .Take(100)
                        .ToListAsync(),
                };
            }
        );
        api.MapPost(
            "/officer-appointments/{id:guid}/removal",
            async (Guid id, ReasonInput r, GovernanceService s) =>
            {
                var w = await s.A.Workspace("leadership.manage");
                await using var tx = await s.A.Mutation(w);
                var p = await s.Db.OfficerAppointments.SingleOrDefaultAsync(x =>
                    x.Id == id && x.TenantId == w.TenantId && s.A.VisibleIds(w).Contains(x.OrgId)
                );
                Check(p != null, "Appointment outside scope.", 404);
                Check(
                    p.Version == r.Version
                        && new[]
                        {
                            "Proposed",
                            "Pending approval",
                            "Scheduled",
                            "Active",
                            "Approved",
                        }.Contains(p.Status)
                        && r.Reason.Length >= 5,
                    "Invalid removal or stale appointment.",
                    409
                );
                var request = await s.Request(
                    w,
                    p.OrgId,
                    "OfficerRemoval",
                    "Remove officer appointment",
                    GovernanceService.Pack(r),
                    appointment: p.Id,
                    subject: await s
                        .Db.Persons.Where(x => x.Id == p.PersonId)
                        .Select(x => x.UserId)
                        .SingleAsync()
                );
                await s.Db.SaveChangesAsync();
                await tx.CommitAsync();
                return Results.Ok(request);
            }
        );
        api.MapPost(
            "/governance/workflows",
            async (GovernanceWorkflowInput r, GovernanceService s) =>
            {
                var w = await s.A.TenantWorkspace("governance.configure");
                await using var tx = await s.A.Mutation(w);
                Check(
                    r.Stages.Length is > 0 and <= 12
                        && r.Stages.All(x =>
                            x.Name.Length is > 0 and <= 100
                            && x.RequiredApprovals is > 0 and <= 20
                            && x.RoleIds.Length is > 0 and <= 30
                        ),
                    "Invalid stages."
                );
                var roles = r.Stages.SelectMany(x => x.RoleIds).Distinct().ToArray();
                Check(
                    await s.Db.Roles.CountAsync(x =>
                        x.TenantId == w.TenantId
                        && roles.Contains(x.Id)
                        && s.Db.RolePermissions.Any(g =>
                            g.RoleId == x.Id && g.PermissionId == "governance.approve"
                        )
                    ) == roles.Length,
                    "Reviewer roles must hold governance.approve in this tenant."
                );
                await s
                    .Db.MembershipWorkflows.Where(x =>
                        x.TenantId == w.TenantId && x.Kind == "Governance" && x.Active
                    )
                    .ExecuteUpdateAsync(u => u.SetProperty(x => x.Active, false));
                var version =
                    (
                        await s
                            .Db.MembershipWorkflows.Where(x =>
                                x.TenantId == w.TenantId && x.Kind == "Governance"
                            )
                            .MaxAsync(x => (int?)x.Version)
                        ?? 0
                    ) + 1;
                var flow = new MembershipWorkflow
                {
                    TenantId = w.TenantId,
                    Kind = "Governance",
                    Version = version,
                    Stages = r
                        .Stages.Select(
                            (x, i) =>
                                new MembershipApprovalStage
                                {
                                    TenantId = w.TenantId,
                                    Sequence = i + 1,
                                    Name = x.Name,
                                    RequiredApprovals = x.RequiredApprovals,
                                    AllowDelegation = r.AllowDelegation,
                                    Roles = x
                                        .RoleIds.Distinct()
                                        .Select(role => new MembershipStageRole
                                        {
                                            TenantId = w.TenantId,
                                            RoleId = role,
                                        })
                                        .ToList(),
                                }
                        )
                        .ToList(),
                };
                s.Db.Add(flow);
                s.Audit(w, w.OrgId, "governance.workflow.version", flow.Id);
                await s.Db.SaveChangesAsync();
                await tx.CommitAsync();
                return Results.Ok(new { flow.Id, flow.Version });
            }
        );
        api.MapGet(
            "/governance/requests",
            async (GovernanceService s, string? status = null, Guid? orgId = null, int page = 1) =>
            {
                var w = await s.A.WorkspaceAny(
                    "governance.approve",
                    "clubs.manage",
                    "leadership.manage",
                    "committees.manage",
                    "governance.documents"
                );
                var q = await s.Requests(w);
                if (status != null)
                    q = q.Where(x => x.Status == status);
                if (orgId != null)
                    q = q.Where(x => x.OrgId == orgId);
                return new
                {
                    total = await q.CountAsync(),
                    items = await q.OrderByDescending(x => x.UpdatedAt)
                        .ThenBy(x => x.Id)
                        .Skip((Math.Clamp(page, 1, 10000) - 1) * 20)
                        .Take(20)
                        .Select(x => new
                        {
                            x.Id,
                            x.Title,
                            x.Kind,
                            x.OrgId,
                            Organization = s
                                .Db.Orgs.Where(o => o.Id == x.OrgId)
                                .Select(o => o.Name)
                                .First(),
                            x.Status,
                            x.Version,
                            x.Revision,
                            x.CurrentSequence,
                            x.CreatedAt,
                            Stage = s
                                .Db.MembershipApprovalStages.Where(st =>
                                    st.WorkflowId == x.WorkflowId
                                    && st.Sequence == x.CurrentSequence
                                )
                                .Select(st => st.Name)
                                .FirstOrDefault(),
                            Mine = x.CreatorId == w.UserId,
                        })
                        .ToListAsync(),
                };
            }
        );
        api.MapGet(
            "/governance/requests/{id:guid}",
            async (Guid id, GovernanceService s) =>
            {
                var w = await s.A.WorkspaceAny(
                    "governance.approve",
                    "clubs.manage",
                    "leadership.manage",
                    "committees.manage",
                    "governance.documents"
                );
                var r = await Request(id, s, w);
                var stage = await s
                    .Db.MembershipApprovalStages.Include(x => x.Roles)
                    .SingleOrDefaultAsync(x =>
                        x.WorkflowId == r.WorkflowId && x.Sequence == r.CurrentSequence
                    );
                return new
                {
                    request = r,
                    stage = stage == null
                        ? null
                        : new
                        {
                            stage.Id,
                            stage.Name,
                            stage.RequiredApprovals,
                            stage.AllowDelegation,
                            Roles = stage.Roles.Select(x => x.RoleId),
                        },
                    canDecide = (await s.A.Grants(w)).Contains("governance.approve")
                        && r.CreatorId != w.UserId
                        && r.SubjectUserId != w.UserId
                        && new[] { "Submitted", "Under Review" }.Contains(r.Status)
                        && stage != null
                        && (stage.Roles.Any(x => x.RoleId == w.RoleId) || stage.AllowDelegation),
                    canEdit = r.CreatorId == w.UserId
                        && new[] { "Draft", "Changes Requested" }.Contains(r.Status),
                    revisions = await s
                        .Db.Audits.Where(x =>
                            x.TenantId == w.TenantId
                            && x.OrgId == r.OrgId
                            && x.Resource == r.Id.ToString()
                            && x.Action == "governance.revised"
                        )
                        .OrderBy(x => x.At)
                        .Select(x => new
                        {
                            x.Id,
                            x.Changes,
                            x.At,
                        })
                        .Take(100)
                        .ToListAsync(),
                    decisions = await s
                        .Db.MembershipApprovals.Where(x => x.GovernanceRequestId == id)
                        .OrderBy(x => x.At)
                        .Select(x => new
                        {
                            x.Id,
                            x.Decision,
                            x.Comment,
                            x.At,
                            x.Revision,
                            x.StageId,
                            x.PrincipalUserId,
                            Actor = s
                                .Db.Users.Where(u => u.Id == x.ActorId)
                                .Select(u => u.Name)
                                .First(),
                        })
                        .ToListAsync(),
                };
            }
        );
        api.MapPost(
            "/governance/requests/{id:guid}/submit",
            async (Guid id, VersionInput input, GovernanceService s) =>
            {
                var w = await s.A.WorkspaceAny(
                    "clubs.manage",
                    "leadership.manage",
                    "committees.manage",
                    "governance.documents"
                );
                await using var tx = await s.A.Mutation(w);
                var r = await Request(id, s, w);
                Check(r.Version == input.Version, "Request changed.", 409);
                await RequiredPermission(r, s, w);
                await s.Submit(r, w);
                await s.Db.SaveChangesAsync();
                await tx.CommitAsync();
                return Results.Ok(
                    new
                    {
                        r.Id,
                        r.Status,
                        r.Version,
                    }
                );
            }
        );
        api.MapPut(
            "/governance/requests/{id:guid}",
            async (Guid id, GovernanceChangeInput input, GovernanceService s) =>
            {
                var w = await s.A.WorkspaceAny(
                    "clubs.manage",
                    "leadership.manage",
                    "committees.manage",
                    "governance.documents"
                );
                await using var tx = await s.A.Mutation(w);
                var r = await Request(id, s, w);
                await RequiredPermission(r, s, w);
                Check(
                    r.CreatorId == w.UserId
                        && new[] { "Draft", "Changes Requested" }.Contains(r.Status)
                        && r.Version == input.Version,
                    "Request is not editable or changed.",
                    409
                );
                Check(input.Payload.Length <= 20000, "Payload too large.");
                var previousPayload = r.Payload;
                if (
                    !new[] { "ClubCreate", "ClubUpdate", "ClubStatus", "Restructure" }.Contains(
                        r.Kind
                    )
                )
                    await s.ReviseLinked(r, input.Payload, w);
                if (r.Kind is "ClubCreate" or "ClubUpdate")
                {
                    var data = GovernanceService.Unpack<ClubInput>(input.Payload);
                    if (r.Kind == "ClubCreate")
                        await s.Parent(data.LevelId, data.ParentId, w);
                    else
                        Check(
                            await s.Db.Orgs.AnyAsync(o =>
                                o.Id == r.OrgId
                                && o.TenantId == w.TenantId
                                && o.ParentId == data.ParentId
                                && o.LevelId == data.LevelId
                            ),
                            "Club identity and parent cannot change through profile revision."
                        );
                    Check(
                        data.ParentId == r.OrgId || r.Kind == "ClubUpdate",
                        "A new club must remain within its submitted parent scope."
                    );
                }
                r.Payload = input.Payload;
                r.Revision++;
                r.Version++;
                r.Status = "Draft";
                r.UpdatedAt = DateTimeOffset.UtcNow;
                s.Audit(
                    w,
                    r.OrgId,
                    "governance.revised",
                    r.Id,
                    GovernanceService.Pack(
                        new
                        {
                            fromRevision = r.Revision - 1,
                            oldPayload = previousPayload,
                            newPayload = r.Payload,
                        }
                    )
                );
                await s.Db.SaveChangesAsync();
                await tx.CommitAsync();
                return Results.Ok(new { r.Id, r.Version });
            }
        );
        api.MapPost(
            "/governance/requests/{id:guid}/withdraw",
            async (Guid id, VersionInput input, GovernanceService s) =>
            {
                var w = await s.A.WorkspaceAny(
                    "clubs.manage",
                    "leadership.manage",
                    "committees.manage",
                    "governance.documents",
                    "governance.approve"
                );
                await using var tx = await s.A.Mutation(w);
                var r = await Request(id, s, w);
                Check(
                    r.Version == input.Version
                        && new[]
                        {
                            "Draft",
                            "Submitted",
                            "Under Review",
                            "Changes Requested",
                        }.Contains(r.Status),
                    "Request is no longer withdrawable.",
                    409
                );
                Check(
                    r.CreatorId == w.UserId
                        || (await s.A.Grants(w)).Contains("governance.configure"),
                    "Only the requester or governance administrator can cancel.",
                    403
                );
                r.Status = r.CreatorId == w.UserId ? "Withdrawn" : "Cancelled";
                r.Version++;
                if (r.Kind == "OfficerAppointment" && r.AppointmentId != null)
                {
                    var p = await s.Db.OfficerAppointments.SingleAsync(x =>
                        x.Id == r.AppointmentId
                    );
                    await s.End(p, "Cancelled", "Proposal withdrawn", w.UserId);
                }
                s.Audit(w, r.OrgId, "governance.withdrawn", r.Id);
                await s.Db.SaveChangesAsync();
                await tx.CommitAsync();
                return Results.NoContent();
            }
        );
        api.MapPost(
            "/governance/requests/{id:guid}/decisions",
            async (Guid id, GovernanceDecisionInput input, GovernanceService s) =>
            {
                var w = await s.A.Workspace("governance.approve");
                await using var tx = await s.A.Mutation(w);
                var r = await Request(id, s, w);
                Check(
                    r.Version == input.Version
                        && new[] { "Submitted", "Under Review" }.Contains(r.Status),
                    "Request changed or is not awaiting review.",
                    409
                );
                Check(
                    r.CreatorId != w.UserId && r.SubjectUserId != w.UserId,
                    "You cannot approve your own request or office.",
                    403
                );
                if (input.DelegationId != null)
                {
                    var d = await s.Db.GovernanceDelegations.SingleOrDefaultAsync(x =>
                        x.Id == input.DelegationId && x.TenantId == w.TenantId
                    );
                    Check(
                        d != null
                            && d.DelegatorId != r.CreatorId
                            && d.DelegatorId != r.SubjectUserId
                            && await s.A.Tree(w.TenantId, d.OrgId).AnyAsync(x => x.Id == r.OrgId),
                        "Delegation cannot bypass subject or scope restrictions.",
                        403
                    );
                }
                var progress = await ApprovalEngine.Record(
                    s.Db,
                    w,
                    r.WorkflowId!.Value,
                    r.CurrentSequence,
                    r.Revision,
                    r.Id,
                    true,
                    new(input.Decision, input.Comment, input.Version),
                    input.DelegationId
                );
                r.Status =
                    input.Decision == "Reject" ? "Rejected"
                    : input.Decision == "RequestChanges" ? "Changes Requested"
                    : "Under Review";
                if (
                    input.Decision == "Approve"
                    && progress.Approvals >= progress.Stage.RequiredApprovals
                )
                {
                    if (progress.Next != null)
                    {
                        r.CurrentSequence = progress.Next.Sequence;
                        await s.NotifyReviewers(r);
                    }
                    else
                    {
                        await s.Apply(r, w);
                        r.Status = "Approved";
                    }
                }
                if (input.Decision == "Reject" && r.Kind == "OfficerAppointment")
                {
                    var p = await s.Db.OfficerAppointments.SingleAsync(x =>
                        x.Id == r.AppointmentId
                    );
                    await s.End(p, "Cancelled", "Appointment rejected: " + input.Comment, w.UserId);
                }
                r.Version++;
                r.UpdatedAt = DateTimeOffset.UtcNow;
                await s.NotifyStatus(r);
                s.Audit(w, r.OrgId, "governance.decision", r.Id, input.Decision);
                await s.Db.SaveChangesAsync();
                await tx.CommitAsync();
                return Results.Ok(
                    new
                    {
                        r.Id,
                        r.Status,
                        r.Version,
                        r.ResultId,
                    }
                );
            }
        );
        api.MapPost(
            "/governance/delegations",
            async (DelegationInput r, GovernanceService s) =>
            {
                var w = await s.A.Workspace("governance.approve");
                Check(
                    r.DelegateId != w.UserId
                        && r.StartsAt < r.EndsAt
                        && r.EndsAt <= DateTimeOffset.UtcNow.AddDays(90),
                    "Invalid delegation dates/recipient."
                );
                await using var tx = await s.A.Mutation(w);
                Check(
                    await s.Db.Assignments.AnyAsync(x =>
                        x.UserId == r.DelegateId
                        && x.TenantId == w.TenantId
                        && x.OrgId == w.OrgId
                        && s.Db.RolePermissions.Any(g =>
                            g.RoleId == x.RoleId && g.PermissionId == "governance.approve"
                        )
                        && (
                            x.AppointmentId == null
                            || GovernanceService.Current(s.Db).Any(p => p.Id == x.AppointmentId)
                        )
                    ),
                    "Delegate must already be an effective reviewer in the same organizational scope.",
                    403
                );
                var d = new GovernanceDelegation
                {
                    TenantId = w.TenantId,
                    OrgId = w.OrgId,
                    RoleId = w.RoleId,
                    DelegatorId = w.UserId,
                    DelegateId = r.DelegateId,
                    StartsAt = r.StartsAt.ToUniversalTime(),
                    EndsAt = r.EndsAt.ToUniversalTime(),
                };
                s.Db.Add(d);
                s.Audit(w, w.OrgId, "governance.delegated", d.Id);
                await s.Db.SaveChangesAsync();
                await tx.CommitAsync();
                return Results.Ok(d);
            }
        );
        api.MapGet(
            "/governance/delegations",
            async (GovernanceService s) =>
            {
                var w = await s.A.Workspace("governance.approve");
                return await s
                    .Db.GovernanceDelegations.Where(x =>
                        x.TenantId == w.TenantId
                        && (x.DelegatorId == w.UserId || x.DelegateId == w.UserId)
                    )
                    .OrderByDescending(x => x.EndsAt)
                    .Take(100)
                    .ToListAsync();
            }
        );
        api.MapPost(
            "/governance/delegations/{id:guid}/revoke",
            async (Guid id, GovernanceService s) =>
            {
                var w = await s.A.Workspace("governance.approve");
                var d = await s.Db.GovernanceDelegations.SingleOrDefaultAsync(x =>
                    x.Id == id && x.DelegatorId == w.UserId && x.TenantId == w.TenantId
                );
                Check(d != null, "Delegation not found.", 404);
                d.Revoked = true;
                await s.A.Log(w, "governance.delegation.revoked", id.ToString());
                return Results.NoContent();
            }
        );
        api.MapPut(
            "/governance/health-configuration",
            async (HealthInput r, GovernanceService s) =>
            {
                var w = await s.A.TenantWorkspace("governance.configure");
                Check(
                    new[]
                    {
                        r.LeadershipWeight,
                        r.MembershipWeight,
                        r.GovernanceWeight,
                        r.DataWeight,
                    }.All(x => x >= 0)
                        && r.LeadershipWeight
                            + r.MembershipWeight
                            + r.GovernanceWeight
                            + r.DataWeight
                            == 100
                        && r.MinimumActiveMembers is >= 1 and <= 100000,
                    "Weights must total 100 and membership target must be positive."
                );
                await using var tx = await s.A.Mutation(w);
                var c = await s.Db.ClubHealthConfigurations.SingleAsync(x =>
                    x.TenantId == w.TenantId
                );
                Check(c.Version == r.Version, "Configuration changed.", 409);
                c.LeadershipWeight = r.LeadershipWeight;
                c.MembershipWeight = r.MembershipWeight;
                c.GovernanceWeight = r.GovernanceWeight;
                c.DataWeight = r.DataWeight;
                c.MinimumActiveMembers = r.MinimumActiveMembers;
                c.RequireClubChangeApproval = r.RequireClubChangeApproval;
                c.Version++;
                s.Audit(w, w.OrgId, "club.health.configuration", c.Id);
                await s.Db.SaveChangesAsync();
                await tx.CommitAsync();
                return Results.Ok(c);
            }
        );
        api.MapClubGovernance();
        api.MapCommittees();
        api.MapGovernanceDocuments();
        api.MapHandovers();
    }

    public static async Task<GovernanceRequest> Request(Guid id, GovernanceService s, Assignment w)
    {
        var r = await (await s.Requests(w)).SingleOrDefaultAsync(x => x.Id == id);
        Check(r != null, "Governance request outside scope.", 404);
        return r;
    }

    private static async Task RequiredPermission(
        GovernanceRequest r,
        GovernanceService s,
        Assignment w
    )
    {
        var permission =
            r.Kind.StartsWith("Club") ? "clubs.manage"
            : r.Kind == "Restructure" ? "governance.restructure"
            : r.Kind.StartsWith("Committee") ? "committees.manage"
            : r.Kind == "Resolution" ? "governance.documents"
            : "leadership.manage";
        Check(
            (await s.A.Grants(w)).Contains(permission),
            "You no longer hold the authority to submit this request.",
            403
        );
    }

    private static async Task<IResult> Position(Guid? id, PositionInput r, GovernanceService s)
    {
        var w = await s.A.TenantWorkspace("governance.configure");
        await using var tx = await s.A.Mutation(w);
        Check(
            r.Code.Trim().Length is > 0 and <= 40
                && r.Title.Trim().Length is > 0 and <= 120
                && r.DefaultMonths is > 0 and <= 120
                && (r.MaximumHolders == null || r.MaximumHolders is > 0 and <= 100)
                && r.MinimumMembershipDays >= 0,
            "Invalid position rules."
        );
        Check(
            await s.Db.Levels.AnyAsync(x => x.Id == r.LevelId && x.TenantId == w.TenantId),
            "Invalid organizational level."
        );
        await s.ValidateGrant(r.GrantRoleId, w);
        if (r.ReportsToId != null)
            Check(
                r.ReportsToId != id
                    && await s.Db.OfficerPositions.AnyAsync(x =>
                        x.Id == r.ReportsToId && x.TenantId == w.TenantId
                    ),
                "Invalid reporting relationship."
            );
        if (id != null && r.ReportsToId != null)
            Check(
                !await s
                    .Db.Database.SqlQuery<Guid>(
                        $"""WITH RECURSIVE chain AS(SELECT "Id","ReportsToId","TenantId" FROM "OfficerPositions" WHERE "Id"={r.ReportsToId.Value} AND "TenantId"={w.TenantId} UNION SELECT p."Id",p."ReportsToId",p."TenantId" FROM "OfficerPositions" p JOIN chain c ON p."Id"=c."ReportsToId" AND p."TenantId"=c."TenantId") SELECT "Id" AS "Value" FROM chain"""
                    )
                    .AnyAsync(x => x == id.Value),
                "Position reporting cycle rejected."
            );
        OfficerPosition p;
        if (id == null)
        {
            p = new() { TenantId = w.TenantId };
            s.Db.Add(p);
        }
        else
        {
            p =
                await s.Db.OfficerPositions.SingleOrDefaultAsync(x =>
                    x.Id == id && x.TenantId == w.TenantId
                ) ?? throw new ApiError(404, "Position not found.");
            Check(p.Version == r.Version, "Position changed.", 409);
            Check(
                !await s.Db.OfficerAppointments.AnyAsync(x =>
                    x.PositionId == id
                    && new[]
                    {
                        "Proposed",
                        "Pending approval",
                        "Approved",
                        "Scheduled",
                        "Active",
                    }.Contains(x.Status)
                ),
                "Positions with live proposals/terms are frozen; create a replacement position.",
                409
            );
        }
        p.Code = r.Code.Trim().ToUpper();
        p.Title = r.Title.Trim();
        p.LevelId = r.LevelId;
        p.Category = r.Category;
        p.DefaultMonths = r.DefaultMonths;
        p.MaximumHolders = r.MaximumHolders;
        p.Mandatory = r.Mandatory;
        p.RequireActiveMembership = r.RequireActiveMembership;
        p.MinimumMembershipDays = r.MinimumMembershipDays;
        p.ApprovalRequired = r.ApprovalRequired;
        p.ReportsToId = r.ReportsToId;
        p.GrantRoleId = r.GrantRoleId;
        p.Active = r.Active;
        p.Version++;
        s.Audit(w, w.OrgId, "position.saved", p.Id);
        await s.Db.SaveChangesAsync();
        await tx.CommitAsync();
        return Results.Ok(p);
    }

    private static async Task<IResult> Appointments(
        GovernanceService s,
        Guid? orgId = null,
        string? search = null,
        string? status = null,
        int page = 1
    )
    {
        var w = await s.A.Workspace("leadership.view");
        var q = s.Db.OfficerAppointments.Where(x =>
            x.TenantId == w.TenantId && s.A.VisibleIds(w).Contains(x.OrgId)
        );
        if (orgId != null)
            q = q.Where(x => x.OrgId == orgId);
        if (status == "Current")
            q = q.Where(x => GovernanceService.Current(s.Db).Any(p => p.Id == x.Id));
        else if (status != null)
            q = q.Where(x => x.Status == status);
        if (!string.IsNullOrWhiteSpace(search))
            q = q.Where(x =>
                s.Db.Persons.Any(p =>
                    p.Id == x.PersonId
                    && EF.Functions.ILike(p.SearchName, "%" + search.ToLower() + "%")
                )
                || s.Db.OfficerPositions.Any(p =>
                    p.Id == x.PositionId && EF.Functions.ILike(p.Title, "%" + search + "%")
                )
            );
        return Results.Ok(
            new
            {
                total = await q.CountAsync(),
                items = await q.OrderByDescending(x => x.StartsAt)
                    .ThenBy(x => x.Id)
                    .Skip((Math.Clamp(page, 1, 10000) - 1) * 20)
                    .Take(20)
                    .Select(x => new
                    {
                        x.Id,
                        x.PersonId,
                        x.OrgId,
                        x.PositionId,
                        x.TermId,
                        x.StartsAt,
                        x.EndsAt,
                        x.EffectiveAt,
                        x.Method,
                        x.Status,
                        x.Version,
                        x.Notes,
                        Name = s
                            .Db.Persons.Where(p => p.Id == x.PersonId)
                            .Select(p => p.DisplayName)
                            .First(),
                        Title = x.PositionTitle != ""
                            ? x.PositionTitle
                            : s
                                .Db.OfficerPositions.Where(p => p.Id == x.PositionId)
                                .Select(p => p.Title)
                                .First(),
                        Organization = x.OrganizationName != ""
                            ? x.OrganizationName
                            : s.Db.Orgs.Where(o => o.Id == x.OrgId).Select(o => o.Name).First(),
                        Effective = GovernanceService.Current(s.Db).Any(p => p.Id == x.Id),
                    })
                    .ToListAsync(),
            }
        );
    }
}

public record LeadershipTermInput(
    Guid OrgId,
    string Name,
    string Calendar,
    DateTimeOffset StartsAt,
    DateTimeOffset EndsAt,
    string TimeZone = "UTC"
);

public record DelegationInput(Guid DelegateId, DateTimeOffset StartsAt, DateTimeOffset EndsAt);

public record HealthInput(
    int LeadershipWeight,
    int MembershipWeight,
    int GovernanceWeight,
    int DataWeight,
    int MinimumActiveMembers,
    bool RequireClubChangeApproval,
    long Version
);
