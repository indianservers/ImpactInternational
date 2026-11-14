using Microsoft.EntityFrameworkCore;
using static Clubs.Access;
namespace Clubs;

public static partial class DuesCenter
{
    static void MapMemberIntelligence(RouteGroupBuilder api)
    {
        api.MapGet("/member-intelligence", async (Access a, CommunicationService comm, Guid orgId, string currency="", string status="", string search="", int page=1, DateOnly? from=null, DateOnly? to=null) =>
        {
            var w=await Workspace(a,comm,View);var db=a.Db;
            Check(await a.Visible(w).AnyAsync(o=>o.Id==orgId),"Organization outside your scope.",404);
            Check(page>=1&&search.Length<=100&&(currency==""||currency.Length==3)&&new[]{"","Paid","Partial","Unpaid","No dues","Overdue","Verification","HasPaid","Pending"}.Contains(status),"Invalid member filters.");
            Check(from==null||to==null||from<=to,"Invalid dates.");
            var scope=a.Visible(w).Where(o=>a.Tree(w.TenantId,orgId).Any(t=>t.Id==o.Id));
            var mq=db.Memberships.AsNoTracking().Where(m=>m.TenantId==w.TenantId&&scope.Any(o=>o.Id==m.ClubId));
            Check(await mq.CountAsync()<=20000,"Choose a smaller scope; member intelligence supports 20,000 memberships.");
            var roster=await mq.Select(m=>new{m.Id,m.PersonId,m.Number,club=m.Club.Name,name=db.Persons.Where(p=>p.Id==m.PersonId).Select(p=>p.DisplayName).First()}).ToListAsync();
            var iq=Invoices(db,a,w).Where(i=>mq.Any(m=>m.Id==i.MembershipId));
            if(from!=null)iq=iq.Where(i=>i.Date>=from);
            if(to!=null)iq=iq.Where(i=>i.Date<=to);
            var currencies=await iq.Select(i=>i.Currency).Distinct().OrderBy(c=>c).ToListAsync();
            var code=currency!=""?currency:currencies.FirstOrDefault()??"INR";
            if(!currencies.Contains(code))currencies.Add(code);
            iq=iq.Where(i=>i.Currency==code);
            Check(await iq.CountAsync()<=20000,"Choose a smaller period; limit is 20,000 dues invoices.");
            var invoices=await iq.AsNoTracking().ToListAsync();var ids=invoices.Select(i=>i.Id).ToArray();
            var claims=await db.DuesCollections.Where(c=>c.TenantId==w.TenantId&&ids.Contains(c.InvoiceId)&&c.Status=="Submitted").GroupBy(c=>c.InvoiceId).Select(g=>new{id=g.Key,amount=g.Sum(c=>c.Amount)}).ToDictionaryAsync(x=>x.id,x=>x.amount);
            var grouped=invoices.ToLookup(i=>i.MembershipId!.Value);
            var rows=roster.Select(m=>{
                var list=grouped[m.Id].ToList();var billed=list.Sum(i=>i.Total);var credited=list.Sum(i=>i.Credited);var paid=list.Sum(i=>i.Paid);var pending=list.Sum(i=>i.Outstanding);
                return new {id=m.Id,m.PersonId,m.Number,m.name,m.club,currency=code,billed,credited,collected=paid,pending,
                    overdue=list.Where(i=>i.DueOn<FinanceService.Today).Sum(i=>i.Outstanding),
                    verification=list.Sum(i=>claims.GetValueOrDefault(i.Id)),invoices=list.Count,
                    dueOn=list.Where(i=>i.Outstanding>0).Select(i=>(DateOnly?)i.DueOn).Min(),
                    status=list.Count==0?"No dues":pending<=0?"Paid":paid>0?"Partial":"Unpaid"};
            }).ToList();
            var counts=new {total=rows.Count,paid=rows.Count(r=>r.status=="Paid"),partial=rows.Count(r=>r.status=="Partial"),unpaid=rows.Count(r=>r.status=="Unpaid"),noDues=rows.Count(r=>r.status=="No dues"),overdue=rows.Count(r=>r.overdue>0),verification=rows.Count(r=>r.verification>0)};
            var summary=new{billed=rows.Sum(r=>r.billed),credited=rows.Sum(r=>r.credited),collected=rows.Sum(r=>r.collected),pending=rows.Sum(r=>r.pending),overdue=rows.Sum(r=>r.overdue),verification=rows.Sum(r=>r.verification)};
            var payments=await (from x in db.FinanceAllocations join p in db.FinancePayments on x.PaymentId equals p.Id where ids.Contains(x.InvoiceId)&&p.Status=="Verified" select new{p.Date,x.Amount}).ToListAsync();
            var months=invoices.Select(i=>i.Date.ToString("yyyy-MM")).Concat(payments.Select(p=>p.Date.ToString("yyyy-MM"))).Distinct().OrderBy(x=>x);
            var trend=months.Select(month=>new{date=month+"-01",generated=invoices.Where(i=>i.Date.ToString("yyyy-MM")==month).Sum(i=>i.Total-i.Credited),collected=payments.Where(p=>p.Date.ToString("yyyy-MM")==month).Sum(p=>p.Amount)}).ToArray();
            var filtered=rows.Where(r=>status==""||status=="Overdue"&&r.overdue>0||status=="Verification"&&r.verification>0||status=="HasPaid"&&r.collected>0||status=="Pending"&&r.pending>0||r.status==status)
                .Where(r=>search==""||r.name.Contains(search,StringComparison.OrdinalIgnoreCase)||r.Number.Contains(search,StringComparison.OrdinalIgnoreCase)||r.club.Contains(search,StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(r=>r.pending).ThenBy(r=>r.name).ToList();
            return Results.Ok(new{currency=code,currencies,counts,summary,trend,total=filtered.Count,page,items=filtered.Skip((page-1)*50).Take(50),
                definition="All authorized club memberships, including members without dues invoices. Paid / cleared includes balances cleared by credits. Pending verification does not reduce dues. Status and totals use the selected currency and invoice date range. Charts and counts describe the complete selection before member filters. Trend shows billing months and verified payment allocations for this invoice cohort."});
        });
    }
}
