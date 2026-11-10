using Microsoft.EntityFrameworkCore;
using static Clubs.Access;

namespace Clubs;

public record TransferInput(
    Guid DestinationClubId,
    Guid CategoryId,
    string Reason,
    long Version,
    DateTimeOffset? EffectiveAt = null
);

public record TransferDecisionInput(string Decision, string Comment, long Version);

public record RenewalInput(DateTimeOffset NewEnd, long Version, string RequestKey);

public record HoldInput(bool Hold, string Reason, long Version);

public static class MembershipLifecycleEndpoints
{
    public static void MapMembershipLifecycle(this RouteGroupBuilder api)
    {
        api.MapPost(
            "/members/{id:guid}/transfers",
            async (Guid id, TransferInput r, MembershipService s, NotificationService n) =>
            {
                var user = await s.Access.User();
                var m = await s.Find(id, "members.manage");
                await using var tx = await s.Db.Database.BeginTransactionAsync();
                await s.Lock(m.PersonId);
                await s.Db.Entry(m).ReloadAsync();
                Check(m.Version == r.Version, "Membership changed.", 409);
                Check(
                    m.Status == "Active"
                        && (m.EndsAt == null || m.EndsAt > DateTimeOffset.UtcNow)
                        && !m.FinancialHold,
                    "Only an active membership without an administrative financial hold can transfer.",
                    409
                );
                Check(r.Reason.Length is >= 5 and <= 2000, "A transfer reason is required.");
                var club = await s.Club(r.DestinationClubId, m.TenantId);
                Check(club.Id != m.ClubId, "Select another club.");
                await s.Category(r.CategoryId, m.TenantId);
                var effective = r.EffectiveAt ?? DateTimeOffset.UtcNow;
                Check(
                    effective >= DateTimeOffset.UtcNow.AddMinutes(-1)
                        && effective <= DateTimeOffset.UtcNow.AddYears(1),
                    "Choose a current or future effective date within one year."
                );
                var policy = await s.Db.MembershipPolicies.SingleAsync(x =>
                    x.TenantId == m.TenantId
                );
                var transfer = new MembershipTransfer
                {
                    TenantId = m.TenantId,
                    MembershipId = m.Id,
                    DestinationClubId = club.Id,
                    CategoryId = r.CategoryId,
                    RequesterId = user.Id,
                    Reason = r.Reason,
                    EffectiveAt = effective,
                    SourceRequired = policy.SourceReviewRequired,
                    AssociationRequired = policy.AssociationReviewRequired,
                    ClearanceRequired = policy.FinancialClearanceRequired,
                    Status = policy.SourceReviewRequired ? "PendingSource" : "PendingDestination",
                };
                s.Db.MembershipTransfers.Add(transfer);
                s.Activity(m, user.Id, "transfer.requested", club.Name);
                await n.User(
                    m.Person.UserId,
                    "transfer.status",
                    "Your membership transfer request is awaiting review.",
                    "/app/my-membership",
                    m.TenantId,
                    m.ClubId
                );
                await s.Db.SaveChangesAsync();
                await tx.CommitAsync();
                return Results.Ok(transfer);
            }
        );
        api.MapGet(
            "/membership/transfers",
            async (MembershipService s, string? status = null, int page = 1) =>
            {
                var w = await s.Access.Workspace("members.transfer.approve");
                var q = s.Db.MembershipTransfers.Where(x =>
                    x.TenantId == w.TenantId
                    && (
                        s.Scoped(w).Any(m => m.Id == x.MembershipId)
                        || s.Access.Visible(w).Any(o => o.Id == x.DestinationClubId)
                    )
                );
                if (!string.IsNullOrEmpty(status))
                    q = q.Where(x => x.Status == status);
                return new
                {
                    total = await q.CountAsync(),
                    items = await q.OrderByDescending(x => x.CreatedAt)
                        .ThenBy(x => x.Id)
                        .Skip((Math.Max(1, page) - 1) * 20)
                        .Take(20)
                        .Select(x => new
                        {
                            x.Id,
                            x.MembershipId,
                            x.Status,
                            x.Reason,
                            x.EffectiveAt,
                            x.Version,
                            x.FinancialCleared,
                            x.ClearanceRequired,
                            Person = s
                                .Db.Memberships.Where(m => m.Id == x.MembershipId)
                                .Select(m => m.Person.DisplayName)
                                .First(),
                            Source = s
                                .Db.Memberships.Where(m => m.Id == x.MembershipId)
                                .Select(m => m.Club.Name)
                                .First(),
                            Destination = s
                                .Db.Orgs.Where(o => o.Id == x.DestinationClubId)
                                .Select(o => o.Name)
                                .First(),
                        })
                        .ToListAsync(),
                };
            }
        );
        api.MapPost(
            "/membership/transfers/{id:guid}/decisions",
            async (Guid id, TransferDecisionInput r, MembershipService s, NotificationService n) =>
            {
                var w = await s.Access.Workspace("members.transfer.approve");
                Check(
                    new[] { "Approve", "Reject" }.Contains(r.Decision) && r.Comment.Length <= 2000,
                    "Invalid decision."
                );
                if (r.Decision == "Reject")
                    Check(!string.IsNullOrWhiteSpace(r.Comment), "Rejection requires a reason.");
                await using var tx = await s.Access.Mutation(w);
                await s.Lock(id);
                var t = await s.Db.MembershipTransfers.SingleOrDefaultAsync(x =>
                    x.Id == id && x.TenantId == w.TenantId
                );
                Check(t != null, "Transfer not found.", 404);
                Check(t.Version == r.Version, "Transfer changed. Refresh.", 409);
                Check(
                    t.RequesterId != w.UserId,
                    "The requester cannot approve their own transfer.",
                    403
                );
                Check(
                    new[] { "PendingSource", "PendingDestination", "PendingAssociation" }.Contains(
                        t.Status
                    ),
                    "Transfer is not awaiting a decision.",
                    409
                );
                var m = await s
                    .Db.Memberships.Include(x => x.Person)
                    .SingleAsync(x => x.Id == t.MembershipId);
                var org = t.Status == "PendingDestination" ? t.DestinationClubId : m.ClubId;
                if (t.Status == "PendingAssociation")
                    await s.Access.TenantWorkspace("members.transfer.approve");
                else
                    Check(
                        await s.Access.Visible(w).AnyAsync(x => x.Id == org),
                        "This workspace cannot review the current transfer stage.",
                        403
                    );
                s.Db.TransferDecisions.Add(
                    new()
                    {
                        TenantId = t.TenantId,
                        TransferId = t.Id,
                        Stage = t.Status,
                        Decision = r.Decision,
                        Comment = r.Comment,
                        ActorId = w.UserId,
                    }
                );
                t.Version++;
                if (r.Decision == "Reject")
                    t.Status = "Rejected";
                else if (t.Status == "PendingSource")
                    t.Status = "PendingDestination";
                else if (t.Status == "PendingDestination" && t.AssociationRequired)
                    t.Status = "PendingAssociation";
                else
                {
                    Check(
                        !m.FinancialHold && (!t.ClearanceRequired || t.FinancialCleared),
                        "Required administrative financial clearance is outstanding.",
                        409
                    );
                    t.Status = "Scheduled";
                    if (t.EffectiveAt <= DateTimeOffset.UtcNow)
                        await CompleteTransfer(t, s, w.UserId);
                }
                s.Activity(m, w.UserId, "transfer." + r.Decision.ToLowerInvariant(), t.Status);
                await n.User(
                    m.Person.UserId,
                    "transfer.status",
                    "Your membership transfer is now " + t.Status.ToLowerInvariant() + ".",
                    "/app/my-membership",
                    m.TenantId,
                    m.ClubId
                );
                await s.Db.SaveChangesAsync();
                await tx.CommitAsync();
                return Results.Ok(
                    new
                    {
                        t.Id,
                        t.Status,
                        t.Version,
                        t.DestinationMembershipId,
                    }
                );
            }
        );
        api.MapPost(
            "/membership/transfers/{id:guid}/cancel",
            async (Guid id, VersionInput r, MembershipService s) =>
            {
                var u = await s.Access.User();
                await using var tx = await s.Db.Database.BeginTransactionAsync();
                await s.Lock(id);
                var t = await s.Db.MembershipTransfers.SingleOrDefaultAsync(x => x.Id == id);
                Check(t != null, "Transfer not found.", 404);
                if (t.RequesterId != u.Id)
                {
                    var w = await s.Access.Workspace("members.transfer.approve");
                    Check(
                        w.TenantId == t.TenantId
                            && await s.Scoped(w).AnyAsync(x => x.Id == t.MembershipId),
                        "Transfer outside authorized scope.",
                        404
                    );
                }
                Check(t.Version == r.Version, "Transfer changed.", 409);
                Check(
                    new[]
                    {
                        "PendingSource",
                        "PendingDestination",
                        "PendingAssociation",
                        "Scheduled",
                        "Blocked",
                    }.Contains(t.Status),
                    "Transfer cannot be cancelled.",
                    409
                );
                t.Status = "Cancelled";
                t.Version++;
                await s.Db.SaveChangesAsync();
                await tx.CommitAsync();
                return Results.NoContent();
            }
        );
        api.MapPost(
            "/membership/transfers/{id:guid}/clearance",
            async (Guid id, HoldInput r, MembershipService s) =>
            {
                var w = await s.Access.Workspace("members.manage");
                Check(
                    !r.Hold && r.Reason.Length is >= 5 and <= 2000,
                    "Record the basis of administrative clearance. This is not a payment confirmation."
                );
                await using var tx = await s.Access.Mutation(w);
                var t = await s.Db.MembershipTransfers.SingleOrDefaultAsync(x =>
                    x.Id == id
                    && x.TenantId == w.TenantId
                    && s.Scoped(w).Any(m => m.Id == x.MembershipId)
                );
                Check(t != null, "Transfer outside authorized scope.", 404);
                Check(t.Version == r.Version, "Transfer changed.", 409);
                Check(
                    new[]
                    {
                        "PendingSource",
                        "PendingDestination",
                        "PendingAssociation",
                        "Blocked",
                    }.Contains(t.Status),
                    "Clearance cannot be changed now.",
                    409
                );
                t.FinancialCleared = true;
                t.Version++;
                await s.Access.Log(w, "transfer.clearance", id.ToString(), r.Reason);
                await tx.CommitAsync();
                return Results.NoContent();
            }
        );
        api.MapPost(
            "/membership/transfers/{id:guid}/retry",
            async (Guid id, VersionInput r, MembershipService s) =>
            {
                var w = await s.Access.Workspace("members.transfer.approve");
                await using var tx = await s.Access.Mutation(w);
                var transfer = await s.Db.MembershipTransfers.SingleOrDefaultAsync(x =>
                    x.Id == id
                    && x.TenantId == w.TenantId
                    && (
                        s.Scoped(w).Any(m => m.Id == x.MembershipId)
                        || s.Access.Visible(w).Any(o => o.Id == x.DestinationClubId)
                    )
                );
                Check(transfer != null, "Transfer not found.", 404);
                Check(
                    transfer.Status == "Blocked" && transfer.Version == r.Version,
                    "Only a current blocked transfer can be retried.",
                    409
                );
                var member = await s.Db.Memberships.SingleAsync(x => x.Id == transfer.MembershipId);
                Check(
                    member.Status == "Active"
                        && !member.FinancialHold
                        && (!transfer.ClearanceRequired || transfer.FinancialCleared),
                    "Resolve source membership/clearance before retrying.",
                    409
                );
                transfer.Status = "Scheduled";
                transfer.Version++;
                await s.Access.Log(w, "transfer.retry", id.ToString());
                await tx.CommitAsync();
                return Results.NoContent();
            }
        );
        api.MapPost(
            "/members/{id:guid}/financial-hold",
            async (Guid id, HoldInput r, MembershipService s) =>
            {
                var w = await s.Access.Workspace("members.manage");
                Check(
                    r.Reason.Length is >= 5 and <= 2000,
                    "Record a reason for the administrative hold decision."
                );
                await using var tx = await s.Access.Mutation(w);
                var m = await s.Find(id, "members.manage", false);
                Check(m.Version == r.Version, "Membership changed.", 409);
                m.FinancialHold = r.Hold;
                m.Version++;
                m.UpdatedAt = DateTimeOffset.UtcNow;
                s.Activity(m, w.UserId, "membership.administrative-hold", r.Reason);
                await s.Db.SaveChangesAsync();
                await tx.CommitAsync();
                return Results.NoContent();
            }
        );
        api.MapPost(
            "/members/{id:guid}/renewals",
            async (Guid id, RenewalInput r, MembershipService s, NotificationService n) =>
            {
                var w = await s.Access.Workspace("members.manage");
                Check(r.RequestKey.Length is >= 8 and <= 100, "An idempotency key is required.");
                await using var tx = await s.Access.Mutation(w);
                await s.Lock(id);
                var m = await s.Find(id, "members.manage", false);
                var previous = await s.Db.MembershipRenewals.SingleOrDefaultAsync(x =>
                    x.MembershipId == id && x.RequestKey == r.RequestKey
                );
                if (previous != null)
                    return Results.Ok(previous);
                Check(m.Version == r.Version, "Membership changed.", 409);
                Check(
                    new[] { "Active", "Inactive", "Expired" }.Contains(m.Status)
                        && r.NewEnd > DateTimeOffset.UtcNow
                        && (m.EndsAt == null || r.NewEnd > m.EndsAt)
                        && r.NewEnd <= DateTimeOffset.UtcNow.AddYears(10),
                    "Invalid renewal period or membership state."
                );
                var renewal = new MembershipRenewal
                {
                    TenantId = m.TenantId,
                    MembershipId = id,
                    ActorId = w.UserId,
                    RequestKey = r.RequestKey,
                    PreviousEnd = m.EndsAt,
                    NewEnd = r.NewEnd,
                };
                s.Db.MembershipRenewals.Add(renewal);
                m.EndsAt = r.NewEnd;
                if (m.Status == "Expired")
                    await s.ChangeStatus(m, "Active", "Membership renewed", w.UserId);
                else
                {
                    m.Version++;
                    s.History(
                        m,
                        m.Status,
                        m.Status,
                        "Validity renewed",
                        w.UserId,
                        DateTimeOffset.UtcNow
                    );
                }
                m.UpdatedAt = DateTimeOffset.UtcNow;
                s.Activity(m, w.UserId, "membership.renewed");
                await n.User(
                    m.Person.UserId,
                    "membership.renewed",
                    "Your membership validity was extended to "
                        + r.NewEnd.ToString("yyyy-MM-dd")
                        + ".",
                    "/app/my-membership",
                    m.TenantId,
                    m.ClubId
                );
                await s.Db.SaveChangesAsync();
                await tx.CommitAsync();
                return Results.Ok(renewal);
            }
        );
        api.MapPost(
            "/members/{id:guid}/category",
            async (Guid id, CategoryChange r, MembershipService s) =>
            {
                var w = await s.Access.Workspace("members.manage");
                Check(r.Reason.Length is >= 5 and <= 2000, "Category changes require a reason.");
                await using var tx = await s.Access.Mutation(w);
                var m = await s.Find(id, "members.manage", false);
                Check(m.Version == r.Version, "Membership changed.", 409);
                var category = await s.Category(r.CategoryId, m.TenantId);
                var before = m.CategoryId;
                m.CategoryId = category.Id;
                m.Version++;
                m.UpdatedAt = DateTimeOffset.UtcNow;
                s.History(m, m.Status, m.Status, r.Reason, w.UserId, DateTimeOffset.UtcNow);
                s.Activity(
                    m,
                    w.UserId,
                    "membership.category",
                    before + " → " + category.Name + ": " + r.Reason
                );
                await s.Db.SaveChangesAsync();
                await tx.CommitAsync();
                return Results.NoContent();
            }
        );
    }

