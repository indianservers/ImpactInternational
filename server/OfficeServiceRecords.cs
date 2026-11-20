using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using static Clubs.Access;

namespace Clubs;

// Archival service records are deliberately separate from appointments and access assignments.
public class HistoricalOfficeRecord : ITenantRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid OrgId { get; set; }
    [MaxLength(200)] public string Name { get; set; } = "";
    [MaxLength(150)] public string OfficeTitle { get; set; } = "";
    public bool IsPresident { get; set; }
    public int StartYear { get; set; }
    public int EndYear { get; set; }
    [MaxLength(2000)] public string Notes { get; set; } = "";
    public bool Archived { get; set; }
    public Guid RecordedBy { get; set; }
    public DateTimeOffset RecordedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public long Version { get; set; } = 1;
}

public partial class Db
{
    public DbSet<HistoricalOfficeRecord> HistoricalOfficeRecords => Set<HistoricalOfficeRecord>();
}

public record OfficeServiceInput(string Name, string OfficeTitle, bool IsPresident, int StartYear, int EndYear, string Notes = "", long Version = 1);
public record ArchiveOfficeServiceInput(long Version, string Reason);
public record OfficeServiceItem(Guid Id, string Name, string OfficeTitle, bool IsPresident, int StartYear, int EndYear, string Notes, string Source, long Version);

