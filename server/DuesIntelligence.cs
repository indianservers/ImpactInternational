using Microsoft.EntityFrameworkCore;
using static Clubs.Access;
namespace Clubs;

public static partial class DuesCenter
{
    static void MapIntelligence(RouteGroupBuilder api)
    {
        api.MapGet("/intelligence", Intelligence);
    }

    static async Task<IResult> Intelligence(Access a, CommunicationService comm, Guid? orgId = null,
        Guid? planId = null, DateOnly? from = null, DateOnly? to = null, string currency = "", string search = "")
    {
        var w = await Workspace(a, comm, View); var db = a.Db; var today = FinanceService.Today;
        var start = from ?? new DateOnly(today.Month < 4 ? today.Year - 1 : today.Year, 4, 1);
        var end = to ?? start.AddYears(1).AddDays(-1);
        Check(start.Year>=2 && start.Year<=9990 && start <= end && end <= start.AddYears(5) && currency.Length <= 3 && search.Length <= 100, "Invalid dues filters.");
        var scope = Invoices(db, a, w).AsNoTracking();
        if (orgId != null)
        {
            Check(await a.Visible(w).AnyAsync(o => o.Id == orgId), "Organization is outside your scope.", 404);
            var tree = a.Tree(w.TenantId, orgId.Value).Select(o => o.Id);
            scope = scope.Where(i => db.Memberships.Any(m => m.Id == i.MembershipId && tree.Contains(m.ClubId)));
        }
        if (planId != null) scope = scope.Where(i => i.PlanId == planId);
        if (currency != "") scope = scope.Where(i => i.Currency == currency);
        if (search != "") scope = scope.Where(i => EF.Functions.ILike(i.Customer, "%" + search + "%") || EF.Functions.ILike(i.Number, "%" + search + "%") || db.Memberships.Any(m => m.Id == i.MembershipId && EF.Functions.ILike(m.Number, "%" + search + "%")));
        var q = scope.Where(i => i.Date >= start && i.Date <= end);
        var previousEnd = start.AddDays(-1); var previousStart = end==start.AddYears(1).AddDays(-1)?start.AddYears(-1):start.AddDays(-(end.DayNumber - start.DayNumber + 1));
        var prior = scope.Where(i => i.Date >= previousStart && i.Date <= previousEnd);
        var summary = await q.GroupBy(i => i.Currency).Select(g => new {
            currency = g.Key, billed = g.Sum(i => i.Total), collected = g.Sum(i => i.Paid), credits = g.Sum(i => i.Credited),
            discounts = db.FinanceInvoiceLines.Where(l=>g.Select(i=>i.Id).Contains(l.InvoiceId)).Sum(l=>(decimal?)l.Discount)??0,
            pending = g.Sum(i => i.Total - i.Paid - i.Credited), overdue = g.Where(i => i.DueOn < today).Sum(i => i.Total - i.Paid - i.Credited),
            upcoming = g.Where(i => i.DueOn >= today && i.DueOn <= today.AddDays(30)).Sum(i => i.Total - i.Paid - i.Credited),
            members = g.Select(i => i.PersonId).Distinct().Count(), invoices = g.Count(),
            pendingVerification = db.DuesCollections.Where(c => c.Status == "Submitted" && g.Select(i => i.Id).Contains(c.InvoiceId)).Sum(c => (decimal?)c.Amount) ?? 0
        }).ToListAsync();
        var previous = await prior.GroupBy(i => i.Currency).Select(g => new { currency = g.Key, billed = g.Sum(i => i.Total), collected = g.Sum(i => i.Paid), credits = g.Sum(i => i.Credited), discounts=db.FinanceInvoiceLines.Where(l=>g.Select(i=>i.Id).Contains(l.InvoiceId)).Sum(l=>(decimal?)l.Discount)??0, pending = g.Sum(i => i.Total-i.Paid-i.Credited), overdue = g.Where(i => i.DueOn < today).Sum(i => i.Total-i.Paid-i.Credited), members = g.Select(i => i.PersonId).Distinct().Count() }).ToListAsync();
        var generated = await q.GroupBy(i => new { i.Date.Year, i.Date.Month, i.Currency }).Select(g => new { g.Key.Year, g.Key.Month, currency=g.Key.Currency, generated=g.Sum(i=>i.Total) }).ToListAsync();
        var allocations = from x in db.FinanceAllocations join p in db.FinancePayments on x.PaymentId equals p.Id
            where p.Status == "Verified" && q.Any(i => i.Id == x.InvoiceId)
            select new { x.InvoiceId, x.Amount, p.Date, p.Currency, p.Method, p.ReceiptNumber, p.SettlementStatus };
        var collected = await allocations.Where(p => p.Date >= start && p.Date <= end).GroupBy(p => new { p.Date.Year, p.Date.Month, p.Currency }).Select(g => new { g.Key.Year, g.Key.Month, currency=g.Key.Currency, collected=g.Sum(p=>p.Amount) }).ToListAsync();
        var aging = await q.Where(i=>i.Total>i.Paid+i.Credited).GroupBy(i=>new { i.Currency, bucket=i.DueOn>=today ? "Not Due" : i.DueOn>=today.AddDays(-30) ? "1–30 Days" : i.DueOn>=today.AddDays(-60) ? "31–60 Days" : i.DueOn>=today.AddDays(-90) ? "61–90 Days" : "90+ Days" }).Select(g=>new {currency=g.Key.Currency, name=g.Key.bucket, amount=g.Sum(i=>i.Total-i.Paid-i.Credited),count=g.Count()}).ToListAsync();
        var clubs = await q.Join(db.Memberships,i=>i.MembershipId,m=>(Guid?)m.Id,(i,m)=>new { invoice=i,m.ClubId,m.PersonId }).GroupBy(x=>new{x.ClubId,x.invoice.Currency}).Select(g=>new {orgId=g.Key.ClubId,currency=g.Key.Currency,billed=g.Sum(x=>x.invoice.Total),collected=g.Sum(x=>x.invoice.Paid),credits=g.Sum(x=>x.invoice.Credited),pending=g.Sum(x=>x.invoice.Total-x.invoice.Paid-x.invoice.Credited),overdue=g.Where(x=>x.invoice.DueOn<today).Sum(x=>x.invoice.Total-x.invoice.Paid-x.invoice.Credited),members=g.Select(x=>x.PersonId).Distinct().Count(),invoices=g.Count()}).ToListAsync();
        var nodes = await (from o in db.Orgs join l in db.Levels on o.LevelId equals l.Id where o.TenantId==w.TenantId select new {o.Id,o.ParentId,o.Name,level=l.Name}).ToDictionaryAsync(o=>o.Id);
        var rollups = new List<object>();
        foreach(var level in new[]{"International","National","Region","District","Zone","Club"})
        {
            Guid? Ancestor(Guid id) { for(int depth=0;depth<16 && nodes.TryGetValue(id,out var n);depth++){if(n.level==level)return id;if(n.ParentId==null)break;id=n.ParentId.Value;}return null; }
            foreach(var g in clubs.GroupBy(c=>new{id=Ancestor(c.orgId),c.currency}).Where(g=>g.Key.id!=null)) {
                var billed=g.Sum(c=>c.billed);var credits=g.Sum(c=>c.credits);var paid=g.Sum(c=>c.collected);
                rollups.Add(new {orgId=g.Key.id,name=nodes[g.Key.id!.Value].Name,level,currency=g.Key.currency,billed,collected=paid,credits,pending=g.Sum(c=>c.pending),overdue=g.Sum(c=>c.overdue),members=g.Sum(c=>c.members),clubs=g.Count(),invoices=g.Sum(c=>c.invoices),collectionRate=billed>credits?Math.Round(100m*paid/(billed-credits),1):(decimal?)null});
            }
        }
        var creditsByMonth = await db.FinanceCredits.Join(q,c=>c.InvoiceId,i=>i.Id,(c,i)=>new{c.Date,c.Amount,i.Currency}).GroupBy(c=>new {c.Date.Year,c.Date.Month,c.Currency}).Select(g=>new{g.Key.Year,g.Key.Month,currency=g.Key.Currency,amount=g.Sum(c=>c.Amount),count=g.Count()}).ToListAsync();
        var distributionQuery=q.Where(i=>i.Total>i.Paid+i.Credited).GroupBy(i=>new{i.PersonId,i.Currency}).Select(g=>new{name=g.Select(i=>i.Customer).First(),currency=g.Key.Currency,amount=g.Sum(i=>i.Total-i.Paid-i.Credited),invoices=g.Count()});
        var memberDistribution=new List<object>();foreach(var code in summary.Select(s=>s.currency))foreach(var row in await distributionQuery.Where(r=>r.currency==code).OrderByDescending(r=>r.amount).ThenBy(r=>r.name).Take(20).ToListAsync())memberDistribution.Add(row);
        var authorizedMembers=db.Memberships.Where(m=>m.TenantId==w.TenantId && a.Visible(w).Any(o=>o.Id==m.ClubId));
        if(orgId!=null)authorizedMembers=authorizedMembers.Where(m=>a.Tree(w.TenantId,orgId.Value).Any(o=>o.Id==m.ClubId));
        var renewals=await authorizedMembers.Where(m=>m.EndsAt!=null).GroupBy(m=>m.EndsAt!.Value < DateTimeOffset.UtcNow ? "Expired" : m.EndsAt<=DateTimeOffset.UtcNow.AddDays(7) ? "Next 7 days" : m.EndsAt<=DateTimeOffset.UtcNow.AddDays(30) ? "8–30 days" : m.EndsAt<=DateTimeOffset.UtcNow.AddDays(60) ? "31–60 days" : m.EndsAt<=DateTimeOffset.UtcNow.AddDays(90) ? "61–90 days" : "Later").Select(g=>new{name=g.Key,count=g.Count()}).ToListAsync();
        var reminders=await q.Where(i=>i.RemindedAt!=null).GroupBy(i=>i.Currency).Select(g=>new{currency=g.Key,invoices=g.Count()}).ToListAsync();
        var afterReminder=await (from x in db.FinanceAllocations join p in db.FinancePayments on x.PaymentId equals p.Id join i in q on x.InvoiceId equals i.Id where i.RemindedAt!=null && p.Status=="Verified" && p.Date>DateOnly.FromDateTime(i.RemindedAt.Value.UtcDateTime) select new{x.InvoiceId,x.Amount,p.Currency}).GroupBy(p=>p.Currency).Select(g=>new{currency=g.Key,invoices=g.Select(x=>x.InvoiceId).Distinct().Count(),amount=g.Sum(x=>x.Amount)}).ToListAsync();
        return Results.Ok(new {from=start,to=end,previousStart,previousEnd,summary,previous,generated,collected,aging,organizations=rollups,memberDistribution,renewals,creditsByMonth,reminded=reminders,afterReminder,
            definitions="Current balances of issued membership-linked invoices in the invoice-date cohort. Generated amounts are issued net invoice values; original line discounts are reported separately from post-issue credits. Collection efficiency = verified paid / (billed minus credits); no currency conversion. Monthly collections use verified payment dates and allocations to the selected invoice cohort. Hierarchy member counts are membership-based across clubs. Reminder outcomes count payments dated strictly after the latest reminder date; association does not prove causation."});
    }
}
