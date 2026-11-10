using Microsoft.EntityFrameworkCore;
using static Clubs.Access;

namespace Clubs;

public static class HandoverEndpoints
{
    public static void MapHandovers(this RouteGroupBuilder api)
    {
        api.MapPost(
            "/leadership-handovers",
            async (HandoverInput r, GovernanceService s) =>
            {
                var w = await s.A.Workspace("leadership.manage");
                await using var tx = await s.A.Mutation(w);
                var outgoing = await s.Db.OfficerAppointments.SingleOrDefaultAsync(x =>
                    x.Id == r.OutgoingAppointmentId
                    && x.TenantId == w.TenantId
                    && s.A.VisibleIds(w).Contains(x.OrgId)
                );
                Check(outgoing != null, "Outgoing appointment outside scope.", 404);
                var incoming = await s.Db.OfficerAppointments.SingleOrDefaultAsync(x =>
                    x.Id == r.IncomingAppointmentId
                    && x.TenantId == w.TenantId
                    && x.OrgId == outgoing.OrgId
                    && x.PositionId == outgoing.PositionId
                    && new[] { "Approved", "Scheduled", "Active" }.Contains(x.Status)
                );
                Check(
                    incoming != null
                        && incoming.Id != outgoing.Id
                        && incoming.StartsAt >= outgoing.EndsAt
                        && incoming.PersonId != outgoing.PersonId,
                    "Choose an approved successor for a nonoverlapping term in the same office."
                );
                Check(
                    r.Notes.Length <= 4000
                        && r.Items.Length is > 0 and <= 30
                        && r.Items.All(x => x.Length is > 0 and <= 200),
                    "Provide a bounded handover checklist."
                );
                var h = new LeadershipHandover
                {
                    TenantId = w.TenantId,
                    OrgId = outgoing.OrgId,
                    OutgoingAppointmentId = outgoing.Id,
                    IncomingAppointmentId = incoming.Id,
                    Notes = r.Notes,
                    CreatorId = w.UserId,
                    Snapshot = GovernanceService.Pack(
                        new
                        {
                            CapturedAt = DateTimeOffset.UtcNow,
                            Club = await s
                                .Db.Orgs.Where(x => x.Id == outgoing.OrgId)
                                .Select(x => new
                                {
                                    x.Name,
                                    x.MeetingDay,
                                    x.MeetingSchedule,
                                    x.MeetingVenue,
                                    x.OperatingPolicies,
                                })
                                .SingleAsync(),
                            ActiveMembers = await s
                                .Db.Memberships.Where(x =>
                                    x.TenantId == w.TenantId
                                    && s.A.Tree(w.TenantId, outgoing.OrgId)
                                        .Any(o => o.Id == x.ClubId)
                                    && x.Status == "Active"
                                    && (x.EndsAt == null || x.EndsAt > DateTimeOffset.UtcNow)
                                )
                                .Select(x => x.PersonId)
                                .Distinct()
                                .CountAsync(),
                            PendingApprovals = await s.Db.GovernanceRequests.CountAsync(x =>
                                x.OrgId == outgoing.OrgId
                                && new[] { "Submitted", "Under Review" }.Contains(x.Status)
                            ),
                            MembershipApplications = await s.Db.MembershipApplications.CountAsync(
                                x =>
                                    x.ClubId == outgoing.OrgId
                                    && new[] { "Submitted", "Under Review" }.Contains(x.Status)
                            ),
                            Committees = await s
                                .Db.Committees.Where(x =>
                                    x.OrgId == outgoing.OrgId && x.Status == "Active"
                                )
                                .Select(x => new
                                {
                                    x.Id,
                                    x.Name,
                                    x.Purpose,
                                })
                                .ToListAsync(),
                            ImportantDocuments = await s
                                .Db.GovernanceDocuments.Where(x =>
                                    x.OrgId == outgoing.OrgId
                                    && x.CommitteeId == null
                                    && x.ShareWithSuccessors
                                    && x.Status == "Available"
                                )
                                .Select(x => new
                                {
                                    x.Id,
                                    x.Title,
                                    x.Kind,
                                })
                                .ToListAsync(),
                            FutureExtensions = new[] { "Events", "Finance", "Projects" },
                        }
                    ),
                };
                s.Db.Add(h);
                s.Db.HandoverChecklistItems.AddRange(
                    r.Items.Select(label => new HandoverChecklistItem
                    {
                        TenantId = w.TenantId,
                        HandoverId = h.Id,
                        Label = label,
                    })
                );
                s.Audit(w, h.OrgId, "handover.prepared", h.Id);
                await s.Db.SaveChangesAsync();
                await tx.CommitAsync();
                return Results.Ok(
                    new
                    {
                        h.Id,
                        h.Status,
                        h.Version,
                    }
                );
            }
        );
        api.MapGet(
            "/leadership-handovers",
            async (GovernanceService s, int page = 1) =>
            {
                var u = await s.A.User();
                var session = await s.A.Session();
                var w = await s.Db.Assignments.SingleOrDefaultAsync(x =>
                    x.Id == session.WorkspaceId && x.UserId == u.Id
                );
                var q = s.Db.LeadershipHandovers.Where(h =>
                    s.Db.OfficerAppointments.Any(a =>
                        (a.Id == h.OutgoingAppointmentId || a.Id == h.IncomingAppointmentId)
                        && s.Db.Persons.Any(p => p.Id == a.PersonId && p.UserId == u.Id)
                    )
                );
                if (
                    w != null
                    && (
                        w.AppointmentId == null
                        || await GovernanceService
                            .Current(s.Db)
                            .AnyAsync(x => x.Id == w.AppointmentId)
                    )
                    && (await s.A.Grants(w)).Contains("leadership.view")
                )
                    q = s.Db.LeadershipHandovers.Where(h =>
                        h.TenantId == w.TenantId && s.A.VisibleIds(w).Contains(h.OrgId)
                    );
                return new
                {
                    total = await q.CountAsync(),
                    items = await q.OrderByDescending(x => x.Id)
                        .Skip((Math.Clamp(page, 1, 10000) - 1) * 20)
                        .Take(20)
                        .Select(x => new
                        {
                            x.Id,
                            x.OrgId,
                            x.OutgoingAppointmentId,
                            x.IncomingAppointmentId,
                            x.Status,
                            x.Notes,
                            x.Version,
                            x.AcceptedAt,
                            x.ArchivedAt,
                        })
                        .ToListAsync(),
                };
            }
        );
        api.MapGet(
            "/leadership-handovers/{id:guid}",
            async (Guid id, GovernanceService s) =>
            {
                var h = await AccessHandover(id, s);
                var viewer = await s.A.User();
                return new
                {
                    handover = h,
                    canAccept = h.Status == "Draft"
                        && await s.Db.OfficerAppointments.AnyAsync(x =>
                            x.Id == h.IncomingAppointmentId
                            && s.Db.Persons.Any(p => p.Id == x.PersonId && p.UserId == viewer.Id)
                        ),
                    items = await s
                        .Db.HandoverChecklistItems.Where(x => x.HandoverId == id)
                        .ToListAsync(),
                };
            }
        );
        api.MapPut(
            "/leadership-handovers/{id:guid}/items/{itemId:guid}",
            async (Guid id, Guid itemId, ChecklistInput r, GovernanceService s) =>
            {
                await using var tx = await s.Db.Database.BeginTransactionAsync();
                var h = await AccessHandover(id, s);
                await s.Db.Database.ExecuteSqlInterpolatedAsync(
                    $"SELECT pg_advisory_xact_lock(hashtext({h.TenantId.ToString()}))"
                );
                await s.Db.Entry(h).ReloadAsync();
                Check(
                    h.CreatorId == (await s.A.User()).Id
                        && h.Status == "Draft"
                        && h.Version == r.Version
                        && r.Notes.Length <= 2000,
                    "Handover changed or is no longer editable.",
                    409
                );
                var item = await s.Db.HandoverChecklistItems.SingleOrDefaultAsync(x =>
                    x.Id == itemId && x.HandoverId == id
                );
                Check(item != null, "Checklist item not found.", 404);
                if (r.DocumentId != null)
                    Check(
                        await s.Db.GovernanceDocuments.AnyAsync(x =>
                            x.Id == r.DocumentId
                            && x.TenantId == h.TenantId
                            && x.OrgId == h.OrgId
                            && x.CommitteeId == null
                            && x.ShareWithSuccessors
                            && x.Status == "Available"
                            && (x.RetainUntil == null || x.RetainUntil > DateTimeOffset.UtcNow)
                        ),
                        "Choose an available, explicitly successor-shared document in this organization."
                    );
                item.Completed = r.Completed;
                item.Notes = r.Notes;
                item.DocumentId = r.DocumentId;
                h.Version++;
                h.AcceptedAt = null;
                await s.Db.SaveChangesAsync();
                await tx.CommitAsync();
                return Results.Ok(new { h.Id, h.Version });
            }
        );
        api.MapPost(
            "/leadership-handovers/{id:guid}/accept",
            async (Guid id, VersionInput r, GovernanceService s) =>
            {
                await using var tx = await s.Db.Database.BeginTransactionAsync();
                var h = await AccessHandover(id, s);
                await s.Db.Database.ExecuteSqlInterpolatedAsync(
                    $"SELECT pg_advisory_xact_lock(hashtext({h.TenantId.ToString()}))"
                );
                await s.Db.Entry(h).ReloadAsync();
                var user = await s.A.User();
                Check(
                    h.Version == r.Version
                        && h.Status == "Draft"
                        && await s.Db.OfficerAppointments.AnyAsync(x =>
                            x.Id == h.IncomingAppointmentId
                            && s.Db.Persons.Any(p => p.Id == x.PersonId && p.UserId == user.Id)
                        ),
                    "Only the incoming officer may accept this draft.",
                    403
                );
                Check(
                    await s
                        .Db.HandoverChecklistItems.Where(x => x.HandoverId == id)
                        .AllAsync(x => x.Completed),
                    "Complete every handover checklist item first.",
                    409
                );
                h.AcceptedAt = DateTimeOffset.UtcNow;
                h.Version++;
                await s.Db.SaveChangesAsync();
                await tx.CommitAsync();
                return Results.Ok(new { h.Id, h.Version });
            }
        );
        api.MapPost(
            "/leadership-handovers/{id:guid}/submit",
            async (Guid id, VersionInput r, GovernanceService s) =>
            {
                var w = await s.A.Workspace("leadership.manage");
                await using var tx = await s.A.Mutation(w);
                var h = await AccessHandover(id, s);
                await s.Scope(w, h.OrgId);
                Check(
                    h.CreatorId == w.UserId
                        && h.Status == "Draft"
                        && h.Version == r.Version
                        && h.AcceptedAt != null,
                    "Handover changed or has not been accepted.",
                    409
                );
                h.Status = "Pending approval";
                h.Version++;
                var subject = await s
                    .Db.OfficerAppointments.Where(x => x.Id == h.IncomingAppointmentId)
                    .Select(x =>
                        s.Db.Persons.Where(p => p.Id == x.PersonId).Select(p => p.UserId).First()
                    )
                    .SingleAsync();
                var req = await s.Request(
                    w,
                    h.OrgId,
                    "Handover",
                    "Approve leadership handover",
                    GovernanceService.Pack(new { h.Id }),
                    handover: h.Id,
                    subject: subject
                );
                await s.Db.SaveChangesAsync();
                await tx.CommitAsync();
                return Results.Ok(req);
            }
        );
    }

    private static async Task<LeadershipHandover> AccessHandover(Guid id, GovernanceService s)
    {
        var u = await s.A.User();
        var h = await s.Db.LeadershipHandovers.SingleOrDefaultAsync(x => x.Id == id);
        Check(h != null, "Handover not found.", 404);
        var own = await s.Db.OfficerAppointments.AnyAsync(x =>
            (x.Id == h.OutgoingAppointmentId || x.Id == h.IncomingAppointmentId)
            && s.Db.Persons.Any(p => p.Id == x.PersonId && p.UserId == u.Id)
        );
        if (!own)
        {
            var w = await s.A.Workspace("leadership.view");
            Check(w.TenantId == h.TenantId, "Handover not found.", 404);
            await s.Scope(w, h.OrgId);
        }
        return h;
    }
}
