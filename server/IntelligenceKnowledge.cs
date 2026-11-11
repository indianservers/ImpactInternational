using Microsoft.EntityFrameworkCore;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;
using static Clubs.Access;

namespace Clubs;

public record KnowledgeResult(
    Guid DocumentId,
    int Version,
    int Page,
    string Title,
    string Excerpt,
    string SourceHash,
    string Url
);

public class IntelligenceKnowledge(IntelligenceService s, CommunicationStorage storage)
{
    static readonly SemaphoreSlim Parsing = new(2);

    public async Task<object> Index(Guid id)
    {
        var w = await s.A.Workspace("operations.documents");
        var d = await s.Db.GovernanceDocuments.SingleOrDefaultAsync(x =>
            x.Id == id && x.TenantId == w.TenantId
        );
        Check(d != null, "Document unavailable.", 404);
        await s.Operations.Document(d);
        await s.Operations.Governance.Scope(w, d.OrgId);
        Check(
            d.ContentType == "application/pdf",
            "Only text-bearing PDFs are indexed; scans require OCR which is not enabled.",
            415
        );
        Check(await Parsing.WaitAsync(0), "PDF indexing is busy; retry later.", 429);
        try
        {
            byte[] bytes =
                d.ObjectKey == ""
                    ? d.Content
                    : await File.ReadAllBytesAsync(storage.PathFor(d.ObjectKey));
            Check(
                bytes.Length <= 5 * 1024 * 1024 && HashBytes(bytes) == d.Sha256,
                "Document integrity check failed.",
                409
            );
            var pages = new List<(int Page, string Text)>();
            try
            {
                using var pdf = PdfDocument.Open(bytes);
                Check(
                    pdf.NumberOfPages is > 0 and <= 100,
                    "PDF indexing supports up to 100 pages."
                );
                var total = 0;
                foreach (var page in pdf.GetPages())
                {
                    var text = ContentOrderTextExtractor.GetText(page);
                    Check(text.Length <= 30000, "PDF page text exceeds indexing limit.");
                    total += text.Length;
                    Check(total <= 1000000, "PDF text exceeds indexing limit.");
                    if (!string.IsNullOrWhiteSpace(text))
                        pages.Add((page.Number, text));
                }
            }
            catch (ApiError)
            {
                throw;
            }
            catch (Exception)
            {
                throw new ApiError(
                    400,
                    "PDF text could not be safely extracted; encrypted or malformed documents are unsupported."
                );
            }
            Check(pages.Count > 0, "No extractable text. Scanned PDF OCR is not enabled.", 422);
            await using var tx = await s.Db.Database.BeginTransactionAsync();
            await s.Operations.Lock(id);
            await s.Db.Entry(d).ReloadAsync();
            await s.Operations.Document(d);
            Check(HashBytes(bytes) == d.Sha256, "Document changed during indexing.", 409);
            await s.Db.KnowledgePassages.Where(p => p.DocumentId == id).ExecuteDeleteAsync();
            foreach (var p in pages)
                s.Db.KnowledgePassages.Add(
                    new()
                    {
                        TenantId = d.TenantId,
                        DocumentId = id,
                        DocumentVersion = d.CurrentVersion,
                        Page = p.Page,
                        Text = p.Text,
                        SourceHash = d.Sha256,
                        IndexedBy = w.UserId,
                    }
                );
            s.Audit(w, "knowledge.index", id.ToString(), "document-pdf", pages.Count);
            await s.Db.SaveChangesAsync();
            await tx.CommitAsync();
            return new
            {
                documentId = id,
                version = d.CurrentVersion,
                pages = pages.Count,
                sourceHash = d.Sha256,
            };
        }
        finally
        {
            Parsing.Release();
        }
    }

    public async Task<List<KnowledgeResult>> Search(string question, Guid? org = null)
    {
        var w = await s.Scoped(org, "intelligence.knowledge");
        Check(question.Length is >= 2 and <= 500, "Knowledge query requires 2–500 characters.");
        var ids = s.A.VisibleIds(w);
        var uid = w.UserId.ToString();
        var candidates =
            from p in s.Db.KnowledgePassages
            join d in s.Db.GovernanceDocuments on p.DocumentId equals d.Id
            where
                p.TenantId == w.TenantId
                && d.TenantId == w.TenantId
                && p.DocumentVersion == d.CurrentVersion
                && p.SourceHash == d.Sha256
                && d.Status != "Expired"
                && (d.RetainUntil == null || d.RetainUntil > DateTimeOffset.UtcNow)
                && (ids.Contains(d.OrgId) || d.SharedUsers.Contains(uid))
                && (
                    d.Visibility != "Private"
                    || d.UploaderId == w.UserId
                    || d.SharedUsers.Contains(uid)
                )
            select new { p, d };
        if (question.Contains("approved", StringComparison.OrdinalIgnoreCase))
            candidates = candidates.Where(x =>
                s.Db.GovernanceRequests.Any(r =>
                    r.Id == x.d.ApprovalRequestId
                    && r.Status == "Approved"
                    && r.Payload.Contains(x.p.SourceHash)
                )
            );
        var stop = new HashSet<string>(
            "what are is the our a an of in to for please summarize summary find latest approved show rules responsibilities about can you tell me document documents".Split(
                ' '
            )
        );
        var terms = string.Join(
            " ",
            System
                .Text.RegularExpressions.Regex.Matches(
                    question.ToLowerInvariant(),
                    @"[\p{L}\p{N}]{2,}"
                )
                .Select(m => m.Value)
                .Where(t => !stop.Contains(t))
                .Take(8)
        );
        if (terms == "")
            return [];
        var rows = await candidates
            .Where(x => x.p.SearchVector.Matches(EF.Functions.PlainToTsQuery("simple", terms)))
            .OrderByDescending(x =>
                x.p.SearchVector.Rank(EF.Functions.PlainToTsQuery("simple", terms))
            )
            .ThenBy(x => x.p.Id)
            .Take(40)
            .ToListAsync();
        var output = new List<KnowledgeResult>();
        foreach (var row in rows)
        {
            try
            {
                await s.Operations.Document(row.d);
            }
            catch (ApiError)
            {
                continue;
            }
            var excerpt = row.p.Text;
            var first = terms.Split(' ')[0];
            var at = excerpt.IndexOf(first, StringComparison.OrdinalIgnoreCase);
            var offset = Math.Max(0, at - 120);
            excerpt = excerpt.Substring(offset, Math.Min(700, excerpt.Length - offset));
            output.Add(
                new(
                    row.d.Id,
                    row.p.DocumentVersion,
                    row.p.Page,
                    row.d.Title,
                    excerpt,
                    row.p.SourceHash,
                    "/api/v1/documents/"
                        + row.d.Id
                        + "/download?preview=true&version="
                        + row.p.DocumentVersion
                        + "#page="
                        + row.p.Page
                )
            );
            if (output.Count == 5)
                break;
        }
        return output;
    }

    public static string HashBytes(byte[] bytes) =>
        Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes));

    public static async Task Purge(Db db, CancellationToken stop) =>
        await db
            .KnowledgePassages.Where(p =>
                db.GovernanceDocuments.Any(d =>
                    d.Id == p.DocumentId
                    && (
                        d.Status == "Expired"
                        || d.CurrentVersion != p.DocumentVersion
                        || d.Sha256 != p.SourceHash
                        || d.RetainUntil < DateTimeOffset.UtcNow
                    )
                )
            )
            .OrderBy(p => p.Id)
            .Take(1000)
            .ExecuteDeleteAsync(stop);
}
