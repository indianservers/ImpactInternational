using Microsoft.EntityFrameworkCore;
using static Clubs.Access;
using static Clubs.GovernanceService;

namespace Clubs;

public record OperationReference(Guid Id, long Version, string Hash);

public class OperationsService(
    Access a,
    GovernanceService governance,
    NotificationService notifications
)
{
    public Access A => a;
    public Db Db => a.Db;
    public GovernanceService Governance => governance;
    public static readonly string[] Permissions =
    [
        "operations.view",
        "operations.manage",
        "operations.verify",
        "operations.documents",
        "operations.certificates",
    ];

    public async Task<Assignment> Workspace(bool write = false) =>
        await A.Workspace(write ? "operations.manage" : "operations.view");

    public async Task<Assignment> Scope(Guid org, bool write = false)
    {
        var w = await Workspace(write);
        await governance.Scope(w, org);
        return w;
    }

    public void Audit(Assignment w, OperationalRecord r, string action, string detail = "")
    {
        r.UpdatedAt = DateTimeOffset.UtcNow;
        governance.Audit(w, r.OrgId, "operations." + action, r.Id, detail);
    }

    public void Revision(
        OperationalRecord r,
        string kind,
        string body,
        Guid actor,
        string status = "Draft"
    ) =>
        Db.OperationRevisions.Add(
            new()
            {
                TenantId = r.TenantId,
                OrgId = r.OrgId,
                RecordId = r.Id,
                Kind = kind,
                Body = body,
                ActorId = actor,
                Status = status,
            }
        );

    public async Task Lock(Guid id) =>
        await Db.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock(hashtextextended({id.ToString()}, 6))"
        );

    public async Task<List<T>> WithPeople<T>(List<T> records)
        where T : IOperationPerson
    {
        var ids = records.Select(r => r.PersonId).Distinct().ToArray();
        var names = await Db
            .Persons.Where(p => ids.Contains(p.Id))
            .Select(p => new { p.Id, p.DisplayName })
            .ToDictionaryAsync(p => p.Id, p => p.DisplayName);
        foreach (var record in records)
            record.PersonName = names.GetValueOrDefault(record.PersonId, "Member");
        return records;
    }

    public async Task<Person> Person(Guid id, Guid tenant)
    {
        var p = await Db
            .Persons.Where(x =>
                x.Id == id && Db.Memberships.Any(m => m.PersonId == id && m.TenantId == tenant)
            )
            .Select(x => new
            {
                x.Id,
                x.UserId,
                x.DisplayName,
            })
            .SingleOrDefaultAsync();
        Check(p != null, "Person unavailable in this association.", 404);
        return new Person
        {
            Id = p.Id,
            UserId = p.UserId,
            DisplayName = p.DisplayName,
        };
    }

    public async Task<bool> IsPerson(Guid? id, Guid user) =>
        id != null && await Db.Persons.AnyAsync(p => p.Id == id && p.UserId == user);

    public async Task<ClubMeeting> Meeting(Guid id, bool write = false)
    {
        var w = await Workspace(write);
        var query = await Meetings(w);
        if (write)
            query = query.Where(m => A.VisibleIds(w).Contains(m.OrgId));
        var m = await query.SingleOrDefaultAsync(m => m.Id == id);
        Check(m != null, "Meeting unavailable.", 404);
        await MeetingPrivacy(m, w);
        return m;
    }

    public async Task MeetingPrivacy(ClubMeeting m, Assignment w)
    {
        if (m.Visibility == "Confidential")
            Check(
                m.OrganizerId == w.UserId
                    || await IsPerson(m.ChairId, w.UserId)
                    || await IsPerson(m.SecretaryId, w.UserId)
                    || await Db.Persons.AnyAsync(p =>
                        p.UserId == w.UserId
                        && (
                            m.RequiredPeople.Contains(p.Id.ToString())
                            || m.OptionalPeople.Contains(p.Id.ToString())
                        )
                    ),
                "Confidential meeting unavailable.",
                404
            );
        if (m.CommitteeId != null)
            await governance.CommitteeAccess(m.CommitteeId.Value);
    }

    public async Task<ServiceProject> Project(Guid id, bool write = false)
    {
        var w = await Workspace(write);
        var p = await Db.ServiceProjects.SingleOrDefaultAsync(p =>
            p.Id == id
            && p.TenantId == w.TenantId
            && (
                A.VisibleIds(w).Contains(p.OrgId)
                || (
                    !write
                    && new[]
                    {
                        "Approved",
                        "Planning",
                        "In Progress",
                        "Completed",
                        "Impact Verified",
                        "Archived",
                        "On Hold",
                        "Cancelled",
                    }.Contains(p.Status)
                    && Db.ProjectOrganizations.Any(o =>
                        o.ProjectId == id && A.VisibleIds(w).Contains(o.OrgId)
                    )
                )
            )
        );
        Check(p != null, "Project unavailable.", 404);
        return p;
    }

    public async Task<OperationTask> TaskRecord(Guid id, bool write = false)
    {
        var w = await Workspace();
        var t = await Db.OperationTasks.SingleOrDefaultAsync(x =>
            x.Id == id
            && x.TenantId == w.TenantId
            && (
                A.VisibleIds(w).Contains(x.OrgId)
                || Db.Persons.Any(p => p.Id == x.PersonId && p.UserId == w.UserId)
            )
        );
        Check(t != null, "Task unavailable.", 404);
        var manager = (await A.Grants(w)).Contains("operations.manage");
        Check(manager || await IsPerson(t.PersonId, w.UserId), "Task unavailable.", 404);
        if (t.SourceType == "Meeting" && t.SourceId != null)
            await Meeting(t.SourceId.Value);
        return t;
    }

    public async Task<GovernanceRequest> Submit(
        OperationalRecord r,
        string kind,
        string body,
        Assignment w
    )
    {
        Guid? subjectPerson =
            r is IssuedCertificate certificate ? certificate.PersonId
            : r is ServiceProject project ? project.ManagerId
            : null;
        Guid? subjectUser =
            subjectPerson == null
                ? null
                : await Db
                    .Persons.Where(p => p.Id == subjectPerson)
                    .Select(p => p.UserId)
                    .SingleOrDefaultAsync();
        var request = await governance.Request(
            w,
            r.OrgId,
            kind,
            kind + " review",
            Pack(new OperationReference(r.Id, r.Version, Hash(body))),
            subject: subjectUser
        );
        await governance.Submit(request, w);
        return request;
    }

    public async Task Notice(Guid? person, string text, OperationalRecord r)
    {
        var sender =
            r is OperationTask task ? task.CreatorId
            : r is ClubMeeting meeting ? meeting.OrganizerId
            : (await A.User()).Id;
        if (r is VolunteerAssignment assignment)
        {
            var supervisor = await Db
                .VolunteerShifts.Where(x => x.Id == assignment.ShiftId)
                .Select(x => x.SupervisorId)
                .SingleAsync();
            sender = await Db
                .Persons.Where(p => p.Id == supervisor)
                .Select(p => p.UserId ?? Guid.Empty)
                .SingleAsync();
        }
        await notifications.Operation(person, text, r, sender, A);
    }

    public static void Title(string text) =>
        Check(
            !string.IsNullOrWhiteSpace(text) && text.Length <= 200,
            "Provide a title up to 200 characters."
        );

    public static void Dates(DateTimeOffset start, DateTimeOffset end) =>
        Check(start < end && end - start <= TimeSpan.FromDays(3660), "Invalid date interval.");

    public async Task Apply(GovernanceRequest request, Assignment w)
    {
        var reference = Unpack<OperationReference>(request.Payload);
        if (request.Kind == "OperationProject")
        {
            var p = await Db.ServiceProjects.SingleAsync(p =>
                p.Id == reference.Id && p.TenantId == w.TenantId
            );
            Check(
                p.Version == reference.Version
                    && p.RequestId == request.Id
                    && p.Status == "Submitted",
                "Project proposal changed.",
                409
            );
            p.Status = "Approved";
            p.Version++;
        }
        else if (request.Kind == "OperationAgenda")
        {
            var m = await Db.ClubMeetings.SingleAsync(x =>
                x.Id == reference.Id && x.TenantId == w.TenantId
            );
            await MeetingPrivacy(m, w);
            Check(
                m.Version == reference.Version
                    && m.AgendaRequestId == request.Id
                    && Hash(m.Agenda) == reference.Hash,
                "Agenda changed.",
                409
            );
            m.AgendaStatus = "Published";
            m.Version++;
        }
        else if (request.Kind == "OperationMinutes")
        {
            var v = await Db.MeetingMinutes.SingleAsync(x =>
                x.Id == reference.Id && x.TenantId == w.TenantId
            );
            await Meeting(v.MeetingId);
            Check(
                v.Version == reference.Version
                    && v.RequestId == request.Id
                    && Hash(v.Body) == reference.Hash,
                "Minutes changed.",
                409
            );
            v.Status = "Published";
            v.Version++;
            Revision(v, "Minutes", v.Body, w.UserId, "Published");
        }
        else if (request.Kind == "OperationImpactCorrection")
        {
            var impact = await Db.ProjectImpacts.SingleAsync(x =>
                x.Id == reference.Id && x.TenantId == w.TenantId
            );
            Check(
                (await A.Grants(w)).Contains("operations.verify") && request.CreatorId != w.UserId,
                "Independent impact verifier required.",
                403
            );
            Check(
                impact.Version == reference.Version && impact.Status == "Verified",
                "Verified impact changed.",
                409
            );
            var revision = await Db.OperationRevisions.SingleAsync(x =>
                x.RecordId == impact.Id
                && x.Kind == "ImpactCorrection"
                && x.Status == "Submitted:" + request.Id
            );
            Check(Hash(revision.Body) == reference.Hash, "Correction content changed.", 409);
            var correction = Unpack<ImpactCorrectionInput>(revision.Body);
            Revision(impact, "Impact", Pack(impact), w.UserId, "Verified before correction");
            impact.Value = correction.Value;
            impact.Evidence = correction.Evidence;
            impact.Version++;
            impact.UpdatedAt = DateTimeOffset.UtcNow;
        }
        else if (request.Kind == "OperationImpact")
        {
            var i = await Db.ProjectImpacts.SingleAsync(x =>
                x.Id == reference.Id && x.TenantId == w.TenantId
            );
            Check(
                (await A.Grants(w)).Contains("operations.verify") && i.SubmittedBy != w.UserId,
                "Independent impact verifier required.",
                403
            );
            Check(
                i.Version == reference.Version && i.RequestId == request.Id && !i.Estimate,
                "Impact proposal changed.",
                409
            );
            i.Status = "Verified";
            i.Version++;
        }
        else if (request.Kind == "OperationDocument")
        {
            var d = await Db.GovernanceDocuments.SingleAsync(x =>
                x.Id == reference.Id && x.TenantId == w.TenantId
            );
            await Document(d);
            Check(
                d.CurrentVersion == reference.Version && d.ApprovalRequestId == request.Id,
                "Document changed.",
                409
            );
            d.Status = "Available";
        }
        else if (request.Kind == "OperationCertificate")
        {
            var c = await Db.IssuedCertificates.SingleAsync(x =>
                x.Id == reference.Id && x.TenantId == w.TenantId
            );
            Check(
                c.Version == reference.Version
                    && c.RequestId == request.Id
                    && Hash(c.Snapshot + c.Layout) == reference.Hash,
                "Certificate changed.",
                409
            );
            c.Status = "Issued";
            c.IssuedAt = DateTimeOffset.UtcNow;
            c.Version++;
        }
        request.ResultId = reference.Id;
    }

    public async Task<IQueryable<ClubMeeting>> Meetings(Assignment w)
    {
        var committee = (await A.Grants(w)).Contains("committees.view");
        var now = DateTimeOffset.UtcNow;
        return Db.ClubMeetings.Where(m =>
            m.TenantId == w.TenantId
            && (
                A.VisibleIds(w).Contains(m.OrgId)
                || m.OrganizerId == w.UserId
                || Db.Persons.Any(p =>
                    p.UserId == w.UserId
                    && (
                        p.Id == m.ChairId
                        || p.Id == m.SecretaryId
                        || m.RequiredPeople.Contains(p.Id.ToString())
                        || m.OptionalPeople.Contains(p.Id.ToString())
                    )
                )
            )
            && (
                m.Visibility != "Confidential"
                || m.OrganizerId == w.UserId
                || Db.Persons.Any(p =>
                    p.UserId == w.UserId
                    && (
                        p.Id == m.ChairId
                        || p.Id == m.SecretaryId
                        || m.RequiredPeople.Contains(p.Id.ToString())
                        || m.OptionalPeople.Contains(p.Id.ToString())
                    )
                )
            )
            && (
                m.CommitteeId == null
                || committee
                || Db.CommitteeMemberships.Any(c =>
                    c.CommitteeId == m.CommitteeId
                    && c.Status == "Active"
                    && c.StartsAt <= now
                    && c.EndsAt > now
                    && Db.Persons.Any(p => p.Id == c.PersonId && p.UserId == w.UserId)
                )
            )
        );
    }

    public async Task<object?> Review(GovernanceRequest r, Assignment w)
    {
        if (!r.Kind.StartsWith("Operation"))
            return null;
        var reference = Unpack<OperationReference>(r.Payload);
        switch (r.Kind)
        {
            case "OperationAgenda":
                var meeting = await Meeting(reference.Id);
                return new { meeting.Title, content = Unpack<AgendaItem[]>(meeting.Agenda) };
            case "OperationMinutes":
                var v = await Db.MeetingMinutes.SingleAsync(x => x.Id == reference.Id);
                await Meeting(v.MeetingId);
                return new
                {
                    v.Body,
                    v.Revision,
                    v.Status,
                };
            case "OperationProject":
                return await Project(reference.Id);
            case "OperationImpactCorrection":
                return await Db
                    .OperationRevisions.Where(x =>
                        x.RecordId == reference.Id
                        && x.Kind == "ImpactCorrection"
                        && x.Status == "Submitted:" + r.Id
                    )
                    .Select(x => x.Body)
                    .SingleAsync();
            case "OperationImpact":
                return await Db.ProjectImpacts.SingleAsync(x =>
                    x.Id == reference.Id && x.TenantId == w.TenantId
                );
            case "OperationDocument":
                var doc = await Db.GovernanceDocuments.SingleAsync(x => x.Id == reference.Id);
                await Document(doc);
                return new
                {
                    doc.Id,
                    doc.Title,
                    doc.CurrentVersion,
                    doc.Sha256,
                };
            case "OperationCertificate":
                var cert = await Db.IssuedCertificates.SingleAsync(x =>
                    x.Id == reference.Id && x.TenantId == w.TenantId
                );
                return new
                {
                    cert.Number,
                    cert.SourceType,
                    cert.SourceId,
                    cert.PersonId,
                    content = Unpack<Dictionary<string, string>>(cert.Snapshot),
                };
            default:
                return null;
        }
    }

    public async Task Document(GovernanceDocument d)
    {
        if (d.ObjectKey == "")
        {
            await GovernanceDocuments.Authorize(d, governance);
            Check(
                d.Status != "Expired"
                    && (d.RetainUntil == null || d.RetainUntil > DateTimeOffset.UtcNow),
                "Document expired.",
                410
            );
            return;
        }
        var w = await A.WorkspaceAny(
            "operations.documents",
            "operations.view",
            "governance.documents"
        );
        Check(
            d.TenantId == w.TenantId
                && (
                    await A.VisibleIds(w).ContainsAsync(d.OrgId)
                    || Unpack<Guid[]>(d.SharedUsers).Contains(w.UserId)
                ),
            "Document unavailable.",
            404
        );
        if (d.Visibility == "Private")
            Check(
                d.UploaderId == w.UserId || Unpack<Guid[]>(d.SharedUsers).Contains(w.UserId),
                "Private document unavailable.",
                404
            );
        if (d.CommitteeId != null)
            await governance.CommitteeAccess(d.CommitteeId.Value);
        if (d.OperationId != null && await Db.ClubMeetings.AnyAsync(m => m.Id == d.OperationId))
            await Meeting(d.OperationId.Value);
        Check(
            d.Status != "Expired"
                && (d.RetainUntil == null || d.RetainUntil > DateTimeOffset.UtcNow),
            "Document expired.",
            410
        );
    }
}
