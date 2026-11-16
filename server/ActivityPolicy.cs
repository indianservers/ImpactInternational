using Microsoft.EntityFrameworkCore;
using static Clubs.Access;
namespace Clubs;
public static class ActivityPolicy
{
    public static readonly string[] Types = ["Service", "Leadership", "Business", "Training", "Nari Cell"];
    public static readonly string[] Levels = ["Club", "Region", "National"];
    public static (DateTimeOffset Start, DateTimeOffset End) FinancialYear(Org root)
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById(root.TimeZone);
        var now = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, zone);
        var month = Math.Clamp(root.FiscalStartMonth, 1, 12);
        var start = new DateTime(now.Month < month ? now.Year - 1 : now.Year, month, 1);
        var end = start.AddYears(1);
        return (new DateTimeOffset(start, zone.GetUtcOffset(start)).ToUniversalTime(), new DateTimeOffset(end, zone.GetUtcOffset(end)).ToUniversalTime());
    }
    public static async Task CheckCurrentYear(Db db, ClubEvent activity)
    {
        var root = await db.Orgs.SingleAsync(o => o.TenantId == activity.TenantId && o.ParentId == null);
        var year = FinancialYear(root);
        Check(activity.StartsAt >= year.Start && activity.EndsAt < year.End, "Activities can only be posted within the current financial year.", 409);
    }
}
