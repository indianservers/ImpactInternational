using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using static Clubs.Access;
namespace Clubs;

// Read projections only: every row retains the original operational record ID.
public sealed class FinanceCommandRow
{
    public Guid Id { get; set; }
    public Guid RecordId { get; set; }
    public Guid? RelatedId { get; set; }
    public DateOnly Date { get; set; }
    public DateOnly? DueOn { get; set; }
    public DateTimeOffset? At { get; set; }
    public string Reference { get; set; } = "";
    public string Party { get; set; } = "";
    public string Type { get; set; } = "";
    public string Source { get; set; } = "";
    public string Method { get; set; } = "";
    public string Status { get; set; } = "";
    public string Category { get; set; } = "";
    public decimal Amount { get; set; }
    public decimal Paid { get; set; }
    public decimal Outstanding { get; set; }
    public decimal Fee { get; set; }
    public decimal Allocated { get; set; }
    public Guid? ActorId { get; set; }
    public string Detail { get; set; } = "";
}
public sealed class FinanceBudgetLineage { public Guid RootId {get;set;} public Guid AncestorId {get;set;} }
public sealed class FinanceScheduleRead { public Guid Id {get;set;} public Guid ExpenseId {get;set;} public DateOnly Date {get;set;} public string Detail {get;set;}=""; public Guid ActorId {get;set;} }
public record FinanceScheduleInput(DateOnly? Date,string Reason);
public static partial class FinanceEndpoints
{
    static readonly string[] CommandPages = ["overview", "transactions", "receipts", "invoices", "payments", "expenses", "reconciliation", "budgets", "audit", "ledger", "gateway", "scheduled"];
    static IQueryable<FinanceCommandRow> CommandRows(FinanceService s, Guid entity, string page)
    {
        var db = s.Db;
        var lineage = BudgetLineage(s,entity);
        if(page=="scheduled") {
            // Scheduling is append-only metadata in the existing audit log; it never creates a payment or journal.
            var schedules=db.Database.SqlQuery<FinanceScheduleRead>($"""
                SELECT a."Id", e."Id" AS "ExpenseId", substring(a."Detail" from 38 for 10)::date AS "Date", a."Detail", a."ActorId"
                FROM "FinanceAudits" a JOIN "FinanceExpenses" e ON left(a."Detail",36)=e."Id"::text AND e."EntityId"=a."EntityId"
                WHERE a."EntityId"={entity} AND a."Action"='expense.schedule' AND e."Status"='Approved' AND e."Paid"<e."Amount"
                AND NOT EXISTS (SELECT 1 FROM "FinanceAudits" n WHERE n."EntityId"=a."EntityId" AND n."Action" IN ('expense.schedule','expense.schedule.cancel')
                    AND left(n."Detail",36)=e."Id"::text AND (n."At",n."Id")>(a."At",a."Id"))
                """);
            return from x in schedules join e in db.FinanceExpenses on x.ExpenseId equals e.Id where e.EntityId==entity select new FinanceCommandRow {
                Id=x.Id,RecordId=e.Id,Date=x.Date,Reference="Scheduled payment",Party=e.Payee,Type="Expense",Source="Approved expense schedule",Method="BankTransfer",Status="Scheduled",Category=e.Category,
                Amount=e.Amount-e.Paid,Paid=e.Paid,Outstanding=e.Amount-e.Paid,ActorId=x.ActorId,Detail=x.Detail,Fee=0,Allocated=0 };
        }
        var collections = db.FinancePayments.Where(x => x.EntityId == entity).Select(x => new FinanceCommandRow {
            Id=x.Id, RecordId=x.Id, Date=x.Date, Reference=x.ReceiptNumber == "" ? x.Reference : x.ReceiptNumber, Party=x.Payer,
            Type="Collection", Source=x.Method == "Stripe" ? "Gateway" : "Manual", Method=x.Method, Status=x.Status,
            Category=x.SettlementStatus, Amount=x.Amount, Paid=x.Refunded, Outstanding=x.Amount-x.Allocated-x.Refunded,
            Fee=x.Fee, Allocated=x.Allocated, ActorId=x.RecorderId, Detail=x.Reference });
        var expenses = db.FinanceExpenses.Where(x=>x.EntityId==entity).Select(x=>new FinanceCommandRow {
            Id=x.Id, RecordId=x.Id, Date=x.Date, Reference="EXP-"+x.Id.ToString(), Party=x.Payee, Type="Expense", Source="Expense claim",
            Method="", Status=x.Status, Category=x.Category, Amount=x.Amount, Paid=x.Paid, Outstanding=x.Amount-x.Paid,
            Fee=0, Allocated=0, ActorId=x.CreatorId, Detail=x.Description });
        var outgoing = from r in db.FinanceReimbursements join e in db.FinanceExpenses on r.ExpenseId equals e.Id
            where r.EntityId==entity && e.EntityId==entity select new FinanceCommandRow {
            Id=r.Id, RecordId=e.Id, Date=r.Date, Reference=r.Reference, Party=e.Payee, Type="Payment", Source="Approved expense",
            Method="BankTransfer", Status="Settled", Category=e.Category, Amount=r.Amount, Paid=r.Amount, Outstanding=0,
            Fee=0, Allocated=r.Amount, ActorId=r.CreatorId, Detail=e.Description };
        var refunds=from r in db.FinanceRefunds join p in db.FinancePayments on r.PaymentId equals p.Id
            where r.EntityId==entity && p.EntityId==entity select new FinanceCommandRow {
            Id=r.Id, RecordId=p.Id, Date=r.Date, Reference=r.Reference, Party=p.Payer, Type="Refund", Source=r.Kind,
            Method=p.Method, Status="Completed", Category=r.Kind, Amount=r.Amount, Paid=r.Amount, Outstanding=0,
            Fee=0, Allocated=0, ActorId=r.CreatorId, Detail=r.Reason };
        var credits=from c in db.FinanceCredits join i in db.FinanceInvoices on c.InvoiceId equals i.Id
            where c.EntityId==entity && i.EntityId==entity select new FinanceCommandRow {
            Id=c.Id, RecordId=i.Id, Date=c.Date, Reference=i.Number, Party=i.Customer, Type="Adjustment", Source="Credit note",
            Method="Credit", Status="Posted", Category=i.Purpose, Amount=c.Amount, Paid=0, Outstanding=0,
            Fee=0, Allocated=0, ActorId=c.CreatorId, Detail=c.Reason };
        if(page=="gateway") return from x in db.FinanceIntents join i in db.FinanceInvoices on x.InvoiceId equals i.Id where x.EntityId==entity&&i.EntityId==entity select new FinanceCommandRow {
            Id=x.Id,RecordId=x.Id,Date=i.Date,Reference=x.ProviderId,Party=i.Customer,Type="Gateway attempt",Source=x.Provider,Method=x.Provider,Status=x.State,Category="Checkout attempts",Amount=x.Amount,Paid=0,Outstanding=0,Fee=0,Allocated=0,ActorId=i.CreatorId,Detail=i.Number };
        if(page=="ledger") return from l in db.FinanceJournalLines join j in db.FinanceJournals on l.JournalId equals j.Id join ac in db.FinanceAccounts on l.AccountId equals ac.Id where l.EntityId==entity&&j.EntityId==entity&&ac.EntityId==entity&&(ac.Kind=="Income"||ac.Kind=="Expense") select new FinanceCommandRow {
            Id=l.Id,RecordId=j.Id,Date=j.Date,Reference=ac.Code,Party=ac.Name,Type="Ledger line",Source=j.Source,Method="Journal",Status="Posted",Category=ac.Kind,Amount=ac.Kind=="Income"?l.Credit-l.Debit:l.Debit-l.Credit,Paid=l.Debit,Outstanding=l.Credit,Fee=0,Allocated=0,ActorId=j.CreatorId,Detail=j.Narration };
        if(page=="receipts" || page=="overview") return collections;
        if(page=="transactions") return collections.Concat(outgoing).Concat(expenses).Concat(refunds).Concat(credits);
        if(page=="expenses") return expenses;
        if(page=="payments") return outgoing;
        if(page=="invoices") return db.FinanceInvoices.Where(x=>x.EntityId==entity).Select(x=>new FinanceCommandRow {
            Id=x.Id, RecordId=x.Id, Date=x.Date, Reference=x.Number=="" ? "Draft" : x.Number, Party=x.Customer, Type="Invoice", Source=x.Purpose,
            Method="", Status=x.State=="Issued" ? x.Total<=x.Paid+x.Credited ? "Paid" : x.DueOn<FinanceService.Today ? "Overdue" : x.Paid>0 ? "Partially Paid" : "Issued" : x.State,
            Category=x.Purpose, Amount=x.Total, Paid=x.Paid, Outstanding=x.Total-x.Paid-x.Credited, Fee=0, Allocated=x.Credited,
            ActorId=x.CreatorId, Detail=x.Notes, DueOn=x.DueOn });
        if(page=="reconciliation") return from x in db.FinanceBankTransactions join b in db.FinanceBanks on x.BankAccountId equals b.Id
            where x.EntityId==entity && b.EntityId==entity select new FinanceCommandRow {
            Id=x.Id, RecordId=x.Id, Date=x.Date, Reference=x.Reference, Party=b.Name+" "+b.MaskedNumber, Type="Statement", Source=b.Name,
            Method="Bank", Status=x.Status, Category=b.Name, Amount=x.Amount, Paid=0, Outstanding=x.Status=="Matched"?0:x.Amount,
            Fee=0, Allocated=0, ActorId=null, Detail=x.PaymentId==null?"No payment matched":"Verified payment matched", RelatedId=x.PaymentId };
        if(page=="budgets") return db.FinanceBudgets.Where(x=>x.EntityId==entity && !db.FinanceBudgets.Any(child=>child.EntityId==entity&&child.ReplacesId==x.Id&&child.Status=="Approved")).Select(x=>new FinanceCommandRow {
            Id=x.Id, RecordId=x.Id, Date=x.StartsOn, Reference=x.Name, Party=x.Name, Type="Budget", Source=x.Kind,
            Method="", Status=x.Status, Category=x.Kind, Amount=x.Amount,
            Paid=db.FinanceExpenses.Where(c=>c.EntityId==entity && lineage.Any(l=>l.RootId==x.Id&&l.AncestorId==c.BudgetId) && c.Status=="Approved").Sum(c=>(decimal?)c.Amount)??0,
            Outstanding=db.FinanceExpenses.Where(c=>c.EntityId==entity && lineage.Any(l=>l.RootId==x.Id&&l.AncestorId==c.BudgetId) && c.Status=="Submitted").Sum(c=>(decimal?)c.Amount)??0,
            Fee=0, Allocated=0, ActorId=x.CreatorId, Detail="Revision "+x.Revision.ToString(), DueOn=x.EndsOn });
        return from x in db.FinanceAudits join u in db.Users on x.ActorId equals u.Id into users from u in users.DefaultIfEmpty()
            where x.EntityId==entity select new FinanceCommandRow {
            Id=x.Id, RecordId=x.Id, Date=DateOnly.FromDateTime(x.At.UtcDateTime), Reference=x.Action, Party=u.Name??"System",
            Type="Audit", Source=x.Action, Method="", Status="Recorded", Category="Immutable history", Amount=0, Paid=0,
            Outstanding=0, Fee=0, Allocated=0, ActorId=x.ActorId, Detail=x.Detail, At=x.At };
    }
    static void MapCommand(RouteGroupBuilder api)
    {
        api.MapGet("/entities/{entity:guid}/command/{workspace}", CommandReport);
        api.MapGet("/entities/{entity:guid}/command-metrics", CommandMetrics);
        api.MapPost("/expenses/{id:guid}/schedule",async (Guid id,FinanceScheduleInput input,FinanceService s)=> {
            var expense=await s.Db.FinanceExpenses.SingleOrDefaultAsync(x=>x.Id==id);
            Check(expense!=null,"Expense unavailable.",404);
            var e=await s.Entity(expense.EntityId,"finance.reconcile");
            Check(input.Reason.Length is >0 and <=1000,"Record a scheduling reason.");
            Check(input.Date==null || input.Date>=FinanceService.Today && input.Date<=FinanceService.Today.AddYears(2),"Choose a schedule date within the next two years.");
            await using var tx=await s.Db.Database.BeginTransactionAsync();await s.Lock(e.Id);
            Check(expense.Status=="Approved"&&expense.Amount>expense.Paid,"Only an approved unpaid expense may be scheduled.",409);
            s.Audit(e,(await s.Workspace()).UserId,input.Date==null?"expense.schedule.cancel":"expense.schedule",id+"|"+(input.Date?.ToString("yyyy-MM-dd")??"cancelled")+"|"+input.Reason);
            await s.Db.SaveChangesAsync();await tx.CommitAsync();
            return Results.Ok(new {scheduledOn=input.Date,notice="Schedule recorded. It does not execute or post a payment. Record execution separately after bank evidence is available."});
        });
        api.MapPost("/entities/{entity:guid}/reconciliation/parse", async (Guid entity, StatementUpload file, FinanceService s) => {
            await s.Entity(entity,"finance.reconcile");
            return Results.Ok(new { rows=FinanceStatement.Parse(file) });
        });
        api.MapGet("/entities/{entity:guid}/collection-history/{id:guid}", async (Guid entity, Guid id, FinanceService s) => {
            await s.Entity(entity);
            var p=await s.Db.FinancePayments.SingleOrDefaultAsync(x=>x.EntityId==entity&&x.Id==id);
            Check(p!=null,"Collection unavailable.",404);
            var allocations=await (from a in s.Db.FinanceAllocations join i in s.Db.FinanceInvoices on a.InvoiceId equals i.Id where a.EntityId==entity&&a.PaymentId==id&&i.EntityId==entity select new {a.Id,a.Amount,invoiceId=i.Id,i.Number,i.Customer}).ToListAsync();
            var refunds=await s.Db.FinanceRefunds.Where(x=>x.EntityId==entity&&x.PaymentId==id).OrderBy(x=>x.Date).ToListAsync();
            return Results.Ok(new {payment=p,allocations,refunds});
        });
        api.MapGet("/entities/{entity:guid}/reconciliation/{id:guid}/suggestions", async (Guid entity, Guid id, FinanceService s) => {
            await s.Entity(entity);
            var r=await s.Db.FinanceBankTransactions.SingleOrDefaultAsync(x=>x.EntityId==entity&&x.Id==id);
            Check(r!=null,"Statement row unavailable.",404);
            var candidates=await s.Db.FinancePayments.Where(p=>r.Status=="Unmatched"&&p.EntityId==entity&&p.BankAccountId==r.BankAccountId&&p.Status=="Verified"&&p.SettlementStatus=="Settled"&&p.Date<=r.Date&&(from l in s.Db.FinanceJournalLines join j in s.Db.FinanceJournals on l.JournalId equals j.Id join bank in s.Db.FinanceBanks on p.BankAccountId equals bank.Id join account in s.Db.FinanceAccounts on l.AccountId equals account.Id where j.Source=="settlement:"+p.Id&&j.EntityId==entity&&account.Code==bank.AccountCode select l.Debit-l.Credit).Sum()==r.Amount&&!s.Db.FinanceBankTransactions.Any(t=>t.EntityId==entity&&t.PaymentId==p.Id&&t.Status=="Matched"))
                .OrderByDescending(p=>p.Date).Take(20).Select(p=>new {p.Id,p.Reference,p.Payer,p.Amount,p.Fee,p.Date,netAmount=r.Amount}).ToListAsync();
            var duplicates=await s.Db.FinanceBankTransactions.CountAsync(t=>t.EntityId==entity&&t.Id!=id&&t.BankAccountId==r.BankAccountId&&t.Date==r.Date&&t.Amount==r.Amount&&t.Reference==r.Reference);
            return Results.Ok(new {candidates,duplicates,rule="Exact bank and net amount, settled verified collection, not already matched. Confirm against statement evidence; this is a suggestion only."});
        });
    }
    static IQueryable<FinanceBudgetLineage> BudgetLineage(FinanceService s,Guid entity) => s.Db.Database.SqlQuery<FinanceBudgetLineage>($"""
        WITH RECURSIVE lineage AS (
            SELECT b."Id" AS "RootId", b."Id" AS "AncestorId", b."ReplacesId", ARRAY[b."Id"] AS path
            FROM "FinanceBudgets" b WHERE b."EntityId"={entity}
            UNION ALL
            SELECT l."RootId", b."Id", b."ReplacesId", l.path || b."Id"
            FROM lineage l JOIN "FinanceBudgets" b ON b."Id"=l."ReplacesId"
            WHERE b."EntityId"={entity} AND cardinality(l.path)<100 AND NOT b."Id"=ANY(l.path)
        ) SELECT "RootId","AncestorId" FROM lineage
        """);
    static async Task<IResult> CommandReport(Guid entity, string workspace, FinanceService s,
        DateOnly? from=null, DateOnly? to=null, string search="", string status="", string type="", string quick="",
        string sort="date", string direction="desc", int page=1, int pageSize=25, string format="json", string columns="")
    {
        var e=await s.Entity(entity);
        string[] columnKeys=["date","reference","party","type","source","method","amount","paid","outstanding","fee","allocated","status","category","detail"];
        var selectedColumns=columns.Split(',',StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries);
        Check(columns.Length<=250 && selectedColumns.Distinct().Count()==selectedColumns.Length && selectedColumns.All(columnKeys.Contains),"Invalid export columns.");
        Check(CommandPages.Contains(workspace), "Unknown finance workspace.",404);
        Check(search.Length<=100 && !search.Contains('\0') && status.Length<=50 && type.Length<=50 && new[]{"","high","unallocated","mine","outstanding","overdue"}.Contains(quick),"Invalid filters.");
        Check(new[]{"date","amount","party","reference","outstanding"}.Contains(sort) && new[]{"asc","desc"}.Contains(direction),"Invalid sort.");
        Check(new[]{"json","csv","pdf","xlsx"}.Contains(format),"Invalid export format.");
        var start=from??new DateOnly(FinanceService.Today.Year,1,1); var end=to??FinanceService.Today;
        Check(start<=end && end<=start.AddYears(20),"Invalid report dates.");
        Check(page>=1 && page<=100000 && pageSize>=1 && pageSize<=100,"Invalid report page.");
        var q=CommandRows(s,entity,workspace);
        q=workspace=="budgets"?q.Where(r=>r.Date<=end&&r.DueOn>=start):q.Where(r=>r.Date>=start&&r.Date<=end);
        if(search!="") {var term="%"+search.Trim()+"%";var hasAmount=decimal.TryParse(search,NumberStyles.Number,CultureInfo.InvariantCulture,out var exactAmount);q=q.Where(r=>EF.Functions.ILike(r.Reference,term)||EF.Functions.ILike(r.Party,term)||EF.Functions.ILike(r.Detail,term)||EF.Functions.ILike(r.Method,term)||EF.Functions.ILike(r.Source,term)||EF.Functions.ILike(r.Category,term)||hasAmount&&(r.Amount==exactAmount||r.Outstanding==exactAmount));}
        if(status!="") q=q.Where(r=>r.Status==status || r.Category==status || r.Source==status);
        if(type!="") q=q.Where(r=>r.Type==type);
        if(quick=="high") q=q.Where(r=>r.Amount>=50000);
        if(quick=="unallocated") q=q.Where(r=>r.Outstanding>0 && r.Type=="Collection");
        if(quick=="outstanding") q=q.Where(r=>r.Type=="Invoice" && r.Outstanding>0);
        if(quick=="overdue") q=q.Where(r=>r.Type=="Invoice" && r.Outstanding>0 && r.DueOn<FinanceService.Today);
        if(quick=="mine") {var actor=(await s.Workspace()).UserId;q=q.Where(r=>r.ActorId==actor);}
        var count=await q.CountAsync();
        var total=await q.SumAsync(r=>(decimal?)r.Amount)??0;
        var paid=await q.SumAsync(r=>(decimal?)r.Paid)??0;
        var outstanding=await q.SumAsync(r=>(decimal?)r.Outstanding)??0;
        var byStatus=await q.GroupBy(r=>r.Status).Select(g=>new {label=g.Key,count=g.Count(),amount=g.Sum(r=>r.Amount)}).OrderBy(g=>g.label).ToListAsync();
        var byParty=await q.GroupBy(r=>r.Party).Select(g=>new {label=g.Key,amount=g.Sum(r=>r.Amount)}).OrderByDescending(g=>g.amount).Take(5).ToListAsync();
        var byCategory=await q.GroupBy(r=>r.Category).Select(g=>new {label=g.Key,amount=g.Sum(r=>r.Amount)}).OrderByDescending(g=>g.amount).Take(8).ToListAsync();
        q=(sort,direction) switch {
            ("amount","asc")=>q.OrderBy(r=>r.Amount), ("amount",_)=>q.OrderByDescending(r=>r.Amount),
            ("outstanding","asc")=>q.OrderBy(r=>r.Outstanding), ("outstanding",_)=>q.OrderByDescending(r=>r.Outstanding),
            ("party","asc")=>q.OrderBy(r=>r.Party), ("party",_)=>q.OrderByDescending(r=>r.Party),
            ("reference","asc")=>q.OrderBy(r=>r.Reference), ("reference",_)=>q.OrderByDescending(r=>r.Reference),
            ("date","asc")=>q.OrderBy(r=>r.Date), _=>q.OrderByDescending(r=>r.Date) };
        q=((IOrderedQueryable<FinanceCommandRow>)q).ThenBy(r=>r.Id);
        if(format!="json") {
            Check(count<=20000,"Narrow the export to at most 20,000 rows.");
            var export=await q.ToListAsync();
            string[] headers=["Date","Reference","Party","Type","Source","Method","Amount","Paid or refunded","Outstanding or unallocated","Fees","Allocated or credits","Status","Category","Details"];
            string V(decimal value)=>value.ToString(CultureInfo.InvariantCulture);
            var values=export.Select(r=>new[]{r.Date.ToString("yyyy-MM-dd"),r.Reference,r.Party,r.Type,r.Source,r.Method,V(r.Amount),V(r.Paid),V(r.Outstanding),V(r.Fee),V(r.Allocated),r.Status,r.Category,r.Detail}).ToList();
            if(selectedColumns.Length>0){var indices=selectedColumns.Select(key=>Array.IndexOf(columnKeys,key)).ToArray();headers=indices.Select(index=>headers[index]).ToArray();values=values.Select(row=>indices.Select(index=>row[index]).ToArray()).ToList();}
            if(format=="pdf") return Results.File(FinancePdf.Build(workspace+" / "+e.Currency,new[]{e.Name,start+" to "+end,string.Join(" | ",headers)}.Concat(values.Select(r=>string.Join(" | ",r))).ToArray()),"application/pdf",workspace+".pdf");
            if(format=="xlsx") return Results.File(FinanceWorkbook.Build(headers,values),"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",workspace+".xlsx");
            string Cell(string v)=>"\""+((v.Length>0 && "=+-@\t\r".Contains(v[0]))?"'":"")+v.Replace("\"","\"\"")+"\"";
            return Results.File(Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(string.Join("\r\n",new[]{headers}.Concat(values).Select(r=>string.Join(",",r.Select(Cell)))))).ToArray(),"text/csv",workspace+".csv");
        }
        return Results.Ok(new {e.Currency,entity=e.Name,count,total,paid,outstanding,byStatus,byCategory,byParty,page,pageSize,items=await q.Skip((page-1)*pageSize).Take(pageSize).ToListAsync()});
    }
    static async Task<IResult> CommandMetrics(Guid entity, FinanceService s, DateOnly? from=null, DateOnly? to=null)
    {
        var e=await s.Entity(entity);var start=from??new DateOnly(FinanceService.Today.Year,1,1);var end=to??FinanceService.Today;
        Check(start<=end && end<=start.AddYears(20),"Invalid report dates.");
        var invoices=s.Db.FinanceInvoices.Where(x=>x.EntityId==entity && x.State=="Issued" && x.Date>=start && x.Date<=end);
        var payments=s.Db.FinancePayments.Where(x=>x.EntityId==entity && x.Status=="Verified" && x.Date>=start && x.Date<=end);
        var generated=await invoices.SumAsync(x=>(decimal?)x.Total)??0;var credits=await invoices.SumAsync(x=>(decimal?)x.Credited)??0;var allocated=await invoices.SumAsync(x=>(decimal?)x.Paid)??0;
        var collections=await payments.SumAsync(x=>(decimal?)(x.Amount-x.Refunded))??0;
        var outstanding=await invoices.SumAsync(x=>(decimal?)(x.Total-x.Paid-x.Credited))??0;
        var overdue=await invoices.Where(x=>x.DueOn<FinanceService.Today).SumAsync(x=>(decimal?)(x.Total-x.Paid-x.Credited))??0;
        var pending=await payments.Where(x=>x.SettlementStatus!="Settled").SumAsync(x=>(decimal?)(x.Amount-x.Refunded))??0;
        var month=new DateOnly(end.Year,end.Month,1);
        var monthInvoiced=await invoices.Where(x=>x.Date>=month).SumAsync(x=>(decimal?)x.Total)??0;
        var trend=await payments.GroupBy(x=>new{x.Date.Year,x.Date.Month}).Select(g=>new{year=g.Key.Year,month=g.Key.Month,collected=g.Sum(x=>x.Amount-x.Refunded)}).OrderBy(x=>x.year).ThenBy(x=>x.month).ToListAsync();
        var billing=await invoices.GroupBy(x=>new{x.Date.Year,x.Date.Month}).Select(g=>new{year=g.Key.Year,month=g.Key.Month,invoiced=g.Sum(x=>x.Total)}).OrderBy(x=>x.year).ThenBy(x=>x.month).ToListAsync();
        var sources=await payments.GroupBy(x=>x.Method).Select(g=>new{label=g.Key,amount=g.Sum(x=>x.Amount-x.Refunded)}).ToListAsync();
        var ageing=await invoices.Where(x=>x.Total>x.Paid+x.Credited).GroupBy(x=>x.DueOn>=FinanceService.Today?"Not due":x.DueOn>=FinanceService.Today.AddDays(-30)?"1–30 days":x.DueOn>=FinanceService.Today.AddDays(-60)?"31–60 days":x.DueOn>=FinanceService.Today.AddDays(-90)?"61–90 days":"90+ days").Select(g=>new{label=g.Key,amount=g.Sum(x=>x.Total-x.Paid-x.Credited)}).ToListAsync();
        var expense=s.Db.FinanceExpenses.Where(x=>x.EntityId==entity && x.Date>=start && x.Date<=end);
        var expenseCategories=await expense.Where(x=>x.Status=="Approved").GroupBy(x=>x.Category).Select(g=>new{label=g.Key,amount=g.Sum(x=>x.Amount)}).OrderByDescending(x=>x.amount).ToListAsync();
        var outgoing=await s.Db.FinanceReimbursements.Where(x=>x.EntityId==entity && x.Date>=start && x.Date<=end).SumAsync(x=>(decimal?)x.Amount)??0;
        var payable=await expense.Where(x=>x.Status=="Approved").SumAsync(x=>(decimal?)(x.Amount-x.Paid))??0;
        var awaiting=await expense.Where(x=>x.Status=="Submitted").SumAsync(x=>(decimal?)x.Amount)??0;
        var ledger=from l in s.Db.FinanceJournalLines join j in s.Db.FinanceJournals on l.JournalId equals j.Id join ac in s.Db.FinanceAccounts on l.AccountId equals ac.Id where l.EntityId==entity && j.Date>=start && j.Date<=end && (ac.Kind=="Income"||ac.Kind=="Expense") select new{j.Date,ac.Kind,amount=ac.Kind=="Income"?l.Credit-l.Debit:l.Debit-l.Credit};
        var ledgerTrend=await ledger.GroupBy(x=>new{x.Date.Year,x.Date.Month,x.Kind}).Select(g=>new{year=g.Key.Year,month=g.Key.Month,kind=g.Key.Kind,amount=g.Sum(x=>x.amount)}).OrderBy(x=>x.year).ThenBy(x=>x.month).ToListAsync();
        var scheduled=await CommandRows(s,entity,"scheduled").Where(x=>x.Date>=start&&x.Date<=end).SumAsync(x=>(decimal?)x.Amount)??0;
        var budgetRows=CommandRows(s,entity,"budgets").Where(x=>x.Status=="Approved"&&x.Date<=end&&x.DueOn>=start);
        var budgetLimit=await budgetRows.SumAsync(x=>(decimal?)x.Amount)??0;
        var budgetActual=await budgetRows.SumAsync(x=>(decimal?)x.Paid)??0;
        var budgetCommitted=await budgetRows.SumAsync(x=>(decimal?)x.Outstanding)??0;
        var topPayers=await payments.GroupBy(p=>p.Payer).Select(g=>new{label=g.Key,amount=g.Sum(p=>p.Amount-p.Refunded)}).OrderByDescending(x=>x.amount).Take(5).ToListAsync();
        var repeated=await payments.Where(p=>payments.Any(q=>q.Id!=p.Id&&q.Payer==p.Payer&&q.Amount==p.Amount&&q.Date>=p.Date.AddDays(-2)&&q.Date<=p.Date.AddDays(2))).OrderByDescending(p=>p.Date).Take(10).Select(p=>new{p.Reference,p.Payer,p.Amount,p.Date}).ToListAsync();
        var averageExpense=await expense.Where(x=>x.Status=="Approved").AverageAsync(x=>(decimal?)x.Amount)??0;
        var unusual=await expense.Where(x=>x.Status=="Approved"&&averageExpense>0&&x.Amount>averageExpense*3).OrderByDescending(x=>x.Amount).Take(5).Select(x=>new{x.Payee,x.Amount,x.Category}).ToListAsync();
        var unallocated=await payments.CountAsync(x=>x.Amount>x.Allocated+x.Refunded);
        var failedAttempts=await s.Db.FinanceIntents.CountAsync(x=>x.EntityId==entity&&x.State=="Failed");
        return Results.Ok(new {e.Currency,scheduled,budgetLimit,budgetActual,budgetCommitted,topPayers,repeated,unusual,unallocated,failedAttempts,generated,credits,allocated,collections,outstanding,overdue,pending,monthInvoiced,collectionRate=generated-credits<=0?0:decimal.Round(allocated/(generated-credits)*100,1),trend,billing,sources,ageing,expenseCategories,outgoing,payable,awaiting,ledgerTrend,
            notice="Current invoice balances for the selected issue dates. Collections use payment dates and exclude refunded amounts. Expense approval is accrual; payment execution is separate. Independent entity books only."});
    }
}
