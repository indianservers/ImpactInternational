using System.Globalization;
using Microsoft.EntityFrameworkCore;
using static Clubs.Access;

namespace Clubs;

public class AnnualDuesSettings
{
    public Guid TenantId { get; set; }
    public int ExpiryMonth { get; set; } = 3;
    public int ExpiryDay { get; set; } = 31;
    public decimal FirstHalfFee { get; set; } = 500;
    public decimal SecondHalfFee { get; set; } = 250;
    public long Version { get; set; }
}

public class AnnualDuesPayment
{
    public Guid MembershipId { get; set; }
    public int CycleYear { get; set; }
    public decimal Amount { get; set; }
    public DateOnly PaidOn { get; set; }
}

public partial class Db
{
    public DbSet<AnnualDuesSettings> AnnualDuesSettings => Set<AnnualDuesSettings>();
    public DbSet<AnnualDuesPayment> AnnualDuesPayments => Set<AnnualDuesPayment>();

    private static void ConfigureAnnualDues(ModelBuilder model)
    {
        model.Entity<AnnualDuesSettings>().HasKey(x => x.TenantId);
        model.Entity<AnnualDuesSettings>().HasOne<Tenant>().WithOne().HasForeignKey<AnnualDuesSettings>(x => x.TenantId);
        model.Entity<AnnualDuesPayment>().HasKey(x => new { x.MembershipId, x.CycleYear });
        model.Entity<AnnualDuesPayment>().HasOne<Membership>().WithMany().HasForeignKey(x => x.MembershipId);
    }
}

public record AnnualDuesSettingsInput(int ExpiryMonth, int ExpiryDay, decimal FirstHalfFee, decimal SecondHalfFee, long Version);
public record AnnualDuesPaymentInput(bool? Paid, DateOnly? PaidOn = null, int? CycleYear = null);
public record AnnualDuesActivationInput(long Version);

public static class AnnualDues
{
    private static readonly TimeZoneInfo India = TimeZoneInfo.FindSystemTimeZoneById("Asia/Kolkata");
    private static readonly string[] CurrentStatuses = ["Active", "Inactive", "Suspended", "Expired"];

    public static Task<bool> Applies(Db db, Guid tenant) =>
        db.Tenants.AnyAsync(t => t.Id == tenant && t.Name == ImpactSetup.Name);

