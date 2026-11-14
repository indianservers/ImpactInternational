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
        "card.expiry_month_year",
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

    public static IdCardLayout Default() => new(
        Front: new("#f8f7f3", [
            new("navy-header", "Rect", 0, 0, 85.6f, 15, Fill: "#101e40", Locked: true),
            new("header-glow", "Ellipse", 62, -18, 45, 45, Fill: "#273a62", Opacity: .65f),
            new("gold-rule", "Rect", 0, 15, 85.6f, .7f, Fill: "#d7b568", Locked: true),
            new("logo-disc", "Ellipse", 4, 2, 11, 11, Fill: "#ffffff"),
            new("logo", "Image", 4.5f, 2.5f, 10, 10, Binding: "association.logo", Radius: 5, Locked: true),
            new("association", "Text", 18, 3, 62, 5, Binding: "association.name", Color: "#ffffff", FontSize: 3.7f, Bold: true, Locked: true),
            new("brand-line", "Text", 18, 9, 62, 3, Text: "TRAINING · BUSINESS · SERVICE · LEADERSHIP", Color: "#dfc68f", FontSize: 1.55f, Locked: true),
            new("photo-frame", "Rect", 4, 19, 23, 28, Fill: "#d7b568", Radius: 2),
            new("photo-placeholder", "Rect", 4.6f, 19.6f, 21.8f, 26.8f, Fill: "#e4e7ef", Radius: 1.5f),
            new("photo-label", "Text", 6, 29, 19, 8, Text: "IMPACT\nMEMBER", Color: "#66718b", FontSize: 2.4f, Align: "Center"),
            new("photo", "Image", 4.6f, 19.6f, 21.8f, 26.8f, Binding: "member.photo", Radius: 1.5f),
            new("identity-label", "Text", 31, 18.5f, 50, 3, Text: "MEMBERSHIP IDENTITY", Color: "#94702f", FontSize: 1.8f, Bold: true),
            new("name", "Text", 31, 22.5f, 50, 11, Binding: "member.full_name", Color: "#101e40", FontSize: 3.5f, Bold: true, Required: true),
            new("id-strip", "Rect", 31, 34, 50, 7, Fill: "#e9e3d4", Radius: 1),
            new("number", "Text", 33, 35, 46, 5, Binding: "member.member_id", Color: "#101e40", FontSize: 3, Bold: true, Required: true),
            new("category", "Text", 31, 42.5f, 50, 4, Binding: "member.membership_category", Color: "#66552f", FontSize: 2.2f),
            new("footer", "Rect", 0, 49, 85.6f, 5, Fill: "#101e40"),
            new("footer-text", "Text", 4, 50, 78, 3, Text: "CONNECTED BY PURPOSE  •  PEOPLE · SERVICE · IMPACT", Color: "#dfc68f", FontSize: 1.6f, Align: "Center")
        ]),
        Back: new("#101e40", [
            new("back-orbit", "Ellipse", 55, -20, 60, 60, Fill: "#23385e", Opacity: .7f),
            new("back-title", "Text", 5, 4, 75, 5, Text: "YOUR IMPACT. YOUR IDENTITY.", Color: "#dfc68f", FontSize: 3, Bold: true),
            new("back-rule", "Rect", 5, 11, 75, .4f, Fill: "#d7b568"),
            new("qr-frame", "Rect", 5, 16, 25, 25, Fill: "#ffffff", Radius: 2),
            new("qr", "Qr", 6.5f, 17.5f, 22, 22, Binding: "card.qr_code"),
            new("club-label", "Text", 34, 15, 46, 3, Text: "MEMBER CLUB", Color: "#dfc68f", FontSize: 1.7f),
            new("club", "Text", 34, 19, 46, 10, Binding: "member.club_name", Color: "#ffffff", FontSize: 2.4f),
            new("district", "Text", 34, 30, 46, 6, Binding: "member.district_name", Color: "#bdc9df", FontSize: 2),
            new("valid", "Text", 34, 37, 46, 4, Text: "VALID TILL  {{card.expiry_month_year}}", Color: "#dfc68f", FontSize: 2),
            new("verification", "Text", 5, 43, 76, 5, Text: "Scan to verify current status. If found, return to the issuing club.", Color: "#bdc9df", FontSize: 1.8f),
            new("serial", "Text", 5, 49, 76, 3, Binding: "card.serial_number", Color: "#8295b5", FontSize: 1.55f)
        ])
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
