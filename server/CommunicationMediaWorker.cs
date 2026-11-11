using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using static Clubs.Access;

namespace Clubs;

// Signed inbound metadata is durable before any remote media retrieval begins.
public class CommunicationMediaWorker(
    IServiceScopeFactory scopes,
    IHttpClientFactory clients,
    CommunicationStorage storage,
    IConfiguration config,
    IHostEnvironment env,
    ILogger<CommunicationMediaWorker> log
) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stop)
    {
        while (!stop.IsCancellationRequested)
        {
            try
            {
                using var scope = scopes.CreateScope();
                var s = scope.ServiceProvider.GetRequiredService<CommunicationService>();
                var meta = scope.ServiceProvider.GetRequiredService<MetaCommunicationProvider>();
                await using var tx = await s.Db.Database.BeginTransactionAsync(stop);
                var file = await s
                    .Db.CommunicationAttachments.FromSqlRaw(
                        "SELECT * FROM \"CommunicationAttachments\" WHERE \"Status\"='Provider pending' AND \"NextAttemptAt\"<=now() ORDER BY \"NextAttemptAt\" LIMIT 1 FOR UPDATE SKIP LOCKED"
                    )
                    .FirstOrDefaultAsync(stop);
                if (file != null)
                {
                    try
                    {
                        var account = await s.Db.CommunicationAccounts.SingleAsync(
                            a => a.Id == file.ProviderAccountId && a.TenantId == file.TenantId,
                            stop
                        );
                        Check(
                            account.Enabled && account.Verified && account.Inbound,
                            "Inbound account disabled.",
                            409
                        );
                        using var req = new HttpRequestMessage(
                            HttpMethod.Get,
                            meta.BaseUrl + "/" + Uri.EscapeDataString(file.ProviderMediaId)
                        );
                        req.Headers.Authorization = new AuthenticationHeaderValue(
                            "Bearer",
                            s.Unprotect(account.Secret)
                        );
                        using var response = await clients
                            .CreateClient("communications")
                            .SendAsync(req, stop);
                        Check(response.IsSuccessStatusCode, "Media metadata unavailable.", 409);
                        using var data = JsonDocument.Parse(
                            await response.Content.ReadAsStringAsync(stop)
                        );
                        var url = new Uri(data.RootElement.GetProperty("url").GetString()!);
                        var local =
                            env.IsDevelopment()
                            && config["Communications:MockBase"] is string mock
                            && url.AbsoluteUri.StartsWith(
                                mock.TrimEnd('/') + "/",
                                StringComparison.Ordinal
                            );
                        Check(
                            local
                                || (
                                    url.Scheme == "https"
                                    && (
                                        url.Host.EndsWith(
                                            ".fbsbx.com",
                                            StringComparison.OrdinalIgnoreCase
                                        )
                                        || url.Host.EndsWith(
                                            ".facebook.com",
                                            StringComparison.OrdinalIgnoreCase
                                        )
                                    )
                                ),
                            "Untrusted media host.",
                            403
                        );
                        Check(
                            file.Type is "application/pdf" or "image/png" or "image/jpeg",
                            "Unsupported media type."
                        );
                        using var download = new HttpRequestMessage(HttpMethod.Get, url);
                        download.Headers.Authorization = new AuthenticationHeaderValue(
                            "Bearer",
                            s.Unprotect(account.Secret)
                        );
                        using var bytesResponse = await clients
                            .CreateClient("communication-media")
                            .SendAsync(download, HttpCompletionOption.ResponseHeadersRead, stop);
                        Check(
                            bytesResponse.IsSuccessStatusCode
                                && bytesResponse.Content.Headers.ContentLength is not > 5242880,
                            "Media download refused."
                        );
                        await using var source = await bytesResponse.Content.ReadAsStreamAsync(
                            stop
                        );
                        using var buffer = new MemoryStream();
                        var block = new byte[32768];
                        int read;
                        while ((read = await source.ReadAsync(block, stop)) > 0)
                        {
                            Check(buffer.Length + read <= 5242880, "Media exceeds size limit.");
                            buffer.Write(block, 0, read);
                        }
                        var bytes = buffer.ToArray();
                        Check(
                            file.Type == "application/pdf" ? bytes.AsSpan().StartsWith("%PDF-"u8)
                                : file.Type == "image/png"
                                    ? bytes
                                        .AsSpan()
                                        .StartsWith(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 })
                                : bytes.Length > 3
                                    && bytes[0] == 255
                                    && bytes[1] == 216
                                    && bytes[2] == 255,
                            "Media signature mismatch."
                        );
                        var key = Guid.NewGuid().ToString("N");
                        Directory.CreateDirectory(storage.Root);
                        await File.WriteAllBytesAsync(storage.PathFor(key), bytes, stop);
                        file.StorageKey = key;
                        file.Hash = Convert.ToHexString(SHA256.HashData(bytes));
                        file.Size = bytes.Length;
                        file.Name =
                            Path.GetFileNameWithoutExtension(file.Name)[
                                ..Math.Min(120, Path.GetFileNameWithoutExtension(file.Name).Length)
                            ]
                            + (
                                file.Type == "application/pdf" ? ".pdf"
                                : file.Type == "image/png" ? ".png"
                                : ".jpg"
                            );
                        file.Status = "Stored";
                        var cid = await s
                            .Db.CommunicationMessages.Where(m => m.Id == file.MessageId)
                            .Select(m => m.ConversationId)
                            .SingleAsync(stop);
                        await s
                            .Db.Conversations.Where(c => c.Id == cid)
                            .ExecuteUpdateAsync(
                                x => x.SetProperty(c => c.UpdatedAt, DateTimeOffset.UtcNow),
                                stop
                            );
                    }
                    catch (Exception e) when (e is not OperationCanceledException)
                    {
                        file.Attempts++;
                        file.NextAttemptAt = DateTimeOffset.UtcNow.AddSeconds(
                            Math.Min(900, Math.Pow(2, file.Attempts) * 15)
                        );
                        if (file.Attempts >= 6 || e is ApiError { Status: 400 or 403 })
                            file.Status = "Failed";
                        log.LogWarning(
                            "Inbound media job {Id} failed ({Type})",
                            file.Id,
                            e.GetType().Name
                        );
                    }
                    await s.Db.SaveChangesAsync(stop);
                }
                await tx.CommitAsync(stop);
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                log.LogWarning("Inbound media polling failed ({Type})", e.GetType().Name);
            }
            await Task.Delay(1000, stop);
        }
    }
}
