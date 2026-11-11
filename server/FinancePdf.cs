using PdfSharp.Drawing;
using PdfSharp.Pdf;

namespace Clubs;

public static class FinancePdf
{
    public static string Issuer(string snapshot)
    {
        using var doc = System.Text.Json.JsonDocument.Parse(snapshot);
        var r = doc.RootElement;
        return string.Join(
            " | ",
            new[] { "name", "address", "country", "taxRegistration" }
                .Select(k => r.TryGetProperty(k, out var v) ? v.GetString() : null)
                .Where(x => !string.IsNullOrWhiteSpace(x))
        );
    }

    public static byte[] Build(string title, string[] lines)
    {
        using var doc = new PdfDocument();
        doc.Info.Title = title;
        doc.Info.Creator = "Impact International";
        XGraphics? gfx = null;
        PdfPage? page = null;
        double y = 0;
        var font = new XFont(
            "Noto Sans",
            10,
            XFontStyleEx.Regular,
            new XPdfFontOptions(PdfFontEncoding.Unicode)
        );
        var heading = new XFont("Noto Sans", 22);
        void NewPage()
        {
            gfx?.Dispose();
            page = doc.AddPage();
            page.Size = PdfSharp.PageSize.A4;
            gfx = XGraphics.FromPdfPage(page);
            gfx.DrawRectangle(
                new XSolidBrush(XColor.FromArgb(236, 244, 240)),
                0,
                0,
                page.Width.Point,
                120
            );
            gfx.DrawString("IMPACT INTERNATIONAL / FINANCE", font, XBrushes.DarkGreen, new XPoint(42, 40));
            gfx.DrawString(title, heading, XBrushes.Black, new XPoint(42, 80));
            y = 145;
        }
        NewPage();
        foreach (var line in lines)
        {
            var words = line.Replace("\r", " ")
                .Replace("\n", " ")
                .Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var current = "";
            void Draw(string text)
            {
                if (y > 755)
                    NewPage();
                gfx!.DrawString(text, font, XBrushes.Black, new XPoint(42, y));
                y += 17;
            }
            foreach (var word in words)
            {
                var next = current == "" ? word : current + " " + word;
                if (gfx!.MeasureString(next, font).Width > page!.Width.Point - 84 && current != "")
                {
                    Draw(current);
                    current = word;
                }
                else
                    current = next;
            }
            if (current != "")
                Draw(current);
            y += 6;
        }
        gfx?.Dispose();
        using var stream = new MemoryStream();
        doc.Save(stream);
        return stream.ToArray();
    }
}
