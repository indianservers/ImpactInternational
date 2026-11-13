using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using static Clubs.Access;

namespace Clubs;

public class LoginChallenge
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string IdentifierHash { get; set; } = "";
    public string Identifier { get; set; } = "";
    public string Email { get; set; } = "";
    public Guid? UserId { get; set; }
    public Guid? PersonId { get; set; }
    public Guid? TenantId { get; set; }
    public string CodeProtected { get; set; } = "";
    public Guid? MailId { get; set; }
    public int Attempts { get; set; }
    public bool Used { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset ExpiresAt { get; set; }
}
public record OtpRequest(string Identifier);
public record OtpVerify(Guid ChallengeId, string Code);

public static class MemberOtp
{
    public static string Normalize(string value)
    {
        if (value.Contains('@')) return value.Trim().ToLowerInvariant();
        var digits = MembershipService.Normalize(value, "Phone");
        return digits.Length == 10 ? "91" + digits : digits;
    }
    record Target(string Email, Guid? UserId, Guid? PersonId);
    static async Task<Target?> Resolve(Db db, string identifier, Guid? tenant)
    {
        var emailLogin = identifier.Contains('@');
        var contacts = await db.PersonContacts.Where(c => c.Kind == (emailLogin ? "Email" : "Phone")
            && c.NormalizedValue == identifier && db.Memberships.Any(m => m.PersonId == c.PersonId
                && (tenant == null || m.TenantId == tenant) && m.Status == "Active" && m.Club.Status == "Active"
                && (m.EndsAt == null || m.EndsAt > DateTimeOffset.UtcNow))).Select(c => c.PersonId).Distinct().Take(2).ToListAsync();
        var users = await db.Users.Where(u => emailLogin ? u.Email == identifier : u.Phone.Replace("+", "").Replace(" ", "").Replace("-", "").Replace("(", "").Replace(")", "") == identifier).Take(2).ToListAsync();
        if (contacts.Count > 1 || users.Count > 1) return null;
        var person = contacts.Count == 1 ? await db.Persons.SingleAsync(p => p.Id == contacts[0]) : null;
        var user = users.SingleOrDefault();
        if (person?.UserId != null)
        {
            if (user != null && user.Id != person.UserId) return null;
            user = await db.Users.SingleAsync(u => u.Id == person.UserId);
        }
        if (person != null && person.UserId == null && user != null) return null; // administrator must resolve conflicting ownership
        var email = user?.Email;
        if (email == null && person != null)
        {
            var emails = await db.PersonContacts.Where(c => c.PersonId == person.Id && c.Kind == "Email")
                .Select(c => c.NormalizedValue).Distinct().Take(2).ToListAsync();
            if (emails.Count != 1) return null;
            email = emails[0];
            if (await db.Users.AnyAsync(u => u.Email == email)) return null;
            if (await db.PersonContacts.AnyAsync(c => c.Kind == "Email" && c.NormalizedValue == email && c.PersonId != person.Id)) return null;
        }
        if (string.IsNullOrEmpty(email) || !System.Net.Mail.MailAddress.TryCreate(email, out _)) return null;
        if (user != null && user.LockedUntil > DateTimeOffset.UtcNow) return null;
        if (tenant != null && person == null && (user == null || !await db.Assignments.AnyAsync(a => a.UserId == user.Id && a.TenantId == tenant))) return null;
        return user == null && person == null ? null : new(email, user?.Id, person?.Id);
    }
    public static void MapMemberOtp(this RouteGroupBuilder api)
    {
        api.MapPost("/auth/otp/request", async (OtpRequest input, Db db, Access access, IDataProtectionProvider dp, IConfiguration config, IHostEnvironment env) =>
        {
            Check((input.Identifier?.Length ?? 0) is > 2 and <= 200, "Enter your registered email or phone number.");
            Check(!string.IsNullOrWhiteSpace(config["Smtp:Host"]) || env.IsDevelopment(), "Email OTP is awaiting Amazon SES setup. Please contact your administrator.", 503);
            var identifier = Normalize(input.Identifier!);
            var key = Hash(identifier);
            await using var tx = await db.Database.BeginTransactionAsync();
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtext({key}))");
            var recent = db.LoginChallenges.Where(c => c.IdentifierHash == key && c.CreatedAt > DateTimeOffset.UtcNow.AddMinutes(-15));
            Check(!await recent.AnyAsync(c => c.CreatedAt > DateTimeOffset.UtcNow.AddSeconds(-60)) && await recent.CountAsync() < 5,
                "Please wait before requesting another code.", 429);
            var oldMailIds = db.LoginChallenges.Where(c => c.IdentifierHash == key && !c.Used).Select(c => c.MailId);
            await db.Mails.Where(m => oldMailIds.Contains(m.Id)).ExecuteUpdateAsync(s => s.SetProperty(m => m.Used, true).SetProperty(m => m.Body, ""));
            await db.LoginChallenges.Where(c => c.IdentifierHash == key && !c.Used).ExecuteUpdateAsync(s => s.SetProperty(c => c.Used, true).SetProperty(c => c.CodeProtected, ""));
            var target = await Resolve(db, identifier, access.BoundTenant);
            var code = RandomNumberGenerator.GetInt32(100000, 1000000).ToString();
            var challenge = new LoginChallenge { IdentifierHash = key, Identifier = identifier, Email = target?.Email ?? "", UserId = target?.UserId,
                PersonId = target?.PersonId, TenantId = access.BoundTenant, ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(10),
                CodeProtected = dp.CreateProtector("Impact.LoginOtp.v1").Protect(code) };
            if (target != null)
            {
                var mail = new Mail { UserId = target.UserId, Kind = "login-otp", Recipient = target.Email,
                    Subject = "Your Impact International sign-in code", TokenHash = Hash(Token()), ExpiresAt = challenge.ExpiresAt,
                    Body = dp.CreateProtector("Impact.LoginOtpMail.v1").Protect($"Your Impact International sign-in code is {code}. It expires in 10 minutes. Do not share this code.") };
                db.Mails.Add(mail); challenge.MailId = mail.Id;
            }
            db.LoginChallenges.Add(challenge);
            await db.SaveChangesAsync(); await tx.CommitAsync();
            return Results.Ok(new { challengeId = challenge.Id, expiresInSeconds = 600, resendAfterSeconds = 60,
                message = "If your email or phone matches an eligible account, a code will be sent to its registered email.",
                deliveryConfigured = !string.IsNullOrWhiteSpace(config["Smtp:Host"]) });
        }).RequireRateLimiting("auth");

