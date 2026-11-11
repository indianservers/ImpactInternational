using PdfSharp.Drawing;
using PdfSharp.Pdf.IO;
using QRCoder;
using SkiaSharp;
using static Clubs.Access;

namespace Clubs;

public record IdCardPublished(IdCardLayout Design, Dictionary<string, string> Assets);

public record IdCardSnapshot(Dictionary<string, string> Values, Dictionary<string, string> Images);

public record IdCardFiles(string Pdf, string Front, string Back);

public class IdCardRenderer(CommunicationStorage storage)
{
    static readonly Lazy<SKTypeface> Typeface = new(() =>
    {
        using var font = typeof(EventFontResolver).Assembly.GetManifestResourceStream(
            "Clubs.Api.Assets.Fonts.NotoSans-Regular.ttf"
        );
        return SKTypeface.FromStream(font!)
            ?? throw new InvalidOperationException("Bundled font missing.");
    });

    public static SKBitmap Decode(byte[] bytes)
    {
        using var stream = new SKMemoryStream(bytes);
        using var codec = SKCodec.Create(stream);
        Check(
            codec != null
                && codec.Info.Width is >= 32 and <= 6000
                && codec.Info.Height is >= 32 and <= 6000
                && (long)codec.Info.Width * codec.Info.Height <= 16000000,
            "PNG/JPEG image requires 32–6000 pixels per edge and at most 16 megapixels."
        );
        return SKBitmap.Decode(bytes) ?? throw new ApiError(400, "Image decode failed.");
    }

