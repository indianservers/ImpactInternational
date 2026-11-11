using System.Text.RegularExpressions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using QRCoder;
using static Clubs.Access;
using static Clubs.GovernanceService;

namespace Clubs;

public record CertificateElement(
    string Text,
    double X = 50,
    double Y = 100,
    double Size = 20,
    string Align = "Center",
    string Color = "#173c42"
);

public record CertificateLayout(
    string Orientation = "Landscape",
    string Background = "#fffdf5",
    string Border = "#b39755",
    CertificateElement[]? Elements = null
);

public record CertificateTemplateInput(
    Guid OrgId,
    string Name,
    string Type,
    CertificateLayout Layout,
    long Version = 1
);

public record CertificateIssueInput(
    Guid OrgId,
    Guid TemplateId,
    Guid[] People,
    string SourceType,
    Guid SourceId,
    bool PublicName = false,
    Guid? ReplacesId = null
);

public static partial class OperationsEndpoints
{
    static void MapCertificates(RouteGroupBuilder api)
    {
        api.MapGet(
            "/operations/certificate-sources",
            async (
                string type,
                OperationsService service,
                Guid? personId = null,
                string? search = null
            ) =>
            {
                var workspace = await service.A.Workspace("operations.certificates");
                Check((search?.Length ?? 0) <= 100, "Search too long.");
                var term = "%" + (search ?? "") + "%";
                if (type == "Membership")
                    return Results.Ok(
                        await service
                            .Db.Memberships.Where(m =>
                                m.TenantId == workspace.TenantId
                                && m.PersonId == personId
                                && m.Status == "Active"
                                && (m.EndsAt == null || m.EndsAt > DateTimeOffset.UtcNow)
                                && service.A.VisibleIds(workspace).Contains(m.ClubId)
                            )
                            .OrderBy(m => m.Number)
                            .Take(100)
                            .Select(m => new { id = m.Id, label = m.Number + " · " + m.Club.Name })
                            .ToListAsync()
                    );
                if (type == "Project")
                    return Results.Ok(
                        await service
                            .Db.ServiceProjects.Where(p =>
                                p.TenantId == workspace.TenantId
                                && service.A.VisibleIds(workspace).Contains(p.OrgId)
                                && EF.Functions.ILike(p.Name, term)
                            )
                            .OrderBy(p => p.Name)
                            .Take(100)
                            .Select(p => new { id = p.Id, label = p.Name })
                            .ToListAsync()
                    );
                if (type == "Event")
                    return Results.Ok(
                        await service
                            .Db.Events.Where(e =>
                                e.TenantId == workspace.TenantId
                                && service.A.VisibleIds(workspace).Contains(e.OrgId)
                                && EF.Functions.ILike(e.Title, term)
                            )
                            .OrderByDescending(e => e.StartsAt)
                            .Take(100)
                            .Select(e => new { id = e.Id, label = e.Title })
                            .ToListAsync()
                    );
                Check(type == "Leadership", "Unsupported source type.");
                return Results.Ok(
                    await service
                        .Db.OfficerAppointments.Where(p =>
                            p.TenantId == workspace.TenantId
                            && p.PersonId == personId
                            && p.Status == "Active"
                            && service.A.VisibleIds(workspace).Contains(p.OrgId)
                        )
                        .OrderByDescending(p => p.StartsAt)
                        .Take(100)
                        .Select(p => new
                        {
                            id = p.Id,
                            label = p.PositionTitle + " · " + p.StartsAt.ToString(),
                        })
                        .ToListAsync()
                );
            }
        );
        api.MapGet(
            "/certificate-templates",
            async (OperationsService s) =>
            {
                var w = await s.Workspace();
                return await s
                    .Db.CertificateTemplates.Where(t =>
                        t.Type != "IdCard" && t.TenantId == w.TenantId && s.A.VisibleIds(w).Contains(t.OrgId)
                    )
                    .OrderBy(t => t.Name)
                    .Take(100)
                    .ToListAsync();
            }
        );

        api.MapPost(
            "/certificate-templates",
            async (CertificateTemplateInput i, OperationsService s) =>
            {
                var w = await s.A.Workspace("operations.certificates");
                await s.Governance.Scope(w, i.OrgId);
                Check(i.Type!="IdCard", "Use the ID card template studio.");
                ValidateLayout(i.Layout);
                OperationsService.Title(i.Name);
                var t = new CertificateTemplate
                {
                    TenantId = w.TenantId,
                    OrgId = i.OrgId,
                    Name = i.Name,
                    Type = i.Type,
                    Layout = Pack(i.Layout),
                };
                s.Db.CertificateTemplates.Add(t);
                s.Revision(t, "CertificateTemplate", t.Layout, w.UserId);
                s.Audit(w, t, "certificate.template.created");
                await s.Db.SaveChangesAsync();
                return t;
            }
        );

        api.MapPut(
            "/certificate-templates/{id:guid}",
            async (Guid id, CertificateTemplateInput i, OperationsService s) =>
            {
                var w = await s.A.Workspace("operations.certificates");
                var t = await s.Db.CertificateTemplates.SingleOrDefaultAsync(t =>
                    t.Id == id && t.Type != "IdCard" && t.TenantId == w.TenantId && s.A.VisibleIds(w).Contains(t.OrgId)
                );
                Check(t != null, "Template unavailable.", 404);
                Check(t.Version == i.Version && t.OrgId == i.OrgId, "Template changed.", 409);
                Check(i.Type!="IdCard", "Use the ID card template studio.");
                ValidateLayout(i.Layout);
                OperationsService.Title(i.Name);
                s.Revision(t, "CertificateTemplate", t.Layout, w.UserId);
                t.Layout = Pack(i.Layout);
                t.Name = i.Name;
                t.Type = i.Type;
                t.Version++;
                s.Audit(w, t, "certificate.template.updated");
                await s.Db.SaveChangesAsync();
                return t;
            }
        );

        api.MapPost(
            "/certificate-templates/preview",
            async (CertificateTemplateInput i, OperationsService s) =>
            {
                await s.Scope(i.OrgId);
                Check(i.Type!="IdCard", "Use the ID card template studio.");
                ValidateLayout(i.Layout);
                return Results.File(
                    CertificatePdf(
                        i.Layout,
                        new Dictionary<string, string>
                        {
                            { "member_name", "Sample Member" },
                            { "member_id", "SAMPLE-001" },
                            { "club_name", "Sample Club" },
                            { "organization_name", "Sample Organization" },
                            { "certificate_date", DateTimeOffset.UtcNow.ToString("yyyy-MM-dd") },
                            { "certificate_number", "PREVIEW" },
                            { "event_name", "Sample event" },
                            { "project_name", "Sample project" },
                            { "service_hours", "12" },
                            { "officer_position", "Sample position" },
                            { "leadership_term", "Sample term" },
                        },
                        ""
                    ),
                    "application/pdf",
                    "preview.pdf"
                );
            }
        );

        api.MapPost(
            "/certificates",
            async (
                CertificateIssueInput i,
                OperationsService s,
                IDataProtectionProvider protection
            ) =>
            {
                var w = await s.A.Workspace("operations.certificates");
                await s.Governance.Scope(w, i.OrgId);
                Check(
                    i.People.Length is > 0 and <= 100
                        && i.People.Distinct().Count() == i.People.Length,
                    "Choose up to 100 distinct recipients."
                );
                var template = await s.Db.CertificateTemplates.SingleOrDefaultAsync(t =>
                    t.Id == i.TemplateId
                    && t.TenantId == w.TenantId
                    && t.Type != "IdCard"
                      && t.Active
                    && s.A.VisibleIds(w).Contains(t.OrgId)
                );
                Check(template != null, "Template unavailable.", 404);
                var layout = Unpack<CertificateLayout>(template.Layout);
                ValidateLayout(layout);
                var list = new List<object>();
                await using var tx = await s.Db.Database.BeginTransactionAsync();
                await s.Lock(template.Id);
                foreach (var person in i.People)
                {
                    var existing = await s.Db.IssuedCertificates.SingleOrDefaultAsync(c =>
                        c.TenantId == w.TenantId
                        && c.OrgId == i.OrgId
                        && c.TemplateId == template.Id
                        && c.TemplateVersion == template.Version
                        && c.PersonId == person
                        && c.SourceType == i.SourceType
                        && c.SourceId == i.SourceId
                        && c.ReplacesId == i.ReplacesId
                    );
                    if (existing != null)
                    {
                        list.Add(
                            new
                            {
                                existing.Id,
                                existing.Number,
                                existing.Status,
                                requestId = existing.RequestId,
                            }
                        );
                        continue;
                    }
                    var p = await s.Person(person, w.TenantId);
                    var values = await CertificateValues(i, p, s, w);
                    values["certificate_type"] = template.Type;
                    foreach (var element in layout.Elements ?? [])
                    foreach (Match match in Regex.Matches(element.Text, @"\{\{([a-z_]+)\}\}"))
                        Check(
                            values.TryGetValue(match.Groups[1].Value, out var v)
                                && !string.IsNullOrWhiteSpace(v),
                            "Missing placeholder: " + match.Groups[1].Value
                        );
                    if (i.ReplacesId != null)
                        Check(
                            await s.Db.IssuedCertificates.AnyAsync(c =>
                                c.Id == i.ReplacesId
                                && c.PersonId == person
                                && c.TenantId == w.TenantId
                                && c.Status == "Revoked"
                            ),
                            "Reissue must reference a revoked certificate for the same recipient."
                        );
                    var token = Token();
                    var c = new IssuedCertificate
                    {
                        TenantId = w.TenantId,
                        OrgId = i.OrgId,
                        TemplateId = template.Id,
                        TemplateVersion = template.Version,
                        PersonId = person,
                        SourceType = i.SourceType,
                        SourceId = i.SourceId,
                        Number =
                            "CS-"
                            + DateTimeOffset.UtcNow.ToString("yyyy")
                            + "-"
                            + Guid.NewGuid().ToString("N")[..16].ToUpperInvariant(),
                        TokenHash = Hash(token),
                        ProtectedToken = protection
                            .CreateProtector("operations.certificate")
                            .Protect(token),
                        Snapshot = Pack(values),
                        Layout = template.Layout,
                        IssuerId = w.UserId,
                        PublicName = i.PublicName,
                        ReplacesId = i.ReplacesId,
                    };
                    values["certificate_number"] = c.Number;
                    c.Snapshot = Pack(values);
                    s.Db.IssuedCertificates.Add(c);
                    var r = await s.Submit(c, "OperationCertificate", c.Snapshot + c.Layout, w);
                    c.RequestId = r.Id;
                    s.Audit(w, c, "certificate.prepared");
                    list.Add(
                        new
                        {
                            c.Id,
                            c.Number,
                            c.Status,
                            requestId = r.Id,
                        }
                    );
                }
                await s.Db.SaveChangesAsync();
                await tx.CommitAsync();
                return list;
            }
        );

        api.MapGet(
            "/certificates",
            async (OperationsService s, bool mine = false) =>
            {
                var w = await s.Workspace();
                var can = (await s.A.Grants(w)).Contains("operations.certificates");
                return await s
                    .Db.IssuedCertificates.Where(c =>
                        c.TenantId == w.TenantId
                        && (
                            can && !mine && s.A.VisibleIds(w).Contains(c.OrgId)
                            || s.Db.Persons.Any(p => p.Id == c.PersonId && p.UserId == w.UserId)
                        )
                    )
                    .OrderByDescending(c => c.UpdatedAt)
                    .Take(100)
                    .Select(c => new
                    {
                        c.Id,
                        c.Number,
                        c.Status,
                        c.PersonId,
                        c.SourceType,
                        c.TemplateId,
                        c.IssuedAt,
                        c.RequestId,
                        c.ReplacesId,
                        c.Version,
                    })
                    .ToListAsync();
            }
        );

        api.MapGet(
            "/certificates/{id:guid}/pdf",
            async (
                Guid id,
                OperationsService s,
                IDataProtectionProvider protection,
                IConfiguration config
            ) =>
            {
                var w = await s.Workspace();
                var c = await s.Db.IssuedCertificates.SingleOrDefaultAsync(c =>
                    c.Id == id && c.TenantId == w.TenantId
                );
                Check(c != null, "Certificate unavailable.", 404);
                Check(
                    (await s.A.Grants(w)).Contains("operations.certificates")
                        && await s.A.VisibleIds(w).ContainsAsync(c.OrgId)
                        || await s.IsPerson(c.PersonId, w.UserId),
                    "Certificate unavailable.",
                    404
                );
                Check(c.Status == "Issued", "Certificate has not been issued or was revoked.", 409);
                var token = protection
                    .CreateProtector("operations.certificate")
                    .Unprotect(c.ProtectedToken);
                var origin = config["PublicOrigin"] ?? "http://127.0.0.1:5174";
                return Results.File(
                    CertificatePdf(
                        Unpack<CertificateLayout>(c.Layout),
                        Unpack<Dictionary<string, string>>(c.Snapshot),
                        origin.TrimEnd('/') + "/verify/certificates/" + token
                    ),
                    "application/pdf",
                    c.Number + ".pdf"
                );
            }
        );

        api.MapPost(
            "/certificates/{id:guid}/revoke",
            async (Guid id, StateInput i, OperationsService s) =>
            {
                var w = await s.A.Workspace("operations.certificates");
                var c = await s.Db.IssuedCertificates.SingleOrDefaultAsync(c =>
                    c.Id == id && c.TenantId == w.TenantId && s.A.VisibleIds(w).Contains(c.OrgId)
                );
                Check(c != null, "Certificate unavailable.", 404);
                Check(
                    c.Status == "Issued" && c.Version == i.Version && i.Reason.Length >= 5,
                    "Provide current version and revocation reason.",
                    409
                );
                c.Status = "Revoked";
                c.RevocationReason = i.Reason;
                c.Version++;
                s.Audit(w, c, "certificate.revoked", i.Reason);
                await s.Db.SaveChangesAsync();
                return new
                {
                    c.Id,
                    c.Status,
                    c.Version,
                };
            }
        );

        api.MapPost(
            "/certificates/bulk-pdf",
            async (
                Guid[] ids,
                OperationsService s,
                IDataProtectionProvider protection,
                IConfiguration config
            ) =>
            {
                var w = await s.A.Workspace("operations.certificates");
                Check(
                    ids.Length is > 0 and <= 50 && ids.Distinct().Count() == ids.Length,
                    "Choose 1–50 distinct issued certificates."
                );
                var records = await s
                    .Db.IssuedCertificates.Where(c =>
                        ids.Contains(c.Id)
                        && c.TenantId == w.TenantId
                        && s.A.VisibleIds(w).Contains(c.OrgId)
                        && c.Status == "Issued"
                    )
                    .ToListAsync();
                Check(
                    records.Count == ids.Length,
                    "Some certificates are unavailable or unissued.",
                    404
                );
                using var output = new MemoryStream();
                using (
                    var zip = new System.IO.Compression.ZipArchive(
                        output,
                        System.IO.Compression.ZipArchiveMode.Create,
                        true
                    )
                )
                {
                    foreach (var c in records)
                    {
                        var token = protection
                            .CreateProtector("operations.certificate")
                            .Unprotect(c.ProtectedToken);
                        var bytes = CertificatePdf(
                            Unpack<CertificateLayout>(c.Layout),
                            Unpack<Dictionary<string, string>>(c.Snapshot),
                            (config["PublicOrigin"] ?? "http://127.0.0.1:5174").TrimEnd('/')
                                + "/verify/certificates/"
                                + token
                        );
                        using var entry = zip.CreateEntry(c.Number + ".pdf").Open();
                        await entry.WriteAsync(bytes);
                    }
                }
                return Results.File(output.ToArray(), "application/zip", "certificates.zip");
            }
        );
        api.MapGet(
                "/certificates/verify/{token}",
                async (string token, Db db) =>
                {
                    Check(token.Length == 64, "Certificate unavailable.", 404);
                    var h = Hash(token);
                    var c = await db
                        .IssuedCertificates.AsNoTracking()
                        .SingleOrDefaultAsync(c =>
                            c.TokenHash == h && new[] { "Issued", "Revoked" }.Contains(c.Status)
                        );
                    Check(c != null, "Certificate unavailable.", 404);
                    var values = Unpack<Dictionary<string, string>>(c.Snapshot);
                    return new
                    {
                        valid = c.Status == "Issued",
                        c.Number,
                        c.Status,
                        c.IssuedAt,
                        recipient = c.PublicName ? values.GetValueOrDefault("member_name") : null,
                        organization = values.GetValueOrDefault("organization_name"),
                        type = values.GetValueOrDefault("certificate_type"),
                    };
                }
            )
            .RequireRateLimiting("cards");
    }