    public static DateOnly Today => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, India).DateTime);

    public static Task<AnnualDuesSettings?> StoredSettings(Db db, Guid tenant) =>
        db.AnnualDuesSettings.SingleOrDefaultAsync(x => x.TenantId == tenant);

    public static async Task<AnnualDuesSettings> Settings(Db db, Guid tenant) =>
        await StoredSettings(db, tenant) ?? new AnnualDuesSettings { TenantId = tenant };

    public static DateOnly CycleEnd(AnnualDuesSettings settings, DateOnly date)
    {
        var end = new DateOnly(date.Year, settings.ExpiryMonth, settings.ExpiryDay);
        return end < date ? end.AddYears(1) : end;
    }

    public static DateOnly SecondHalfStarts(DateOnly end) => end.AddYears(-1).AddDays(1).AddMonths(6);

    // Store the end of the selected calendar day in India.
    public static DateTimeOffset EndsAt(DateOnly end) =>
        new(TimeZoneInfo.ConvertTimeToUtc(end.AddDays(1).ToDateTime(TimeOnly.MinValue).AddMilliseconds(-1), India));

    public static async Task<DateTimeOffset?> InitialExpiry(Db db, Guid tenant, DateTimeOffset joined, int? validityMonths)
    {
        if (await Applies(db, tenant))
            return EndsAt(CycleEnd(await Settings(db, tenant), Today));
        return validityMonths.HasValue ? joined.AddMonths(validityMonths.Value) : null;
    }

    private static bool CanWrite(bool platformAdmin, string role) =>
        platformAdmin || role is "Regional President" or "Club President";

    private static async Task<Assignment> Workspace(Access access)
    {
        var workspace = await access.Workspace(DuesCenter.View);
        Check(await Applies(access.Db, workspace.TenantId), "Annual membership dues are unavailable in this association.", 404);
        return workspace;
    }

    private static IQueryable<Membership> Members(Access access, Assignment workspace)
    {
        var orgs = access.VisibleIds(workspace);
        return access.Db.Memberships.Where(m => m.TenantId == workspace.TenantId && orgs.Contains(m.ClubId));
    }

    public static void MapAnnualDues(this RouteGroupBuilder root)
    {
        var api = root.MapGroup("/finance/annual-dues");
        api.MapGet("/settings", async (Access access) =>
        {
            var workspace = await Workspace(access);
            var settings = await Settings(access.Db, workspace.TenantId);
            var today = Today;
            var end = CycleEnd(settings, today);
            var secondHalf = SecondHalfStarts(end);
            var administrator = (await access.User()).PlatformAdmin;
            var earliest = await Members(access, workspace).MinAsync(m => (DateTimeOffset?)m.JoinedAt);
            var firstReportYear = earliest.HasValue
                ? Math.Max(1901, Math.Min(end.Year - 1, CycleEnd(settings, DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(earliest.Value, India).DateTime)).Year))
                : end.Year;
            var organizations = await access.Visible(workspace)
                .Where(o => access.Db.Levels.Any(l => l.Id == o.LevelId && (l.Name == "Region" || l.Name == "Club")))
                .OrderBy(o => o.Name)
                .Select(o => new { o.Id, o.Name, o.ParentId, type = access.Db.Levels.Where(l => l.Id == o.LevelId).Select(l => l.Name).First() })
                .ToListAsync();
            return new
            {
                settings.ExpiryMonth, settings.ExpiryDay, settings.FirstHalfFee, settings.SecondHalfFee, settings.Version,
                expiryDate = end, today, firstReportYear, secondHalfStarts = secondHalf, currentFee = today < secondHalf ? settings.FirstHalfFee : settings.SecondHalfFee,
                canConfigure = administrator, canWrite = CanWrite(administrator, await IciPolicy.Role(access, workspace)), organizations,
            };
        });

        api.MapPut("/settings", async (AnnualDuesSettingsInput input, Access access) =>
        {
            var workspace = await Workspace(access);
            Check((await access.User()).PlatformAdmin, "Only platform administrators can change dues settings.", 403);
            Check(input.ExpiryMonth is >= 1 and <= 12 && input.ExpiryDay >= 1
                && input.ExpiryDay <= DateTime.DaysInMonth(2025, input.ExpiryMonth), "Enter a valid annual expiry month and day.");
            Check(input.FirstHalfFee is >= 0 and <= 10000000 && input.SecondHalfFee is >= 0 and <= 10000000
                && decimal.Round(input.FirstHalfFee, 2) == input.FirstHalfFee
                && decimal.Round(input.SecondHalfFee, 2) == input.SecondHalfFee, "Enter valid INR amounts with up to two decimal places.");

            await using var transaction = await access.Mutation(workspace);
            var db = access.Db;
            var settings = await Settings(db, workspace.TenantId);
            Check(settings.Version == input.Version, "Dues settings changed. Refresh and try again.", 409);
            if (settings.Version == 0) db.AnnualDuesSettings.Add(settings);
            var today = Today;
            var oldEnd = CycleEnd(settings, today);
            settings.ExpiryMonth = input.ExpiryMonth;
            settings.ExpiryDay = input.ExpiryDay;
            settings.FirstHalfFee = input.FirstHalfFee;
            settings.SecondHalfFee = input.SecondHalfFee;
            settings.Version++;
            var newEnd = CycleEnd(settings, today);
            if (oldEnd != newEnd)
            {
                if (oldEnd.Year != newEnd.Year)
                {
                    var payments = db.AnnualDuesPayments.Where(p => p.CycleYear == oldEnd.Year
                        && db.Memberships.Any(m => m.Id == p.MembershipId && m.TenantId == workspace.TenantId));
                    Check(!await db.AnnualDuesPayments.AnyAsync(p => p.CycleYear == newEnd.Year
                        && payments.Any(current => current.MembershipId == p.MembershipId)),
                        "This change would overlap two cycles with recorded payments.", 409);
                    await payments.ExecuteUpdateAsync(update => update.SetProperty(p => p.CycleYear, newEnd.Year));
                }
                var end = EndsAt(newEnd);
                await db.Memberships.Where(m => m.TenantId == workspace.TenantId
                    && (m.Status == "Active" || m.Status == "Inactive" || m.Status == "Suspended"))
                    .ExecuteUpdateAsync(update => update.SetProperty(m => m.EndsAt, end)
                        .SetProperty(m => m.UpdatedAt, DateTimeOffset.UtcNow).SetProperty(m => m.Version, m => m.Version + 1));
            }
            await access.Log(workspace, "annual-dues.settings", workspace.TenantId.ToString(), System.Text.Json.JsonSerializer.Serialize(input));
            await db.SaveChangesAsync();
            await transaction.CommitAsync();
            return Results.NoContent();
        });

        api.MapGet("/members", Report);
        api.MapPut("/members/{id:guid}/payment", async (Guid id, AnnualDuesPaymentInput input, Access access) =>
        {
            Check(input.Paid.HasValue, "Choose Paid or Unpaid.");
            var workspace = await Workspace(access);
            Check(CanWrite((await access.User()).PlatformAdmin, await IciPolicy.Role(access, workspace)), "Payment updates require a president or platform administrator.", 403);
            await using var transaction = await access.Mutation(workspace);
            var membership = await Members(access, workspace).SingleOrDefaultAsync(m => m.Id == id);
            Check(membership != null, "Membership outside your scope.", 404);

            var settings = await Settings(access.Db, workspace.TenantId);
            var today = Today;
            var currentEnd = CycleEnd(settings, today);
            var end = ReportEnd(settings, currentEnd, input.CycleYear);
            var start = end.AddYears(-1).AddDays(1);
            var paidOn = input.PaidOn ?? today;
            if (input.Paid == true)
                Check(paidOn >= start && paidOn <= end && paidOn <= today,
                    "Enter the actual payment date within the selected membership year.");
            var cutoff = EndsAt(end);
            Check(membership!.JoinedAt <= cutoff, "This membership did not exist in the selected year.", 409);
            var statusAtEnd = end == currentEnd ? membership.Status : await access.Db.MembershipHistory
                .Where(h => h.MembershipId == id && h.EffectiveAt <= cutoff)
                .OrderByDescending(h => h.EffectiveAt).ThenByDescending(h => h.RecordedAt).ThenByDescending(h => h.Id)
                .Select(h => h.NewStatus).FirstOrDefaultAsync() ?? membership.Status;
            var payment = await access.Db.AnnualDuesPayments.FindAsync(id, end.Year);
            if (input.Paid == true && payment == null)
            {
                Check(CurrentStatuses.Contains(statusAtEnd), "Membership was closed in the selected year.", 409);
                var amount = paidOn < SecondHalfStarts(end) ? settings.FirstHalfFee : settings.SecondHalfFee;
                access.Db.AnnualDuesPayments.Add(new AnnualDuesPayment { MembershipId = id, CycleYear = end.Year, Amount = amount, PaidOn = paidOn });
                await access.Log(workspace, "annual-dues.paid", id.ToString(), $"{paidOn:yyyy-MM-dd} · INR {amount}");
            }
            if (input.Paid == false && payment != null)
            {
                access.Db.AnnualDuesPayments.Remove(payment);
                await access.Log(workspace, "annual-dues.unpaid", id.ToString(), $"{payment.PaidOn:yyyy-MM-dd} · INR {payment.Amount}");
            }
            await access.Db.SaveChangesAsync();
            await transaction.CommitAsync();
            return Results.NoContent();
        });

        api.MapPost("/members/{id:guid}/activate", async (Guid id, AnnualDuesActivationInput input, MembershipService service) =>
        {
            var access = service.Access;
            var workspace = await Workspace(access);
            Check(CanWrite((await access.User()).PlatformAdmin, await IciPolicy.Role(access, workspace)), "Activation requires a president or platform administrator.", 403);
            await using var transaction = await access.Mutation(workspace);
            await service.Lock(id);
            var member = await Members(access, workspace).Include(m => m.Person).SingleOrDefaultAsync(m => m.Id == id);
            Check(member != null, "Membership outside your scope.", 404);
            Check(member!.Version == input.Version, "Membership changed. Refresh and try again.", 409);
            Check(CurrentStatuses.Contains(member.Status), "This membership cannot be activated.", 409);
            var end = EndsAt(CycleEnd(await Settings(service.Db, workspace.TenantId), Today));
            if (member.Status != "Active")
            {
                member.EndsAt = end;
                await service.ChangeStatus(member, "Active", "Membership activated for annual dues cycle", workspace.UserId);
            }
            else if (member.EndsAt != end)
            {
                member.EndsAt = end;
                member.Version++;
                member.UpdatedAt = DateTimeOffset.UtcNow;
                service.History(member, member.Status, member.Status, "Membership activated for annual dues cycle", workspace.UserId, DateTimeOffset.UtcNow);
            }
            await service.Db.SaveChangesAsync();
            await transaction.CommitAsync();
            return Results.NoContent();
        });
    }

    private static DateOnly ReportEnd(AnnualDuesSettings settings, DateOnly currentEnd, int? year)
    {
        if (!year.HasValue) return currentEnd;
        Check(year >= 1901 && year <= currentEnd.Year, "Choose a valid membership year.");
        return new DateOnly(year.Value, settings.ExpiryMonth, settings.ExpiryDay);
    }

    private static async Task<IResult> Report(Access access, Guid? regionId, Guid? clubId, string? status,
        string? search, int? cycleYear, string? format, int page = 1)
    {
        var workspace = await Workspace(access);
        var db = access.Db;
        var settings = await Settings(db, workspace.TenantId);
        var currentEnd = CycleEnd(settings, Today);
        var end = ReportEnd(settings, currentEnd, cycleYear);
        var historical = end != currentEnd;
        var cutoff = EndsAt(end);
        var members = Members(access, workspace);
        if (regionId.HasValue)
        {
            var region = access.Tree(workspace.TenantId, regionId.Value);
            Check(await access.Visible(workspace).AnyAsync(o => o.Id == regionId && db.Levels.Any(l => l.Id == o.LevelId && l.Name == "Region")),
                "Region outside your scope.", 404);
            members = members.Where(m => region.Any(o => o.Id == m.ClubId));
        }
        if (clubId.HasValue)
        {
            Check(await access.Visible(workspace).AnyAsync(o => o.Id == clubId && db.Levels.Any(l => l.Id == o.LevelId && l.IsClub)), "Club outside your scope.", 404);
            members = members.Where(m => m.ClubId == clubId);
        }
        var rows = from member in members
                   join payment in db.AnnualDuesPayments.Where(p => p.CycleYear == end.Year) on member.Id equals payment.MembershipId into payments
                   from payment in payments.DefaultIfEmpty()
                   let statusAtEnd = historical
                       ? db.MembershipHistory.Where(h => h.MembershipId == member.Id && h.EffectiveAt <= cutoff)
                           .OrderByDescending(h => h.EffectiveAt).ThenByDescending(h => h.RecordedAt).ThenByDescending(h => h.Id)
                           .Select(h => h.NewStatus).FirstOrDefault() ?? member.Status
                       : member.Status
                   where payment != null || (member.JoinedAt <= cutoff && CurrentStatuses.Contains(statusAtEnd))
                   select new
                   {
                       member.Id, member.Number, member.ClubId, club = member.Club.Name,
                       name = member.Person.DisplayName, membershipStatus = statusAtEnd, member.Version,
                       member.EndsAt, paid = payment != null, amount = payment == null ? 0 : payment.Amount,
                       paidOn = payment == null ? (DateOnly?)null : payment.PaidOn,
                   };
        var totals = await rows.GroupBy(_ => 1).Select(g => new
        {
            paid = g.Count(x => x.paid), unpaid = g.Count(x => !x.paid), collected = g.Sum(x => x.amount),
        }).SingleOrDefaultAsync();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = "%" + search.Trim() + "%";
            rows = rows.Where(x => EF.Functions.ILike(x.name, term) || EF.Functions.ILike(x.Number, term));
        }
        Check(status is null or "paid" or "unpaid", "Choose Paid or Unpaid.");
        if (status != null) rows = rows.Where(x => x.paid == (status == "paid"));
        var ordered = rows.OrderBy(x => x.name).ThenBy(x => x.Id);
        if (format == "csv")
        {
            var exported = await ordered.ToListAsync();
            return BulkImportEndpoints.Csv(["Year ending", "Membership ID", "Member", "Club", "Membership status", "Payment status", "Paid on", "Collected (INR)"],
                exported.Select(r => new[] { end.ToString("yyyy-MM-dd"), r.Number, r.name, r.club, r.membershipStatus,
                    r.paid ? "Paid" : "Unpaid", r.paidOn?.ToString("yyyy-MM-dd") ?? "", r.amount.ToString("0.00", CultureInfo.InvariantCulture) }),
                $"dues-{end.Year}-{status ?? "all"}.csv");
        }
        page = Math.Clamp(page, 1, 10000);
        return Results.Ok(new
        {
            cycleYear = end.Year, cycleStart = end.AddYears(-1).AddDays(1), cycleEnd = end, historical,
            totals, total = await rows.CountAsync(), page,
            items = await ordered.Skip((page - 1) * 50).Take(50).ToListAsync(),
        });
    }
}
