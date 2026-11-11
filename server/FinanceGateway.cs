using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using static Clubs.Access;

namespace Clubs;

public interface IFinancePaymentProvider
{
    Task<(string Id, string Url)> Checkout(
        FinanceEntity entity,
        FinanceInvoice invoice,
        FinanceIntent intent
    );
    Task<JsonElement> Retrieve(FinanceEntity entity, string session);
    void VerifySignature(FinanceEntity entity, string body, string signature);
}

public class FinanceGateway(IConfiguration config, IHttpClientFactory clients, IHostEnvironment env)
    : IFinancePaymentProvider
{
    string Setting(FinanceEntity e, string key) =>
        config[$"Finance:Gateways:{e.GatewayKey}:{key}"] ?? "";

    public bool Available(FinanceEntity e) =>
        (
            new[] { "USD", "EUR", "GBP", "INR", "CAD", "AUD", "SGD", "AED" }.Contains(e.Currency)
                && e.Decimals == 2
            || e.Currency == "JPY" && e.Decimals == 0
        )
        && e.GatewayKey != ""
        && Setting(e, "EntityId") == e.Id.ToString()
        && Setting(e, "Provider") == "Stripe"
        && Setting(e, "SecretKey") != ""
        && Setting(e, "WebhookSecret") != ""
        && Setting(e, "ReturnUrl").StartsWith("https://");

    string Base(FinanceEntity e)
    {
        var url = Setting(e, "ApiBase");
        if (url == "")
            return "https://api.stripe.com/v1/";
        Check(
            env.IsDevelopment() && Uri.TryCreate(url, UriKind.Absolute, out var u) && u.IsLoopback,
            "Custom provider API bases are restricted to Development loopback.",
            403
        );
        return url.TrimEnd('/') + "/";
    }

    async Task<JsonElement> Send(
        FinanceEntity e,
        HttpMethod method,
        string path,
        Dictionary<string, string>? data = null,
        string? key = null
    )
    {
        Check(Available(e), "No authorized payment provider is configured.", 409);
        var secret = Setting(e, "SecretKey");
        Check(
            env.IsProduction()
                || secret.StartsWith("sk_test_")
                || env.IsDevelopment() && new Uri(Base(e)).IsLoopback,
            "Nonproduction environments accept test credentials only.",
            403
        );
        using var r = new HttpRequestMessage(method, Base(e) + path);
        r.Headers.Authorization = new("Bearer", secret);
        r.Headers.Add("Stripe-Version", "2026-10-28.endive");
        if (key != null)
            r.Headers.Add("Idempotency-Key", key);
        if (data != null)
            r.Content = new FormUrlEncodedContent(data);
        using var response = await clients.CreateClient("finance").SendAsync(r);
        Check(
            response.IsSuccessStatusCode,
            "Payment provider request failed; retry using the same intent.",
            502
        );
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return doc.RootElement.Clone();
    }

    public async Task<(string Id, string Url)> Checkout(
        FinanceEntity e,
        FinanceInvoice i,
        FinanceIntent intent
    )
    {
        var minor = decimal.ToInt64(intent.Amount * Pow(e.Decimals));
        var result = await Send(
            e,
            HttpMethod.Post,
            "checkout/sessions",
            new()
            {
                ["mode"] = "payment",
                ["success_url"] = Setting(e, "ReturnUrl"),
                ["cancel_url"] = Setting(e, "ReturnUrl"),
                ["client_reference_id"] = intent.Id.ToString(),
                ["metadata[finance_intent]"] = intent.Id.ToString(),
                ["line_items[0][price_data][currency]"] = e.Currency.ToLowerInvariant(),
                ["line_items[0][price_data][unit_amount]"] = minor.ToString(
                    CultureInfo.InvariantCulture
                ),
                ["line_items[0][price_data][product_data][name]"] = "Invoice " + i.Number,
                ["line_items[0][quantity]"] = "1",
            },
            intent.Id.ToString()
        );
        var url = result.GetProperty("url").GetString() ?? "";
        Check(
            Uri.TryCreate(url, UriKind.Absolute, out var u)
                && u.Scheme == "https"
                && u.Host == "checkout.stripe.com",
            "Provider returned an invalid checkout URL.",
            502
        );
        return (result.GetProperty("id").GetString()!, url);
    }

    public Task<JsonElement> Retrieve(FinanceEntity e, string session)
    {
        Check(
            System.Text.RegularExpressions.Regex.IsMatch(session, "^cs_[A-Za-z0-9_]{1,200}$"),
            "Invalid checkout reference."
        );
        return Send(e, HttpMethod.Get, "checkout/sessions/" + session);
    }

    public void VerifySignature(FinanceEntity e, string body, string signature)
    {
        Check(Available(e), "Gateway unavailable.", 404);
        var parts = signature
            .Split(',')
            .Select(x => x.Split('=', 2))
            .Where(x => x.Length == 2)
            .ToArray();
        Check(
            long.TryParse(parts.FirstOrDefault(x => x[0] == "t")?.ElementAt(1), out var seconds)
                && Math.Abs(DateTimeOffset.UtcNow.ToUnixTimeSeconds() - seconds) <= 300,
            "Invalid or stale payment webhook signature.",
            400
        );
        var expected = HMACSHA256.HashData(
            Encoding.UTF8.GetBytes(Setting(e, "WebhookSecret")),
            Encoding.UTF8.GetBytes(seconds + "." + body)
        );
        var matches = false;
        foreach (var part in parts.Where(x => x[0] == "v1"))
        {
            try
            {
                matches |= CryptographicOperations.FixedTimeEquals(
                    expected,
                    Convert.FromHexString(part[1])
                );
            }
            catch (FormatException) { }
        }
        Check(matches, "Invalid payment webhook signature.", 400);
    }

    public static decimal Pow(int decimals) =>
        decimals switch
        {
            0 => 1,
            1 => 10,
            2 => 100,
            3 => 1000,
            4 => 10000,
            _ => throw new ApiError(400, "Unsupported currency precision."),
        };
}

