using Microsoft.EntityFrameworkCore;
using static Clubs.Access;

namespace Clubs;

public record ApplicationInput(
    Guid ClubId,
    Guid CategoryId,
    string Statement = "",
    long Version = 1
);

public record VersionInput(long Version);

public record ApprovalInput(string Decision, string Comment, long Version);

public record StageInput(string Name, Guid[] RoleIds, int RequiredApprovals = 1);

public record WorkflowInput(StageInput[] Stages);

public record InvitationInput(
    Guid ClubId,
    Guid CategoryId,
    string Email = "",
    int ExpiresInDays = 7,
    int UsageLimit = 1,
    Guid? PersonId = null, string Message = ""
);

public record TokenInput(string Token);

public record LinkDecisionInput(string Decision, string Reason);

public static class MembershipWorkflowEndpoints
{
    public static void MapMembershipWorkflows(this RouteGroupBuilder api)
    {
        api.MapGet(
            "/membership/config",
            async (MembershipService s) =>
            {
                var w = await s.Access.WorkspaceAny("members.view", "members.workflow.manage");
                return new
                {
                    roles = (await s.Access.Grants(w)).Contains("members.workflow.manage")
                        ? await s
                            .Db.Roles.Where(x => x.TenantId == w.TenantId)
                            .Select(x => new
                            {
                                x.Id,
                                x.Name,
                                CanApprove = s.Db.RolePermissions.Any(g =>
                                    g.RoleId == x.Id && g.PermissionId == "members.approve"
                                ),
                            })
                            .ToListAsync()
                        : null,
                    categories = await s
                        .Db.MembershipCategories.Where(x => x.TenantId == w.TenantId)
                        .ToListAsync(),
                    policy = await s.Db.MembershipPolicies.SingleOrDefaultAsync(x =>
                        x.TenantId == w.TenantId
                    ),
                    workflow = await s
                        .Db.MembershipWorkflows.Include(x => x.Stages)
                            .ThenInclude(x => x.Roles)
                        .SingleOrDefaultAsync(x =>
                            x.TenantId == w.TenantId && x.Kind == "Membership" && x.Active
                        ),
                };
            }
        );
        api.MapPost(
            "/membership/categories",
            async (MembershipCategory r, MembershipService s) =>
            {
                var w = await s.Access.TenantWorkspace("members.workflow.manage");
                Check(
                    r.Name.Length is > 0 and <= 100
                        && r.Code.Length is > 0 and <= 20
                        && (r.ValidityMonths == null || r.ValidityMonths is > 0 and <= 120),
                    "Invalid category."
                );
                r.Id = Guid.NewGuid();
                r.TenantId = w.TenantId;
                r.Code = r.Code.ToUpperInvariant();
                s.Db.MembershipCategories.Add(r);
                await s.Access.Log(w, "membership.category.create", r.Name);
                return Results.Ok(r);
            }
        );
        api.MapPut(
            "/membership/policy",
            async (MembershipPolicy r, MembershipService s) =>
            {
                var w = await s.Access.TenantWorkspace("members.workflow.manage");
                await using var tx = await s.Access.Mutation(w);
                var p = await s.Db.MembershipPolicies.SingleAsync(x => x.TenantId == w.TenantId);
                if (!r.AllowMultipleClubs && p.AllowMultipleClubs)
                    Check(
                        !await s
                            .Db.Memberships.Where(x =>
                                x.TenantId == w.TenantId
                                && new[] { "Active", "Inactive", "Suspended", "Expired" }.Contains(
                                    x.Status
                                )
                            )
                            .GroupBy(x => x.PersonId)
                            .AnyAsync(g => g.Select(x => x.ClubId).Distinct().Count() > 1),
                        "Resolve existing multiple affiliations before disabling this policy.",
                        409
                    );
                p.AllowMultipleClubs = r.AllowMultipleClubs;
                p.SourceReviewRequired = r.SourceReviewRequired;
                p.AssociationReviewRequired = r.AssociationReviewRequired;
                p.FinancialClearanceRequired = r.FinancialClearanceRequired;
                await s.Access.Log(w, "membership.policy.update", w.TenantId.ToString());
                await tx.CommitAsync();
                return Results.NoContent();
            }
        );
        api.MapPost(
            "/membership/workflows",
            async (WorkflowInput r, MembershipService s) =>
            {
                var w = await s.Access.TenantWorkspace("members.workflow.manage");
                Check(
                    r.Stages.Length is > 0 and <= 12
                        && r.Stages.All(x =>
                            x.Name.Length is > 0 and <= 100
                            && x.RequiredApprovals is > 0 and <= 20
                            && x.RoleIds.Length is > 0 and <= 30
                        ),
                    "Provide 1–12 named approval stages, reviewer roles, and valid quorums."
                );
                var ids = r.Stages.SelectMany(x => x.RoleIds).Distinct().ToArray();
                Check(
                    await s.Db.Roles.CountAsync(x =>
                        x.TenantId == w.TenantId
                        && ids.Contains(x.Id)
                        && s.Db.RolePermissions.Any(g =>
                            g.RoleId == x.Id && g.PermissionId == "members.approve"
                        )
                    ) == ids.Length,
                    "Reviewer roles must belong to this association."
                );
                await using var tx = await s.Access.Mutation(w);
                await s
                    .Db.MembershipWorkflows.Where(x =>
                        x.TenantId == w.TenantId && x.Kind == "Membership" && x.Active
                    )
                    .ExecuteUpdateAsync(x => x.SetProperty(p => p.Active, false));
                var v =
                    (
                        await s
                            .Db.MembershipWorkflows.Where(x =>
                                x.TenantId == w.TenantId && x.Kind == "Membership"
                            )
                            .MaxAsync(x => (int?)x.Version)
                        ?? 0
                    ) + 1;
                var flow = new MembershipWorkflow
                {
                    TenantId = w.TenantId,
                    Version = v,
                    Stages = r
                        .Stages.Select(
                            (x, i) =>
                                new MembershipApprovalStage
                                {
                                    TenantId = w.TenantId,
                                    Sequence = i + 1,
                                    Name = x.Name,
                                    RequiredApprovals = x.RequiredApprovals,
                                    Roles = x
                                        .RoleIds.Distinct()
                                        .Select(id => new MembershipStageRole
                                        {
                                            TenantId = w.TenantId,
                                            RoleId = id,
                                        })
                                        .ToList(),
                                }
                        )
                        .ToList(),
                };
                s.Db.MembershipWorkflows.Add(flow);
                await s.Access.Log(w, "membership.workflow.version", v.ToString());
                await tx.CommitAsync();
                return Results.Ok(new { flow.Id, flow.Version });
            }
        );
        api.MapPost(
            "/membership/applications",
            async (ApplicationInput r, MembershipService s) =>
            {
                var p = await s.OwnPerson(true);
                var club = await s.Club(r.ClubId);
                await s.Category(r.CategoryId, club.TenantId);
                Check(r.Statement.Length <= 4000, "Application statement is too long.");
                var existing = await s.Db.MembershipApplications.SingleOrDefaultAsync(x =>
                    x.PersonId == p.Id
                    && x.ClubId == club.Id
                    && x.CategoryId == r.CategoryId
                    && new[] { "Draft", "Submitted", "Under Review", "Changes Requested" }.Contains(
                        x.Status
                    )
                );
                if (existing != null)
                    return Results.Ok(
                        new
                        {
                            existing.Id,
                            existing.Status,
                            existing.Version,
                        }
                    );
                var app = new MembershipApplication
                {
                    TenantId = club.TenantId,
                    PersonId = p.Id,
                    ApplicantUserId = p.UserId!.Value,
                    ClubId = club.Id,
                    CategoryId = r.CategoryId,
                    Statement = r.Statement,
                };
                s.Db.MembershipApplications.Add(app);
                await s.Db.SaveChangesAsync();
                return Results.Ok(
                    new
                    {
                        app.Id,
                        app.Status,
                        app.Version,
                    }
                );
            }
        );
        api.MapPut(
            "/membership/applications/{id:guid}",
            async (Guid id, ApplicationInput r, MembershipService s) =>
            {
                var user = await s.Access.User();
                await using var tx = await s.Db.Database.BeginTransactionAsync();
                await s.Lock(id);
                var app = await s.Db.MembershipApplications.SingleOrDefaultAsync(x =>
                    x.Id == id && x.ApplicantUserId == user.Id
                );
                Check(app != null, "Application not found.", 404);
                Check(app.Version == r.Version, "Application changed. Refresh.", 409);
                Check(
                    new[] { "Draft", "Changes Requested" }.Contains(app.Status),
                    "Only editable drafts or requested changes may be saved.",
                    409
                );
                Check(r.Statement.Length <= 4000, "Application statement is too long.");
                if (app.InvitationId != null)
                    Check(
                        app.ClubId == r.ClubId && app.CategoryId == r.CategoryId,
                        "An invitation is bound to its intended club and category."
                    );
                await s.Club(r.ClubId, app.TenantId);
                await s.Category(r.CategoryId, app.TenantId);
                app.ClubId = r.ClubId;
                app.CategoryId = r.CategoryId;
                app.Statement = r.Statement;
                app.Version++;
                app.UpdatedAt = DateTimeOffset.UtcNow;
                await s.Db.SaveChangesAsync();
                await tx.CommitAsync();
                return Results.Ok(
                    new
                    {
                        app.Id,
                        app.Status,
                        app.Version,
                    }
                );
            }
        );
        api.MapPost(
            "/membership/applications/{id:guid}/submit",
            async (Guid id, VersionInput r, MembershipService s, NotificationService n) =>
            {
                var user = await s.Access.User();
                Check(user.Verified, "Verify your email before submitting.", 403);
                await using var tx = await s.Db.Database.BeginTransactionAsync();
                await s.Lock(id);
                var app = await s.Db.MembershipApplications.SingleOrDefaultAsync(x =>
                    x.Id == id && x.ApplicantUserId == user.Id
                );
                Check(app != null, "Application not found.", 404);
                Check(app.Version == r.Version, "Application changed. Refresh.", 409);
                Check(
                    new[] { "Draft", "Changes Requested" }.Contains(app.Status),
                    "Application cannot be submitted in its current state.",
                    409
                );
                await s.Club(app.ClubId, app.TenantId);
                await s.Category(app.CategoryId, app.TenantId);
                var workflow =
                    app.WorkflowId == null
                        ? await s
                            .Db.MembershipWorkflows.Include(x => x.Stages)
                            .SingleOrDefaultAsync(x =>
                                x.TenantId == app.TenantId && x.Kind == "Membership" && x.Active
                            )
                        : await s
                            .Db.MembershipWorkflows.Include(x => x.Stages)
                            .SingleAsync(x => x.Id == app.WorkflowId);
                Check(
                    workflow != null && workflow.Stages.Count > 0,
                    "The association must configure an approval workflow first.",
                    409
                );
                app.WorkflowId = workflow.Id;
                app.Revision++;
                app.CurrentSequence = workflow.Stages.Min(x => x.Sequence);
                app.Status = "Submitted";
                app.SubmittedAt = DateTimeOffset.UtcNow;
                app.UpdatedAt = DateTimeOffset.UtcNow;
                app.Version++;
                await n.User(
                    user.Id,
                    "application.submitted",
                    "Your membership application has been submitted for review.",
                    "/app/apply",
                    app.TenantId,
                    app.ClubId
                );
                await NotifyReviewers(s, n, app);
                s.Db.MemberActivities.Add(
                    new()
                    {
                        TenantId = app.TenantId,
                        PersonId = app.PersonId,
                        ClubId = app.ClubId,
                        ActorId = user.Id,
                        Action = "application.submitted",
                        Detail = app.Id.ToString(),
                    }
                );
                await s.Db.SaveChangesAsync();
                await tx.CommitAsync();
                return Results.Ok(
                    new
                    {
                        app.Id,
                        app.Status,
                        app.Version,
                    }
                );
            }
        );
        api.MapPost(
            "/membership/applications/{id:guid}/withdraw",
            async (Guid id, VersionInput r, MembershipService s) =>
            {
                var user = await s.Access.User();
                await using var tx = await s.Db.Database.BeginTransactionAsync();
                await s.Lock(id);
                var app = await s.Db.MembershipApplications.SingleOrDefaultAsync(x =>
                    x.Id == id && x.ApplicantUserId == user.Id
                );
                Check(app != null, "Application not found.", 404);
                Check(app.Version == r.Version, "Application changed.", 409);
                Check(
                    new[] { "Draft", "Submitted", "Under Review", "Changes Requested" }.Contains(
                        app.Status
                    ),
                    "Application cannot be withdrawn.",
                    409
                );
                app.Status = "Withdrawn";
                app.Version++;
                app.UpdatedAt = DateTimeOffset.UtcNow;
                await s.Db.SaveChangesAsync();
                await tx.CommitAsync();
                return Results.NoContent();
            }
        );
        api.MapGet(
            "/membership/applications",
            async (MembershipService s, bool mine = false, string? status = null, int page = 1) =>
            {
                var user = await s.Access.User();
                var q = s.Db.MembershipApplications.AsQueryable();
                if (mine)
                    q = q.Where(x => x.ApplicantUserId == user.Id);
                else
                {
                    var w = await s.Access.Workspace("members.approve");
                    q = q.Where(x =>
                        x.TenantId == w.TenantId && s.Access.Visible(w).Any(o => o.Id == x.ClubId)
                    );
                }
                if (!string.IsNullOrEmpty(status))
                    q = q.Where(x => x.Status == status);
                return new
                {
                    total = await q.CountAsync(),
                    items = await q.OrderByDescending(x => x.CreatedAt)
                        .ThenBy(x => x.Id)
                        .Skip((Math.Max(page, 1) - 1) * 20)
                        .Take(20)
                        .Select(x => new
                        {
                            x.Id,
                            x.PersonId,
                            Name = s
                                .Db.Persons.Where(p => p.Id == x.PersonId)
                                .Select(p => p.DisplayName)
                                .First(),
                            Club = s
                                .Db.Orgs.Where(o => o.Id == x.ClubId)
                                .Select(o => o.Name)
                                .First(),
                            Category = s
                                .Db.MembershipCategories.Where(c => c.Id == x.CategoryId)
                                .Select(c => c.Name)
                                .First(),
                            x.Status,
                            x.SubmittedAt,
                            x.CurrentSequence,
                            x.Version,
                            Stage = s
                                .Db.MembershipApprovalStages.Where(st =>
                                    st.WorkflowId == x.WorkflowId
                                    && st.Sequence == x.CurrentSequence
                                )
                                .Select(st => st.Name)
                                .FirstOrDefault(),
                        })
                        .ToListAsync(),
                };
            }
        );
        api.MapGet(
            "/membership/applications/{id:guid}",
            async (Guid id, MembershipService s) =>
            {
                var u = await s.Access.User();
                var app = await s.Db.MembershipApplications.SingleOrDefaultAsync(x => x.Id == id);
                Check(app != null, "Application not found.", 404);
                var own = app.ApplicantUserId == u.Id;
                Assignment? w = own ? null : await s.Access.Workspace("members.approve");
                Check(
                    own
                        || app.TenantId == w!.TenantId
                            && await s.Access.Visible(w).AnyAsync(o => o.Id == app.ClubId),
                    "Application outside authorized scope.",
                    404
                );
                var p = await s.People.SingleAsync(x => x.Id == app.PersonId);
                var grants = w == null ? [] : await s.Access.Grants(w);
                var stage = await s
                    .Db.MembershipApprovalStages.Include(x => x.Roles)
                    .SingleOrDefaultAsync(x =>
                        x.WorkflowId == app.WorkflowId && x.Sequence == app.CurrentSequence
                    );
                return new
                {
                    application = app,
                    person = await s.Profile(
                        p,
                        own || grants.Contains("members.personal.view"),
                        own || grants.Contains("members.contacts.view"),
                        own || p.Privacy.ProfessionalVisible,
                        own
                    ),
                    stage = stage == null
                        ? null
                        : new
                        {
                            stage.Id,
                            stage.Name,
                            stage.Sequence,
                            stage.RequiredApprovals,
                        },
                    decisions = await s
                        .Db.MembershipApprovals.Where(x => x.ApplicationId == id)
                        .OrderBy(x => x.At)
                        .Select(x => new
                        {
                            x.Id,
                            x.StageId,
                            x.Revision,
                            x.Decision,
                            x.Comment,
                            x.At,
                            Actor = s
                                .Db.Users.Where(u => u.Id == x.ActorId)
                                .Select(u => u.Name)
                                .First(),
                        })
                        .ToListAsync(),
                    canDecide = !own
                        && stage != null
                        && stage.Roles.Any(x => x.RoleId == w!.RoleId)
                        && new[] { "Submitted", "Under Review" }.Contains(app.Status),
                };
            }
        );
        api.MapPost("/membership/applications/{id:guid}/decisions", Decide);
        api.MapPost(
            "/membership/invitations",
            async (InvitationInput r, MembershipService s, NotificationService n) =>
            {
                var w = await s.Access.Workspace(
                    r.PersonId == null ? "members.invite" : "members.link"
                );
                Check(
                    await s.Access.Visible(w).AnyAsync(x => x.Id == r.ClubId),
                    "Club outside authorized scope.",
                    403
                );
                await s.Club(r.ClubId, w.TenantId);
                await s.Category(r.CategoryId, w.TenantId);
                Check(
                    r.ExpiresInDays is >= 1 and <= 30
                        && r.UsageLimit is >= 1 and <= 20
                        && (r.Email == "" || System.Net.Mail.MailAddress.TryCreate(r.Email, out _)),
                    "Invalid invitation limits or email."
                );
                if (r.PersonId != null)
                {
                    Check(
                        r.Email != "" && r.UsageLimit == 1,
                        "Account linking requires one intended verified email recipient."
                    );
                    Check(
                        await s.Scoped(w)
                            .AnyAsync(x => x.PersonId == r.PersonId && x.ClubId == r.ClubId)
                            && await s.Db.Persons.AnyAsync(x =>
                                x.Id == r.PersonId && x.UserId == null
                            ),
                        "Choose an unlinked person in your authorized club.",
                        403
                    );
                }
                Check(r.Message.Length<=2000,"Invitation message must be 2,000 characters or fewer.");
                var token = Token();
                var invite = new MembershipInvitation
                {
                    TenantId = w.TenantId,
                    ClubId = r.ClubId,
                    CategoryId = r.CategoryId,
                    Email = r.Email.Trim().ToLowerInvariant(),
                    InviterId = w.UserId,
                    TokenHash = Hash(token),
                    ProtectedToken = s.Protect(token),
                    ExpiresAt = DateTimeOffset.UtcNow.AddDays(r.ExpiresInDays),
                    UsageLimit = r.UsageLimit,
                    PersonId = r.PersonId,
                    Kind = r.PersonId == null ? "Membership" : "AccountLink",
                };
                s.Db.MembershipInvitations.Add(invite);
                var link = "/join?token=" + token;
                if (invite.Email != "")
                    await n.Recipient(
                        invite.Email,
                        "membership.invitation",
                        invite.Kind == "AccountLink"
                            ? "Verify your account and request a reviewed link to your existing membership."
                            : "You are invited to apply for club membership. Required approvals still apply." + (string.IsNullOrWhiteSpace(r.Message)?"":"\n\n"+r.Message.Trim()),
                        link,
                        w.TenantId,
                        invite.ClubId
                    );
                await s.Access.Log(w, "membership.invitation.create", invite.Id.ToString());
                return Results.Ok(
                    new
                    {
                        invite.Id,
                        invite.ExpiresAt,
                        Link = link,
                        invite.Kind,
                    }
                );
            }
        );
        api.MapGet(
            "/membership/invitations",
            async (MembershipService s) =>
            {
                var w = await s.Access.Workspace("members.invite");
                return await s
                    .Db.MembershipInvitations.Where(x =>
                        x.TenantId == w.TenantId && s.Access.Visible(w).Any(o => o.Id == x.ClubId)
                    )
                    .OrderByDescending(x => x.CreatedAt)
                    .Select(x => new
                    {
                        x.Id,
                        x.Email,
                        x.Kind,
                        x.ExpiresAt,
                        x.Uses,
                        x.UsageLimit,
                        x.Revoked,
                        x.ClubId,
                    })
                    .Take(100)
                    .ToListAsync();
            }
        );
        api.MapDelete(
            "/membership/invitations/{id:guid}",
            async (Guid id, MembershipService s) =>
            {
                var w = await s.Access.Workspace("members.invite");
                var invite = await s.Db.MembershipInvitations.SingleOrDefaultAsync(x =>
                    x.Id == id
                    && x.TenantId == w.TenantId
                    && s.Access.Visible(w).Any(o => o.Id == x.ClubId)
                );
                Check(invite != null, "Invitation not found.", 404);
                invite.Revoked = true;
                await s.Access.Log(w, "membership.invitation.revoke", id.ToString());
                return Results.NoContent();
            }
        );
        api.MapPost("/membership/invitations/accept", Accept).RequireRateLimiting("auth");
        api.MapGet(
            "/membership/link-requests",
            async (MembershipService s) =>
            {
                var w = await s.Access.Workspace("members.link");
                return await s
                    .Db.PersonLinkRequests.Where(x =>
                        x.TenantId == w.TenantId && s.Access.Visible(w).Any(o => o.Id == x.ClubId)
                    )
                    .OrderByDescending(x => x.At)
                    .Select(x => new
                    {
                        x.Id,
                        x.PersonId,
                        x.Status,
                        x.At,
                        Person = s
                            .Db.Persons.Where(p => p.Id == x.PersonId)
                            .Select(p => p.DisplayName)
                            .First(),
                        Account = s
                            .Db.Users.Where(u => u.Id == x.UserId)
                            .Select(u => u.Email)
                            .First(),
                    })
                    .Take(100)
                    .ToListAsync();
            }
        );
        api.MapPost(
            "/membership/link-requests/{id:guid}/decide",
            async (Guid id, LinkDecisionInput r, MembershipService s) =>
            {
                var w = await s.Access.Workspace("members.link");
                Check(
                    new[] { "Approve", "Reject" }.Contains(r.Decision)
                        && r.Reason.Length is >= 5 and <= 1000,
                    "Record an identity-review decision and reason."
                );
                await using var tx = await s.Access.Mutation(w);
                await s.Lock(id);
                var request = await s.Db.PersonLinkRequests.SingleOrDefaultAsync(x =>
                    x.Id == id
                    && x.TenantId == w.TenantId
                    && s.Access.Visible(w).Any(o => o.Id == x.ClubId)
                );
                Check(request != null, "Link request not found.", 404);
                Check(request.Status == "Pending", "Link request already reviewed.", 409);
                await s.Lock(request.PersonId);
                if (r.Decision == "Approve")
                {
                    Check(
                        !await s
                            .Db.Memberships.Where(m => m.PersonId == request.PersonId)
                            .AnyAsync(m => !s.Scoped(w).Any(v => v.Id == m.Id)),
                        "Shared identities require review covering all current affiliations.",
                        403
                    );
                    var p = await s.Db.Persons.SingleAsync(x => x.Id == request.PersonId);
                    Check(
                        p.UserId == null
                            && !await s.Db.Persons.AnyAsync(x => x.UserId == request.UserId),
                        "These identities are already linked; automatic merging is prohibited.",
                        409
                    );
                    p.UserId = request.UserId;
                    p.Version++;
                    foreach (
                        var member in await s.Scoped(w)
                            .Where(x => x.PersonId == p.Id && x.Status == "Active")
                            .ToListAsync()
                    )
                        await s.Grant(member, request.UserId);
                }
                request.Status = r.Decision == "Approve" ? "Approved" : "Rejected";
                request.ReviewerId = w.UserId;
                request.ReviewReason = r.Reason;
                await s.Access.Log(w, "person.link.review", id.ToString());
                await tx.CommitAsync();
                return Results.NoContent();
            }
        );
    }