public static class OfficeServiceRecords
{
    public static void MapOfficeServiceRecords(this RouteGroupBuilder api)
    {
        api.MapGet("/organizations/{orgId:guid}/office-service-records", async (Guid orgId, Access a, int? year = null, bool presidentsOnly = false, int page = 1) =>
        {
            var w = await a.Workspace("leadership.view");
            var unit = await a.Visible(w).SingleOrDefaultAsync(x => x.Id == orgId) ?? throw new ApiError(404, "Organization outside authorized scope.");
            TimeZoneInfo zone;
            try { zone = TimeZoneInfo.FindSystemTimeZoneById(unit.TimeZone); }
            catch (TimeZoneNotFoundException) { zone = TimeZoneInfo.Utc; }
            catch (InvalidTimeZoneException) { zone = TimeZoneInfo.Utc; }
            Check(year == null || year >= 1800 && year <= DateTimeOffset.UtcNow.Year, "Choose a valid service year.");
            var manual = await a.Db.HistoricalOfficeRecords.AsNoTracking()
                .Where(x => x.TenantId == w.TenantId && x.OrgId == orgId && !x.Archived)
                .Select(x => new OfficeServiceItem(x.Id, x.Name, x.OfficeTitle, x.IsPresident, x.StartYear, x.EndYear, x.Notes, "Historical record", x.Version)).ToListAsync();
            var now = DateTimeOffset.UtcNow;
            var recorded = await a.Db.OfficerAppointments.AsNoTracking()
                .Where(x => x.TenantId == w.TenantId && x.OrgId == orgId && x.StartsAt < now
                    && (x.Status == "Completed" || x.Status == "Active" && x.EndsAt <= now || x.Status == "Removed" || x.Status == "Resigned"))
                .Select(x => new {
                    x.Id, x.StartsAt, x.EndsAt, x.Status, x.Version,
                    title = x.PositionTitle != "" ? x.PositionTitle : a.Db.OfficerPositions.Where(p => p.Id == x.PositionId && p.TenantId == w.TenantId).Select(p => p.Title).FirstOrDefault(),
                    name = a.Db.Persons.Where(p => p.Id == x.PersonId).Select(p => p.DisplayName).FirstOrDefault(),
                    closedAt = a.Db.OfficerAppointmentHistory.Where(h => h.TenantId == w.TenantId && h.AppointmentId == x.Id && (h.Status == "Removed" || h.Status == "Resigned"))
                        .OrderByDescending(h => h.At).Select(h => (DateTimeOffset?)h.At).FirstOrDefault()
                }).ToListAsync();
            var appointments = recorded.Select(x => new { value = x, end = x.closedAt != null && x.closedAt < x.EndsAt ? x.closedAt.Value : x.EndsAt })
                .Where(x => x.end <= now && x.end > x.value.StartsAt)
                .Select(x => new OfficeServiceItem(x.value.Id, x.value.name ?? "Name not recorded", x.value.title ?? "Office bearer",
                    IsPresident(x.value.title ?? ""), TimeZoneInfo.ConvertTime(x.value.StartsAt, zone).Year, TimeZoneInfo.ConvertTime(x.end.AddTicks(-1), zone).Year, "", "Appointment", x.value.Version));
            var all = manual.Concat(appointments).Where(x => (!presidentsOnly || x.IsPresident) && (year == null || x.StartYear <= year && x.EndYear >= year))
                .OrderByDescending(x => x.EndYear).ThenByDescending(x => x.StartYear).ThenBy(x => x.OfficeTitle).ThenBy(x => x.Name).ThenBy(x => x.Id).ToList();
            var selectedPage = Math.Clamp(page, 1, Math.Max(1, (all.Count + 24) / 25));
            return Results.Ok(new { orgId, total = all.Count, page = selectedPage, pageSize = 25,
                items = all.Skip((selectedPage - 1) * 25).Take(25), canManage = (await a.Grants(w)).Contains("leadership.manage") });
        });
        api.MapPost("/organizations/{orgId:guid}/office-service-records", async (Guid orgId, OfficeServiceInput input, Access a) =>
        {
            var w = await a.Workspace("leadership.manage");
            await using var tx = await a.Mutation(w);
            Check(await a.VisibleIds(w).ContainsAsync(orgId), "Organization outside authorized scope.", 404);
            Validate(input);
            var record = new HistoricalOfficeRecord { TenantId = w.TenantId, OrgId = orgId, RecordedBy = w.UserId };
            Apply(record, input);
            a.Db.HistoricalOfficeRecords.Add(record);
            await a.Log(w, "leadership.history.create", record.Id.ToString(), System.Text.Json.JsonSerializer.Serialize(record));
            await tx.CommitAsync();
            return Results.Ok(record);
        });
        api.MapPut("/organizations/{orgId:guid}/office-service-records/{id:guid}", async (Guid orgId, Guid id, OfficeServiceInput input, Access a) =>
        {
            var w = await a.Workspace("leadership.manage");
            await using var tx = await a.Mutation(w);
            var record = await Find(orgId, id, w, a);
            Check(record.Version == input.Version, "This record changed. Refresh and try again.", 409);
            Validate(input);
            var before = System.Text.Json.JsonSerializer.Serialize(record);
            Apply(record, input); record.Version++;
            await a.Log(w, "leadership.history.update", id.ToString(), System.Text.Json.JsonSerializer.Serialize(new { before, after = record }));
            await tx.CommitAsync();
            return Results.Ok(record);
        });
        api.MapPost("/organizations/{orgId:guid}/office-service-records/{id:guid}/archive", async (Guid orgId, Guid id, ArchiveOfficeServiceInput input, Access a) =>
        {
            var w = await a.Workspace("leadership.manage");
            await using var tx = await a.Mutation(w);
            var record = await Find(orgId, id, w, a);
            Check(record.Version == input.Version, "This record changed. Refresh and try again.", 409);
            Check((input.Reason?.Trim().Length ?? 0) is >= 3 and <= 500, "Enter an archive reason (3–500 characters).");
            record.Archived = true; record.UpdatedAt = DateTimeOffset.UtcNow; record.Version++;
            await a.Log(w, "leadership.history.archive", id.ToString(), input.Reason!.Trim());
            await tx.CommitAsync();
            return Results.NoContent();
        });
    }
    static async Task<HistoricalOfficeRecord> Find(Guid orgId, Guid id, Assignment w, Access a)
    {
        Check(await a.VisibleIds(w).ContainsAsync(orgId), "Organization outside authorized scope.", 404);
        return await a.Db.HistoricalOfficeRecords.SingleOrDefaultAsync(x => x.Id == id && x.OrgId == orgId && x.TenantId == w.TenantId && !x.Archived)
            ?? throw new ApiError(404, "Historical record not found.");
    }
    static void Validate(OfficeServiceInput input)
    {
        Check((input.Name?.Trim().Length ?? 0) is >= 2 and <= 200, "Enter the office bearer's name (2–200 characters).");
        Check((input.OfficeTitle?.Trim().Length ?? 0) is >= 2 and <= 150, "Enter the office title (2–150 characters).");
        Check(input.StartYear >= 1800 && input.EndYear >= input.StartYear && input.EndYear <= DateTimeOffset.UtcNow.Year, "Service years must run from 1800 through the current year, with the start before the end.");
        Check((input.Notes?.Length ?? 0) <= 2000, "Notes must be at most 2,000 characters.");
    }
    static void Apply(HistoricalOfficeRecord record, OfficeServiceInput input)
    {
        record.Name = input.Name.Trim(); record.OfficeTitle = input.OfficeTitle.Trim(); record.IsPresident = input.IsPresident;
        record.StartYear = input.StartYear; record.EndYear = input.EndYear; record.Notes = input.Notes?.Trim() ?? ""; record.UpdatedAt = DateTimeOffset.UtcNow;
    }
    static bool IsPresident(string title) => title.Contains("president", StringComparison.OrdinalIgnoreCase)
        && !title.Contains("vice", StringComparison.OrdinalIgnoreCase) && !title.Contains("elect", StringComparison.OrdinalIgnoreCase);
}
