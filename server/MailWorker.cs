using System.Net;
using System.Net.Mail;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.DataProtection;

namespace Clubs;

// PostgreSQL transaction lock gives one delivery owner across API replicas. Delivery is at-least-once.
public class MailWorker(
    IServiceScopeFactory scopes,
    IConfiguration config,
    IHostEnvironment env,
    ILogger<MailWorker> log
) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var pollDelay = TimeSpan.FromMilliseconds(
                Math.Clamp(config.GetValue("Communications:PollMilliseconds", 1500), 100, 15000)
            );
            try
            {
                using var scope = scopes.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<Db>();
                await using var tx = await db.Database.BeginTransactionAsync(stoppingToken);
                await db
                    .Mails.Where(m =>
                        m.DeliveryStatus == "Dispatching"
                        && m.NextAttemptAt <= DateTimeOffset.UtcNow
                    )
                    .ExecuteUpdateAsync(
                        x =>
                            x.SetProperty(m => m.DeadLetter, true)
                                .SetProperty(m => m.DeliveryStatus, "Uncertain")
                                .SetProperty(
                                    m => m.LastError,
                                    "Interrupted dispatch: reconcile provider acceptance before retry"
                                ),
                        stoppingToken
                    );
                var hasSmtp = !string.IsNullOrEmpty(config["Smtp:Host"]);
                var otpEnabled = config.GetValue("Authentication:MemberOtpEnabled", true);
                var committed = false;
                var mail = await db
                    .Mails.FromSqlInterpolated(
                        $"""
                        SELECT * FROM "Mails" WHERE "SentAt" IS NULL AND NOT "Used" AND NOT "Suppressed" AND NOT "DeadLetter" AND "DeliveryStatus" NOT IN ('Dispatching','Draft') AND "Channel"<>'InApp' AND ("Channel"<>'Email' OR {hasSmtp}) AND ("Kind"<>'login-otp' OR {otpEnabled}) AND "ExpiresAt">now() AND "NextAttemptAt"<=now() ORDER BY CASE WHEN "Kind" IN ('verify','reset') THEN 0 ELSE 1 END, "NextAttemptAt" FOR UPDATE SKIP LOCKED LIMIT 1
                        """
                    )
                    .FirstOrDefaultAsync(stoppingToken);
                if (mail != null)
                {
                    pollDelay = TimeSpan.FromMilliseconds(100);
                    async Task Touch()
                    {
                        if (mail.CommunicationId == null)
                            return;
                        try
                        {
                            var cid = await db
                                .CommunicationMessages.Where(m => m.Id == mail.CommunicationId)
                                .Select(m => m.ConversationId)
                                .SingleAsync(stoppingToken);
                            if (cid != null)
                                await db
                                    .Conversations.Where(c => c.Id == cid)
                                    .ExecuteUpdateAsync(
                                        x => x.SetProperty(c => c.UpdatedAt, DateTimeOffset.UtcNow),
                                        stoppingToken
                                    );
                        }
                        catch (Exception e) when (e is not OperationCanceledException)
                        {
                            log.LogWarning(
                                "Conversation delivery signal deferred ({Type})",
                                e.GetType().Name
                            );
                        }
                    }
                    try
                    {
                        var communications =
                            scope.ServiceProvider.GetRequiredService<CommunicationService>();
                        if (mail.TenantId != null && mail.Kind is not "verify" and not "reset" and not "login-otp")
                        {
                            if (mail.UserId != null)
                            {
                                var current = await communications.Contact(
                                    mail.TenantId.Value,
                                    mail.UserId,
                                    mail.Channel,
                                    mail.Purpose
                                );
                                if (!current.Allowed)
                                {
                                    mail.Suppressed = true;
                                    mail.DeliveryStatus = "Suppressed";
                                    await db.SaveChangesAsync(stoppingToken);
                                    await Touch();
                                    await tx.CommitAsync(stoppingToken);
                                    continue;
                                }
                            }
                            else if (
                                mail.Recipient != ""
                                && await db.CommunicationSuppressions.AnyAsync(
                                    x =>
                                        x.TenantId == mail.TenantId
                                        && x.Channel == mail.Channel
                                        && x.AddressHash
                                            == Access.Hash(mail.Recipient.ToLowerInvariant()),
                                    stoppingToken
                                )
                            )
                            {
                                mail.Suppressed = true;
                                mail.DeliveryStatus = "Suppressed";
                                await db.SaveChangesAsync(stoppingToken);
                                await Touch();
                                await tx.CommitAsync(stoppingToken);
                                continue;
                            }
                        }
                        if (mail.AccountId != null)
                        {
                            var account = await db.CommunicationAccounts.SingleAsync(
                                a => a.Id == mail.AccountId,
                                stoppingToken
                            );
                            Access.Check(
                                account.Enabled
                                    && account.Verified
                                    && account.Channel == mail.Channel
                                    && account.OrgId == mail.OrgId,
                                "Account disabled or sender changed.",
                                409
                            );
                            await db.Database.ExecuteSqlInterpolatedAsync(
                                $"SELECT 1 FROM \"CommunicationAccounts\" WHERE \"Id\"={account.Id} FOR NO KEY UPDATE",
                                stoppingToken
                            );
                            await db.Entry(account).ReloadAsync(stoppingToken);
                            if (account.WindowAt < DateTimeOffset.UtcNow.AddMinutes(-1))
                            {
                                account.WindowAt = DateTimeOffset.UtcNow;
                                account.WindowCount = 0;
                            }
                            if (account.WindowCount >= account.PerMinute)
                            {
                                mail.NextAttemptAt = account.WindowAt.AddMinutes(1);
                                await db.SaveChangesAsync(stoppingToken);
                                await Touch();
                                await tx.CommitAsync(stoppingToken);
                                continue;
                            }
                            account.WindowCount++;
                        }
                        if (mail.Kind.StartsWith("communication."))
                        {
                            var contact = await communications.Contact(
                                mail.TenantId!.Value,
                                mail.UserId,
                                mail.Channel,
                                mail.Purpose
                            );
                            var authority = await communications.Sender(
                                mail.SenderAssignmentId,
                                mail.OrgId!.Value
                            );
                            if (!contact.Allowed || !authority)
                            {
                                mail.Suppressed = true;
                                mail.DeliveryStatus = "Suppressed";
                                await db.SaveChangesAsync(stoppingToken);
                                await Touch();
                                await tx.CommitAsync(stoppingToken);
                                continue;
                            }
                            mail.Recipient = contact.Address;
                            var pref = await db.CommunicationPreferences.SingleOrDefaultAsync(
                                p => p.UserId == mail.UserId && p.Channel == mail.Channel,
                                stoppingToken
                            );
                            if (pref?.QuietEnabled == true)
                            {
                                var zone = await db
                                    .Users.Where(u => u.Id == mail.UserId)
                                    .Select(u => u.TimeZone)
                                    .SingleAsync(stoppingToken);
                                var until = CommunicationService.QuietUntil(
                                    pref,
                                    zone,
                                    DateTimeOffset.UtcNow
                                );
                                if (until > DateTimeOffset.UtcNow)
                                {
                                    mail.NextAttemptAt = until;
                                    await db.SaveChangesAsync(stoppingToken);
                                    await Touch();
                                    await tx.CommitAsync(stoppingToken);
                                    continue;
                                }
                            }
                        }
                        if (mail.Channel != "Email")
                        {
                            var account = await db.CommunicationAccounts.SingleAsync(
                                a => a.Id == mail.AccountId,
                                stoppingToken
                            );
                            Access.Check(
                                account.Enabled && account.Verified,
                                "Sending account is not verified.",
                                409
                            );
                            var provider = scope
                                .ServiceProvider.GetServices<ICommunicationProvider>()
                                .Single(p => p.Name == account.Provider);
                            if (mail.Channel == "WhatsApp")
                            {
                                if (mail.TemplateName == "")
                                {
                                    var conversation = await db
                                        .CommunicationMessages.Where(m =>
                                            m.Id == mail.CommunicationId
                                        )
                                        .Select(m => m.ConversationId)
                                        .SingleOrDefaultAsync(stoppingToken);
                                    Access.Check(
                                        conversation != null
                                            && await db.Conversations.AnyAsync(
                                                c =>
                                                    c.Id == conversation
                                                    && c.LastInboundAt
                                                        > DateTimeOffset.UtcNow.AddHours(-24),
                                                stoppingToken
                                            ),
                                        "WhatsApp service window expired.",
                                        409
                                    );
                                }
                                else
                                    Access.Check(
                                        await db.CommunicationTemplates.AnyAsync(
                                            t =>
                                                t.TenantId == mail.TenantId
                                                && t.OrgId == mail.OrgId
                                                && t.ProviderName == mail.TemplateName
                                                && t.AccountId == mail.AccountId
                                                && t.Version == mail.TemplateVersion
                                                && t.ProviderStatus == "APPROVED"
                                                && t.Active,
                                            stoppingToken
                                        ),
                                        "WhatsApp template approval changed.",
                                        409
                                    );
                            }
                            mail.DeliveryStatus = "Dispatching";
                            mail.NextAttemptAt = DateTimeOffset.UtcNow.AddMinutes(2);
                            await db.SaveChangesAsync(stoppingToken);
                            await Touch();
                            await tx.CommitAsync(stoppingToken);
                            committed = true;
                            var result = await provider.Send(
                                account,
                                mail,
                                communications,
                                stoppingToken
                            );
                            mail.ProviderId = result.Id;
                            mail.DeliveryStatus = result.Status;
                            mail.Cost = result.Cost;
                            mail.Currency = result.Currency;
                            mail.SentAt = DateTimeOffset.UtcNow;
                            db.MailDeliveryAttempts.Add(
                                new() { MailId = mail.Id, Succeeded = true }
                            );
                            await db.SaveChangesAsync(stoppingToken);
                            await Touch();

                            continue;
                        }
                        if (string.IsNullOrEmpty(config["Smtp:Host"]))
                        {
                            mail.NextAttemptAt = DateTimeOffset.UtcNow.AddMinutes(1);
                            await db.SaveChangesAsync(stoppingToken);
                            await Touch();
                            await tx.CommitAsync(stoppingToken);
                            continue;
                        }
                        var recipient = !string.IsNullOrEmpty(mail.Recipient)
                            ? mail.Recipient
                            : await db
                                .Users.Where(x => x.Id == mail.UserId)
                                .Select(x => x.Email)
                                .SingleAsync(stoppingToken);
                        var origin =
                            config["PublicOrigin"]
                            ?? throw new InvalidOperationException("PublicOrigin required");
                        if (!env.IsDevelopment() && !origin.StartsWith("https://"))
                            throw new InvalidOperationException("PublicOrigin must use HTTPS");
                        using var smtp = new SmtpClient(
                            config["Smtp:Host"],
                            config.GetValue("Smtp:Port", 587)
                        )
                        {
                            EnableSsl = config.GetValue("Smtp:EnableSsl", true),
                            Credentials = new NetworkCredential(
                                config["Smtp:Username"],
                                config["Smtp:Password"]
                            ),
                        };
                        using var message = new MailMessage(
                            config["Smtp:From"]
                                ?? throw new InvalidOperationException("Smtp:From required"),
                            recipient,
                            !string.IsNullOrEmpty(mail.Subject)
                                ? mail.Subject
                                : "Impact International: "
                                    + (
                                        mail.Kind == "verify"
                                            ? "verify your email"
                                            : "reset your password"
                                    ),
                            !string.IsNullOrEmpty(mail.Body)
                                ? (mail.Kind == "login-otp" ? scope.ServiceProvider.GetRequiredService<IDataProtectionProvider>().CreateProtector("Impact.LoginOtpMail.v1").Unprotect(mail.Body) : mail.Body)
                                    + (
                                        string.IsNullOrEmpty(mail.Link)
                                            ? ""
                                            : "\n\n" + origin.TrimEnd('/') + mail.Link
                                    )
                                : "Open this one-time link within one hour: "
                                    + origin.TrimEnd('/')
                                    + mail.Link
                        );
                        message.Headers.Add("X-ClubSphere-Message-ID", mail.Id.ToString());
                        if (mail.CommunicationId != null)
                        {
                            var storage =
                                scope.ServiceProvider.GetRequiredService<CommunicationStorage>();
                            foreach (
                                var file in await db
                                    .CommunicationAttachments.Where(f =>
                                        f.MessageId == mail.CommunicationId
                                        && f.ExpiresAt > DateTimeOffset.UtcNow
                                    )
                                    .ToListAsync(stoppingToken)
                            )
                            {
                                var attachment = new Attachment(
                                    storage.PathFor(file.StorageKey),
                                    file.Type
                                )
                                {
                                    Name = file.Name,
                                };
                                message.Attachments.Add(attachment);
                            }
                        }
                        if (mail.Kind.StartsWith("communication."))
                        {
                            message.IsBodyHtml = true;
                            var encoded = System
                                .Net.WebUtility.HtmlEncode(message.Body)
                                .Replace("\n", "<br/>");
                            encoded = System.Text.RegularExpressions.Regex.Replace(
                                encoded,
                                @"\*\*(.+?)\*\*",
                                "<strong>$1</strong>"
                            );
                            if (mail.Purpose == "Promotional")
                            {
                                var token = communications.Protect(
                                    mail.Id
                                        + "|"
                                        + DateTimeOffset.UtcNow.AddDays(30).ToUnixTimeSeconds()
                                );
                                var unsubscribe =
                                    origin.TrimEnd('/')
                                    + "/api/v1/communications/unsubscribe?token="
                                    + Uri.EscapeDataString(token);
                                encoded +=
                                    "<br/><br/><a href=\""
                                    + System.Net.WebUtility.HtmlEncode(unsubscribe)
                                    + "\">Stop optional "
                                    + mail.Channel
                                    + " messages</a>";
                                message.Headers.Add("List-Unsubscribe", "<" + unsubscribe + ">");
                                message.Headers.Add(
                                    "List-Unsubscribe-Post",
                                    "List-Unsubscribe=One-Click"
                                );
                            }
                            message.Body =
                                "<html><body style=\"font-family:Arial,sans-serif;line-height:1.7\"><div>"
                                + encoded
                                + "</div></body></html>";
                            mail.DeliveryStatus = "Dispatching";
                            mail.NextAttemptAt = DateTimeOffset.UtcNow.AddMinutes(2);
                            await db.SaveChangesAsync(stoppingToken);
                            await Touch();
                            await tx.CommitAsync(stoppingToken);
                            committed = true;
                        }
                        await smtp.SendMailAsync(message, stoppingToken);
                        db.MailDeliveryAttempts.Add(new() { MailId = mail.Id, Succeeded = true });
                        mail.SentAt = DateTimeOffset.UtcNow;
                        mail.DeliveryStatus = "Accepted";
                        mail.ProviderId = mail.Id.ToString();
                        mail.Link = "";
                        if (mail.Kind == "login-otp") mail.Body = "";
                    }
                    catch (Exception e) when (e is not OperationCanceledException)
                    {
                        db.MailDeliveryAttempts.Add(
                            new() { MailId = mail.Id, ErrorCode = e.GetType().Name }
                        );
                        mail.Attempts++;
                        mail.LastError = e is ProviderFailure failure
                            ? "Provider HTTP " + failure.Status
                            : e.GetType().Name;
                        mail.DeadLetter =
                            mail.Attempts >= 6
                            || e is ProviderFailure f
                                && f.Status is >= 400 and < 500
                                && f.Status != 429;
                        if (
                            e is HttpRequestException or TaskCanceledException
                            || committed
                                && e is SmtpException { StatusCode: SmtpStatusCode.GeneralFailure }
                        )
                        {
                            mail.DeadLetter = true;
                            mail.DeliveryStatus = "Uncertain";
                        }
                        else
                            mail.DeliveryStatus = mail.DeadLetter ? "Failed" : "Retrying";
                        mail.NextAttemptAt = DateTimeOffset.UtcNow.AddSeconds(
                            Math.Min(900, 15 * Math.Pow(2, Math.Min(mail.Attempts, 6)))
                        );
                        log.LogWarning(
                            "Mail delivery attempt {Attempt} failed for outbox item {Id}; retry scheduled",
                            mail.Attempts,
                            mail.Id
                        );
                    }
                    await db.SaveChangesAsync(stoppingToken);
                    await Touch();
                }
                if (!committed)
                    await tx.CommitAsync(stoppingToken);
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                log.LogError("Outbox polling failed ({Type})", e.GetType().Name);
            }
            await Task.Delay(pollDelay, stoppingToken);
        }
    }
}
