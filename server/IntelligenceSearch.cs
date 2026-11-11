using Microsoft.EntityFrameworkCore;
using static Clubs.Access;

namespace Clubs;

public record IntelligenceSearchHit(string Kind, Guid Id, string Title, string Context, string Url);

public static class IntelligenceSearch
{
    public static string Pattern(string term) =>
        "%" + term.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_") + "%";

    public static async Task<List<IntelligenceSearchHit>> Find(
        IntelligenceService s,
        string query,
        string kind = "all",
        Guid? org = null
    )
    {
        Check(query.Trim().Length is >= 2 and <= 100, "Search requires 2–100 characters.");
        var w = await s.Scoped(org, "intelligence.assistant");
        var g = await s.A.Grants(w);
        var ids = s.A.VisibleIds(w);
        var pattern = Pattern(query.Trim());
        var output = new List<IntelligenceSearchHit>();
        bool Want(string k) => kind == "all" || kind == k;
        if (Want("organizations") && (g.Contains("organization.view") || g.Contains("clubs.view")))
            output.AddRange(
                await s
                    .A.Visible(w)
                    .Where(o =>
                        EF.Functions.ILike(o.Name, pattern, "\\")
                        || EF.Functions.ILike(o.Code, pattern, "\\")
                    )
                    .OrderBy(o => o.Name)
                    .Take(10)
                    .Select(o => new IntelligenceSearchHit(
                        "organizations",
                        o.Id,
                        o.Name,
                        o.Country,
                        "/app/organizations?search=" + query
                    ))
                    .ToListAsync()
            );
        if (Want("members") && (g.Contains("members.view") || g.Contains("directory.view")))
        {
            var q = s.Db.Memberships.Where(m => m.TenantId == w.TenantId && ids.Contains(m.ClubId));
            if (!g.Contains("members.view"))
                q = q.Where(m =>
                    m.Status == "Active"
                    && (m.EndsAt == null || m.EndsAt > DateTimeOffset.UtcNow)
                    && s.Db.PersonPrivacy.Any(p => p.PersonId == m.PersonId && p.DirectoryVisible)
                );
            output.AddRange(
                await q.Where(m =>
                        EF.Functions.ILike(m.Person.DisplayName, pattern, "\\")
                        || EF.Functions.ILike(m.Number, pattern, "\\")
                    )
                    .OrderBy(m => m.Person.DisplayName)
                    .Take(10)
                    .Select(m => new IntelligenceSearchHit(
                        "members",
                        m.Id,
                        m.Person.DisplayName,
                        m.Club.Name,
                        "/app/" + (g.Contains("members.view") ? "members/" : "members/") + m.Id
                    ))
                    .ToListAsync()
            );
        }
        if (Want("officers") && g.Contains("leadership.view"))
            output.AddRange(
                await GovernanceService
                    .Current(s.Db)
                    .Where(a =>
                        ids.Contains(a.OrgId)
                        && (
                            EF.Functions.ILike(a.PositionTitle, pattern, "\\")
                            || s.Db.Persons.Any(p =>
                                p.Id == a.PersonId
                                && EF.Functions.ILike(p.DisplayName, pattern, "\\")
                            )
                        )
                    )
                    .Take(10)
                    .Select(a => new IntelligenceSearchHit(
                        "officers",
                        a.Id,
                        s.Db.Persons.Where(p => p.Id == a.PersonId)
                            .Select(p => p.DisplayName)
                            .First(),
                        a.PositionTitle,
                        "/app/leadership"
                    ))
                    .ToListAsync()
            );
        if (Want("committees") && g.Contains("committees.view"))
        {
            var candidates = await s
                .Db.Committees.Where(c =>
                    c.TenantId == w.TenantId
                    && ids.Contains(c.OrgId)
                    && EF.Functions.ILike(c.Name, pattern, "\\")
                )
                .OrderBy(c => c.Name)
                .Take(20)
                .ToListAsync();
            foreach (var c in candidates)
                try
                {
                    await s.Operations.Governance.CommitteeAccess(c.Id);
                    output.Add(
                        new("committees", c.Id, c.Name, "Committee", "/app/committees/" + c.Id)
                    );
                }
                catch (ApiError) { }
        }
        if (Want("events") && g.Contains("events.view"))
        {
            var candidates = await s
                .Db.Events.Where(e =>
                    e.TenantId == w.TenantId
                    && ids.Contains(e.OrgId)
                    && EF.Functions.ILike(e.Title, pattern, "\\")
                )
                .OrderByDescending(e => e.StartsAt)
                .Take(20)
                .ToListAsync();
            foreach (var e in candidates)
                try
                {
                    await s.Events.View(e);
                    output.Add(
                        new(
                            "events",
                            e.Id,
                            e.Title,
                            e.StartsAt.ToString("yyyy-MM-dd"),
                            "/app/events/" + e.Id
                        )
                    );
                }
                catch (ApiError) { }
        }
        if (Want("meetings") && g.Contains("operations.view"))
        {
            var meetings = await s.Operations.Meetings(w);
            output.AddRange(
                await meetings
                    .Where(m => EF.Functions.ILike(m.Title, pattern, "\\"))
                    .OrderByDescending(m => m.StartsAt)
                    .Take(10)
                    .Select(m => new IntelligenceSearchHit(
                        "meetings",
                        m.Id,
                        m.Title,
                        m.Type,
                        "/app/operations/meetings/" + m.Id
                    ))
                    .ToListAsync()
            );
        }
        if (Want("tasks") && g.Contains("operations.view"))
        {
            var candidates = await s
                .Db.OperationTasks.Where(t =>
                    t.TenantId == w.TenantId
                    && (
                        ids.Contains(t.OrgId)
                        || s.Db.Persons.Any(p => p.Id == t.PersonId && p.UserId == w.UserId)
                    )
                    && EF.Functions.ILike(t.Title, pattern, "\\")
                )
                .OrderBy(t => t.Id)
                .Take(20)
                .ToListAsync();
            foreach (var t in candidates)
                try
                {
                    await s.Operations.TaskRecord(t.Id);
                    output.Add(new("tasks", t.Id, t.Title, t.Status, "/app/operations/tasks"));
                }
                catch (ApiError) { }
        }
        if (Want("projects") && g.Contains("operations.view"))
        {
            var candidates = await s
                .Db.ServiceProjects.Where(p =>
                    p.TenantId == w.TenantId
                    && (
                        ids.Contains(p.OrgId)
                        || s.Db.ProjectOrganizations.Any(o =>
                            o.ProjectId == p.Id && ids.Contains(o.OrgId)
                        )
                    )
                    && EF.Functions.ILike(p.Name, pattern, "\\")
                )
                .OrderBy(p => p.Name)
                .Take(20)
                .ToListAsync();
            foreach (var p in candidates)
                try
                {
                    await s.Operations.Project(p.Id);
                    output.Add(
                        new("projects", p.Id, p.Name, p.Status, "/app/operations/projects/" + p.Id)
                    );
                }
                catch (ApiError) { }
        }
        if (Want("documents") && g.Contains("operations.view"))
        {
            var uid = w.UserId.ToString();
            var candidates = await s
                .Db.GovernanceDocuments.Where(d =>
                    d.TenantId == w.TenantId
                    && (ids.Contains(d.OrgId) || d.SharedUsers.Contains(uid))
                    && d.Status != "Expired"
                    && EF.Functions.ILike(d.Title, pattern, "\\")
                )
                .OrderByDescending(d => d.At)
                .Take(20)
                .ToListAsync();
            foreach (var d in candidates)
                try
                {
                    await s.Operations.Document(d);
                    output.Add(
                        new(
                            "documents",
                            d.Id,
                            d.Title,
                            "Version " + d.CurrentVersion,
                            "/app/operations/documents?document=" + d.Id
                        )
                    );
                }
                catch (ApiError) { }
        }
        if (Want("certificates") && g.Contains("operations.view"))
        {
            output.AddRange(
                await s
                    .Db.IssuedCertificates.Where(c =>
                        c.TenantId == w.TenantId
                        && c.Status == "Issued"
                        && (
                            g.Contains("operations.certificates") && ids.Contains(c.OrgId)
                            || s.Db.Persons.Any(p => p.Id == c.PersonId && p.UserId == w.UserId)
                        )
                        && EF.Functions.ILike(c.Number, pattern, "\\")
                    )
                    .OrderBy(c => c.Number)
                    .Take(10)
                    .Select(c => new IntelligenceSearchHit(
                        "certificates",
                        c.Id,
                        c.Number,
                        c.SourceType,
                        "/app/operations/certificates"
                    ))
                    .ToListAsync()
            );
        }
        if (Want("invoices") && (g.Contains("finance.self") || g.Contains("finance.view")))
        {
            var q = s.Db.FinanceInvoices.Where(i =>
                i.TenantId == w.TenantId
                && i.State == "Issued"
                && (
                    s.Db.Persons.Any(p => p.Id == i.PersonId && p.UserId == w.UserId)
                    || g.Contains("finance.view")
                        && ids.Contains(i.OrgId)
                        && s.Db.FinanceGrants.Any(a =>
                            a.EntityId == i.EntityId && a.UserId == w.UserId
                        )
                )
            );
            output.AddRange(
                await q.Where(i =>
                        EF.Functions.ILike(i.Number, pattern, "\\")
                        || EF.Functions.ILike(i.Customer, pattern, "\\")
                    )
                    .OrderBy(i => i.Number)
                    .Take(10)
                    .Select(i => new IntelligenceSearchHit(
                        "invoices",
                        i.Id,
                        i.Number,
                        i.Currency + " / " + i.Customer,
                        "/app/finance/invoices/" + i.Id
                    ))
                    .ToListAsync()
            );
        }
        return output.Take(100).ToList();
    }
}