    private static async Task<IResult> Decide(
        Guid id,
        ApprovalInput r,
        MembershipService s,
        NotificationService n
    )
    {
        var w = await s.Access.Workspace("members.approve");
        Check(
            new[] { "Approve", "Reject", "RequestChanges" }.Contains(r.Decision)
                && r.Comment.Length <= 2000,
            "Invalid approval decision."
        );
        if (r.Decision != "Approve")
            Check(
                !string.IsNullOrWhiteSpace(r.Comment),
                "A comment is required for this decision."
            );
        await using var tx = await s.Access.Mutation(w);
        await s.Lock(id);
        var app = await s.Db.MembershipApplications.SingleOrDefaultAsync(x =>
            x.Id == id && x.TenantId == w.TenantId && s.Access.Visible(w).Any(o => o.Id == x.ClubId)
        );
        Check(app != null, "Application outside authorized scope.", 404);
        Check(app.Version == r.Version, "Another reviewer changed this application. Refresh.", 409);
        Check(
            new[] { "Submitted", "Under Review" }.Contains(app.Status),
            "Application is not awaiting approval.",
            409
        );
        Check(
            app.ApplicantUserId != w.UserId,
            "Applicants cannot approve their own application.",
            403
        );
        var progress = await ApprovalEngine.Record(
            s.Db,
            w,
            app.WorkflowId!.Value,
            app.CurrentSequence,
            app.Revision,
            id,
            false,
            r
        );
        var stage = progress.Stage;
        var decision = progress.Decision;
        app.Version++;
        app.UpdatedAt = DateTimeOffset.UtcNow;
        if (r.Decision == "Reject")
        {
            app.Status = "Rejected";
            await n.User(
                app.ApplicantUserId,
                "application.rejected",
                "Your membership application was rejected. Review the decision in your application.",
                "/app/apply",
                app.TenantId,
                app.ClubId
            );
        }
        else if (r.Decision == "RequestChanges")
        {
            app.Status = "Changes Requested";
            await n.User(
                app.ApplicantUserId,
                "application.changes",
                "Your application needs changes. Review the requested changes before resubmitting.",
                "/app/apply",
                app.TenantId,
                app.ClubId
            );
        }
        else
        {
            var count = progress.Approvals;
            app.Status = "Under Review";
            if (count >= stage.RequiredApprovals)
            {
                var next = progress.Next;
                if (next != null)
                {
                    app.CurrentSequence = next.Sequence;
                    await NotifyReviewers(s, n, app);
                }
                else
                {
                    var person = await s.People.SingleAsync(x => x.Id == app.PersonId);
                    var club = await s.Club(app.ClubId, app.TenantId);
                    var category = await s.Category(app.CategoryId, app.TenantId);
                    var m = await s.Activate(person, club, category, w.UserId, application: app.Id);
                    foreach (
                        var history in s
                            .Db.ChangeTracker.Entries<MembershipHistory>()
                            .Where(x => x.Entity.MembershipId == m.Id)
                    )
                        history.Entity.ApprovalId = decision.Id;
                    app.Status = "Approved";
                    await n.User(
                        app.ApplicantUserId,
                        "application.approved",
                        "Your application was approved. Your membership and digital identity are ready.",
                        "/app/my-membership",
                        app.TenantId,
                        app.ClubId
                    );
                }
            }
        }
        s.Db.MemberActivities.Add(
            new()
            {
                TenantId = app.TenantId,
                ClubId = app.ClubId,
                PersonId = app.PersonId,
                ActorId = w.UserId,
                Action = "application." + r.Decision.ToLowerInvariant(),
                Detail = stage.Name,
            }
        );
        s.Db.Audits.Add(
            new()
            {
                TenantId = app.TenantId,
                OrgId = app.ClubId,
                ActorId = w.UserId,
                Action = "application.decision",
                Resource = id.ToString(),
                Changes = r.Decision,
            }
        );
        await s.Db.SaveChangesAsync();
        await tx.CommitAsync();
        return Results.Ok(
            new
            {
                app.Id,
                app.Status,
                app.Version,
                app.CurrentSequence,
            }
        );
    }

