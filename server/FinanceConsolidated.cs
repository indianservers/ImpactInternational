using System.Text;
using Microsoft.EntityFrameworkCore;
using static Clubs.Access;
namespace Clubs;

public sealed class ConsolidatedFinanceRow
{
    public Guid Id {get;set;}
    public Guid EntityId {get;set;}
    public string Ledger {get;set;}="";
    public string Organization {get;set;}="";
    public string Currency {get;set;}="";
    public DateOnly Date {get;set;}
    public string Reference {get;set;}="";
    public string Party {get;set;}="";
    public string Status {get;set;}="";
    public decimal Amount {get;set;}
    public decimal Outstanding {get;set;}
}
public static partial class FinanceEndpoints
{
    static void MapConsolidated(RouteGroupBuilder api)
    {
        api.MapGet("/consolidated", Consolidated);
    }
    static async Task<IResult> Consolidated(FinanceService s, IHostEnvironment env, Guid? orgId=null, string kind="invoices", string search="", string status="", string currency="", string balance="", DateOnly? from=null, DateOnly? to=null, int page=1, string format="json")
    {
        var w=await s.Workspace();var grants=await s.A.Grants(w);var admin=(await s.A.User()).PlatformAdmin;
        var db=s.Db;var visible=s.A.Visible(w).AsNoTracking();
        Check(orgId==null||await visible.AnyAsync(o=>o.Id==orgId),"Organization outside your scope.",404);
        var branch=orgId==null?visible:visible.Where(o=>s.A.Tree(w.TenantId,orgId.Value).Any(t=>t.Id==o.Id));
        var ids=branch.Select(o=>o.Id);
        var readable=grants.Any(p=>p.StartsWith("finance.")&&p!="finance.self"&&!p.StartsWith("finance.dues."));
        var entities=await db.FinanceEntitys.AsNoTracking().Where(e=>e.TenantId==w.TenantId&&e.Active&&ids.Contains(e.OrgId)&&(admin||readable&&db.FinanceGrants.Any(g=>g.TenantId==w.TenantId&&g.EntityId==e.Id&&g.UserId==w.UserId&&new[]{"Reader","Billing","Reviewer","Treasurer","Owner"}.Contains(g.Role)))).OrderBy(e=>e.Name).ToListAsync();
        Check(entities.Count<=100,"Narrow the hierarchy to at most 100 source ledgers.");
        var start=from??new DateOnly(FinanceService.Today.Year,1,1);var end=to??FinanceService.Today;
        Check(start<=end&&end<=start.AddYears(20)&&page>=1&&page<=10000&&search.Length<=150&&status.Length<=40,"Invalid finance filters.");
        Check(new[]{"invoices","receipts","expenses","payments"}.Contains(kind)&&new[]{"json","csv"}.Contains(format),"Unknown finance view.");
        Check(currency==""||currency.Length==3,"Invalid currency.");
        Check(new[]{"","outstanding","overdue"}.Contains(balance),"Invalid balance filter.");
        var entityIds=entities.Where(e=>currency==""||e.Currency==currency).Select(e=>e.Id).ToArray();
        var inv=db.FinanceInvoices.AsNoTracking().Where(i=>i.TenantId==w.TenantId&&entityIds.Contains(i.EntityId)&&i.State=="Issued"&&i.Date>=start&&i.Date<=end);
        var pay=db.FinancePayments.AsNoTracking().Where(p=>p.TenantId==w.TenantId&&entityIds.Contains(p.EntityId)&&p.Status=="Verified"&&p.Date>=start&&p.Date<=end);
        var exp=db.FinanceExpenses.AsNoTracking().Where(e=>e.TenantId==w.TenantId&&entityIds.Contains(e.EntityId)&&e.Status=="Approved"&&e.Date>=start&&e.Date<=end);
        var invoiceTotals=await inv.GroupBy(i=>i.Currency).Select(g=>new{currency=g.Key,billed=g.Sum(i=>i.Total-i.Credited),allocated=g.Sum(i=>i.Paid),outstanding=g.Sum(i=>i.Total-i.Credited-i.Paid),overdue=g.Sum(i=>i.DueOn<FinanceService.Today?i.Total-i.Credited-i.Paid:0)}).ToListAsync();
        var collectionTotals=await pay.GroupBy(p=>p.Currency).Select(g=>new{currency=g.Key,collected=g.Sum(p=>p.Amount-p.Refunded),unsettled=g.Sum(p=>p.SettlementStatus!="Settled"?p.Amount-p.Refunded:0)}).ToListAsync();
        var expenses=await (from e in exp join b in db.FinanceEntitys on e.EntityId equals b.Id group e by b.Currency into g select new{currency=g.Key,approved=g.Sum(e=>e.Amount),payable=g.Sum(e=>e.Amount-e.Paid)}).ToListAsync();
        var outgoing=await (from r in db.FinanceReimbursements join e in db.FinanceEntitys on r.EntityId equals e.Id where r.TenantId==w.TenantId&&entityIds.Contains(r.EntityId)&&r.Date>=start&&r.Date<=end group r by e.Currency into g select new{currency=g.Key,paid=g.Sum(r=>r.Amount)}).ToListAsync();
        IQueryable<ConsolidatedFinanceRow>? rows=null;
        foreach(var entity in entities.Where(e=>entityIds.Contains(e.Id)))
        {
            var name=entity.Name;var code=entity.Currency;var id=entity.Id;
            var org=await db.Orgs.Where(o=>o.Id==entity.OrgId).Select(o=>o.Name).SingleAsync();
            var part=CommandRows(s,id,kind).Where(r=>r.Date>=start&&r.Date<=end).Select(r=>new ConsolidatedFinanceRow{Id=r.Id,EntityId=id,Ledger=name,Organization=org,Currency=code,Date=r.Date,Reference=r.Reference,Party=r.Party,Status=r.Status,Amount=r.Amount,Outstanding=r.Outstanding});
            rows=rows==null?part:rows.Concat(part);
        }
        // Use a translated empty projection so a reader with no ledger grants sees no records.
        rows??=db.FinanceInvoices.Where(i=>false).Select(i=>new ConsolidatedFinanceRow{Id=i.Id,EntityId=i.EntityId,Date=i.Date,Reference=i.Number,Party=i.Customer,Status=i.State,Amount=i.Total,Outstanding=i.Total-i.Paid-i.Credited});
        if(search.Trim()!=""){var term="%"+search.Trim()+"%";rows=rows.Where(r=>EF.Functions.ILike(r.Party,term)||EF.Functions.ILike(r.Reference,term)||EF.Functions.ILike(r.Ledger,term));}
        if(status!="")rows=rows.Where(r=>r.Status==status);
        if(balance=="outstanding")rows=rows.Where(r=>r.Outstanding>0);
        if(balance=="overdue")rows=rows.Where(r=>r.Status=="Overdue"&&r.Outstanding>0);
        rows=rows.OrderByDescending(r=>r.Date).ThenBy(r=>r.Id);
        var count=await rows.CountAsync();
        if(format=="csv")
        {
            Check(count<=20000,"Narrow this export to 20,000 records.");var data=await rows.ToListAsync();
            string Cell(string v)=>"\""+MembershipImport.Safe(v).Replace("\"","\"\"")+"\"";
            var csv=new StringBuilder("Date,Reference,Party,Organization,Source ledger,Currency,Amount,Outstanding,Status\r\n");
            foreach(var r in data)csv.AppendLine(string.Join(",",new[]{r.Date.ToString("yyyy-MM-dd"),r.Reference,r.Party,r.Organization,r.Ledger,r.Currency,r.Amount.ToString(System.Globalization.CultureInfo.InvariantCulture),r.Outstanding.ToString(System.Globalization.CultureInfo.InvariantCulture),r.Status}.Select(Cell)));
            await s.A.Log(w,"finance.consolidated.export",orgId?.ToString()??w.OrgId.ToString(),kind+" · "+count+" rows");
            return Results.File(Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv.ToString())).ToArray(),"text/csv","impact-finance-"+kind+".csv");
        }
        return Results.Ok(new{mockData=env.IsDevelopment()&&db.Database.GetDbConnection().Database.StartsWith("clubs_impact_dues_upgrade_"),organizations=await visible.OrderBy(o=>o.Name).Select(o=>new{o.Id,o.ParentId,o.Name,type=db.Levels.Where(l=>l.Id==o.LevelId).Select(l=>l.ParentLevelId==null?"International":l.Name).First()}).ToListAsync(),currencies=entities.Select(e=>e.Currency).Distinct().OrderBy(x=>x),ledgerCount=entityIds.Length,invoiceTotals,collectionTotals,expenses,outgoing,kind,page,pageSize=20,count,items=await rows.Skip((page-1)*20).Take(20).ToListAsync(),definition="Consolidated read view of authorized source ledgers. Hierarchy filters use each ledger's owning unit. Membership dues analysis follows the member's club. Invoice balances use issue dates; collections and outgoing payments use payment dates. Currencies remain separate. No books or records are merged."});
    }
}
