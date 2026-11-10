using System.Net;
using System.Net.Mail;
using Microsoft.EntityFrameworkCore;

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
        if (string.IsNullOrEmpty(config["Smtp:Host"]))
        {
            if (!env.IsDevelopment())
                log.LogError(
                    "SMTP is not configured. Verification and recovery messages will remain queued."
                );
            return;
        }
        while (!stoppingToken.IsCancellationRequested)
        {
            var pollDelay = TimeSpan.FromSeconds(15);
            try
            {
                using var scope = scopes.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<Db>();
                await using var tx = await db.Database.BeginTransactionAsync(stoppingToken);
                var mail = await db
                    .Mails.FromSqlRaw(
                        "SELECT * FROM \"Mails\" WHERE \"SentAt\" IS NULL AND NOT \"Used\" AND NOT \"Suppressed\" AND \"ExpiresAt\">now() AND \"NextAttemptAt\"<=now() ORDER BY \"NextAttemptAt\" FOR UPDATE SKIP LOCKED LIMIT 1"
                    )
                    .FirstOrDefaultAsync(stoppingToken);
                if (mail != null)
                {
                    pollDelay = TimeSpan.FromMilliseconds(100);
                    try
                    {
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
                                : "ClubSphere: "
                                    + (
                                        mail.Kind == "verify"
                                            ? "verify your email"
                                            : "reset your password"
                                    ),
                            !string.IsNullOrEmpty(mail.Body)
                                ? mail.Body
                                    + (
                                        string.IsNullOrEmpty(mail.Link)
                                            ? ""
                                            : "\n\n" + origin.TrimEnd('/') + mail.Link
                                    )
                                : "Open this one-time link within one hour: "
                                    + origin.TrimEnd('/')
                                    + mail.Link
                        );
                        await smtp.SendMailAsync(message, stoppingToken);
                        db.MailDeliveryAttempts.Add(new() { MailId = mail.Id, Succeeded = true });
                        mail.SentAt = DateTimeOffset.UtcNow;
                        mail.Link = "";
                    }
                    catch (Exception e) when (e is not OperationCanceledException)
                    {
                        db.MailDeliveryAttempts.Add(
                            new() { MailId = mail.Id, ErrorCode = e.GetType().Name }
                        );
                        mail.Attempts++;
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
                }
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
