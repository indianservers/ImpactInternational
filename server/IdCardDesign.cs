using System.Text.RegularExpressions;
using static Clubs.Access;

namespace Clubs;

public record IdCardField(
    string Key,
    string Source,
    string Type,
    string Scope,
    string Privacy,
    string Fallback,
    string[] Formats
);

public record IdCardElement(
    string Id,
    string Kind,
    float X = 4,
    float Y = 4,
    float Width = 35,
    float Height = 8,
    string Text = "",
    string Binding = "",
    string Color = "#173c42",
    string Fill = "#ffffff",
    float FontSize = 3.5f,
    float Rotation = 0,
    float Opacity = 1,
    float Border = 0,
    float Radius = 0,
    string Align = "Left",
    bool Locked = false,
    string Group = "",
    string Asset = "",
    float CropX = .5f,
    float CropY = .5f,
    float CropZoom = 1,
    bool Required = false,
    string When = "",
    bool Bold = false,
    float Shadow = 0
);

public record IdCardSide(string Background, IdCardElement[] Elements);

public record IdCardLayout(
    float Width = 85.6f,
    float Height = 53.98f,
    float Bleed = 3,
    float Safe = 3,
    bool DoubleSided = true,
    IdCardSide? Front = null,
    IdCardSide? Back = null,
    Guid? CategoryId = null,
    Guid? PositionId = null,
    Guid? ParentVersionId = null
);

public record IdCardPrintLayout(
    float SheetWidth = 210,
    float SheetHeight = 297,
    float Margin = 10,
    float GapX = 4,
    float GapY = 4,
    string Sides = "Duplex",
    string Flip = "Long",
    float OffsetX = 0,
    float OffsetY = 0,
    bool CropMarks = true,
    bool IncludeBleed = false
);

public record IdCardPlacement(int Index, int Row, int Column, float X, float Y);

public static class IdCardDesign
{
    public static readonly string[] Permissions =
    [
        "cards.design",
        "cards.publish",
        "cards.issue",
        "cards.bulk",
        "cards.settings",
    ];
    public static readonly string[] TextKeys =
    [
        "member.full_name",
        "member.member_id",
        "member.membership_category",
        "member.joining_date",
        "member.valid_until",
        "member.current_designation",
        "member.club_name",
        "member.club_code",
        "member.district_name",
        "member.national_organization",
        "association.name",
        "card.issue_date",
        "card.expiry_date",
        "card.serial_number",
    ];
    public static readonly string[] ImageKeys =
    [
        "member.photo",
        "member.club_logo",
        "association.logo",
        "card.qr_code",
    ];
    public static readonly IdCardField[] Fields = TextKeys
        .Concat(ImageKeys)
        .Select(k => new IdCardField(
            k,
            k.StartsWith("member")
                    ? "Authorized membership/person/current leadership and configured hierarchy"
                : k.StartsWith("association") ? "Owning tenant branding"
                : "Immutable issuance snapshot",
            ImageKeys.Contains(k) ? "Image"
                : k.Contains("date") || k.Contains("until") ? "Date"
                : "Text",
            "Active tenant and authorized owning membership",
            k == "member.photo" ? "Private photograph"
                : k.StartsWith("member") ? "Private card identity"
                : "Card / association metadata",
            k == "member.member_id" || k == "member.full_name"
                ? "Issuance blocked when missing"
                : "Empty; required elements block issuance",
            ["Default"]
        ))
        .ToArray();

    public static IdCardLayout Default() =>
        new(
            Front: new(
                "#f5f7f3",
                [
                    new("band", "Rect", 0, 0, 85.6f, 12, Fill: "#174f46"),
                    new(
                        "association",
                        "Text",
                        5,
                        4,
                        75,
                        5,
                        Text: "Impact International",
                        Binding: "association.name",
                        Color: "#ffffff",
                        FontSize: 3.5f,
                        Locked: true
                    ),
                    new("photo", "Image", 5, 17, 22, 27, Binding: "member.photo", Radius: 2),
                    new(
                        "name",
                        "Text",
                        32,
                        18,
                        49,
                        12,
                        Binding: "member.full_name",
                        FontSize: 4.1f,
                        Required: true,
                        Bold: true
                    ),
                    new(
                        "number",
                        "Text",
                        32,
                        32,
                        47,
                        6,
                        Binding: "member.member_id",
                        FontSize: 3.2f,
                        Required: true
                    ),
                    new("club", "Text", 32, 40, 47, 7, Binding: "member.club_name", FontSize: 2.6f),
                ]
            ),
            Back: new(
                "#174f46",
                [
                    new("qr", "Qr", 6, 9, 30, 30, Binding: "card.qr_code"),
                    new(
                        "title",
                        "Text",
                        42,
                        11,
                        38,
                        8,
                        Text: "CONNECTED BY PURPOSE",
                        Color: "#ffffff",
                        FontSize: 3.2f
                    ),
                    new(
                        "valid",
                        "Text",
                        42,
                        23,
                        38,
                        7,
                        Text: "Valid until {{card.expiry_date}}",
                        Color: "#ffffff",
                        FontSize: 2.8f
                    ),
                    new(
                        "return",
                        "Text",
                        6,
                        43,
                        73,
                        8,
                        Text: "If found, return to the issuing organization. Verify this card using its secure QR code.",
                        Color: "#ffffff",
                        FontSize: 2.3f
                    ),
                ]
            )
        );

