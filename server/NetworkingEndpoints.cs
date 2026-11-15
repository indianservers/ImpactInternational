using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using static Clubs.Access;

namespace Clubs;

public record NetworkingProfileInput(string Company, string Designation, string Industry, string Profession,
    string BusinessCategory, string WorkLocation, string Website, string City, string State, string Country,
    string Networking, string[] Skills, string[] Interests, string[] Expertise, bool Visible,
    bool AllowRequests, long Version, long PrivacyVersion);
public record NetworkingRequestInput(string Message);
public record NetworkingResponseInput(string Status, string Reply = "");

public static class NetworkingEndpoints
{
    public static readonly string[] SuggestedSkills = ["Public speaking", "Sales", "Leadership", "Digital marketing", "Communication", "Training", "Business development", "Finance", "Technology", "Event management", "Mentoring"];
    public static readonly string[] SuggestedInterests = ["Public speaking", "Sales", "Entrepreneurship", "Business networking", "Community service", "Learning", "Mentoring", "Volunteering", "Technology", "Health and wellness", "Arts and culture"];

    public static async Task<Assignment> Workspace(Access a)
    {
        var w = await a.Workspace("directory.view");
        var grants = await a.Grants(w);
        Check(await a.Db.Users.AnyAsync(u => u.Id == w.UserId && u.PlatformAdmin)
            || grants.Any(p => p is "members.view" or "leadership.view" or "communications.manage")
            || await Active(a.Db, w.TenantId).AnyAsync(m => m.Person.UserId == w.UserId),
            "Networking is available to approved active members and authorized officers.", 403);
        return w;
    }
    public static IQueryable<Membership> Active(Db db, Guid tenant) => db.Memberships.Where(m => m.TenantId == tenant
        && m.Status == "Active" && (m.EndsAt == null || m.EndsAt > DateTimeOffset.UtcNow));
    static IQueryable<Person> Visible(Db db, Guid tenant) => db.Persons.Where(p => p.Privacy.DirectoryVisible
        && p.Privacy.ProfessionalVisible && Active(db, tenant).Any(m => m.PersonId == p.Id));
    static string[] Tags(string[] values) { Check(values != null && values.Length <= 30, "Use at most 30 tags per field."); var result = values.Select(v => Regex.Replace(v?.Trim() ?? "", @"\s+", " ")).Where(v => v != "").Distinct(StringComparer.OrdinalIgnoreCase).ToArray(); Check(result.All(v => v.Length <= 100), "Tags are limited to 100 characters."); return result; }
    static string Location(Person p) => p.Addresses.OrderByDescending(a => a.Type == "Office").ThenByDescending(a => a.IsPrimary).Select(a => a.City).FirstOrDefault() ?? "";
    static object MyProfile(Person? p, bool eligible) => new { eligible, personId = p?.Id, company = p?.Company ?? "", designation = p?.Designation ?? "",
        industry = p?.Industry ?? "", profession = p?.Profession ?? "", businessCategory = p?.BusinessCategory ?? "", workLocation = p?.WorkLocation ?? "", website = p?.Website ?? "",
        city = p == null ? "" : Location(p), state = p?.Addresses.OrderByDescending(a => a.Type == "Office").Select(a => a.State).FirstOrDefault() ?? "",
        country = p?.Addresses.OrderByDescending(a => a.Type == "Office").Select(a => a.Country).FirstOrDefault() ?? "", networking = p?.Networking ?? "",
        skills = p?.Tags.Where(t => t.Kind == "Skill").Select(t => t.Value).ToArray() ?? [], interests = p?.Tags.Where(t => t.Kind == "Interest").Select(t => t.Value).ToArray() ?? [],
        expertise = p?.Tags.Where(t => t.Kind == "Expertise").Select(t => t.Value).ToArray() ?? [], visible = p?.Privacy.DirectoryVisible == true && p.Privacy.ProfessionalVisible,
        allowRequests = p?.Privacy.AllowContactRequests ?? true, version = p?.Version ?? 1, privacyVersion = p?.Privacy.Version ?? 1 };