    private static async Task<IResult> Accept(TokenInput r, MembershipService s)
    {
        var user = await s.Access.User();
        Check(user.Verified, "Verify your account email first.", 403);
        Check(r.Token.Length == 64, "Invalid invitation.");
        var hash = Hash(r.Token);
        await using var tx = await s.Db.Database.BeginTransactionAsync();
        var invite = await s.Db.MembershipInvitations.SingleOrDefaultAsync(x =>
            x.TokenHash == hash
        );
        Check(invite != null, "Invalid invitation.", 404);
        await s.Lock(invite.Id);
        await s.Db.Entry(invite).ReloadAsync();
        Check(
            !invite.Revoked
                && invite.ExpiresAt > DateTimeOffset.UtcNow
                && invite.Uses < invite.UsageLimit,
            "Invitation is expired, revoked, or fully used.",
            409
        );
        Check(
            invite.Email == "" || invite.Email == user.Email,
            "This invitation is intended for another verified account.",
            403
        );
        await s.Club(invite.ClubId, invite.TenantId);
        Check(
            !await s.Db.MembershipApplications.AnyAsync(x =>
                x.InvitationId == invite.Id && x.ApplicantUserId == user.Id
            )
                && !await s.Db.PersonLinkRequests.AnyAsync(x =>
                    x.InvitationId == invite.Id && x.UserId == user.Id
                ),
            "Invitation already accepted by this account.",
            409
        );
        Guid resultId;
        string kind;
        if (invite.Kind == "AccountLink")
        {
            Check(
                !await s.Db.Persons.AnyAsync(x => x.UserId == user.Id),
                "Your account already has a person identity. Ask for a reviewed identity resolution.",
                409
            );
            var link = new PersonLinkRequest
            {
                TenantId = invite.TenantId,
                PersonId = invite.PersonId!.Value,
                ClubId = invite.ClubId,
                UserId = user.Id,
                InvitationId = invite.Id,
            };
            s.Db.PersonLinkRequests.Add(link);
            resultId = link.Id;
            kind = "Identity review required";
        }
        else
        {
            var p = await s.OwnPerson(true);
            var app = await s.Db.MembershipApplications.SingleOrDefaultAsync(x =>
                x.PersonId == p.Id
                && x.ClubId == invite.ClubId
                && x.CategoryId == invite.CategoryId
                && new[] { "Draft", "Submitted", "Under Review", "Changes Requested" }.Contains(
                    x.Status
                )
            );
            Check(
                app == null || app.Status == "Draft" && app.InvitationId == null,
                "An application is already in progress.",
                409
            );
            if (app == null)
            {
                app = new()
                {
                    TenantId = invite.TenantId,
                    PersonId = p.Id,
                    ApplicantUserId = user.Id,
                    ClubId = invite.ClubId,
                    CategoryId = invite.CategoryId,
                };
                s.Db.MembershipApplications.Add(app);
            }
            app.InvitationId = invite.Id;
            resultId = app.Id;
            kind = "Application draft";
        }
        invite.Uses++;
        await s.Db.SaveChangesAsync();
        await tx.CommitAsync();
        return Results.Ok(new { Id = resultId, Kind = kind });
    }

