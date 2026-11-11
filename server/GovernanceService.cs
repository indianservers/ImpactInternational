using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using static Clubs.Access;

namespace Clubs;

public record ClubInput(
    string Name,
    string Code,
    Guid ParentId,
    Guid LevelId,
    string Country = "",
    string State = "",
    string City = "",
    string Address = "",
    string Email = "",
    string Phone = "",
    string Website = "",
    string Logo = "",
    DateOnly? CharterDate = null,
    string Description = "",
    string ClubType = "Community",
    string Registration = "",
    string PostalCode = "",
    double? Latitude = null,
    double? Longitude = null,
    string TimeZone = "UTC",
    string Currency = "USD",
    string MeetingDay = "",
    string MeetingSchedule = "",
    string MeetingVenue = "",
    int FiscalStartMonth = 1,
    string PreferredLanguage = "en",
    string BrandingColor = "#2563eb",
    string OperatingPolicies = "",
    long Version = 1
);

public record PositionInput(
    string Code,
    string Title,
    Guid LevelId,
    string Category = "Leadership",
    int DefaultMonths = 12,
    int? MaximumHolders = 1,
    bool Mandatory = false,
    bool RequireActiveMembership = true,
    int MinimumMembershipDays = 0,
    bool ApprovalRequired = true,
    Guid? ReportsToId = null,
    Guid? GrantRoleId = null,
    bool Active = true,
    long Version = 1
);

public record AppointmentInput(
    Guid OrgId,
    Guid PersonId,
    Guid PositionId,
    DateTimeOffset StartsAt,
    DateTimeOffset EndsAt,
    DateTimeOffset? EffectiveAt = null,
    Guid? TermId = null,
    string Method = "Appointed",
    string Notes = "",
    Guid? DocumentId = null
);

public record CommitteeInput(
    Guid OrgId,
    string Name,
    string Code,
    string Purpose,
    DateTimeOffset StartsAt,
    DateTimeOffset EndsAt,
    string Category = "Custom",
    string Description = "",
    string Notes = "",
    long Version = 1
);

public record CommitteeMemberInput(
    Guid PersonId,
    Guid RoleId,
    DateTimeOffset StartsAt,
    DateTimeOffset EndsAt,
    string Responsibilities = "",
    long Version = 1
);

public record GovernanceDecisionInput(
    string Decision,
    string Comment,
    long Version,
    Guid? DelegationId = null
);

public record GovernanceChangeInput(string Payload, long Version);

public record ReasonInput(string Reason, long Version);

public record ClubStatusInput(
    string Status,
    string Reason,
    long Version,
    Guid? MergeTargetId = null
);

public record ReparentInput(Guid ParentId, string Reason, long Version);

public record ResolutionInput(
    Guid OrgId,
    string Number,
    string Title,
    string Subject,
    string Text,
    DateTimeOffset Date,
    DateTimeOffset EffectiveAt,
    Guid? DocumentId = null
);

public record HandoverInput(
    Guid OutgoingAppointmentId,
    Guid IncomingAppointmentId,
    string Notes,
    string[] Items
);

public record ChecklistInput(bool Completed, string Notes, long Version, Guid? DocumentId = null);

public record GovernanceWorkflowInput(StageInput[] Stages, bool AllowDelegation = false);

