using Microsoft.EntityFrameworkCore;
using static Clubs.Access;
namespace Clubs;

public class DuesFollowup
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid InvoiceId { get; set; }
    public Guid ActorId { get; set; }
    public Guid RequestKey { get; set; }
    public string Outcome { get; set; } = "Contacted";
    public string Notes { get; set; } = "";
    public DateOnly? NextFollowUp { get; set; }
    public DateOnly? PromisedOn { get; set; }
    public decimal? PromisedAmount { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
public record DuesFollowupInput(string Outcome, string Notes, DateOnly? NextFollowUp, DateOnly? PromisedOn, decimal? PromisedAmount, Guid RequestKey);
public record DuesCancelInput(string Reason);

public static partial class DuesCenter
{
    static async Task OwnClub(Access a, Assignment w, FinanceInvoice invoice)
    {
        var club = await a.Db.Memberships.Where(m => m.Id == invoice.MembershipId).Select(m => m.ClubId).SingleAsync();
        Check((await a.User()).PlatformAdmin || club == w.OrgId, "Update collections and follow-ups only for your assigned club.", 403);
    }
    static void MapOperations(RouteGroupBuilder api)
    {
        api.MapGet("/invoices/{id:guid}", async (Guid id, Access a, CommunicationService comm) =>
        {
            var w = await Workspace(a, comm, View); var db = a.Db;
            var invoice = await Invoices(db, a, w).AsNoTracking().SingleOrDefaultAsync(i => i.Id == id);
            Check(invoice != null, "Invoice is outside your authorized members.", 404);
            var member = await db.Memberships.Where(m => m.Id == invoice.MembershipId).Select(m => new { m.Number, m.ClubId, club = m.Club.Name }).SingleAsync();
            var claims = await db.DuesCollections.Where(c => c.InvoiceId == id).OrderByDescending(c => c.CreatedAt).ThenByDescending(c => c.Id).ToListAsync();
            var payments = await (from allocation in db.FinanceAllocations join p in db.FinancePayments on allocation.PaymentId equals p.Id
                where allocation.InvoiceId == id select new { p.Id, p.ReceiptNumber, p.Date, p.Method, p.Reference, p.Status, p.SettlementStatus, allocation.Amount }).ToListAsync();
            var followups = await db.DuesFollowups.Where(f => f.InvoiceId == id && f.TenantId == w.TenantId).OrderByDescending(f => f.CreatedAt).ThenByDescending(f => f.Id).ToListAsync();
            var actorIds = claims.Select(c => c.RecorderId).Concat(claims.Where(c => c.ReviewerId != null).Select(c => c.ReviewerId!.Value)).Concat(followups.Select(f => f.ActorId)).Distinct().ToArray();
            var actors = await db.Users.Where(u => actorIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.Name);
            var pendingVerification = claims.Where(c => c.Status == "Submitted").Sum(c => c.Amount);
            var grants = await a.Grants(w); var admin = (await a.User()).PlatformAdmin;
            var editable = grants.Contains(Collect) && (admin || member.ClubId == w.OrgId);
            var canReview = grants.Contains("finance.reconcile") && (admin || await db.FinanceGrants.AnyAsync(g => g.EntityId == invoice.EntityId && g.UserId == w.UserId && (g.Role == "Owner" || g.Role == "Treasurer")));
            return Results.Ok(new { invoice = new { invoice.Id, invoice.Number, invoice.Customer, invoice.Currency, invoice.Date, invoice.DueOn, invoice.Total, invoice.Paid, invoice.Credited, invoice.Outstanding, invoice.PaymentStatus, invoice.Purpose, invoice.Notes }, member, pendingVerification, availableToCollect = Math.Max(0, invoice.Outstanding - pendingVerification),
                canCollect = editable && invoice.Outstanding > pendingVerification, canFollowUp = editable,
                collections = claims.Select(c => new { c.Id, c.Amount, c.Currency, c.Date, c.Method, c.Reference, c.AccountHolder, c.Destination, c.Evidence, c.Notes, c.Status, c.ReviewNote, c.CreatedAt, c.ReviewedAt,
                    recorder = actors.GetValueOrDefault(c.RecorderId, "Unknown"), reviewer = c.ReviewerId == null ? "" : actors.GetValueOrDefault(c.ReviewerId.Value, "Unknown"),
                    canCancel = c.Status == "Submitted" && (admin || c.RecorderId == w.UserId) && editable,
                    canReview = c.Status == "Submitted" && c.RecorderId != w.UserId && canReview }),
                followups = followups.Select(f => new { f.Id, f.Outcome, f.Notes, f.NextFollowUp, f.PromisedOn, f.PromisedAmount, f.CreatedAt, actor = actors.GetValueOrDefault(f.ActorId, "Unknown") }), payments });
        });
        api.MapPost("/invoices/{id:guid}/followups", async (Guid id, DuesFollowupInput input, Access a, CommunicationService comm, FinanceService finance) =>
        {
            var w = await Workspace(a, comm, Collect); var db = a.Db;
            Check(input.RequestKey != Guid.Empty && new[] { "Contacted", "NoAnswer", "PromiseToPay", "Disputed", "Resolved" }.Contains(input.Outcome)
                && !string.IsNullOrWhiteSpace(input.Notes) && input.Notes.Length <= 2000 && (input.NextFollowUp == null || input.NextFollowUp >= FinanceService.Today), "Enter a valid follow-up outcome, notes, and next follow-up date.");
            Check(input.Outcome != "PromiseToPay" || input.PromisedOn >= FinanceService.Today && input.PromisedAmount > 0,
                "Enter a promise date today or later and a positive promised amount.");
            Check(input.Outcome == "PromiseToPay" || input.PromisedOn == null && input.PromisedAmount == null, "Promise details apply only to Promise to pay.");
            await using var tx = await db.Database.BeginTransactionAsync();
            var invoice = await Invoices(db, a, w).SingleOrDefaultAsync(i => i.Id == id);
            Check(invoice != null, "Invoice is outside your assigned members.", 404);
            await finance.Lock(invoice.EntityId);
            Check((await Workspace(a, comm, Collect)).Id == w.Id, "Workspace changed. Try again.", 409);
            await OwnClub(a, w, invoice);
            var prior = await db.DuesFollowups.SingleOrDefaultAsync(f => f.TenantId == w.TenantId && f.ActorId == w.UserId && f.RequestKey == input.RequestKey);
            if (prior != null)
            {
                Check(prior.InvoiceId == id && prior.Outcome == input.Outcome && prior.Notes == input.Notes.Trim() && prior.NextFollowUp == input.NextFollowUp && prior.PromisedOn == input.PromisedOn && prior.PromisedAmount == input.PromisedAmount, "Follow-up request already used for different details.", 409);
                return Results.Ok(prior);
            }
            if (input.PromisedAmount != null)
            {
                var entity = await db.FinanceEntitys.SingleAsync(e => e.Id == invoice.EntityId); finance.Exact(entity, input.PromisedAmount.Value);
                Check(input.PromisedAmount <= invoice.Outstanding, "Promise exceeds current outstanding dues.", 409);
            }
            var row = new DuesFollowup { TenantId = w.TenantId, InvoiceId = id, ActorId = w.UserId, RequestKey = input.RequestKey, Outcome = input.Outcome,
                Notes = input.Notes.Trim(), NextFollowUp = input.NextFollowUp, PromisedOn = input.PromisedOn, PromisedAmount = input.PromisedAmount };
            db.DuesFollowups.Add(row);
            finance.Audit(await db.FinanceEntitys.SingleAsync(e => e.Id == invoice.EntityId), w.UserId, "dues.followup", row.Id + ":" + row.Outcome);
            await db.SaveChangesAsync(); await tx.CommitAsync(); return Results.Ok(row);
        });
        api.MapPost("/collections/{id:guid}/cancel", async (Guid id, DuesCancelInput input, Access a, CommunicationService comm, FinanceService finance) =>
        {
            var w = await Workspace(a, comm, Collect); var db = a.Db;
            Check(!string.IsNullOrWhiteSpace(input.Reason) && input.Reason.Length <= 2000, "Enter the cancellation reason.");
            await using var tx = await db.Database.BeginTransactionAsync();
            var c = await db.DuesCollections.SingleOrDefaultAsync(c => c.Id == id && c.TenantId == w.TenantId);
            Check(c != null, "Collection unavailable.", 404);
            var invoice = await Invoices(db, a, w).SingleOrDefaultAsync(i => i.Id == c.InvoiceId);
            Check(invoice != null, "Collection outside your scope.", 404);
            await finance.Lock(invoice.EntityId); await db.Entry(c).ReloadAsync();
            Check((await Workspace(a, comm, Collect)).Id == w.Id, "Workspace changed. Try again.", 409);
            await OwnClub(a, w, invoice);
            Check(c.RecorderId == w.UserId || (await a.User()).PlatformAdmin, "Only the recorder or platform administrator may cancel this entry.", 403);
            Check(c.Status == "Submitted", "Only unverified collections may be cancelled. Verified payments use the finance reversal workflow.", 409);
            c.Status = "Cancelled"; c.ReviewNote = input.Reason.Trim(); c.ReviewerId = w.UserId; c.ReviewedAt = DateTimeOffset.UtcNow;
            finance.Audit(await db.FinanceEntitys.SingleAsync(e => e.Id == invoice.EntityId), w.UserId, "dues.collection.cancel", c.Id.ToString());
            await db.SaveChangesAsync(); await tx.CommitAsync(); return Results.Ok(c);
        });
    }
}