    static void ValidateLayout(CertificateLayout l)
    {
        Check(
            l.Orientation is "Portrait" or "Landscape"
                && (l.Elements?.Length ?? 0) is > 0 and <= 30,
            "Choose page orientation and 1–30 elements."
        );
        Check(
            Regex.IsMatch(l.Background, "^#[0-9a-fA-F]{6}$")
                && Regex.IsMatch(l.Border, "^#[0-9a-fA-F]{6}$"),
            "Use hexadecimal colors."
        );
        foreach (var e in l.Elements!)
        {
            Check(
                e.Text.Length <= 2000
                    && double.IsFinite(e.X)
                    && double.IsFinite(e.Y)
                    && double.IsFinite(e.Size)
                    && e.X is >= 0 and <= 100
                    && e.Y is >= 0 and <= 100
                    && e.Size is >= 8 and <= 80
                    && e.Align is "Left" or "Center" or "Right"
                    && Regex.IsMatch(e.Color, "^#[0-9a-fA-F]{6}$"),
                "Invalid certificate element."
            );
        }
    }

    static async Task<Dictionary<string, string>> CertificateValues(
        CertificateIssueInput i,
        Person p,
        OperationsService s,
        Assignment w
    )
    {
        var org = await s.Db.Orgs.SingleAsync(o => o.Id == i.OrgId);
        var member = await s
            .Db.Memberships.Where(m =>
                m.PersonId == p.Id
                && m.TenantId == w.TenantId
                && m.Status == "Active"
                && (m.EndsAt == null || m.EndsAt > DateTimeOffset.UtcNow)
                && (i.SourceType != "Membership" || m.Id == i.SourceId)
                && s.A.VisibleIds(w).Contains(m.ClubId)
            )
            .Select(m => new
            {
                m.Id,
                m.Number,
                ClubName = m.Club.Name,
            })
            .FirstOrDefaultAsync();
        var v = new Dictionary<string, string>
        {
            { "member_name", p.DisplayName },
            { "member_id", member?.Number ?? "" },
            { "club_name", member?.ClubName ?? "" },
            { "organization_name", org.Name },
            { "certificate_date", DateTimeOffset.UtcNow.ToString("yyyy-MM-dd") },
            { "certificate_number", "PENDING" },
        };

        switch (i.SourceType)
        {
            case "Membership":
                Check(
                    member != null && member.Id == i.SourceId,
                    "Active scoped membership required."
                );
                break;
            case "Event":
                var ev = await s.Db.Events.SingleOrDefaultAsync(e =>
                    e.Id == i.SourceId
                    && e.TenantId == w.TenantId
                    && s.A.VisibleIds(w).Contains(e.OrgId)
                );
                Check(
                    ev != null
                        && await s.Db.EventRegistrations.AnyAsync(r =>
                            r.EventId == ev.Id
                            && r.PersonId == p.Id
                            && s.Db.EventCheckins.Any(c => c.RegistrationId == r.Id && !c.Voided)
                        ),
                    "Verified event attendance required."
                );
                v["event_name"] = ev.Title;
                break;
            case "Project":
                var project = await s.Project(i.SourceId, true);
                var hours =
                    await s
                        .Db.VolunteerHours.Where(h =>
                            h.ProjectId == project.Id
                            && h.PersonId == p.Id
                            && h.Status == "Verified"
                        )
                        .SumAsync(h => (decimal?)h.Hours)
                    ?? 0;
                Check(hours > 0, "Verified project service required.");
                v["project_name"] = project.Name;
                v["service_hours"] = hours.ToString(
                    System.Globalization.CultureInfo.InvariantCulture
                );
                break;
            case "Leadership":
                var appointment = await s.Db.OfficerAppointments.SingleOrDefaultAsync(a =>
                    a.Id == i.SourceId
                    && a.PersonId == p.Id
                    && a.TenantId == w.TenantId
                    && s.A.VisibleIds(w).Contains(a.OrgId)
                    && a.Status == "Active"
                );
                Check(appointment != null, "Approved appointment required.");
                v["officer_position"] = appointment.PositionTitle;
                v["leadership_term"] =
                    appointment.StartsAt.ToString("yyyy-MM-dd")
                    + " – "
                    + appointment.EndsAt.ToString("yyyy-MM-dd");
                break;
            default:
                throw new ApiError(
                    400,
                    "Supported certificate sources: Membership, Event, Project, Leadership."
                );
        }
        return v;
    }