    public static async Task CompleteTransfer(MembershipTransfer t, MembershipService s, Guid actor)
    {
        await s.Lock(t.MembershipId);
        var m = await s
            .Db.Memberships.Include(x => x.Person)
                .ThenInclude(x => x.Privacy)
            .Include(x => x.Club)
            .Include(x => x.Category)
            .SingleAsync(x => x.Id == t.MembershipId);
        await s.Lock(m.PersonId);
        Check(
            m.Status == "Active"
                && !m.FinancialHold
                && (!t.ClearanceRequired || t.FinancialCleared),
            "Transfer is blocked by a changed source state or clearance.",
            409
        );
        var club = await s.Club(t.DestinationClubId, t.TenantId);
        var category = await s.Category(t.CategoryId, t.TenantId);
        m.Status = "Transferred";
        m.Version++;
        m.UpdatedAt = DateTimeOffset.UtcNow;
        s.History(m, "Active", "Transferred", t.Reason, actor, t.EffectiveAt);
        await s.RevokeManaged(m);
        await s
            .Db.DigitalCards.Where(x => x.MembershipId == m.Id && x.RevokedAt == null)
            .ExecuteUpdateAsync(update =>
                update.SetProperty(x => x.RevokedAt, DateTimeOffset.UtcNow)
            );
        await s.Db.SaveChangesAsync();
        var next = await s.Activate(m.Person, club, category, actor, t.EffectiveAt);
        t.DestinationMembershipId = next.Id;
        t.Status = "Completed";
        t.Version++;
        s.Activity(m, actor, "transfer.completed", next.Number);
    }
}

public record CategoryChange(Guid CategoryId, string Reason, long Version);
