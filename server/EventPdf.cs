using PdfSharp.Drawing;
using PdfSharp.Fonts;
using PdfSharp.Pdf;
using QRCoder;

namespace Clubs;

// Embedded, OFL-licensed font makes pass generation independent of host font paths.
public sealed class EventFontResolver : IFontResolver
{
    private static readonly Lazy<byte[]> Font = new(() =>
    {
        using var input =
            typeof(EventFontResolver).Assembly.GetManifestResourceStream(
                "Clubs.Api.Assets.Fonts.NotoSans-Regular.ttf"
            ) ?? throw new InvalidOperationException("Bundled event font missing.");
        using var ms = new MemoryStream();
        input.CopyTo(ms);
        return ms.ToArray();
    });

    public FontResolverInfo ResolveTypeface(string familyName, bool bold, bool italic) =>
        new("ClubSphere.NotoSans", false, italic);

    public byte[] GetFont(string faceName) => Font.Value;
}

public static class EventPdf
{
    public static byte[] Build(string[] lines, QRCodeData? qr = null)
    {
        using var document = new PdfDocument();
        document.Info.Title =
            qr == null ? "Impact International participation certificate" : "Impact International event pass";
        document.Info.Creator = "Impact International";
        var page = document.AddPage();
        page.Size = PdfSharp.PageSize.A4;
        var gfx = XGraphics.FromPdfPage(page);
        var ink = new XSolidBrush(XColor.FromArgb(22, 48, 68));
        var teal = new XSolidBrush(XColor.FromArgb(30, 101, 113));
        var soft = new XSolidBrush(XColor.FromArgb(239, 246, 247));
        var small = new XFont(
            "Noto Sans",
            10,
            XFontStyleEx.Regular,
            new XPdfFontOptions(PdfFontEncoding.Unicode)
        );
        var body = new XFont(
            "Noto Sans",
            12,
            XFontStyleEx.Regular,
            new XPdfFontOptions(PdfFontEncoding.Unicode)
        );
        var title = new XFont(
            "Noto Sans",
            21,
            XFontStyleEx.Regular,
            new XPdfFontOptions(PdfFontEncoding.Unicode)
        );
        double y = 70;
        void Header()
        {
            gfx.DrawRectangle(soft, 0, 0, page.Width.Point, 130);
            gfx.DrawRectangle(teal, 42, 39, 34, 4);
        }
        void Footer()
        {
            gfx.DrawLine(
                new XPen(XColor.FromArgb(216, 228, 232)),
                44,
                page.Height.Point - 65,
                page.Width.Point - 44,
                page.Height.Point - 65
            );
            gfx.DrawString(
                "Connected communities. Meaningful experiences.",
                small,
                ink,
                new XPoint(44, page.Height.Point - 43)
            );
        }
        void NextPage()
        {
            Footer();
            gfx.Dispose();
            page = document.AddPage();
            page.Size = PdfSharp.PageSize.A4;
            gfx = XGraphics.FromPdfPage(page);
            Header();
            gfx.DrawString("IMPACT INTERNATIONAL / EVENT RECORD - CONTINUED", small, ink, new XPoint(44, 70));
            y = 160;
        }
        void DrawLine(string line, XFont font, double gap)
        {
            if (y > page.Height.Point - 100)
                NextPage();
            gfx.DrawString(line, font, ink, new XPoint(44, y));
            y += gap;
        }
        void DrawWrapped(string text, XFont font, double gap)
        {
            var line = "";
            foreach (
                var word in text.Replace("\r", "")
                    .Replace("\n", " ")
                    .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            )
            {
                var test = line.Length == 0 ? word : line + " " + word;
                if (gfx.MeasureString(test, font).Width > 495 && line.Length > 0)
                {
                    DrawLine(line, font, gap);
                    line = "";
                }
                foreach (var rune in ((line.Length == 0 ? "" : " ") + word).EnumerateRunes())
                {
                    if (gfx.MeasureString(line + rune, font).Width > 495 && line.Length > 0)
                    {
                        DrawLine(line, font, gap);
                        line = "";
                    }
                    line += rune.ToString();
                }
            }
            if (line.Length > 0)
                DrawLine(line, font, gap);
        }
        Header();
        for (int i = 0; i < lines.Length; i++)
        {
            if (i == 0)
            {
                DrawWrapped(lines[i], small, 15);
                y = 160;
            }
            else
            {
                DrawWrapped(lines[i], i == 1 ? title : body, i == 1 ? 28 : 19);
                y += i == 1 ? 16 : 8;
            }
        }
        if (qr != null)
        {
            var top = Math.Max(405, y + 20);
            if (top + 280 > page.Height.Point - 75)
            {
                NextPage();
                top = 180;
            }
            gfx.DrawRectangle(XBrushes.White, 40, top, 238, 238);
            var matrix = qr.ModuleMatrix;
            double scale = 220d / matrix.Count;
            for (int row = 0; row < matrix.Count; row++)
            for (int col = 0; col < matrix.Count; col++)
                if (matrix[row][col])
                    gfx.DrawRectangle(
                        XBrushes.Black,
                        48 + col * scale,
                        top + 8 + row * scale,
                        scale,
                        scale
                    );
            gfx.DrawString(
                "Present this QR at the authorized check-in desk.",
                small,
                ink,
                new XPoint(44, top + 260)
            );
        }
        Footer();
        gfx.Dispose();
        using var output = new MemoryStream();
        document.Save(output, false);
        return output.ToArray();
    }
}