public record FinanceIntentInput(string Key);

public static partial class FinanceEndpoints
{
    static void MapGateway(WebApplication app, RouteGroupBuilder api)
    {
        api.MapPost(
            "/invoices/{id:guid}/payment-intents",
            async (Guid id, FinanceIntentInput input, FinanceService s, FinanceGateway gateway) =>
            {
                var invoice = await s.Invoice(id);
                var e = await s.Db.FinanceEntitys.SingleAsync(x => x.Id == invoice.EntityId);
                Check(gateway.Available(e), "No payment provider is configured.", 409);
                Check(input.Key.Length is > 0 and <= 150, "Payment request key required.");
                FinanceIntent intent;
                await using (var tx = await s.Db.Database.BeginTransactionAsync())
                {
                    await s.Lock(e.Id);
                    Check(
                        invoice.State == "Issued"
                            && invoice.Total > invoice.Paid + invoice.Credited,
                        "Invoice has no payable balance.",
                        409
                    );
                    var old = await s.Db.FinanceIntents.SingleOrDefaultAsync(x =>
                        x.EntityId == e.Id && x.Key == input.Key
                    );
                    if (old != null)
                    {
                        Check(old.InvoiceId == id, "Payment key belongs to another invoice.", 409);
                        intent = old;
                    }
                    else
                    {
                        var active = await s.Db.FinanceIntents.SingleOrDefaultAsync(x =>
                            x.InvoiceId == id && new[] { "Created", "Checkout" }.Contains(x.State)
                        );
                        if (active != null)
                            intent = active;
                        else
                        {
                            intent = s.Bind(
                                new FinanceIntent
                                {
                                    InvoiceId = id,
                                    Key = input.Key,
                                    Amount = invoice.Total - invoice.Paid - invoice.Credited,
                                },
                                e
                            );
                            s.Db.FinanceIntents.Add(intent);
                            await s.Db.SaveChangesAsync();
                        }
                    }
                    await tx.CommitAsync();
                }
                if (intent.ProviderId == "")
                {
                    var checkout = await gateway.Checkout(e, invoice, intent);
                    await using var tx = await s.Db.Database.BeginTransactionAsync();
                    await s.Lock(e.Id);
                    intent.ProviderId = checkout.Id;
                    intent.CheckoutUrl = checkout.Url;
                    intent.State = "Checkout";
                    intent.Version++;
                    await s.Db.SaveChangesAsync();
                    await tx.CommitAsync();
                }
                return new
                {
                    intent.Id,
                    intent.CheckoutUrl,
                    intent.State,
                };
            }
        );
        app.MapPost(
                "/api/v1/finance/webhooks/{entity:guid}",
                async (
                    Guid entity,
                    HttpContext context,
                    FinanceService s,
                    FinanceGateway gateway,
                    EventService events
                ) =>
                {
                    var e = await s.Db.FinanceEntitys.SingleOrDefaultAsync(x =>
                        x.Id == entity && x.Active
                    );
                    Check(e != null, "Gateway unavailable.", 404);
                    using var reader = new StreamReader(context.Request.Body, Encoding.UTF8);
                    var body = await reader.ReadToEndAsync();
                    Check(body.Length <= 60000, "Webhook too large.");
                    gateway.VerifySignature(
                        e,
                        body,
                        context.Request.Headers["Stripe-Signature"].ToString()
                    );
                    using var document = JsonDocument.Parse(body);
                    var root = document.RootElement;
                    var eventId = root.GetProperty("id").GetString() ?? "";
                    var type = root.GetProperty("type").GetString() ?? "";
                    Check(eventId.Length is > 0 and <= 200, "Webhook event ID required.");
                    var seen = await s
                        .Db.FinanceWebhooks.AsNoTracking()
                        .SingleOrDefaultAsync(x =>
                            x.EntityId == entity && x.ProviderEventId == eventId
                        );
                    if (seen != null)
                    {
                        Check(seen.PayloadHash == Hash(body), "Webhook ID payload mismatch.", 409);
                        return Results.Ok(new { duplicate = true });
                    }
                    var obj = root.GetProperty("data").GetProperty("object");
                    var session = obj.TryGetProperty("id", out var sessionId)
                        ? sessionId.GetString() ?? ""
                        : "";
                    // Signed messages are still revalidated against the provider's current authoritative session.
                    JsonElement? verified = null;
                    if (
                        type
                        is "checkout.session.completed"
                            or "checkout.session.async_payment_succeeded"
                    )
                        verified = await gateway.Retrieve(e, session);
                    await using var tx = await s.Db.Database.BeginTransactionAsync();
                    await s.Lock(e.Id);
                    var duplicate = await s.Db.FinanceWebhooks.SingleOrDefaultAsync(x =>
                        x.EntityId == entity && x.ProviderEventId == eventId
                    );
                    if (duplicate != null)
                    {
                        Check(
                            duplicate.PayloadHash == Hash(body),
                            "Webhook ID payload mismatch.",
                            409
                        );
                        return Results.Ok(new { duplicate = true });
                    }
                    var intent = await s.Db.FinanceIntents.SingleOrDefaultAsync(x =>
                        x.EntityId == entity && x.ProviderId == session
                    );
                    if (verified != null && intent != null && intent.PaymentId == null)
                    {
                        var v = verified.Value;
                        var i = await s.Db.FinanceInvoices.SingleAsync(x =>
                            x.Id == intent.InvoiceId && x.EntityId == entity
                        );
                        Check(
                            v.GetProperty("id").GetString() == session
                                && v.GetProperty("payment_status").GetString() == "paid"
                                && v.GetProperty("currency").GetString()
                                    == e.Currency.ToLowerInvariant()
                                && v.GetProperty("amount_total").GetDecimal()
                                    == intent.Amount * FinanceGateway.Pow(e.Decimals)
                                && v.GetProperty("client_reference_id").GetString()
                                    == intent.Id.ToString(),
                            "Provider confirmation does not match the frozen payment intent.",
                            409
                        );
                        var reference = v.GetProperty("payment_intent").GetString() ?? "";
                        Check(
                            reference.Length is > 0 and <= 200,
                            "Provider transaction missing.",
                            409
                        );
                        var p = s.Bind(
                            new FinancePayment
                            {
                                PersonId = i.PersonId,
                                Payer = i.Customer,
                                Reference = "Stripe:" + reference,
                                Method = "Stripe",
                                Amount = intent.Amount,
                                Currency = e.Currency,
                                Date = FinanceService.Today,
                                IdempotencyKey = "intent:" + intent.Id,
                                RecorderId = Guid.Empty,
                                Evidence = "Verified provider checkout " + session,
                            },
                            e
                        );
                        s.Db.FinancePayments.Add(p);
                        await s.Verify(p, e, Guid.Empty, true);
                        var allocation = Math.Min(intent.Amount, i.Total - i.Paid - i.Credited);
                        if (allocation > 0 && i.State == "Issued")
                            await s.Allocate(
                                p,
                                i,
                                allocation,
                                e,
                                Guid.Empty,
                                "intent:" + intent.Id
                            );
                        intent.PaymentId = p.Id;
                        intent.State = "Paid";
                        intent.Version++;
                        await s.Db.SaveChangesAsync();
                        if (i.RegistrationId != null)
                        {
                            await events.Lock(i.EventId!.Value);
                            var registration = await s.Db.EventRegistrations.SingleAsync(x =>
                                x.Id == i.RegistrationId
                            );
                            var ev = await s.Db.Events.SingleAsync(x => x.Id == i.EventId);
                            if (
                                registration.Status == "Pending Payment"
                                && ev.Lifecycle is not "Cancelled" and not "Archived"
                                && ev.EndsAt > DateTimeOffset.UtcNow
                            )
                                await events.Confirm(ev, registration);
                        }
                    }
                    else if (
                        intent != null
                        && intent.PaymentId == null
                        && type
                            is "checkout.session.async_payment_failed"
                                or "checkout.session.expired"
                    )
                    {
                        intent.State = type.EndsWith("expired") ? "Expired" : "Failed";
                        intent.Version++;
                    }
                    s.Db.FinanceWebhooks.Add(
                        s.Bind(
                            new FinanceWebhook
                            {
                                ProviderEventId = eventId,
                                Type = type,
                                PayloadHash = Hash(body),
                            },
                            e
                        )
                    );
                    s.Audit(e, Guid.Empty, "gateway.webhook", eventId + ":" + type);
                    await s.Db.SaveChangesAsync();
                    await tx.CommitAsync();
                    return Results.Ok(new { received = true });
                }
            )
            .RequireRateLimiting("events");
    }
}