    private static async Task NotifyReviewers(
        MembershipService s,
        NotificationService n,
        MembershipApplication app
    )
    {
        var stage = await s
            .Db.MembershipApprovalStages.Include(x => x.Roles)
            .SingleAsync(x => x.WorkflowId == app.WorkflowId && x.Sequence == app.CurrentSequence);
        var ancestors = await s
            .Db.Database.SqlQuery<Guid>(
                $"WITH RECURSIVE a AS(SELECT \"Id\",\"ParentId\" FROM \"Orgs\" WHERE \"Id\"={app.ClubId} AND \"TenantId\"={app.TenantId} UNION ALL SELECT o.\"Id\",o.\"ParentId\" FROM \"Orgs\" o JOIN a ON a.\"ParentId\"=o.\"Id\" WHERE o.\"TenantId\"={app.TenantId}) SELECT \"Id\" AS \"Value\" FROM a"
            )
            .ToArrayAsync();
        var roles = stage.Roles.Select(x => x.RoleId).ToArray();
        var users = await s
            .Db.Assignments.Where(x =>
                x.TenantId == app.TenantId
                && ancestors.Contains(x.OrgId)
                && roles.Contains(x.RoleId)
                && x.UserId != app.ApplicantUserId
            )
            .Select(x => x.UserId)
            .Distinct()
            .ToArrayAsync();
        foreach (var id in users)
            await n.User(
                id,
                "approval.request",
                "A membership application is awaiting " + stage.Name + ".",
                "/app/approvals",
                app.TenantId,
                app.ClubId
            );
    }
}