    public static void Validate(IdCardLayout d)
    {
        Check(
            float.IsFinite(d.Width)
                && float.IsFinite(d.Height)
                && d.Width is >= 20 and <= 200
                && d.Height is >= 20 and <= 200
                && d.Bleed is >= 0 and <= 10
                && d.Safe is >= 0 and <= 15
                && d.Front != null
                && d.Back != null,
            "Invalid physical card dimensions or sides."
        );
        foreach (var side in new[] { d.Front!, d.Back! })
        {
            Check(
                Color(side.Background) && side.Elements.Length <= 100,
                "Invalid background or more than 100 elements per side."
            );
            Check(
                side.Elements.Select(e => e.Id).Distinct().Count() == side.Elements.Length,
                "Duplicate layer IDs."
            );
            foreach (var e in side.Elements)
            {
                Check(
                    e.Id.Length is > 0 and <= 80
                        && new[] { "Text", "Rect", "Ellipse", "Image", "Qr", "Line" }.Contains(
                            e.Kind
                        )
                        && new[]
                        {
                            e.X,
                            e.Y,
                            e.Width,
                            e.Height,
                            e.FontSize,
                            e.Rotation,
                            e.Opacity,
                            e.Border,
                            e.Radius,
                            e.CropX,
                            e.CropY,
                            e.CropZoom,
                            e.Shadow,
                        }.All(float.IsFinite),
                    "Invalid element type or numeric value."
                );
                Check(
                    e.Width is > 0 and <= 400
                        && e.Height is > 0 and <= 400
                        && Math.Abs(e.X) <= 400
                        && Math.Abs(e.Y) <= 400
                        && e.FontSize is >= 1 and <= 30
                        && e.Rotation is >= -360 and <= 360
                        && e.Opacity is >= 0 and <= 1
                        && e.Border is >= 0 and <= 10
                        && e.Radius is >= 0 and <= 100
                        && e.CropZoom is >= 1 and <= 10
                        && e.CropX is >= 0 and <= 1
                        && e.CropY is >= 0 and <= 1
                        && e.Shadow is >= 0 and <= 10
                        && Color(e.Color)
                        && Color(e.Fill),
                    "Invalid element geometry, crop or styling."
                );
                Check(
                    e.Text.Length <= 2000
                        && e.Group.Length <= 80
                        && new[] { "Left", "Center", "Right" }.Contains(e.Align)
                        && (e.Binding == "" || TextKeys.Concat(ImageKeys).Contains(e.Binding))
                        && (e.When == "" || TextKeys.Concat(ImageKeys).Contains(e.When))
                        && (e.Asset == "" || Guid.TryParse(e.Asset, out _)),
                    "Invalid field binding, asset or text. Executable content is not supported."
                );
                foreach (Match m in Regex.Matches(e.Text, @"\{\{(.*?)\}\}"))
                    Check(TextKeys.Contains(m.Groups[1].Value), "Unknown or private placeholder.");
                Check(
                    e.Binding == ""
                        || e.Kind == "Text" && TextKeys.Contains(e.Binding)
                        || e.Kind == "Image" && ImageKeys.Contains(e.Binding)
                        || e.Kind == "Qr" && e.Binding == "card.qr_code",
                    "Binding type does not match element."
                );
            }
        }
    }

