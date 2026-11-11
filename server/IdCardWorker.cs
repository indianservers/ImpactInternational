using System.IO.Compression;
using Microsoft.EntityFrameworkCore;
using static Clubs.GovernanceService;

namespace Clubs;

public class IdCardWorker(IServiceScopeFactory scopes, ILogger<IdCardWorker> log)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stop)
    {
        while (!stop.IsCancellationRequested)
        {
            var worked = false;
            try
            {
                using var scope = scopes.CreateScope();
                var s = scope.ServiceProvider.GetRequiredService<IdCardService>();
                var db = s.Db;
                await using var tx = await db.Database.BeginTransactionAsync(stop);
                var acquired = await db
                    .Database.SqlQuery<bool>(
                        $"SELECT pg_try_advisory_xact_lock(511109) AS \"Value\""
                    )
                    .SingleAsync(stop);
                if (!acquired)
                {
                    await tx.RollbackAsync(stop);
                    await Task.Delay(200, stop);
                    continue;
                }
                var expired = await db
                    .IdCardJobs.Where(j => j.ExpiresAt < DateTimeOffset.UtcNow && j.OutputKey != "")
                    .OrderBy(j => j.ExpiresAt)
                    .Take(10)
                    .ToListAsync(stop);
                foreach (var j in expired)
                {
                    var path = s.Storage.PathFor(j.OutputKey);
                    if (File.Exists(path))
                        File.Delete(path);
                    j.OutputKey = "";
                }
                var job = await db
                    .IdCardJobs.Where(j => j.Status == "Queued" || j.Status == "Running")
                    .OrderBy(j => j.CreatedAt)
                    .FirstOrDefaultAsync(stop);
                if (job != null)
                {
                    worked = true;
                    Assignment? actor = null;
                    foreach (
                        var w in await db
                            .Assignments.Where(w =>
                                w.TenantId == job.TenantId && w.UserId == job.CreatorId
                            )
                            .ToListAsync(stop)
                    )
                        if (await s.A.Effective(w))
                        {
                            var grants = await s.A.Grants(w);
                            if (
                                grants.Contains("cards.bulk")
                                && (job.ReuseIssued||grants.Contains("cards.issue"))
                                && await s.A.VisibleIds(w).ContainsAsync(job.OrgId)
                            )
                            {
                                actor = w;
                                break;
                            }
                        }
                    if (
                        actor == null
                        || job.CancelRequested
                        || job.ExpiresAt < DateTimeOffset.UtcNow
                    )
                    {
                        job.Status = "Cancelled";
                        job.CancelRequested = true;
                        job.Error =
                            "Creator authority expired, scope changed, cancellation requested or job expired.";
                    }
                    else
                    {
                        job.Status = "Running";
                        var item = await db
                            .IdCardJobItems.Where(i => i.JobId == job.Id && i.Status == "Pending")
                            .OrderBy(i => i.Ordinal)
                            .FirstOrDefaultAsync(stop);
                        if (item == null)
                        {
                            try
                            {
                                Package(s, job);
                                job.Status = job.Failed > 0 ? "Completed with errors" : "Completed";
                            }
                            catch (Exception ex)
                            {
                                job.Status = "Failed";
                                job.Error =
                                    "Packaging failed; retry after checking private storage.";
                                log.LogWarning(ex, "Card job packaging failed {JobId}", job.Id);
                            }
                        }
                        else
                        {
                            await tx.CreateSavepointAsync("card_item", stop);
                            try
                            {
                                DigitalCard card;
                                if (job.ReuseIssued)
                                {
                                    var member = await s.Load(item.MembershipId, actor);
                                    if(member.Status != "Active" || member.EndsAt <= DateTimeOffset.UtcNow) throw new ApiError(409,"Membership no longer eligible.");
                                    card = await db.DigitalCards.SingleOrDefaultAsync(c=>c.TenantId==job.TenantId && c.MembershipId==item.MembershipId && c.TemplateVersionId==job.TemplateVersionId && c.State=="Issued" && c.RevokedAt==null && c.ExpiryAt>DateTimeOffset.UtcNow,stop) ?? throw new ApiError(409,"Issued card no longer available.");
                                    item.CardId=card.Id;
                                }
                                else if (item.CardId != null)
                                {
                                    card = await db.DigitalCards.SingleAsync(
                                        c => c.Id == item.CardId,
                                        stop
                                    );
                                }
                                else
                                {
                                    var old = job.Reissue
                                        ? await db
                                            .DigitalCards.Where(c =>
                                                c.MembershipId == item.MembershipId
                                                && c.RevokedAt == null
                                                && (
                                                    c.State == "Legacy"
                                                    || c.State == "Issued"
                                                    || c.State == "Generated"
                                                )
                                            )
                                            .Select(c => (Guid?)c.Id)
                                            .SingleOrDefaultAsync(stop)
                                        : null;
                                    card = await s.Draft(
                                        new(
                                            item.MembershipId,
                                            job.TemplateVersionId,
                                            ReplacesId: old,
                                            Reason: "Independently approved bulk job " + job.Id
                                        ),
                                        actor
                                    );
                                    item.CardId = card.Id;
                                    card.State = "Approved";
                                    card.RequestId = job.RequestId;
                                    await db.SaveChangesAsync(stop);
                                }
                                if (card.State == "Approved")
                                    await s.Generate(card, actor);
                                if (card.State == "Generated")
                                    await s.Issue(card, actor);
                                if (card.State != "Issued")
                                    throw new ApiError(
                                        409,
                                        "Item is no longer eligible for issuance."
                                    );
                                item.Status = "Succeeded";
                                item.Error = "";
                                job.Success++;
                                await db.SaveChangesAsync(stop);
                            }
                            catch (Exception ex) when (ex is not OperationCanceledException)
                            {
                                await tx.RollbackToSavepointAsync("card_item", stop);
                                db.ChangeTracker.Clear();
                                job = await db.IdCardJobs.SingleAsync(j => j.Id == job.Id, stop);
                                item = await db.IdCardJobItems.SingleAsync(
                                    i => i.Id == item.Id,
                                    stop
                                );
                                item.Status = "Failed";
                                item.Error = ex is ApiError error
                                    ? error.Message
                                    : "Rendering failed. Check source fields/assets and retry.";
                                job.Failed++;
                                log.LogWarning(
                                    "Card job item failed {JobId} {Ordinal}",
                                    job.Id,
                                    item.Ordinal
                                );
                            }
                        }
                    }
                    job.Version++;
                    job.UpdatedAt = DateTimeOffset.UtcNow;
                }
                await db.SaveChangesAsync(stop);
                await tx.CommitAsync(stop);
            }
            catch (OperationCanceledException) when (stop.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                log.LogWarning(ex, "ID card queue pass failed");
            }
            await Task.Delay(worked ? 10 : 500, stop);
        }
    }

    static void Package(IdCardService s, IdCardJob job)
    {
        var version = s
            .Db.IdCardTemplateVersions.AsNoTracking()
            .Single(v => v.Id == job.TemplateVersionId);
        var template = Unpack<IdCardPublished>(version.Layout);
        var items = s
            .Db.IdCardJobItems.AsNoTracking()
            .Where(i => i.JobId == job.Id && i.Status == "Succeeded")
            .OrderBy(i => i.Ordinal)
            .Select(i => i.CardId)
            .ToArray();
        var key = Guid.NewGuid().ToString("N");
        Directory.CreateDirectory(s.Storage.Root);
        using var output = File.Create(s.Storage.PathFor(key));
        if (job.Format is "Pdf" or "Print")
        {
            IEnumerable<IdCardSnapshot> Snapshots()
            {
                foreach (var id in items)
                {
                    var json = s
                        .Db.DigitalCards.AsNoTracking()
                        .Where(c => c.Id == id)
                        .Select(c => c.Snapshot)
                        .Single();
                    yield return Unpack<IdCardSnapshot>(json);
                }
            }
            var preset =
                job.Format == "Print"
                    ? Unpack<IdCardPrintLayout>(job.PrintLayout)
                    : new IdCardPrintLayout(
                        template.Design.Width,
                        template.Design.Height,
                        0,
                        0,
                        0,
                        template.Design.DoubleSided ? "Both" : "Front",
                        CropMarks: false
                    );
            s.Renderer.Print(output, template, Snapshots(), preset);
        }
        else
        {
            using var zip = new ZipArchive(output, ZipArchiveMode.Create, true);
            foreach (var id in items)
            {
                var c = s.Db.DigitalCards.AsNoTracking().Single(c => c.Id == id);
                var files = Unpack<IdCardFiles>(c.Files);
                if (job.Format == "Zip")
                    Add(files.Pdf, c.Serial + ".pdf");
                if (job.Format is "Zip" or "FrontImages" or "Pairs")
                    Add(files.Front, c.Serial + "-front.png");
                if (job.Format is "Zip" or "BackImages" or "Pairs" && files.Back != "")
                    Add(files.Back, c.Serial + "-back.png");
            }
            var errors = zip.CreateEntry("generation-report.csv");
            using var writer = new StreamWriter(errors.Open());
            writer.WriteLine("Ordinal,Status,Error");
            foreach (
                var row in s
                    .Db.IdCardJobItems.AsNoTracking()
                    .Where(i => i.JobId == job.Id)
                    .OrderBy(i => i.Ordinal)
                    .Select(i => new
                    {
                        i.Ordinal,
                        i.Status,
                        i.Error,
                    })
                    .AsEnumerable()
            )
                writer.WriteLine(
                    row.Ordinal + "," + row.Status + ",\"" + row.Error.Replace("\"", "\"\"") + "\""
                );
            void Add(string storageKey, string name)
            {
                if (storageKey == "")
                    return;
                var entry = zip.CreateEntry(name, CompressionLevel.Fastest);
                using var source = File.OpenRead(s.Storage.PathFor(storageKey));
                using var destination = entry.Open();
                source.CopyTo(destination);
            }
        }
        job.OutputKey = key;
    }
}
