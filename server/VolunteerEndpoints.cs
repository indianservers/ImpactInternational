using Microsoft.EntityFrameworkCore;
using static Clubs.Access;
using static Clubs.GovernanceService;

namespace Clubs;

public record ShiftInput(
    Guid ProjectId,
    string Title,
    DateTimeOffset StartsAt,
    DateTimeOffset EndsAt,
    DateTimeOffset Deadline,
    Guid SupervisorId,
    int Capacity = 10,
    string Location = "",
    Guid? ActivityId = null,
    string RequiredTraining = "",
    bool ApprovalRequired = true
);

public record VolunteerInput(
    Guid PersonId,
    string Skills = "",
    string Interests = "",
    string Availability = "",
    string Training = "",
    string Language = "en"
);

public record ShiftAttendanceInput(string Action, int BreakMinutes = 0, string Reason = "");

public record HoursInput(
    Guid AssignmentId,
    DateTimeOffset StartsAt,
    DateTimeOffset EndsAt,
    int BreakMinutes = 0,
    string Reason = ""
);

public static partial class OperationsEndpoints
{
    static void MapVolunteers(RouteGroupBuilder api)
    {
        api.MapGet(
            "/volunteers",
            async (OperationsService s, bool mine = false) =>
            {
                var w = await s.Workspace();
                var can = (await s.A.Grants(w)).Contains("operations.manage");
                var user = w.UserId;
                return await s.WithPeople(
                    await s
                        .Db.VolunteerProfiles.Where(p =>
                            p.TenantId == w.TenantId
                            && s.A.VisibleIds(w).Contains(p.OrgId)
                            && (
                                can && !mine
                                || s.Db.Persons.Any(x => x.Id == p.PersonId && x.UserId == user)
                            )
                        )
                        .OrderBy(p => p.Id)
                        .Take(100)
                        .ToListAsync()
                );
            }
        );
        api.MapPost(
            "/volunteers",
            async (VolunteerInput i, OperationsService s) =>
            {
                var w = await s.Workspace();
                var p = await s.Person(i.PersonId, w.TenantId);
                Check(p.UserId == w.UserId, "Members manage their own volunteer profile.", 403);
                Check(
                    i.Skills.Length + i.Interests.Length + i.Availability.Length + i.Training.Length
                        <= 10000,
                    "Profile too long."
                );
                var row = await s.Db.VolunteerProfiles.SingleOrDefaultAsync(x =>
                    x.PersonId == i.PersonId && x.TenantId == w.TenantId
                );
                if (row == null)
                {
                    row = new()
                    {
                        TenantId = w.TenantId,
                        OrgId = w.OrgId,
                        PersonId = p.Id,
                    };
                    s.Db.VolunteerProfiles.Add(row);
                }
                row.Skills = i.Skills;
                row.Interests = i.Interests;
                row.Availability = i.Availability;
                row.Training = i.Training;
                row.Language = i.Language;
                row.Version++;
                await s.Db.SaveChangesAsync();
                return row;
            }
        );
        api.MapGet(
            "/volunteers/opportunities",
            async (OperationsService s, Guid? projectId = null) =>
            {
                var w = await s.Workspace();
                var q = s.Db.VolunteerShifts.Where(x =>
                    x.TenantId == w.TenantId
                    && (
                        s.A.VisibleIds(w).Contains(x.OrgId)
                        || s.Db.ProjectOrganizations.Any(o =>
                            o.ProjectId == x.ProjectId && s.A.VisibleIds(w).Contains(o.OrgId)
                        )
                    )
                    && s.Db.ServiceProjects.Any(p =>
                        p.Id == x.ProjectId
                        && new[] { "Approved", "Planning", "In Progress" }.Contains(p.Status)
                    )
                );
                if (projectId != null)
                    q = q.Where(x => x.ProjectId == projectId);
                return await q.OrderBy(x => x.StartsAt)
                    .Take(100)
                    .Select(x => new
                    {
                        shift = x,
                        assigned = s.Db.VolunteerAssignments.Count(a =>
                            a.ShiftId == x.Id
                            && new[] { "Approved", "Confirmed", "Present", "Completed" }.Contains(
                                a.Status
                            )
                        ),
                        waiting = s.Db.VolunteerAssignments.Count(a =>
                            a.ShiftId == x.Id && a.Status == "Waiting"
                        ),
                    })
                    .ToListAsync();
            }
        );
        api.MapPost(
            "/volunteers/shifts",
            async (ShiftInput i, OperationsService s) =>
            {
                var p = await s.Project(i.ProjectId, true);
                var w = await s.Workspace(true);
                OperationsService.Title(i.Title);
                OperationsService.Dates(i.StartsAt, i.EndsAt);
                Check(
                    i.Capacity is > 0 and <= 10000
                        && i.StartsAt >= p.StartsAt
                        && i.EndsAt <= p.EndsAt
                        && i.Deadline <= i.StartsAt,
                    "Invalid capacity or shift dates."
                );
                await s.Person(i.SupervisorId, w.TenantId);
                if (i.ActivityId != null)
                    Check(
                        await s.Db.ProjectActivities.AnyAsync(a =>
                            a.Id == i.ActivityId && a.ProjectId == p.Id
                        ),
                        "Activity unavailable."
                    );
                var row = new VolunteerShift
                {
                    TenantId = p.TenantId,
                    OrgId = p.OrgId,
                    ProjectId = p.Id,
                    Title = i.Title,
                    StartsAt = i.StartsAt.ToUniversalTime(),
                    EndsAt = i.EndsAt.ToUniversalTime(),
                    Deadline = i.Deadline.ToUniversalTime(),
                    Capacity = i.Capacity,
                    SupervisorId = i.SupervisorId,
                    Location = i.Location,
                    ActivityId = i.ActivityId,
                    RequiredTraining = i.RequiredTraining,
                    ApprovalRequired = i.ApprovalRequired,
                };
                s.Db.VolunteerShifts.Add(row);
                s.Audit(w, row, "shift.created");
                await s.Db.SaveChangesAsync();
                return row;
            }
        );
        api.MapPost(
            "/volunteers/shifts/{id:guid}/apply",
            async (Guid id, OperationsService s) =>
            {
                var w = await s.Workspace();
                var shift = await s.Db.VolunteerShifts.SingleOrDefaultAsync(x =>
                    x.Id == id && x.TenantId == w.TenantId
                );
                Check(shift != null, "Opportunity unavailable.", 404);
                var project = await s.Project(shift.ProjectId);
                Check(
                    new[] { "Approved", "Planning", "In Progress" }.Contains(project.Status)
                        && shift.Deadline > DateTimeOffset.UtcNow,
                    "Opportunity closed.",
                    409
                );
                var person = await s.Db.Persons.SingleOrDefaultAsync(p =>
                    p.UserId == w.UserId
                    && s.Db.Memberships.Any(m =>
                        m.PersonId == p.Id && m.TenantId == w.TenantId && m.Status == "Active"
                    )
                );
                Check(person != null, "Active member profile required.", 403);
                if (shift.RequiredTraining != "")
                    Check(
                        await s.Db.VolunteerProfiles.AnyAsync(p =>
                            p.PersonId == person.Id
                            && p.TenantId == w.TenantId
                            && p.Training.Contains(shift.RequiredTraining)
                        ),
                        "Required training missing."
                    );
                await using var tx = await s.Db.Database.BeginTransactionAsync();
                await s.Lock(id);
                var old = await s.Db.VolunteerAssignments.SingleOrDefaultAsync(x =>
                    x.ShiftId == id && x.PersonId == person.Id
                );
                if (old != null)
                    return Results.Ok(old);
                var used = await s.Db.VolunteerAssignments.CountAsync(x =>
                    x.ShiftId == id
                    && new[] { "Approved", "Confirmed", "Present", "Completed" }.Contains(x.Status)
                );
                var row = new VolunteerAssignment
                {
                    TenantId = w.TenantId,
                    OrgId = shift.OrgId,
                    ShiftId = id,
                    PersonId = person.Id,
                    Status =
                        used >= shift.Capacity ? "Waiting"
                        : shift.ApprovalRequired ? "Applied"
                        : "Approved",
                };
                s.Db.VolunteerAssignments.Add(row);
                s.Audit(w, row, "volunteer.applied");
                await s.Db.SaveChangesAsync();
                await tx.CommitAsync();
                return Results.Ok(row);
            }
        );
        api.MapGet(
            "/volunteers/assignments",
            async (OperationsService s, Guid? shiftId = null) =>
            {
                var w = await s.Workspace();
                var manage = (await s.A.Grants(w)).Contains("operations.manage");
                var user = w.UserId;
                var q = s.Db.VolunteerAssignments.Where(a =>
                    a.TenantId == w.TenantId
                    && (
                        manage && s.A.VisibleIds(w).Contains(a.OrgId)
                        || s.Db.Persons.Any(p => p.Id == a.PersonId && p.UserId == user)
                    )
                );
                if (shiftId != null)
                    q = q.Where(a => a.ShiftId == shiftId);
                return await s.WithPeople(
                    await q.OrderByDescending(a => a.UpdatedAt).Take(100).ToListAsync()
                );
            }
        );
        api.MapPost(
            "/volunteers/assignments/{id:guid}/status",
            async (Guid id, StateInput i, OperationsService s) =>
            {
                var w = await s.Workspace();
                var row = await s.Db.VolunteerAssignments.SingleOrDefaultAsync(a =>
                    a.Id == id && a.TenantId == w.TenantId
                );
                Check(row != null, "Assignment unavailable.", 404);
                var own = await s.IsPerson(row.PersonId, w.UserId);
                var manager =
                    (await s.A.Grants(w)).Contains("operations.manage")
                    && await s.A.VisibleIds(w).ContainsAsync(row.OrgId);
                Check(
                    manager || own && i.Status is "Confirmed" or "Cancelled",
                    "Volunteer coordinator required.",
                    403
                );
                Check(row.Version == i.Version, "Assignment changed.", 409);
                Check(
                    new[] { "Approved", "Rejected", "Confirmed", "Cancelled" }.Contains(i.Status),
                    "Invalid assignment status."
                );
                await using var tx = await s.Db.Database.BeginTransactionAsync();
                await s.Lock(row.ShiftId);
                var shift = await s.Db.VolunteerShifts.SingleAsync(x => x.Id == row.ShiftId);
                if (i.Status == "Approved")
                {
                    Check(row.Status is "Applied" or "Waiting", "Application is not pending.", 409);
                    var count = await s.Db.VolunteerAssignments.CountAsync(a =>
                        a.ShiftId == shift.Id
                        && a.Id != id
                        && new[] { "Approved", "Confirmed", "Present", "Completed" }.Contains(
                            a.Status
                        )
                    );
                    Check(count < shift.Capacity, "Shift capacity reached.", 409);
                }
                if (i.Status == "Confirmed")
                    Check(row.Status == "Approved", "Approval required before confirmation.", 409);
                Check(
                    row.Status is not "Completed" and not "Present",
                    "Attendance has already started.",
                    409
                );
                s.Revision(row, "Assignment", Pack(row), w.UserId);
                row.Status = i.Status;
                row.Version++;
                await s.Notice(row.PersonId, "Volunteer assignment: " + i.Status, row);
                s.Audit(w, row, "volunteer.status");
                await s.Db.SaveChangesAsync();
                await tx.CommitAsync();
                return row;
            }
        );
        api.MapPost(
            "/volunteers/assignments/{id:guid}/attendance",
            async (Guid id, ShiftAttendanceInput i, OperationsService s) =>
            {
                var w = await s.Workspace(true);
                var row = await s.Db.VolunteerAssignments.SingleOrDefaultAsync(x =>
                    x.Id == id && x.TenantId == w.TenantId && s.A.VisibleIds(w).Contains(x.OrgId)
                );
                Check(row != null, "Assignment unavailable.", 404);
                Check(
                    i.Action is "CheckIn" or "CheckOut" or "Absent",
                    "Invalid attendance action."
                );
                await using var tx = await s.Db.Database.BeginTransactionAsync();
                await s.Lock(id);
                if (i.Action == "CheckIn")
                {
                    Check(
                        row.Status is "Approved" or "Confirmed" && row.CheckIn == null,
                        "Assignment is not ready for check-in.",
                        409
                    );
                    row.CheckIn = DateTimeOffset.UtcNow;
                    row.Status = "Present";
                }
                else if (i.Action == "CheckOut")
                {
                    Check(row.CheckIn != null && row.CheckOut == null, "Check in first.", 409);
                    var now = DateTimeOffset.UtcNow;
                    Check(
                        i.BreakMinutes >= 0
                            && i.BreakMinutes < (now - row.CheckIn.Value).TotalMinutes,
                        "Break exceeds actual attendance."
                    );
                    row.CheckOut = now;
                    row.BreakMinutes = i.BreakMinutes;
                    row.Status = "Completed";
                    var hours = new VolunteerHours
                    {
                        TenantId = w.TenantId,
                        OrgId = row.OrgId,
                        AssignmentId = id,
                        PersonId = row.PersonId,
                        ProjectId = await s
                            .Db.VolunteerShifts.Where(x => x.Id == row.ShiftId)
                            .Select(x => x.ProjectId)
                            .SingleAsync(),
                        StartsAt = row.CheckIn.Value,
                        EndsAt = now,
                        BreakMinutes = i.BreakMinutes,
                        Hours = Math.Round(
                            ((decimal)(now - row.CheckIn.Value).TotalMinutes - i.BreakMinutes) / 60,
                            2
                        ),
                        Reason = "Actual supervisor-recorded attendance",
                        SubmittedBy = w.UserId,
                    };
                    s.Db.VolunteerHours.Add(hours);
                }
                else
                {
                    Check(
                        row.CheckIn == null,
                        "Checked-in assignment cannot be marked absent.",
                        409
                    );
                    row.Status = "Absent";
                }
                row.Version++;
                s.Audit(w, row, "volunteer.attendance", i.Reason);
                await s.Db.SaveChangesAsync();
                await tx.CommitAsync();
                return row;
            }
        );
        api.MapPost(
            "/volunteers/hours",
            async (HoursInput i, OperationsService s) =>
            {
                i = i with
                {
                    StartsAt = i.StartsAt.ToUniversalTime(),
                    EndsAt = i.EndsAt.ToUniversalTime(),
                };
                var w = await s.Workspace();
                var a = await s.Db.VolunteerAssignments.SingleOrDefaultAsync(x =>
                    x.Id == i.AssignmentId && x.TenantId == w.TenantId
                );
                Check(a != null, "Assignment unavailable.", 404);
                Check(
                    await s.IsPerson(a.PersonId, w.UserId)
                        || (await s.A.Grants(w)).Contains("operations.manage")
                            && await s.A.VisibleIds(w).ContainsAsync(a.OrgId),
                    "Assignment unavailable.",
                    403
                );
                OperationsService.Dates(i.StartsAt, i.EndsAt);
                Check(
                    i.EndsAt <= DateTimeOffset.UtcNow
                        && i.EndsAt - i.StartsAt <= TimeSpan.FromHours(24)
                        && i.BreakMinutes >= 0
                        && i.BreakMinutes < (i.EndsAt - i.StartsAt).TotalMinutes
                        && i.Reason.Length >= 5,
                    "Manual hours need a valid past interval, breaks and evidence."
                );
                Check(
                    new[] { "Approved", "Confirmed", "Present", "Completed" }.Contains(a.Status),
                    "Approved assignment required.",
                    409
                );
                await using var tx = await s.Db.Database.BeginTransactionAsync();
                await s.Lock(a.PersonId);
                Check(
                    !await s.Db.VolunteerHours.AnyAsync(h =>
                        h.PersonId == a.PersonId
                        && h.TenantId == w.TenantId
                        && h.Status != "Rejected"
                        && h.StartsAt < i.EndsAt
                        && h.EndsAt > i.StartsAt
                    ),
                    "Overlapping hour claim.",
                    409
                );
                var row = new VolunteerHours
                {
                    TenantId = w.TenantId,
                    OrgId = a.OrgId,
                    AssignmentId = a.Id,
                    PersonId = a.PersonId,
                    ProjectId = await s
                        .Db.VolunteerShifts.Where(x => x.Id == a.ShiftId)
                        .Select(x => x.ProjectId)
                        .SingleAsync(),
                    StartsAt = i.StartsAt.ToUniversalTime(),
                    EndsAt = i.EndsAt.ToUniversalTime(),
                    BreakMinutes = i.BreakMinutes,
                    Hours = Math.Round(
                        ((decimal)(i.EndsAt - i.StartsAt).TotalMinutes - i.BreakMinutes) / 60,
                        2
                    ),
                    Reason = i.Reason,
                    SubmittedBy = w.UserId,
                };
                s.Db.VolunteerHours.Add(row);
                s.Audit(w, row, "hours.submitted");
                await s.Db.SaveChangesAsync();
                await tx.CommitAsync();
                return row;
            }
        );
        api.MapGet(
            "/volunteers/hours",
            async (OperationsService s, Guid? projectId = null) =>
            {
                var w = await s.Workspace();
                var manage = (await s.A.Grants(w)).Contains("operations.manage");
                var q = s.Db.VolunteerHours.Where(h =>
                    h.TenantId == w.TenantId
                    && (
                        manage && s.A.VisibleIds(w).Contains(h.OrgId)
                        || s.Db.Persons.Any(p => p.Id == h.PersonId && p.UserId == w.UserId)
                    )
                );
                if (projectId != null)
                    q = q.Where(h => h.ProjectId == projectId);
                return await s.WithPeople(
                    await q.OrderByDescending(h => h.StartsAt).Take(100).ToListAsync()
                );
            }
        );
        api.MapPost(
            "/volunteers/hours/{id:guid}/verify",
            async (Guid id, StateInput i, OperationsService s) =>
            {
                var w = await s.A.Workspace("operations.verify");
                var h = await s.Db.VolunteerHours.SingleOrDefaultAsync(x =>
                    x.Id == id && x.TenantId == w.TenantId && s.A.VisibleIds(w).Contains(x.OrgId)
                );
                Check(h != null, "Hours unavailable.", 404);
                Check(
                    h.SubmittedBy != w.UserId && !await s.IsPerson(h.PersonId, w.UserId),
                    "Independent verification required.",
                    403
                );
                Check(
                    h.Status == "Submitted"
                        && h.Version == i.Version
                        && i.Status is "Verified" or "Rejected",
                    "Invalid verification state.",
                    409
                );
                await using var tx = await s.Db.Database.BeginTransactionAsync();
                await s.Lock(h.PersonId);
                if (i.Status == "Verified")
                    Check(
                        !await s.Db.VolunteerHours.AnyAsync(x =>
                            x.Id != id
                            && x.TenantId == h.TenantId
                            && x.PersonId == h.PersonId
                            && x.Status == "Verified"
                            && x.StartsAt < h.EndsAt
                            && x.EndsAt > h.StartsAt
                        ),
                        "Overlapping verified hours.",
                        409
                    );
                s.Revision(h, "Hours", Pack(h), w.UserId);
                h.Status = i.Status;
                h.VerifiedBy = w.UserId;
                h.Version++;
                s.Audit(w, h, "hours.verified", i.Reason);
                await s.Db.SaveChangesAsync();
                await tx.CommitAsync();
                return h;
            }
        );
    }
}