    public static void Publishable(IdCardLayout d)
    {
        Validate(d);
        var enabled = d.DoubleSided ? new[] { d.Front!, d.Back! } : new[] { d.Front! };
        Check(
            enabled.Any(side => side.Elements.Any(e => e.Kind == "Qr")),
            "Add a secure verification QR to an enabled side before publishing."
        );
        foreach (var side in enabled)
            for (var n = 0; n < side.Elements.Length; n++)
            {
                var q = side.Elements[n];
                if (q.Kind != "Qr")
                    continue;
                Check(
                    q.Binding == "card.qr_code"
                        && q.Width >= 20
                        && Math.Abs(q.Width - q.Height) < .01
                        && q.X >= 0
                        && q.Y >= 0
                        && q.X + q.Width <= d.Width
                        && q.Y + q.Height <= d.Height
                        && q.Rotation == 0
                        && q.Opacity == 1
                        && q.Radius == 0
                        && q.CropZoom == 1
                        && q.When == "",
                    "Secure QR requires an unobscured square of at least 20mm, inside the card, with no crop, rotation, conditional hiding or opacity."
                );
                foreach (var e in side.Elements.Skip(n + 1))
                {
                    var angle = e.Rotation * Math.PI / 180;
                    var points = new[]
                    {
                        (0f, 0f),
                        (e.Width, 0f),
                        (0f, e.Height),
                        (e.Width, e.Height),
                    }
                        .Select(p =>
                            (
                                X: e.X + p.Item1 * Math.Cos(angle) - p.Item2 * Math.Sin(angle),
                                Y: e.Y + p.Item1 * Math.Sin(angle) + p.Item2 * Math.Cos(angle)
                            )
                        )
                        .ToArray();
                    Check(
                        e.Opacity == 0
                            || points.Max(p => p.X) <= q.X
                            || points.Min(p => p.X) >= q.X + q.Width
                            || points.Max(p => p.Y) <= q.Y
                            || points.Min(p => p.Y) >= q.Y + q.Height,
                        "Move the QR above any overlapping layers so its quiet zone remains readable."
                    );
                }
            }
    }

    public static bool Color(string color) => Regex.IsMatch(color, "^#[0-9a-fA-F]{6}$");

    public static string Resolve(IdCardElement e, IReadOnlyDictionary<string, string> values) =>
        e.Binding != ""
            ? values.GetValueOrDefault(e.Binding, "")
            : Regex.Replace(
                e.Text,
                @"\{\{(.*?)\}\}",
                m => values.GetValueOrDefault(m.Groups[1].Value, "")
            );

    public static List<IdCardPlacement> Positions(IdCardLayout d, IdCardPrintLayout p, bool back)
    {
        Check(
            p.SheetWidth is >= 50 and <= 600
                && p.SheetHeight is >= 50 and <= 600
                && p.Margin is >= 0 and <= 50
                && p.GapX is >= 0 and <= 30
                && p.GapY is >= 0 and <= 30
                && Math.Abs(p.OffsetX) <= 20
                && Math.Abs(p.OffsetY) <= 20
                && new[] { "Front", "Back", "Both", "Duplex" }.Contains(p.Sides)
                && new[] { "Long", "Short" }.Contains(p.Flip),
            "Invalid print preset."
        );
        var bleed = p.IncludeBleed ? d.Bleed : 0;
        var width = d.Width + 2 * bleed;
        var height = d.Height + 2 * bleed;
        var cols = (int)((p.SheetWidth - 2 * p.Margin + p.GapX) / (width + p.GapX));
        var rows = (int)((p.SheetHeight - 2 * p.Margin + p.GapY) / (height + p.GapY));
        Check(
            cols > 0 && rows > 0 && cols * rows <= 100,
            "No cards fit this sheet, or more than 100 slots."
        );
        var mirrorX = p.SheetHeight >= p.SheetWidth ? p.Flip == "Long" : p.Flip == "Short";
        var result = new List<IdCardPlacement>();
        for (int r = 0; r < rows; r++)
        for (int c = 0; c < cols; c++)
        {
            var x = p.Margin + c * (width + p.GapX);
            var y = p.Margin + r * (height + p.GapY);
            if (back && p.Sides == "Duplex")
            {
                if (mirrorX)
                    x = p.SheetWidth - x - width;
                else
                    y = p.SheetHeight - y - height;
            }
            result.Add(
                new(result.Count, r, c, x + (back ? p.OffsetX : 0), y + (back ? p.OffsetY : 0))
            );
        }
        Check(
            result.All(s =>
                s.X >= 0 && s.Y >= 0 && s.X + width <= p.SheetWidth && s.Y + height <= p.SheetHeight
            ),
            "Calibration moves cards off the printable sheet."
        );
        return result;
    }
}