    public byte[] Png(
        IdCardPublished t,
        IdCardSnapshot snapshot,
        bool back = false,
        bool bleed = false,
        bool preview = false
    )
    {
        var d = t.Design;
        var b = bleed ? d.Bleed : 0;
        using var image = new SKBitmap(
            (int)Math.Ceiling((d.Width + 2 * b) * 300 / 25.4),
            (int)Math.Ceiling((d.Height + 2 * b) * 300 / 25.4)
        );
        using var canvas = new SKCanvas(image);
        canvas.Clear(SKColors.White);
        canvas.Scale(300 / 25.4f);
        canvas.Translate(b, b);
        Draw(canvas, t, snapshot, back, bleed);
        if (preview)
        {
            using var paint = new SKPaint { IsAntialias = true, Color = SKColors.DarkRed };
            using var font = new SKFont(Typeface.Value, 1.8f);
            canvas.DrawText("PREVIEW · NOT ISSUED", 3, d.Height - 1, SKTextAlign.Left, font, paint);
        }
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    public void Pdf(Stream output, IdCardPublished t, IdCardSnapshot snapshot)
    {
        using var raw = new MemoryStream();
        using var doc = SKDocument.CreatePdf(raw);
        foreach (var back in t.Design.DoubleSided ? new[] { false, true } : new[] { false })
        {
            var canvas = doc.BeginPage(t.Design.Width * 72 / 25.4f, t.Design.Height * 72 / 25.4f);
            canvas.Scale(72 / 25.4f);
            Draw(canvas, t, snapshot, back);
            doc.EndPage();
        }
        doc.Close();
        ExactMediaBox(raw, output, t.Design.Width, t.Design.Height);
    }

    public void Draw(
        SKCanvas canvas,
        IdCardPublished published,
        IdCardSnapshot snapshot,
        bool back,
        bool includeBleed = false
    )
    {
        var d = published.Design;
        var side = back ? d.Back! : d.Front!;
        canvas.Save();
        var bleed = includeBleed ? d.Bleed : 0;
        canvas.ClipRect(new(-bleed, -bleed, d.Width + bleed, d.Height + bleed));
        canvas.Clear(SKColor.Parse(side.Background));
        foreach (var e in side.Elements)
        {
            if (
                e.When != ""
                && string.IsNullOrEmpty(snapshot.Values.GetValueOrDefault(e.When))
                && !snapshot.Images.ContainsKey(e.When)
            )
                continue;
            canvas.Save();
            canvas.Translate(e.X, e.Y);
            canvas.RotateDegrees(e.Rotation);
            using var paint = new SKPaint
            {
                IsAntialias = true,
                Color = SKColor.Parse(e.Fill).WithAlpha((byte)(255 * e.Opacity)),
            };
            var rect = new SKRect(0, 0, e.Width, e.Height);
            if (e.Shadow > 0)
                paint.ImageFilter = SKImageFilter.CreateDropShadow(
                    .5f,
                    .5f,
                    e.Shadow / 3,
                    e.Shadow / 3,
                    SKColors.Black.WithAlpha(50)
                );
            if (e.Kind == "Rect")
                canvas.DrawRoundRect(rect, e.Radius, e.Radius, paint);
            else if (e.Kind == "Ellipse")
                canvas.DrawOval(rect, paint);
            else if (e.Kind == "Line")
            {
                paint.Color = SKColor.Parse(e.Color).WithAlpha((byte)(255 * e.Opacity));
                paint.StrokeWidth = Math.Max(.1f, e.Border);
                canvas.DrawLine(0, 0, e.Width, e.Height, paint);
            }
            else if (e.Kind == "Text")
            {
                canvas.ClipRect(rect);
                paint.ImageFilter = null;
                paint.Color = SKColor.Parse(e.Color).WithAlpha((byte)(255 * e.Opacity));
                using var font = new SKFont(Typeface.Value, e.FontSize) { Embolden = e.Bold };
                var text = IdCardDesign.Resolve(e, snapshot.Values);
                var line = "";
                float y = e.FontSize;
                foreach (var word in text.Replace("\r", "").Replace("\n", " \n ").Split(' '))
                {
                    var next = line.Length == 0 ? word : line + " " + word;
                    if ((font.MeasureText(next) > e.Width && line.Length > 0) || word == "\n")
                    {
                        Text(line, y);
                        y += e.FontSize * 1.25f;
                        line = word == "\n" ? "" : word;
                    }
                    else
                        line = next;
                }
                Text(line, y);
                void Text(string value, float y)
                {
                    var width = font.MeasureText(value);
                    var x =
                        e.Align == "Center" ? (e.Width - width) / 2
                        : e.Align == "Right" ? e.Width - width
                        : 0;
                    canvas.DrawText(value, x, y, SKTextAlign.Left, font, paint);
                }
            }
            else if (e.Kind is "Image" or "Qr")
            {
                byte[]? bytes = null;
                if (e.Kind == "Qr")
                {
                    using var qr = QRCodeGenerator.GenerateQrCode(
                        snapshot.Values.GetValueOrDefault(
                            "card.qr_code",
                            "https://example.invalid/preview"
                        ),
                        QRCodeGenerator.ECCLevel.M
                    );
                    bytes = new PngByteQRCode(qr).GetGraphic(6);
                }
                else
                {
                    var key =
                        e.Asset != ""
                            ? published.Assets.GetValueOrDefault(e.Asset)
                            : snapshot.Images.GetValueOrDefault(e.Binding);
                    if (key != null && File.Exists(storage.PathFor(key)))
                        bytes = File.ReadAllBytes(storage.PathFor(key));
                }
                if (bytes != null)
                {
                    using var image = SKBitmap.Decode(bytes);
                    if (image != null)
                    {
                        using var round = new SKRoundRect(
                            rect,
                            e.Radius >= Math.Min(e.Width, e.Height) / 2 ? e.Width / 2 : e.Radius,
                            e.Radius >= Math.Min(e.Width, e.Height) / 2 ? e.Height / 2 : e.Radius
                        );
                        canvas.ClipRoundRect(round, SKClipOperation.Intersect, true);
                        var aspect = e.Width / e.Height;
                        var sw = Math.Min(image.Width, image.Height * aspect) / e.CropZoom;
                        var sh = sw / aspect;
                        var sx = (image.Width - sw) * e.CropX;
                        var sy = (image.Height - sh) * e.CropY;
                        canvas.DrawBitmap(
                            image,
                            new(sx, sy, sx + sw, sy + sh),
                            rect,
                            new SKSamplingOptions(SKFilterMode.Linear),
                            new SKPaint
                            {
                                IsAntialias = true,
                                Color = SKColors.White.WithAlpha((byte)(255 * e.Opacity)),
                            }
                        );
                    }
                }
            }
            if (e.Border > 0 && e.Kind != "Line")
            {
                paint.ImageFilter = null;
                paint.Style = SKPaintStyle.Stroke;
                paint.StrokeWidth = e.Border;
                paint.Color = SKColor.Parse(e.Color).WithAlpha((byte)(255 * e.Opacity));
                canvas.DrawRoundRect(rect, e.Radius, e.Radius, paint);
            }
            canvas.Restore();
        }
        canvas.Restore();
    }

    public void Print(
        Stream output,
        IdCardPublished template,
        IEnumerable<IdCardSnapshot> snapshots,
        IdCardPrintLayout preset
    )
    {
        var front = IdCardDesign.Positions(template.Design, preset, false);
        var back = IdCardDesign.Positions(template.Design, preset, true);
        using var raw = new MemoryStream();
        using var doc = SKDocument.CreatePdf(raw);
        foreach (var batch in snapshots.Chunk(front.Count))
        {
            foreach (
                var isBack in preset.Sides == "Front" ? new[] { false }
                : preset.Sides == "Back" ? new[] { true }
                : new[] { false, true }
            )
            {
                var canvas = doc.BeginPage(
                    preset.SheetWidth * 72 / 25.4f,
                    preset.SheetHeight * 72 / 25.4f
                );
                canvas.Scale(72 / 25.4f);
                canvas.Clear(SKColors.White);
                for (var i = 0; i < batch.Length; i++)
                {
                    var pos = (isBack ? back : front)[i];
                    canvas.Save();
                    var bleed = preset.IncludeBleed ? template.Design.Bleed : 0;
                    canvas.Translate(pos.X + bleed, pos.Y + bleed);
                    Draw(canvas, template, batch[i], isBack, preset.IncludeBleed);
                    canvas.Restore();
                    if (preset.CropMarks)
                    {
                        using var paint = new SKPaint { Color = SKColors.Black, StrokeWidth = .1f };
                        var x = pos.X + bleed;
                        var y = pos.Y + bleed;
                        var w = template.Design.Width;
                        var h = template.Design.Height;
                        foreach (
                            var point in new[]
                            {
                                (x, y, true, true),
                                (x + w, y, false, true),
                                (x, y + h, true, false),
                                (x + w, y + h, false, false),
                            }
                        )
                        {
                            var dx = point.Item3 ? -1 : 1;
                            var dy = point.Item4 ? -1 : 1;
                            canvas.DrawLine(
                                point.Item1 + dx * (bleed + .5f),
                                point.Item2,
                                point.Item1 + dx * (bleed + 2),
                                point.Item2,
                                paint
                            );
                            canvas.DrawLine(
                                point.Item1,
                                point.Item2 + dy * (bleed + .5f),
                                point.Item1,
                                point.Item2 + dy * (bleed + 2),
                                paint
                            );
                        }
                    }
                }
                doc.EndPage();
            }
        }
        doc.Close();
        ExactMediaBox(raw, output, preset.SheetWidth, preset.SheetHeight);
    }

    static void ExactMediaBox(MemoryStream raw, Stream output, float width, float height)
    {
        raw.Position = 0;
        using var pdf = PdfReader.Open(raw, PdfDocumentOpenMode.Modify);
        foreach (var page in pdf.Pages)
        {
            page.Width = XUnit.FromMillimeter(width);
            page.Height = XUnit.FromMillimeter(height);
        }
        pdf.Save(output, false);
    }
}
