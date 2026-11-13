using System.Text.Json;
using System.Text.RegularExpressions;
using static Clubs.Access;

namespace Clubs;

public static class SocialProfiles
{
    static readonly Dictionary<string, (string Prefix, string[] Hosts)> Platforms = new()
    {
        ["LinkedIn"] = ("https://www.linkedin.com/in/", ["linkedin.com"]),
        ["Facebook"] = ("https://www.facebook.com/", ["facebook.com"]),
        ["Instagram"] = ("https://www.instagram.com/", ["instagram.com"]),
        ["X"] = ("https://x.com/", ["x.com", "twitter.com"]),
        ["YouTube"] = ("https://www.youtube.com/@", ["youtube.com", "youtu.be"]),
        ["Threads"] = ("https://www.threads.com/@", ["threads.com", "threads.net"]),
        ["Telegram"] = ("https://t.me/", ["t.me"]),
    };
    public static Dictionary<string, string> Normalize(Dictionary<string, string>? input)
    {
        Check((input?.Count ?? 0) <= Platforms.Count, "Too many social media profiles.");
        var result = new Dictionary<string, string>();
        foreach (var (platform, value) in input ?? [])
        {
            Check(Platforms.ContainsKey(platform), "Unsupported social media platform.");
            var text = (value ?? "").Trim();
            if (text == "") continue;
            Check(text.Length <= 500, "Social media links must be at most 500 characters.");
            var rule = Platforms[platform];
            if (!text.Contains("://"))
            {
                var handle = text.TrimStart('@');
                Check(Regex.IsMatch(handle, "^[A-Za-z0-9_.-]{1,100}$"), "Enter a valid social handle or full HTTPS profile link.");
                text = rule.Prefix + handle;
            }
            Check(Uri.TryCreate(text, UriKind.Absolute, out var uri) && uri.Scheme == "https" && uri.UserInfo == ""
                && rule.Hosts.Any(host => uri.Host == host || uri.Host.EndsWith("." + host, StringComparison.OrdinalIgnoreCase)),
                "Use an HTTPS profile link for the selected social media platform.");
            result[platform] = text;
        }
        return result;
    }
    public static Dictionary<string, string> Read(string json) => string.IsNullOrWhiteSpace(json) ? [] : JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? [];
}