        api.MapPost("/auth/otp/verify", async (OtpVerify input, Db db, Access access, MembershipService membership, IDataProtectionProvider dp, HttpContext http, IHostEnvironment env) =>
        {
            await using var tx = await db.Database.BeginTransactionAsync();
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtext({input.ChallengeId.ToString()}))");
            var c = await db.LoginChallenges.SingleOrDefaultAsync(c => c.Id == input.ChallengeId);
            if (c == null || c.Used || c.ExpiresAt <= DateTimeOffset.UtcNow || c.Attempts >= 5 || c.TenantId != access.BoundTenant)
                return Results.Json(new { message = "Invalid or expired code. Request a new code." }, statusCode: 401);
            // Serialize verification and resend/provisioning for this identity across replicas.
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtext({c.IdentifierHash}))");
            await db.Entry(c).ReloadAsync();
            if (c.Used) return Results.Json(new { message = "Code already used. Request a new code." }, statusCode: 401);
            c.Attempts++;
            if (c.Email != "") await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtext({"otp-account:" + c.Email}))");
            var correct = input.Code?.Length == 6 && CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(input.Code), Encoding.UTF8.GetBytes(dp.CreateProtector("Impact.LoginOtp.v1").Unprotect(c.CodeProtected)));
            var target = await Resolve(db, c.Identifier, access.BoundTenant);
            if (!correct || target == null || target.Email != c.Email || target.UserId != c.UserId || target.PersonId != c.PersonId)
            {
                if (c.Attempts >= 5) { c.Used = true; c.CodeProtected = ""; }
                db.AuthenticationAudits.Add(new() { ActorId = c.UserId, Action = "auth.otp", Outcome = "Denied" });
                await db.SaveChangesAsync(); await tx.CommitAsync();
                return Results.Json(new { message = "Invalid or expired code. Request a new code if needed." }, statusCode: 401);
            }
            var user = target.UserId != null ? await db.Users.SingleAsync(u => u.Id == target.UserId) : new User { Email = target.Email };
            if (target.UserId == null) db.Users.Add(user);
            user.Verified = true; user.FailedAttempts = 0; user.LockedUntil = null;
            if (target.PersonId != null)
            {
                var person = await db.Persons.SingleAsync(p => p.Id == target.PersonId);
                person.UserId = user.Id;
                await db.PersonContacts.Where(contact => contact.PersonId == person.Id && contact.Kind == "Email" && contact.NormalizedValue == target.Email)
                    .ExecuteUpdateAsync(s => s.SetProperty(contact => contact.Verified, true));
                if (string.IsNullOrEmpty(user.Name)) user.Name = person.DisplayName;
                foreach (var m in await db.Memberships.Where(m => m.PersonId == person.Id && m.Status == "Active" && m.Club.Status == "Active"
                    && (access.BoundTenant == null || m.TenantId == access.BoundTenant) && (m.EndsAt == null || m.EndsAt > DateTimeOffset.UtcNow)).ToListAsync())
                    await membership.Grant(m, user.Id);
            }
            await db.SaveChangesAsync();
            Guid? workspace = null;
            foreach (var assignment in await db.Assignments.Where(a => a.UserId == user.Id).OrderBy(a => db.Roles.Any(r => r.Id == a.RoleId && LeaderCommunications.SendingRoles.Contains(r.Name)) ? 0 : 1).ThenBy(a => a.Id).ToListAsync())
                if ((access.BoundTenant == null || assignment.TenantId == access.BoundTenant) && await access.Effective(assignment)) { workspace = assignment.Id; break; }
            c.Used = true; c.CodeProtected = "";
            await db.Mails.Where(m => m.Id == c.MailId).ExecuteUpdateAsync(s => s.SetProperty(m => m.Used, true).SetProperty(m => m.Body, ""));
            var raw = Token();
            db.Sessions.Add(new() { Id = Hash(raw), UserId = user.Id, WorkspaceId = workspace, ExpiresAt = DateTimeOffset.UtcNow.AddHours(8) });
            db.AuthenticationAudits.Add(new() { ActorId = user.Id, Action = "auth.otp" });
            await db.SaveChangesAsync(); await tx.CommitAsync();
            http.Response.Cookies.Append("clubs_session", raw, new() { HttpOnly = true, Secure = !env.IsDevelopment(), SameSite = SameSiteMode.Strict, Path = "/", MaxAge = TimeSpan.FromHours(8) });
            return Results.Ok(new { user.Name });
        }).RequireRateLimiting("auth");
    }
}