    public static void MapNetworking(this RouteGroupBuilder api)
    {
        api.MapGet("/networking/me", async (MembershipService s) => {
            var w = await Workspace(s.Access); var p = await s.People.SingleOrDefaultAsync(p => p.UserId == w.UserId);
            return MyProfile(p, await Active(s.Db, w.TenantId).AnyAsync(m => m.Person.UserId == w.UserId));
        });
        api.MapPut("/networking/me", async (NetworkingProfileInput input, MembershipService s) => {
            var w = await Workspace(s.Access);
            await using var tx = await s.Access.Mutation(w);
            Check(await Active(s.Db, w.TenantId).AnyAsync(m => m.Person.UserId == w.UserId), "Only an approved active member may publish their networking profile.", 403);
            var p = await s.People.SingleAsync(p => p.UserId == w.UserId);
            Check(p.Version == input.Version && p.Privacy.Version == input.PrivacyVersion, "Your profile or privacy settings changed. Refresh before saving.", 409);
            var text = new[] { input.Company, input.Designation, input.Industry, input.Profession, input.BusinessCategory, input.WorkLocation, input.Website, input.City, input.State, input.Country };
            Check(text.All(v => v != null && v.Length <= 200) && input.Networking is { Length: <= 1000 }, "Keep profile fields within 200 characters and networking notes within 1,000.");
            Check(input.Website == "" || Uri.TryCreate(input.Website, UriKind.Absolute, out var url) && url.Scheme is "http" or "https" && url.UserInfo == "", "Use a valid HTTP or HTTPS business website.");
            var skills = Tags(input.Skills); var interests = Tags(input.Interests); var expertise = Tags(input.Expertise);
            p.Company = input.Company.Trim(); p.Designation = input.Designation.Trim(); p.Industry = input.Industry.Trim(); p.Profession = input.Profession.Trim();
            p.BusinessCategory = input.BusinessCategory.Trim(); p.WorkLocation = input.WorkLocation.Trim(); p.Website = input.Website.Trim(); p.Networking = input.Networking.Trim();
            var address = p.Addresses.FirstOrDefault(a => a.Type == "Office");
            if (address == null) { address = new PersonAddress { PersonId = p.Id, Type = "Office" }; p.Addresses.Add(address); s.Db.PersonAddresses.Add(address); }
            address.City = input.City.Trim(); address.State = input.State.Trim(); address.Country = input.Country.Trim();
            s.Db.PersonTags.RemoveRange(p.Tags.Where(t => t.Kind is "Skill" or "Interest" or "Expertise"));
            foreach (var pair in new[] { ("Skill", skills), ("Interest", interests), ("Expertise", expertise) })
                foreach (var value in pair.Item2) s.Db.PersonTags.Add(new() { PersonId = p.Id, Kind = pair.Item1, Value = value });
            if (input.Visible) p.Privacy.DirectoryVisible = true; p.Privacy.ProfessionalVisible = input.Visible; p.Privacy.AllowContactRequests = input.AllowRequests;
            p.Version++; p.Privacy.Version++; p.UpdatedAt = DateTimeOffset.UtcNow;
            await s.Access.Log(w, "networking.profile.updated", p.Id.ToString(), GovernanceService.Pack(new { input.Visible, input.AllowRequests, skills = skills.Length, interests = interests.Length }));
            await tx.CommitAsync(); return Results.Ok(new { p.Version, privacyVersion = p.Privacy.Version });
        });
        api.MapGet("/networking/options", async (Access a) => {
            var w = await Workspace(a); var q = Visible(a.Db, w.TenantId);
            var tags = await a.Db.PersonTags.Where(t => q.Any(p => p.Id == t.PersonId)).Select(t => new { t.Kind, t.Value }).Distinct().ToListAsync();
            return new { skills = tags.Where(t => t.Kind == "Skill").Select(t => t.Value).Concat(SuggestedSkills).Distinct(StringComparer.OrdinalIgnoreCase).Order().Take(300),
                interests = tags.Where(t => t.Kind == "Interest").Select(t => t.Value).Concat(SuggestedInterests).Distinct(StringComparer.OrdinalIgnoreCase).Order().Take(300),
                industries = await q.Where(p => p.Industry != "").Select(p => p.Industry).Distinct().OrderBy(x => x).Take(200).ToListAsync(),
                cities = await a.Db.PersonAddresses.Where(ad => ad.City != "" && q.Any(p => p.Id == ad.PersonId)).Select(ad => ad.City).Distinct().OrderBy(x => x).Take(300).ToListAsync(),
                areas = await q.Where(p => p.WorkLocation != "").Select(p => p.WorkLocation).Distinct().OrderBy(x => x).Take(300).ToListAsync() };
        });
        api.MapGet("/networking/people", async (MembershipService s, string search = "", string skill = "", string interest = "", string industry = "", string city = "", string area = "", int page = 1, string sort = "matches") => {
            var w = await Workspace(s.Access); Check(new[] { search, skill, interest, industry, city, area }.All(v => v.Length <= 100) && page is >= 1 and <= 10000 && sort is "matches" or "name", "Invalid networking filters.");
            var own = await s.People.SingleOrDefaultAsync(p => p.UserId == w.UserId);
            var ownSkills = own?.Tags.Where(t => t.Kind == "Skill").Select(t => t.Value.ToLower()).ToArray() ?? [];
            var ownInterests = own?.Tags.Where(t => t.Kind == "Interest").Select(t => t.Value.ToLower()).ToArray() ?? [];
            var q = Visible(s.Db, w.TenantId).Where(p => p.UserId != w.UserId);
            if (search.Trim() != "") { var term = search.Trim().ToLower(); q = q.Where(p => p.DisplayName.ToLower().Contains(term) || p.Company.ToLower().Contains(term) || p.Profession.ToLower().Contains(term) || p.BusinessCategory.ToLower().Contains(term) || p.Networking.ToLower().Contains(term) || p.Tags.Any(t => new[] { "Skill", "Interest", "Expertise" }.Contains(t.Kind) && t.Value.ToLower().Contains(term))); }
            if (skill != "") q = q.Where(p => p.Tags.Any(t => t.Kind == "Skill" && t.Value.ToLower() == skill.ToLower()));
            if (interest != "") q = q.Where(p => p.Tags.Any(t => t.Kind == "Interest" && t.Value.ToLower() == interest.ToLower()));
            if (industry != "") q = q.Where(p => p.Industry.ToLower() == industry.ToLower());
            if (city != "") q = q.Where(p => p.Addresses.Any(a => a.City.ToLower() == city.ToLower()));
            if (area != "") q = q.Where(p => p.WorkLocation.ToLower().Contains(area.ToLower()));
            var ranked = q.Select(p => new { p.Id, p.DisplayName, p.Company, p.Designation, p.Industry, p.Profession, p.BusinessCategory, p.WorkLocation,
                city = p.Addresses.OrderByDescending(ad => ad.Type == "Office").Select(ad => ad.City).FirstOrDefault() ?? "",
                skills = p.Tags.Where(t => t.Kind == "Skill").Select(t => t.Value).ToArray(), interests = p.Tags.Where(t => t.Kind == "Interest").Select(t => t.Value).ToArray(),
                shared = p.Tags.Count(t => t.Kind == "Skill" && ownSkills.Contains(t.Value.ToLower()) || t.Kind == "Interest" && ownInterests.Contains(t.Value.ToLower())),
                canRequest = p.UserId != null && p.Privacy.AllowContactRequests, photo = p.Photo != null && p.Privacy.PhotoVisible });
            var ordered = sort == "name" ? ranked.OrderBy(p => p.DisplayName).ThenBy(p => p.Id) : ranked.OrderByDescending(p => p.shared).ThenBy(p => p.DisplayName).ThenBy(p => p.Id);
            return new { total = await q.CountAsync(), page, pageSize = 24, items = await ordered.Skip((page - 1) * 24).Take(24).ToListAsync() };
        });
        api.MapGet("/networking/people/{id:guid}", async (Guid id, MembershipService s) => {
            var w = await Workspace(s.Access); Check(await Visible(s.Db, w.TenantId).AnyAsync(p => p.Id == id), "Networking profile is unavailable.", 404);
            var p = await s.People.SingleAsync(p => p.Id == id); var clubs = await Active(s.Db, w.TenantId).Where(m => m.PersonId == id).Select(m => new { m.Club.Name }).Distinct().ToListAsync();
            return new { p.Id, name = p.DisplayName, p.Company, p.Designation, p.Industry, p.Profession, p.BusinessCategory, p.WorkLocation, p.Website, p.Networking,
                city = Location(p), state = p.Addresses.OrderByDescending(a => a.Type == "Office").Select(a => a.State).FirstOrDefault(),
                country = p.Addresses.OrderByDescending(a => a.Type == "Office").Select(a => a.Country).FirstOrDefault(), clubs,
                skills = p.Tags.Where(t => t.Kind == "Skill").Select(t => t.Value), interests = p.Tags.Where(t => t.Kind == "Interest").Select(t => t.Value), expertise = p.Tags.Where(t => t.Kind == "Expertise").Select(t => t.Value),
                email = p.Privacy.DisplayEmail ? p.Contacts.Where(c => c.Kind == "Email").Select(c => c.Value).FirstOrDefault() : null,
                phone = p.Privacy.DisplayPhone ? p.Contacts.Where(c => c.Kind == "Phone").Select(c => c.Value).FirstOrDefault() : null,
                canRequest = p.UserId != null && p.UserId != w.UserId && p.Privacy.AllowContactRequests };
        });
        api.MapGet("/networking/people/{id:guid}/photo", async (Guid id, Access a) => {
            var w = await Workspace(a);
            var photo = await Visible(a.Db, w.TenantId).Where(p => p.Id == id && p.Privacy.PhotoVisible && p.Photo != null)
                .Select(p => new { p.Photo, p.PhotoType }).SingleOrDefaultAsync();
            Check(photo != null, "Photograph is unavailable.", 404);
            return Results.File(photo.Photo!, photo.PhotoType is "image/png" or "image/jpeg" or "image/webp" ? photo.PhotoType : "application/octet-stream");
        });
        api.MapPost("/networking/people/{id:guid}/connect", async (Guid id, NetworkingRequestInput input, MembershipService s, NotificationService n) => {
            var w = await Workspace(s.Access); Check(input.Message?.Trim().Length is >= 5 and <= 1000, "Write an introduction of 5–1,000 characters.");
            await using var tx = await s.Access.Mutation(w);
            Check(await Visible(s.Db, w.TenantId).AnyAsync(p => p.UserId == w.UserId), "Publish your networking profile before sending introductions.", 403);
            var target = await Visible(s.Db, w.TenantId).Where(p => p.Id == id && p.UserId != null && p.UserId != w.UserId && p.Privacy.AllowContactRequests).SingleOrDefaultAsync();
            Check(target != null, "This member is not available for introductions.", 404);
            Check(!await s.Db.MemberContactRequests.AnyAsync(r => r.TenantId == w.TenantId && new[] { "Pending", "Accepted" }.Contains(r.Status)
                && (r.RequesterId == w.UserId && s.Db.Memberships.Any(m => m.Id == r.MembershipId && m.PersonId == id)
                    || r.RequesterId == target.UserId && s.Db.Memberships.Any(m => m.Id == r.MembershipId && m.Person.UserId == w.UserId))), "An introduction or connection already exists.", 409);
            Check(await s.Db.MemberContactRequests.CountAsync(r => r.RequesterId == w.UserId && r.At > DateTimeOffset.UtcNow.AddDays(-1)) < 20, "You can send up to 20 introductions per day.", 429);
            var member = await Active(s.Db, w.TenantId).Where(m => m.PersonId == id).OrderBy(m => m.Id).FirstAsync();
            var request = new MemberContactRequest { TenantId = w.TenantId, MembershipId = member.Id, RequesterId = w.UserId, Message = input.Message!.Trim() };
            s.Db.MemberContactRequests.Add(request);
            await n.User(target.UserId, "contact.request", "A member sent you a networking introduction.", "/app/networking/connections", w.TenantId, member.ClubId);
            await s.Access.Log(w, "networking.introduction.sent", request.Id.ToString()); await tx.CommitAsync(); return Results.Ok(new { request.Id, request.Status });
        });
        api.MapGet("/networking/connections", async (Access a, int page = 1, string view = "all") => {
            var w = await Workspace(a); Check(page is >= 1 and <= 10000 && view is "all" or "incoming" or "outgoing" or "accepted", "Invalid connection filters.");
            var q = a.Db.MemberContactRequests.Where(r => r.TenantId == w.TenantId && (r.RequesterId == w.UserId || a.Db.Memberships.Any(m => m.Id == r.MembershipId && m.Person.UserId == w.UserId)));
            if (view == "incoming") q = q.Where(r => r.RequesterId != w.UserId && r.Status == "Pending");
            if (view == "outgoing") q = q.Where(r => r.RequesterId == w.UserId);
            if (view == "accepted") q = q.Where(r => r.Status == "Accepted");
            var rows = await q.OrderByDescending(r => r.At).Skip((page - 1) * 30).Take(30).ToListAsync(); var items = new List<object>();
            foreach (var r in rows) {
                var recipient = await a.Db.Memberships.Where(m => m.Id == r.MembershipId).Select(m => new { m.PersonId, m.Person.UserId }).SingleAsync();
                var incoming = r.RequesterId != w.UserId;
                var personId = incoming ? await a.Db.Persons.Where(p => p.UserId == r.RequesterId).Select(p => (Guid?)p.Id).SingleOrDefaultAsync() : recipient.PersonId;
                var profile = await Visible(a.Db, w.TenantId).Where(p => p.Id == personId).Select(p => new { p.Id, name = p.DisplayName, p.Company, p.WorkLocation }).SingleOrDefaultAsync();
                items.Add(new { r.Id, r.Message, r.Status, r.At, incoming, profile, canRespond = incoming && r.Status == "Pending" && await Active(a.Db, w.TenantId).AnyAsync(m => m.Id == r.MembershipId && m.Person.UserId == w.UserId) });
            }
            return new { total = await q.CountAsync(), page, pageSize = 30, items };
        });
        api.MapPost("/networking/connections/{id:guid}/respond", async (Guid id, NetworkingResponseInput input, MembershipService s, NotificationService n) => {
            var w = await Workspace(s.Access); Check(input.Status is "Accepted" or "Declined" && input.Reply is { Length: <= 1000 }, "Choose accept or decline and a reply of at most 1,000 characters.");
            await using var tx = await s.Access.Mutation(w);
            var request = await s.Db.MemberContactRequests.SingleOrDefaultAsync(r => r.Id == id && r.TenantId == w.TenantId && Active(s.Db, w.TenantId).Any(m => m.Id == r.MembershipId && m.Person.UserId == w.UserId));
            Check(request != null, "Introduction unavailable.", 404); Check(request.Status == "Pending", "This introduction was already reviewed.", 409);
            request.Status = input.Status;
            await n.User(request.RequesterId, "contact.response", "Your networking introduction was " + input.Status.ToLowerInvariant() + "." + (input.Status == "Accepted" && input.Reply != "" ? " " + input.Reply : ""), "/app/networking/connections", w.TenantId);
            await s.Access.Log(w, "networking.introduction." + input.Status.ToLowerInvariant(), id.ToString()); await tx.CommitAsync(); return Results.NoContent();
        });
    }
}
