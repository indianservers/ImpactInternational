using Microsoft.EntityFrameworkCore;
using static Clubs.Access;

namespace Clubs;

// Shared by membership and governance; one stage/role/quorum/revision/decision model.
public static class ApprovalEngine
{
    // Callers must first enforce the target's tenant and organizational scope.
    public static Task<bool> PlatformAuthority(Db db, Assignment w) =>
        db.Users.AnyAsync(u => u.Id == w.UserId && u.PlatformAdmin);

    public static async Task<bool> HierarchyAuthority(Db db, Assignment w) =>
        await PlatformAuthority(db, w) || await db.Roles.AnyAsync(r =>
            r.Id == w.RoleId && r.TenantId == w.TenantId &&
            (r.Name == "Club President" || r.Name == "Zonal President" ||
             r.Name == "District President" || r.Name == "Regional President" ||
             r.Name == "National President" || r.Name == "International President"));

    public static async Task<bool> HierarchyRequest(Db db, Assignment w, GovernanceRequest r) =>
        r.Kind == "OfficerAppointment" && await HierarchyAuthority(db, w);

    public record Progress(
        MembershipApprovalStage Stage,
        MembershipApproval Decision,
        int Approvals,
        MembershipApprovalStage? Next
    );

    public static async Task<Progress> Record(
        Db db,
        Assignment w,
        Guid workflow,
        int sequence,
        int revision,
        Guid id,
        bool governance,
        ApprovalInput r,
        Guid? delegationId = null
    )
    {
        Check(
            new[] { "Approve", "Reject", "RequestChanges" }.Contains(r.Decision)
                && r.Comment.Length <= 2000,
            "Invalid decision."
        );
        Check(
            r.Decision == "Approve" || !string.IsNullOrWhiteSpace(r.Comment),
            "Explain this decision."
        );
        var stage = await db
            .MembershipApprovalStages.Include(x => x.Roles)
            .SingleAsync(x =>
                x.WorkflowId == workflow && x.Sequence == sequence && x.TenantId == w.TenantId
            );
        Guid principal = w.UserId;
        if (delegationId != null)
        {
            var d = await db.GovernanceDelegations.SingleOrDefaultAsync(x =>
                x.Id == delegationId
                && x.TenantId == w.TenantId
                && x.DelegateId == w.UserId
                && !x.Revoked
                && x.StartsAt <= DateTimeOffset.UtcNow
                && x.EndsAt > DateTimeOffset.UtcNow
            );
            Check(
                governance && stage.AllowDelegation && d != null,
                "Delegation is not active for this stage.",
                403
            );
            Check(
                stage.Roles.Any(x => x.RoleId == d.RoleId)
                    && await db.Assignments.AnyAsync(x =>
                        x.UserId == d.DelegatorId
                        && x.OrgId == d.OrgId
                        && x.RoleId == d.RoleId
                        && (
                            x.AppointmentId == null
                            || GovernanceService.Current(db).Any(p => p.Id == x.AppointmentId)
                        )
                        && (
                            x.MembershipId == null
                            || db.Memberships.Any(m =>
                                m.Id == x.MembershipId
                                && m.Status == "Active"
                                && (m.EndsAt == null || m.EndsAt > DateTimeOffset.UtcNow)
                            )
                        )
                    ),
                "Delegating authority no longer holds the reviewer role.",
                403
            );
            principal = d.DelegatorId;
        }
        else
            Check(
                stage.Roles.Any(x => x.RoleId == w.RoleId)
                    || (!governance && await HierarchyAuthority(db, w))
                    || (governance && await HierarchyRequest(db, w,
                        await db.GovernanceRequests.SingleAsync(x => x.Id == id && x.TenantId == w.TenantId))),
                "This role cannot review the current stage.",
                403
            );
        var decisions = db
            .MembershipApprovals.Where(x =>
                governance ? x.GovernanceRequestId == id : x.ApplicationId == id
            )
            .Where(x => x.Revision == revision && x.StageId == stage.Id);
        Check(
            !await decisions.AnyAsync(x =>
                x.ActorId == w.UserId || (x.PrincipalUserId ?? x.ActorId) == principal
            ),
            "Your decision is already recorded for this revision/stage.",
            409
        );
        var decision = new MembershipApproval
        {
            TenantId = w.TenantId,
            ApplicationId = governance ? null : id,
            GovernanceRequestId = governance ? id : null,
            PrincipalUserId = principal,
            StageId = stage.Id,
            Revision = revision,
            ActorId = w.UserId,
            Decision = r.Decision,
            Comment = r.Comment,
        };
        var count =
            await decisions.CountAsync(x => x.Decision == "Approve")
            + (r.Decision == "Approve" ? 1 : 0);
        db.MembershipApprovals.Add(decision);
        var next =
            count >= stage.RequiredApprovals
                ? await db
                    .MembershipApprovalStages.Where(x =>
                        x.WorkflowId == workflow && x.Sequence > sequence
                    )
                    .OrderBy(x => x.Sequence)
                    .FirstOrDefaultAsync()
                : null;
        return new(stage, decision, count, next);
    }
}