public class GovernanceService(
    Access access,
    NotificationService notifications,
    MembershipService memberships,
    Microsoft.AspNetCore.DataProtection.IDataProtectionProvider eventProtection
)
{
    public Task<IQueryable<Membership>> Members(Assignment w) => memberships.ScopedList(w);

    public Access A => access;
    public Db Db => access.Db;

    public async Task<IQueryable<Org>> Organizations(Assignment w)
    {
        var roots = await Db
            .Orgs.Where(o => o.TenantId == w.TenantId && o.ParentId == null)
            .Select(o => o.Id)
            .Take(2)
            .ToArrayAsync();
        if (roots.Length != 1 || roots[0] != w.OrgId)
            return A.Visible(w);
        return Db.Orgs.Where(o =>
            o.TenantId == w.TenantId
            && Db.Orgs.Any(root =>
                root.Id == w.OrgId && root.TenantId == w.TenantId && root.ParentId == null
            )
            && !Db.Orgs.Any(root =>
                root.TenantId == w.TenantId && root.ParentId == null && root.Id != w.OrgId
            )
        );
    }

    public static readonly string[] Permissions =
    [
        "clubs.view",
        "clubs.manage",
        "leadership.view",
        "leadership.manage",
        "governance.configure",
        "governance.approve",
        "governance.restructure",
        "governance.documents",
        "committees.view",
        "committees.manage",
        "governance.reports",
    ];
    public static readonly string[] RequestKinds =
    [
        "ClubCreate",
        "ClubUpdate",
        "ClubStatus",
        "Restructure",
        "OfficerAppointment",
        "OfficerRemoval",
        "CommitteeFormation",
        "CommitteeChair",
        "CommitteeClose",
        "CommitteeRemoval",
        "Handover",
        "Resolution",
        "EventUpdate",
        "EventPublication",
        "EventRegistration",
        "CommunicationBroadcast",
        "CommunicationAutomation",
        "IdCardIssue", "IdCardBatch",
        "FinanceExpense", "FinanceBudget",
        "OperationImpactCorrection",
        "OperationProject",
        "OperationAgenda",
        "OperationMinutes",
        "OperationImpact",
        "OperationDocument",
        "OperationCertificate",
    ];
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static string Pack<T>(T value) => JsonSerializer.Serialize(value, Json);

    public static T Unpack<T>(string value)
    {
        try
        {
            return JsonSerializer.Deserialize<T>(value, Json)
                ?? throw new ApiError(400, "Invalid request payload.");
        }
        catch (JsonException)
        {
            throw new ApiError(400, "Invalid request payload.");
        }
    }

    public static IQueryable<OfficerAppointment> Current(Db db)
    {
        var instant = DateTimeOffset.UtcNow;
        return db.OfficerAppointments.Where(x =>
            x.Status == "Active"
            && x.StartsAt <= instant
            && x.EffectiveAt <= instant
            && x.EndsAt > instant
            && db.Orgs.Any(o => o.Id == x.OrgId && o.Status == "Active")
            && (
                x.MembershipId == null
                || db.Memberships.Any(m =>
                    m.Id == x.MembershipId
                    && m.Status == "Active"
                    && (m.EndsAt == null || m.EndsAt > instant)
                )
            )
        );
    }

    public async Task<Org> Org(Guid id, string permission, bool club = false)
    {
        var w = await A.Workspace(permission);
        var o = await A.Visible(w).SingleOrDefaultAsync(x => x.Id == id);
        Check(o != null, "Organization outside authorized scope.", 404);
        if (club)
            Check(await Db.Levels.AnyAsync(x => x.Id == o.LevelId && x.IsClub), "Choose a club.");
        return o;
    }

    public async Task Scope(Assignment w, Guid orgId) =>
        Check(
            await A.VisibleIds(w).ContainsAsync(orgId),
            "Organization outside authorized scope.",
            404
        );

    public void Audit(Assignment w, Guid org, string action, Guid id, string detail = "") =>
        Db.Audits.Add(
            new()
            {
                TenantId = w.TenantId,
                OrgId = org,
                ActorId = w.UserId,
                Action = action,
                Resource = id.ToString(),
                Changes = detail,
            }
        );

    public void History(OfficerAppointment p, string detail, Guid? actor) =>
        Db.OfficerAppointmentHistory.Add(
            new()
            {
                TenantId = p.TenantId,
                AppointmentId = p.Id,
                Status = p.Status,
                Detail = detail,
                ActorId = actor,
            }
        );

    public async Task<IQueryable<GovernanceRequest>> Requests(Assignment w)
    {
        var q = Db.GovernanceRequests.Where(x =>
            x.TenantId == w.TenantId && A.VisibleIds(w).Contains(x.OrgId)
        );
        q=q.Where(r=>!r.Kind.StartsWith("Finance") || Db.FinanceExpenses.Any(e=>e.RequestId==r.Id&&Db.FinanceGrants.Any(g=>g.EntityId==e.EntityId&&g.UserId==w.UserId)) || Db.FinanceBudgets.Any(b=>b.RequestId==r.Id&&Db.FinanceGrants.Any(g=>g.EntityId==b.EntityId&&g.UserId==w.UserId)));
        var grants = await A.Grants(w);
        if (
            !grants.Any(p =>
                new[]
                {
                    "governance.approve",
                    "clubs.manage",
                    "leadership.manage",
                    "committees.manage",
                    "governance.documents",
                    "governance.configure",
                }.Contains(p)
            )
        )
            q = q.Where(x =>
                (grants.Contains("cards.issue") && x.Kind.StartsWith("IdCard"))
                || (grants.Contains("events.manage") && x.Kind.StartsWith("Event"))
                || (grants.Contains("communications.manage") && x.Kind.StartsWith("Communication"))
                || (grants.Contains("operations.manage") && x.Kind.StartsWith("Operation"))
            );
        if (w.AppointmentId != null && !(await A.Grants(w)).Contains("audit.view"))
        {
            var start = await Db
                .OfficerAppointments.Where(x => x.Id == w.AppointmentId)
                .Select(x => x.StartsAt)
                .SingleAsync();
            q = q.Where(x =>
                x.CreatedAt >= start
                || x.CreatorId == w.UserId
                || x.SubjectUserId == w.UserId
                || x.Status == "Submitted"
                || x.Status == "Under Review"
            );
        }
        return q;
    }

    public async Task<GovernanceRequest> Request(
        Assignment w,
        Guid orgId,
        string kind,
        string title,
        string payload,
        Guid? appointment = null,
        Guid? committee = null,
        Guid? handover = null,
        Guid? resolution = null,
        Guid? subject = null,
        Guid? document = null
    )
    {
        Check(RequestKinds.Contains(kind), "Unsupported governance request.");
        var r = new GovernanceRequest
        {
            TenantId = w.TenantId,
            OrgId = orgId,
            Kind = kind,
            Title = title,
            Payload = payload,
            CreatorId = w.UserId,
            SubjectUserId = subject,
            AppointmentId = appointment,
            CommitteeId = committee,
            HandoverId = handover,
            ResolutionId = resolution,
            DocumentId = document,
        };
        Db.GovernanceRequests.Add(r);
        Audit(w, orgId, "governance.draft", r.Id, kind);
        return r;
    }

    public async Task ReviseLinked(GovernanceRequest r, string payload, Assignment w)
    {
        Check(!r.Kind.StartsWith("Finance"),"Financial review snapshots cannot be rewritten. Create a new corrected proposal.",409);
        if (r.Kind == "OfficerAppointment")
        {
            var input = Unpack<AppointmentInput>(payload);
            var p = await Db.OfficerAppointments.SingleAsync(x =>
                x.Id == r.AppointmentId && x.TenantId == w.TenantId
            );
            Check(
                input.OrgId == p.OrgId
                    && input.PersonId == p.PersonId
                    && input.PositionId == p.PositionId
                    && new[] { "Proposed", "Pending approval" }.Contains(p.Status),
                "Proposal identity is immutable.",
                409
            );
            Check(
                input.StartsAt < input.EndsAt
                    && input.EndsAt > DateTimeOffset.UtcNow
                    && input.EndsAt <= input.StartsAt.AddYears(10)
                    && input.Notes.Length <= 4000,
                "Invalid revised term."
            );
            await ValidateDocument(input.DocumentId, w, p.OrgId);
            await ValidateGrant(p.GrantRoleId, w);
            if (input.TermId != null)
            {
                var term = await Db.LeadershipTerms.SingleOrDefaultAsync(x =>
                    x.Id == input.TermId && x.TenantId == w.TenantId
                );
                Check(
                    term != null
                        && input.StartsAt >= term.StartsAt
                        && input.EndsAt <= term.EndsAt
                        && await A.Tree(w.TenantId, term.OrgId).AnyAsync(x => x.Id == p.OrgId),
                    "Appointment does not fit leadership term."
                );
            }
            p.StartsAt = input.StartsAt.ToUniversalTime();
            p.EndsAt = input.EndsAt.ToUniversalTime();
            p.EffectiveAt = (input.EffectiveAt ?? input.StartsAt).ToUniversalTime();
            Check(
                p.EffectiveAt >= p.StartsAt && p.EffectiveAt < p.EndsAt,
                "Invalid effective date."
            );
            var pos = await Db.OfficerPositions.SingleAsync(x => x.Id == p.PositionId);
            await Capacity(p, pos.MaximumHolders);
            p.Notes = input.Notes;
            p.DocumentId = input.DocumentId;
            p.TermId = input.TermId;
            p.Status = "Proposed";
            p.Version++;
            History(p, "Proposal revised", w.UserId);
            r.DocumentId = input.DocumentId;
        }
        else if (r.Kind == "CommitteeFormation")
        {
            var input = Unpack<CommitteeInput>(payload);
            var c = await Db.Committees.SingleAsync(x =>
                x.Id == r.CommitteeId && x.TenantId == w.TenantId
            );
            Check(
                c.Status == "Proposed" && input.OrgId == c.OrgId && input.Code.ToUpper() == c.Code,
                "Committee identity is immutable.",
                409
            );
            Check(
                input.Name.Length is > 0 and <= 150
                    && input.Purpose.Length is > 0 and <= 4000
                    && input.StartsAt < input.EndsAt
                    && input.EndsAt <= input.StartsAt.AddYears(10),
                "Invalid committee proposal."
            );
            c.Name = input.Name;
            c.Purpose = input.Purpose;
            c.Description = input.Description;
            c.Notes = input.Notes;
            c.StartsAt = input.StartsAt.ToUniversalTime();
            c.EndsAt = input.EndsAt.ToUniversalTime();
            c.Version++;
        }
        else if (r.Kind == "Resolution")
        {
            var input = Unpack<ResolutionInput>(payload);
            var x = await Db.GovernanceResolutions.SingleAsync(x =>
                x.Id == r.ResolutionId && x.TenantId == w.TenantId
            );
            Check(
                x.Status == "Draft" && input.OrgId == x.OrgId && input.Number == x.Number,
                "Resolution identity is immutable.",
                409
            );
            Check(
                input.Text.Length is > 0 and <= 20000 && input.Title.Length is > 0 and <= 200,
                "Invalid resolution text."
            );
            await ValidateDocument(input.DocumentId, w, r.OrgId);
            r.DocumentId = input.DocumentId;
            x.Title = input.Title;
            x.Subject = input.Subject;
            x.Text = input.Text;
            x.Date = input.Date.ToUniversalTime();
            x.EffectiveAt = input.EffectiveAt.ToUniversalTime();
            x.Version++;
        }
        else if (r.Kind is "OfficerRemoval" or "CommitteeClose")
        {
            Check(Unpack<ReasonInput>(payload).Reason.Length >= 5, "A clear reason is required.");
        }
        else if (r.Kind == "CommitteeChair")
        {
            var old = Unpack<CommitteeMemberInput>(r.Payload);
            var revised = Unpack<CommitteeMemberInput>(payload);
            Check(
                old.PersonId == revised.PersonId
                    && old.RoleId == revised.RoleId
                    && revised.StartsAt < revised.EndsAt,
                "Committee leadership identity is immutable."
            );
        }
        else
            throw new ApiError(
                409,
                "Withdraw and replace this transition proposal to preserve its checklist and identity."
            );
    }

    public async Task Submit(GovernanceRequest r, Assignment w)
    {
        Check(
            r.CreatorId == w.UserId && new[] { "Draft", "Changes Requested" }.Contains(r.Status),
            "Only the requester can submit an editable request.",
            403
        );
        if (r.Kind == "ClubStatus")
        {
            var data = Unpack<ClubStatusInput>(r.Payload);
            var version = await Db
                .Orgs.Where(x => x.Id == r.OrgId && x.TenantId == w.TenantId)
                .Select(x => x.Version)
                .SingleAsync();
            r.Payload = Pack(data with { Version = version });
        }
        var wf = await Db.MembershipWorkflows.SingleOrDefaultAsync(x =>
            x.TenantId == r.TenantId && x.Kind == "Governance" && x.Active
        );
        Check(wf != null, "Configure a governance workflow first.", 409);
        if (r.WorkflowId == null)
            r.WorkflowId = wf.Id;
        r.CurrentSequence = 1;
        r.Status = "Submitted";
        r.Version++;
        r.UpdatedAt = DateTimeOffset.UtcNow;
        if (r.AppointmentId != null && r.Kind == "OfficerAppointment")
        {
            var p = await Db.OfficerAppointments.SingleAsync(x => x.Id == r.AppointmentId);
            p.Status = "Pending approval";
            p.Version++;
            History(p, "Appointment submitted", w.UserId);
        }
        Audit(w, r.OrgId, "governance.submitted", r.Id, r.Kind);
        await NotifyReviewers(r);
        await notifications.User(
            r.CreatorId,
            "governance.update",
            "Your " + r.Title + " request is awaiting review.",
            "/app/governance",
            r.TenantId,
            r.OrgId
        );
    }

    public async Task NotifyStatus(GovernanceRequest r) =>
        await notifications.User(
            r.CreatorId,
            "governance.update",
            r.Title + ": " + r.Status,
            "/app/governance?request=" + r.Id,
            r.TenantId,
            r.OrgId
        );

    public async Task NotifyReviewers(GovernanceRequest r)
    {
        var roleIds = Db
            .MembershipStageRoles.Where(x =>
                x.StageId
                == Db.MembershipApprovalStages.Where(st =>
                        st.WorkflowId == r.WorkflowId && st.Sequence == r.CurrentSequence
                    )
                    .Select(st => st.Id)
                    .First()
            )
            .Select(x => x.RoleId);
        var ancestors = await Db
            .Database.SqlQuery<Guid>(
                $"""WITH RECURSIVE chain AS(SELECT "Id","ParentId","TenantId" FROM "Orgs" WHERE "Id"={r.OrgId} AND "TenantId"={r.TenantId} UNION ALL SELECT o."Id",o."ParentId",o."TenantId" FROM "Orgs" o JOIN chain c ON c."ParentId"=o."Id" AND c."TenantId"=o."TenantId") SELECT "Id" AS "Value" FROM chain"""
            )
            .ToArrayAsync();
        var users = await Db
            .Assignments.Where(x =>
                x.TenantId == r.TenantId
                && ancestors.Contains(x.OrgId)
                && roleIds.Contains(x.RoleId)
                && x.UserId != r.CreatorId
                && x.UserId != r.SubjectUserId
                && (x.AppointmentId == null || Current(Db).Any(p => p.Id == x.AppointmentId))
            )
            .Select(x => x.UserId)
            .Distinct()
            .Take(100)
            .ToArrayAsync();
        foreach (var user in users)
            await notifications.User(
                user,
                "governance.review",
                "Review required: " + r.Title,
                "/app/governance",
                r.TenantId,
                r.OrgId
            );
    }

    public async Task ValidateDocument(Guid? id, Assignment w, Guid org)
    {
        if (id == null)
            return;
        var d = await Db.GovernanceDocuments.SingleOrDefaultAsync(x =>
            x.Id == id
            && x.TenantId == w.TenantId
            && x.OrgId == org
            && x.CommitteeId == null
            && x.Status == "Available"
        );
        Check(
            d != null && (d.RetainUntil == null || d.RetainUntil > DateTimeOffset.UtcNow),
            "Supporting document is unavailable or outside the request scope.",
            404
        );
        Check(
            (await A.Grants(w)).Contains("governance.documents"),
            "Document permission required.",
            403
        );
        if (w.AppointmentId != null)
        {
            var start = await Db
                .OfficerAppointments.Where(x => x.Id == w.AppointmentId)
                .Select(x => x.StartsAt)
                .SingleAsync();
            Check(
                d.ShareWithSuccessors || d.At >= start || d.UploaderId == w.UserId,
                "Historical document is not shared with this successor.",
                404
            );
        }
    }

    public async Task<OfficerAppointment> Propose(AppointmentInput r, Assignment w)
    {
        await Scope(w, r.OrgId);
        var o = await Db.Orgs.SingleAsync(x => x.Id == r.OrgId);
        Check(o.Status == "Active", "Organization must be active.");
        Check(
            r.StartsAt < r.EndsAt
                && r.EndsAt > DateTimeOffset.UtcNow
                && r.EndsAt <= r.StartsAt.AddYears(10),
            "Invalid term dates."
        );
        var p = await Db.OfficerPositions.SingleOrDefaultAsync(x =>
            x.Id == r.PositionId && x.TenantId == w.TenantId && x.Active && x.LevelId == o.LevelId
        );
        Check(p != null, "Position is not active at this organizational level.");
        var person = await Db.Persons.SingleOrDefaultAsync(x => x.Id == r.PersonId);
        Check(person != null, "Person not found.", 404);
        Check(person.UserId != w.UserId, "You cannot appoint yourself.", 403);
        var scopeIds = A.Tree(w.TenantId, r.OrgId).Select(x => x.Id);
        var membership = await Db
            .Memberships.Where(x =>
                x.PersonId == r.PersonId
                && x.TenantId == w.TenantId
                && scopeIds.Contains(x.ClubId)
                && x.Status == "Active"
                && (x.EndsAt == null || x.EndsAt > DateTimeOffset.UtcNow)
                && x.JoinedAt <= r.StartsAt.AddDays(-p.MinimumMembershipDays)
            )
            .OrderBy(x => x.JoinedAt)
            .FirstOrDefaultAsync();
        Check(
            membership != null
                || !p.RequireActiveMembership
                    && await Db.Memberships.AnyAsync(x =>
                        x.PersonId == r.PersonId
                        && x.TenantId == w.TenantId
                        && scopeIds.Contains(x.ClubId)
                    ),
            "Candidate is outside this scope or does not satisfy membership eligibility."
        );
        if (r.TermId != null)
        {
            var term = await Db.LeadershipTerms.SingleOrDefaultAsync(x =>
                x.Id == r.TermId && x.TenantId == w.TenantId
            );
            Check(
                term != null
                    && r.StartsAt >= term.StartsAt
                    && r.EndsAt <= term.EndsAt
                    && await A.Tree(w.TenantId, term.OrgId).AnyAsync(x => x.Id == r.OrgId),
                "Appointment does not fit the selected leadership term."
            );
        }
        Check(
            new[] { "Appointed", "Elected", "Interim", "ExOfficio" }.Contains(r.Method)
                && r.Notes.Length <= 4000,
            "Invalid appointment details."
        );
        await ValidateGrant(p.GrantRoleId, w);
        await ValidateDocument(r.DocumentId, w, r.OrgId);
        var a = new OfficerAppointment
        {
            PositionTitle = p.Title,
            OrganizationName = o.Name,
            TenantId = w.TenantId,
            OrgId = r.OrgId,
            PersonId = r.PersonId,
            PositionId = p.Id,
            TermId = r.TermId,
            MembershipId = p.RequireActiveMembership ? membership?.Id : null,
            GrantRoleId = p.GrantRoleId,
            StartsAt = r.StartsAt.ToUniversalTime(),
            EndsAt = r.EndsAt.ToUniversalTime(),
            EffectiveAt = (r.EffectiveAt ?? r.StartsAt).ToUniversalTime(),
            Method = r.Method,
            Notes = r.Notes,
            DocumentId = r.DocumentId,
            ProposerId = w.UserId,
        };
        Check(
            a.EffectiveAt >= a.StartsAt && a.EffectiveAt < a.EndsAt,
            "Effective date must lie inside the term."
        );
        await Capacity(a, p.MaximumHolders);
        Db.OfficerAppointments.Add(a);
        History(a, "Proposed", w.UserId);
        // Approval-free title-only positions may activate immediately; access-bearing appointments always require review.
        if (!p.ApprovalRequired && p.GrantRoleId == null)
        {
            a.Status = "Approved";
            await Activate(a, w.UserId);
        }
        else
            await Request(
                w,
                r.OrgId,
                "OfficerAppointment",
                "Appoint " + person.DisplayName + " as " + p.Title,
                Pack(r),
                appointment: a.Id,
                subject: person.UserId,
                document: r.DocumentId
            );
        Audit(w, r.OrgId, "appointment.proposed", a.Id, p.Title);
        return a;
    }

    public async Task ValidateGrant(Guid? role, Assignment w)
    {
        if (role == null)
            return;
        var grants = await A.Grants(w);
        Check(
            grants.Contains("roles.assign"),
            "Role assignment permission required for access-bearing offices.",
            403
        );
        var r = await Db.Roles.SingleOrDefaultAsync(x => x.Id == role && x.TenantId == w.TenantId);
        Check(
            r != null && r.Permissions.All(grants.Contains),
            "The configured access exceeds your authority.",
            403
        );
    }

    public async Task Capacity(OfficerAppointment p, int? maximum)
    {
        if (maximum == null)
            return;
        var intervals = await Db
            .OfficerAppointments.Where(x =>
                x.Id != p.Id
                && x.TenantId == p.TenantId
                && x.OrgId == p.OrgId
                && x.PositionId == p.PositionId
                && new[] { "Approved", "Scheduled", "Active" }.Contains(x.Status)
                && x.StartsAt < p.EndsAt
                && x.EndsAt > p.StartsAt
            )
            .Select(x => new { x.StartsAt, x.EndsAt })
            .ToArrayAsync();
        var points = intervals
            .SelectMany(x => new[] { (At: x.StartsAt, Delta: 1), (At: x.EndsAt, Delta: -1) })
            .Concat(new[] { (At: p.StartsAt, Delta: 1), (At: p.EndsAt, Delta: -1) })
            .OrderBy(x => x.At)
            .ThenBy(x => x.Delta);
        int count = 0;
        foreach (var point in points)
        {
            count += point.Delta;
            Check(
                count <= maximum,
                "This position has overlapping terms beyond its configured capacity.",
                409
            );
        }
    }

    public async Task Activate(OfficerAppointment p, Guid? actor)
    {
        if (p.EndsAt <= DateTimeOffset.UtcNow)
        {
            p.Status = "Completed";
            History(p, "Term completed", actor);
            return;
        }
        Check(
            await Db.Orgs.AnyAsync(x => x.Id == p.OrgId && x.Status == "Active")
                && (
                    p.MembershipId == null
                    || await Db.Memberships.AnyAsync(x =>
                        x.Id == p.MembershipId
                        && x.Status == "Active"
                        && (x.EndsAt == null || x.EndsAt > DateTimeOffset.UtcNow)
                    )
                ),
            "Organization or membership eligibility no longer holds.",
            409
        );
        var pos = await Db.OfficerPositions.SingleAsync(x => x.Id == p.PositionId);
        await Capacity(p, pos.MaximumHolders);
        p.Status =
            p.EffectiveAt > DateTimeOffset.UtcNow || p.StartsAt > DateTimeOffset.UtcNow
                ? "Scheduled"
                : "Active";
        p.Version++;
        History(
            p,
            p.Status == "Scheduled" ? "Approved for future activation" : "Term activated",
            actor
        );
        if (p.Status == "Active" && p.GrantRoleId != null)
        {
            // Persist the effective office within the caller's transaction before the access FK/guard.
            await Db.SaveChangesAsync();
            var person = await Db.Persons.SingleAsync(x => x.Id == p.PersonId);
            Check(
                person.UserId != null
                    && await Db.Users.AnyAsync(x => x.Id == person.UserId && x.Verified),
                "Link a verified user account before granting application access.",
                409
            );
            Check(
                await Current(Db).AnyAsync(x => x.Id == p.Id)
                    || Db.Entry(p).State == EntityState.Added
                    || p.MembershipId == null
                    || await Db.Memberships.AnyAsync(x =>
                        x.Id == p.MembershipId
                        && x.Status == "Active"
                        && (x.EndsAt == null || x.EndsAt > DateTimeOffset.UtcNow)
                    ),
                "Membership eligibility no longer holds.",
                409
            );
            var exists = await Db.Assignments.AnyAsync(x =>
                x.TenantId == p.TenantId
                && x.OrgId == p.OrgId
                && x.UserId == person.UserId
                && x.RoleId == p.GrantRoleId
            );
            if (!exists)
                Db.Assignments.Add(
                    new()
                    {
                        TenantId = p.TenantId,
                        OrgId = p.OrgId,
                        UserId = person.UserId.Value,
                        RoleId = p.GrantRoleId.Value,
                        AppointmentId = p.Id,
                    }
                );
        }
    }

    public async Task End(OfficerAppointment p, string status, string reason, Guid? actor)
    {
        p.Status = status;
        p.Version++;
        History(p, reason, actor);
        var assignments = await Db.Assignments.Where(x => x.AppointmentId == p.Id).ToArrayAsync();
        foreach (var a in assignments)
        {
            var successor = await Current(Db)
                .Where(x =>
                    x.Id != p.Id
                    && x.OrgId == p.OrgId
                    && x.PersonId == p.PersonId
                    && x.GrantRoleId == a.RoleId
                )
                .FirstOrDefaultAsync();
            if (successor == null)
                Db.Assignments.Remove(a);
            else
                a.AppointmentId = successor.Id;
        }
    }

    public async Task<Committee> CommitteeAccess(Guid id, bool manage = false)
    {
        var u = await A.User();
        var c = await Db.Committees.SingleOrDefaultAsync(x => x.Id == id);
        Check(c != null, "Committee not found.", 404);
        var session = await A.Session();
        var w = await Db.Assignments.SingleOrDefaultAsync(x =>
            x.Id == session.WorkspaceId && x.UserId == u.Id
        );
        if (
            w != null
            && w.TenantId == c.TenantId
            && (w.AppointmentId == null || await Current(Db).AnyAsync(x => x.Id == w.AppointmentId))
            && (
                w.MembershipId == null
                || await Db.Memberships.AnyAsync(m =>
                    m.Id == w.MembershipId
                    && m.Status == "Active"
                    && (m.EndsAt == null || m.EndsAt > DateTimeOffset.UtcNow)
                )
            )
            && (await A.Grants(w)).Contains(manage ? "committees.manage" : "committees.view")
            && await A.VisibleIds(w).ContainsAsync(c.OrgId)
        )
            return c;
        Check(
            c.Status == "Active"
                && c.StartsAt <= DateTimeOffset.UtcNow
                && c.EndsAt > DateTimeOffset.UtcNow
                && await Db.CommitteeMemberships.AnyAsync(x =>
                    x.CommitteeId == id
                    && x.Status == "Active"
                    && x.StartsAt <= DateTimeOffset.UtcNow
                    && x.EndsAt > DateTimeOffset.UtcNow
                    && Db.Persons.Any(p => p.Id == x.PersonId && p.UserId == u.Id)
                    && (!manage || Db.CommitteeRoles.Any(r => r.Id == x.RoleId && r.CanManage))
                ),
            "Committee outside authorized scope.",
            404
        );
        return c;
    }

    public async Task Apply(GovernanceRequest r, Assignment w)
    {
        if(r.Kind.StartsWith("IdCard")){await IdCardService.Approve(A,r,w);return;}
        if(r.Kind.StartsWith("Finance")){await new FinanceService(A,this,notifications).Apply(r,w);return;}
        if (r.Kind.StartsWith("Operation"))
        {
            await new OperationsService(A, this, notifications).Apply(r, w);
            return;
        }
        if (r.Kind == "CommunicationAutomation")
        {
            var reference = Unpack<BroadcastReference>(r.Payload);
            var rule = await Db.CommunicationAutomations.SingleAsync(x =>
                x.Id == reference.Id && x.TenantId == w.TenantId && x.OrgId == r.OrgId
            );
            Check(
                rule.Status == "Review"
                    && rule.Version == reference.Version
                    && rule.ContentHash == reference.Hash
                    && rule.ApprovalRequestId == r.Id,
                "Automation changed.",
                409
            );
            if(rule.Definition!=""){var d=Unpack<IntelligenceWorkflowDefinition>(rule.Definition);IntelligenceWorkflows.Validate(d);Check(rule.ContentHash==IntelligenceWorkflows.Digest(rule),"Workflow snapshot changed.",409);await IntelligenceWorkflows.Authority(A,w,d,rule.OrgId);await Db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(511105)");}
            Check(
                await Db.CommunicationTemplates.AnyAsync(t =>
                    t.Id == rule.TemplateId && t.Version == rule.TemplateVersion && t.Active
                ),
                "Template changed; replace this automation proposal.",
                409
            );
            rule.Status = "Approved";
            rule.Active = true;
            rule.StartsAt = DateTimeOffset.UtcNow;
            rule.Version++;
            r.ResultId = rule.Id;
            return;
        }
        if (r.Kind == "CommunicationBroadcast")
        {
            var reference = Unpack<BroadcastReference>(r.Payload);
            var broadcast = await Db.CommunicationBroadcasts.SingleAsync(b =>
                b.Id == reference.Id && b.TenantId == w.TenantId && b.OrgId == r.OrgId
            );
            Check(
                broadcast.Status == "Review"
                    && broadcast.Version == reference.Version
                    && broadcast.ContentHash == reference.Hash
                    && broadcast.ApprovalRequestId == r.Id,
                "Broadcast content/audience changed or proposal was cancelled.",
                409
            );
            broadcast.Status = "Approved";
            broadcast.Version++;
            r.ResultId = broadcast.Id;
            return;
        }
        if (r.Kind.StartsWith("Event"))
        {
            var reference = Unpack<EventReference>(r.Payload);
            var es = new EventService(A, this, notifications, eventProtection);
            await es.Lock(reference.EventId);
            var ev = await es.Find(reference.EventId);
            Check(
                ev.TenantId == w.TenantId && ev.OrgId == r.OrgId && ev.Lifecycle != "Cancelled",
                "Event is no longer approvable.",
                409
            );
            if (r.Kind == "EventUpdate")
            {
                var change = Unpack<EventUpdateInput>(r.Payload);
                Check(
                    ev.Publication == "Published"
                        && ev.Version == change.Version
                        && ev.Lifecycle is not "Completed" and not "Archived",
                    "Event changed; replace this amendment.",
                    409
                );
                ev.Title = change.Title;
                ev.Description = change.Description;
                ev.Venue = change.Venue;
                ev.OnlineUrl = change.OnlineUrl;
                ev.Version++;
                es.QueueNotice(ev, "Event updated. Venue: " + ev.Venue);
                es.QueueNotice(ev, "Event updated. Venue: " + ev.Venue, "UnregisteredInvitations");
            }
            else if (r.Kind == "EventPublication")
            {
                Check(
                    ev.ApprovalRequestId == r.Id && ev.Publication == "Draft",
                    "Event proposal was replaced.",
                    409
                );
                ev.Approval = "Approved";
                ev.Version++;
            }
            else
            {
                var registration = await Db.EventRegistrations.SingleAsync(x =>
                    x.Id == reference.RegistrationId && x.EventId == ev.Id
                );
                Check(
                    registration.Status == "Awaiting Approval",
                    "Registration is no longer awaiting approval.",
                    409
                );
                Check(
                    ev.Publication == "Published" && ev.EndsAt > DateTimeOffset.UtcNow,
                    "Event is no longer accepting approvals.",
                    409
                );
                await es.Confirm(ev, registration);
            }
            r.ResultId = reference.RegistrationId ?? ev.Id;
            return;
        }

        switch (r.Kind)
        {
            case "OfficerAppointment":
            {
                var p = await Db.OfficerAppointments.SingleAsync(x =>
                    x.Id == r.AppointmentId && x.TenantId == w.TenantId
                );
                await ValidateGrant(p.GrantRoleId, w);
                Check(
                    (await Db.Persons.SingleAsync(x => x.Id == p.PersonId)).UserId != w.UserId,
                    "You cannot approve your own office.",
                    403
                );
                p.ApproverId = w.UserId;
                await Activate(p, w.UserId);
                break;
            }
            case "OfficerRemoval":
            {
                var p = await Db.OfficerAppointments.SingleAsync(x =>
                    x.Id == r.AppointmentId && x.TenantId == w.TenantId
                );
                var removal = Unpack<ReasonInput>(r.Payload);
                Check(
                    p.Version == removal.Version
                        && new[]
                        {
                            "Proposed",
                            "Pending approval",
                            "Approved",
                            "Scheduled",
                            "Active",
                        }.Contains(p.Status),
                    "Appointment changed since removal proposal.",
                    409
                );
                await End(p, "Revoked", Unpack<ReasonInput>(r.Payload).Reason, w.UserId);
                break;
            }
            case "ClubCreate":
            case "ClubUpdate":
            {
                var data = Unpack<ClubInput>(r.Payload);
                var club = await SaveClub(data, r.Kind == "ClubCreate" ? null : r.OrgId, w);
                r.ResultId = club.Id;
                break;
            }
            case "ClubStatus":
            {
                var data = Unpack<ClubStatusInput>(r.Payload);
                var o = await Db.Orgs.SingleAsync(x => x.Id == r.OrgId && x.TenantId == w.TenantId);
                Check(o.Version == data.Version, "Club changed since submission.", 409);
                await SetClubStatus(o, data, w);
                break;
            }
            case "Restructure":
            {
                Check(
                    (await A.Grants(w)).Contains("governance.restructure"),
                    "Restructuring authority required.",
                    403
                );
                var data = Unpack<ReparentInput>(r.Payload);
                var o = await Db.Orgs.SingleAsync(x => x.Id == r.OrgId && x.TenantId == w.TenantId);
                Check(
                    o.Version == data.Version && o.Id != w.OrgId,
                    "Organization changed or is your scope root.",
                    409
                );
                await Parent(o.LevelId, data.ParentId, w, o.Id);
                o.ParentId = data.ParentId;
                o.Version++;
                o.UpdatedAt = DateTimeOffset.UtcNow;
                break;
            }
            case "CommitteeFormation":
            {
                var c = await Db.Committees.SingleAsync(x =>
                    x.Id == r.CommitteeId && x.TenantId == w.TenantId
                );
                Check(c.EndsAt > DateTimeOffset.UtcNow, "Committee term has ended.", 409);
                c.Status = "Active";
                c.Version++;
                break;
            }
            case "CommitteeChair":
            {
                var data = Unpack<CommitteeMemberInput>(r.Payload);
                await AddCommitteeMember(r.CommitteeId!.Value, data, w, true);
                break;
            }
            case "CommitteeClose":
            {
                var c = await Db.Committees.SingleAsync(x =>
                    x.Id == r.CommitteeId && x.TenantId == w.TenantId
                );
                Check(
                    c.Version == Unpack<ReasonInput>(r.Payload).Version && c.Status == "Active",
                    "Committee changed since closure proposal.",
                    409
                );
                c.Status = "Closed";
                c.Version++;
                foreach (
                    var m in await Db
                        .CommitteeMemberships.Where(x =>
                            x.CommitteeId == c.Id && x.Status == "Active"
                        )
                        .ToListAsync()
                )
                    m.Status = "Completed";
                break;
            }
            case "CommitteeRemoval":
            {
                var data = Unpack<CommitteeRemoveInput>(r.Payload);
                var m = await Db.CommitteeMemberships.SingleOrDefaultAsync(x =>
                    x.Id == data.MembershipId
                    && x.CommitteeId == r.CommitteeId
                    && x.TenantId == w.TenantId
                );
                Check(
                    m != null && m.Version == data.Version && m.Status == "Active",
                    "Committee membership changed.",
                    409
                );
                m.Status = "Removed";
                m.Version++;
                Db.CommitteeActivities.Add(
                    new()
                    {
                        TenantId = w.TenantId,
                        CommitteeId = m.CommitteeId,
                        ActorId = w.UserId,
                        Action = "leadership.removed",
                        Detail = data.Reason,
                    }
                );
                break;
            }
            case "Resolution":
            {
                var x = await Db.GovernanceResolutions.SingleAsync(x =>
                    x.Id == r.ResolutionId && x.TenantId == w.TenantId
                );
                x.Status = "Approved";
                x.Version++;
                break;
            }
            case "Handover":
            {
                var h = await Db.LeadershipHandovers.SingleAsync(x =>
                    x.Id == r.HandoverId && x.TenantId == w.TenantId
                );
                Check(
                    h.AcceptedAt != null
                        && await Db
                            .HandoverChecklistItems.Where(x => x.HandoverId == h.Id)
                            .AllAsync(x => x.Completed),
                    "Incoming officer must accept the completed handover checklist.",
                    409
                );
                h.Status = "Approved";
                h.Version++;
                break;
            }
            default:
                throw new ApiError(400, "Unsupported request.");
        }
    }

    public async Task Parent(Guid level, Guid parent, Assignment w, Guid? id = null)
    {
        await Scope(w, parent);
        var p = await Db.Orgs.SingleAsync(x => x.Id == parent);
        Check(
            p.Status == "Active"
                && await Db.Levels.AnyAsync(x =>
                    x.Id == level && x.TenantId == w.TenantId && x.ParentLevelId == p.LevelId
                ),
            "Invalid active parent organization."
        );
        if (id != null)
            Check(
                !await A.Tree(w.TenantId, id.Value).AnyAsync(x => x.Id == parent),
                "Hierarchy cycle rejected."
            );
    }

    public async Task<Org> SaveClub(ClubInput r, Guid? id, Assignment w)
    {
        Check(
            r.Name.Trim().Length is >= 2 and <= 200
                && r.Code.Length is > 0 and <= 40
                && r.Description.Length <= 4000
                && r.OperatingPolicies.Length <= 10000,
            "Invalid club identity or policies."
        );
        Check(
            await Db.Levels.AnyAsync(x =>
                x.Id == r.LevelId && x.TenantId == w.TenantId && x.IsClub
            ),
            "Choose a club level."
        );
        if (id == null)
            await Parent(r.LevelId, r.ParentId, w);
        else
            await Scope(w, id.Value);
        Check(
            r.Latitude is null or >= -90 and <= 90
                && r.Longitude is null or >= -180 and <= 180
                && r.FiscalStartMonth is >= 1 and <= 12,
            "Invalid geography/fiscal year."
        );
        Check(
            r.Email == "" || System.Net.Mail.MailAddress.TryCreate(r.Email, out _),
            "Invalid official email."
        );
        foreach (var url in new[] { r.Website, r.Logo })
            Check(
                url == "" || Uri.TryCreate(url, UriKind.Absolute, out var u) && u.Scheme == "https",
                "Use HTTPS URLs."
            );
        Check(
            r.Currency.Length == 3
                && r.PreferredLanguage.Length <= 20
                && System.Text.RegularExpressions.Regex.IsMatch(
                    r.BrandingColor,
                    "^#[0-9a-fA-F]{6}$"
                ),
            "Invalid localization/branding."
        );
        try
        {
            TimeZoneInfo.FindSystemTimeZoneById(r.TimeZone);
        }
        catch
        {
            throw new ApiError(400, "Invalid time zone.");
        }
        Org o;
        if (id == null)
        {
            o = new()
            {
                TenantId = w.TenantId,
                LevelId = r.LevelId,
                Status = "Active",
            };
            Db.Orgs.Add(o);
        }
        else
        {
            o = await Db.Orgs.SingleAsync(x => x.Id == id && x.TenantId == w.TenantId);
            Check(
                o.Version == r.Version && o.LevelId == r.LevelId,
                "Club changed since submission.",
                409
            );
        }
        Check(
            !await Db.Orgs.AnyAsync(x =>
                x.TenantId == w.TenantId && x.Code == r.Code.Trim().ToUpper() && x.Id != o.Id
            ),
            "Club code already exists.",
            409
        );
        if (id != null)
            Check(
                o.ParentId == r.ParentId,
                "Use the controlled restructuring workflow for parent changes.",
                409
            );
        o.Name = r.Name.Trim();
        o.Code = r.Code.Trim().ToUpper();
        o.ParentId = r.ParentId;
        o.Country = r.Country;
        o.State = r.State;
        o.City = r.City;
        o.Address = r.Address;
        o.Email = r.Email;
        o.Phone = r.Phone;
        o.Website = r.Website;
        o.Logo = r.Logo;
        o.CharterDate = r.CharterDate;
        o.Description = r.Description;
        o.ClubType = r.ClubType;
        o.Registration = r.Registration;
        o.PostalCode = r.PostalCode;
        o.Latitude = r.Latitude;
        o.Longitude = r.Longitude;
        o.TimeZone = r.TimeZone;
        o.Currency = r.Currency;
        o.MeetingDay = r.MeetingDay;
        o.MeetingSchedule = r.MeetingSchedule;
        o.MeetingVenue = r.MeetingVenue;
        o.FiscalStartMonth = r.FiscalStartMonth;
        o.PreferredLanguage = r.PreferredLanguage;
        o.BrandingColor = r.BrandingColor;
        o.OperatingPolicies = r.OperatingPolicies;
        o.Version++;
        o.UpdatedAt = DateTimeOffset.UtcNow;
        Audit(w, o.Id, id == null ? "club.created" : "club.updated", o.Id);
        return o;
    }

    public async Task SetClubStatus(Org o, ClubStatusInput r, Assignment w)
    {
        var graph = new Dictionary<string, string[]>
        {
            { "Proposed", ["Pending approval", "Closed"] },
            { "Pending approval", ["Active", "Closed"] },
            { "Pending", ["Active", "Closed"] },
            { "Archived", ["Active", "Closed"] },
            { "Active", ["Suspended", "Inactive", "Merged", "Closed"] },
            { "Suspended", ["Active", "Inactive", "Closed"] },
            { "Inactive", ["Active", "Merged", "Closed"] },
        };
        Check(
            r.Reason.Length >= 5
                && graph.TryGetValue(o.Status, out var next)
                && next.Contains(r.Status),
            "Invalid lifecycle transition or reason.",
            409
        );
        if (r.Status == "Merged")
        {
            Check(
                r.MergeTargetId != null && r.MergeTargetId != o.Id,
                "Choose a distinct merger destination."
            );
            await Scope(w, r.MergeTargetId.Value);
            Check(
                await Db.Orgs.AnyAsync(x =>
                    x.Id == r.MergeTargetId
                    && x.TenantId == o.TenantId
                    && x.Status == "Active"
                    && Db.Levels.Any(l => l.Id == x.LevelId && l.IsClub)
                ),
                "Invalid merger destination."
            );
            Check(
                !await Db.Memberships.AnyAsync(x =>
                    x.ClubId == o.Id
                    && new[] { "Active", "Inactive", "Suspended", "Expired" }.Contains(x.Status)
                ),
                "Transfer current memberships before merging; history will remain.",
                409
            );
        }
        if (new[] { "Closed", "Merged" }.Contains(r.Status))
            Check(
                !await Db.Memberships.AnyAsync(x => x.ClubId == o.Id && x.Status == "Active"),
                "Resolve active memberships before closing this club.",
                409
            );
        o.Status = r.Status;
        o.Version++;
        o.UpdatedAt = DateTimeOffset.UtcNow;
        Audit(w, o.Id, "club.status", o.Id, r.Reason);
    }

    public async Task AddCommitteeMember(
        Guid id,
        CommitteeMemberInput r,
        Assignment? w,
        bool approved = false
    )
    {
        var c = await Db.Committees.SingleAsync(x => x.Id == id);
        Check(
            c.Status == "Active"
                && r.StartsAt < r.EndsAt
                && r.StartsAt >= c.StartsAt
                && r.EndsAt <= c.EndsAt,
            "Committee is inactive or dates are outside its term."
        );
        var role = await Db.CommitteeRoles.SingleOrDefaultAsync(x =>
            x.Id == r.RoleId && x.TenantId == c.TenantId && x.Active
        );
        Check(role != null, "Invalid committee role.");
        Check(
            !role.CanManage || approved,
            "Committee leadership requires governance approval.",
            409
        );
        Check(
            await Db.Memberships.AnyAsync(x =>
                x.PersonId == r.PersonId
                && x.TenantId == c.TenantId
                && x.Status == "Active"
                && (x.EndsAt == null || x.EndsAt > DateTimeOffset.UtcNow)
                && A.Tree(c.TenantId, c.OrgId).Any(o => o.Id == x.ClubId)
            ),
            "Choose an active member within the committee scope."
        );
        Check(
            !await Db.CommitteeMemberships.AnyAsync(x =>
                x.CommitteeId == id
                && x.PersonId == r.PersonId
                && x.Status == "Active"
                && x.StartsAt < r.EndsAt
                && x.EndsAt > r.StartsAt
            ),
            "Overlapping committee membership.",
            409
        );
        Db.CommitteeMemberships.Add(
            new()
            {
                TenantId = c.TenantId,
                CommitteeId = id,
                PersonId = r.PersonId,
                RoleId = r.RoleId,
                StartsAt = r.StartsAt.ToUniversalTime(),
                EndsAt = r.EndsAt.ToUniversalTime(),
                Responsibilities = r.Responsibilities,
            }
        );
        Db.CommitteeActivities.Add(
            new()
            {
                TenantId = c.TenantId,
                CommitteeId = id,
                ActorId = (await A.User()).Id,
                Action = "member.assigned",
                Detail = role.Name,
            }
        );
    }
}