    public static byte[] CertificatePdf(
        CertificateLayout layout,
        Dictionary<string, string> values,
        string verification
    )
    {
        using var pdf = new PdfDocument();
        pdf.Info.Title = "Impact International certificate";
        var page = pdf.AddPage();
        page.Size = PdfSharp.PageSize.A4;
        page.Orientation =
            layout.Orientation == "Landscape"
                ? PdfSharp.PageOrientation.Landscape
                : PdfSharp.PageOrientation.Portrait;
        using var g = XGraphics.FromPdfPage(page);
        var width = page.Width.Point;
        var height = page.Height.Point;
        g.DrawRectangle(
            new XSolidBrush(
                XColor.FromArgb(
                    Convert.ToInt32(layout.Background[1..], 16) | unchecked((int)0xff000000)
                )
            ),
            0,
            0,
            width,
            height
        );
        g.DrawRectangle(
            new XPen(
                XColor.FromArgb(
                    Convert.ToInt32(layout.Border[1..], 16) | unchecked((int)0xff000000)
                ),
                2
            ),
            25,
            25,
            width - 50,
            height - 50
        );
        foreach (var e in layout.Elements ?? [])
        {
            var text = Regex.Replace(
                e.Text,
                @"\{\{([a-z_]+)\}\}",
                m => values.GetValueOrDefault(m.Groups[1].Value, "")
            );
            var font = new XFont(
                "Noto Sans",
                e.Size,
                XFontStyleEx.Regular,
                new XPdfFontOptions(PdfFontEncoding.Unicode)
            );
            var brush = new XSolidBrush(
                XColor.FromArgb(Convert.ToInt32(e.Color[1..], 16) | unchecked((int)0xff000000))
            );
            var x = e.X / 100 * width;
            var y = e.Y / 100 * height;
            var align =
                e.Align == "Left" ? XStringFormats.TopLeft
                : e.Align == "Right" ? XStringFormats.TopRight
                : XStringFormats.TopCenter;
            g.DrawString(
                text,
                font,
                brush,
                new XRect(
                    e.Align == "Center" ? x - width / 2
                        : e.Align == "Right" ? x - width
                        : x,
                    y,
                    width,
                    Math.Max(100, e.Size * 2)
                ),
                align
            );
        }
        if (verification != "")
        {
            using var generator = new QRCodeGenerator();
            using var qr = generator.CreateQrCode(verification, QRCodeGenerator.ECCLevel.Q);
            var modules = qr.ModuleMatrix;
            var cell = 80d / modules.Count;
            var left = width - 120;
            var top = height - 120;
            g.DrawRectangle(XBrushes.White, left, top, 80, 80);
            for (var y = 0; y < modules.Count; y++)
            for (var x = 0; x < modules.Count; x++)
                if (modules[y][x])
                    g.DrawRectangle(
                        XBrushes.Black,
                        left + x * cell,
                        top + y * cell,
                        cell + .02,
                        cell + .02
                    );
        }
        using var output = new MemoryStream();
        pdf.Save(output, false);
        return output.ToArray();
    }
}
