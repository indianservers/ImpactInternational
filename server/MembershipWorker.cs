using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using static Clubs.Access;

namespace Clubs;

public class MembershipWorker(IServiceScopeFactory scopes, ILogger<MembershipWorker> log)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                using var scope = scopes.CreateScope();
                var s = scope.ServiceProvider.GetRequiredService<MembershipService>();
                var db = s.Db;
                await Scheduled(s, token);
                await Imports(scope.ServiceProvider, token);
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                log.LogError("Membership worker cycle failed ({Type})", e.GetType().Name);
            }
            await Task.Delay(TimeSpan.FromSeconds(3), token);
        }
    }

    private async Task Scheduled(MembershipService s, CancellationToken token)
    {
        var transfers = await s
            .Db.MembershipTransfers.Where(x =>
                x.Status == "Scheduled" && x.EffectiveAt <= DateTimeOffset.UtcNow
            )
            .Select(x => x.Id)
            .Take(10)
            .ToListAsync(token);
        foreach (var id in transfers)
        {
            await using var tx = await s.Db.Database.BeginTransactionAsync(token);
            var tenant = await s
                .Db.MembershipTransfers.Where(x => x.Id == id)
                .Select(x => x.TenantId)
                .SingleAsync(token);
            await s.Lock(tenant);
            await s.Lock(id);
            var t = await s.Db.MembershipTransfers.SingleAsync(x => x.Id == id, token);
            if (t.Status != "Scheduled")
            {
                await tx.RollbackAsync(token);
                continue;
            }
            try
            {
                await MembershipLifecycleEndpoints.CompleteTransfer(t, s, Guid.Empty);
                await s.Db.SaveChangesAsync(token);
                await tx.CommitAsync(token);
            }
            catch (ApiError)
            {
                await tx.RollbackAsync(token);
                s.Db.ChangeTracker.Clear();
                await s
                    .Db.MembershipTransfers.Where(x => x.Id == id && x.Status == "Scheduled")
                    .ExecuteUpdateAsync(
                        u =>
                            u.SetProperty(x => x.Status, "Blocked")
                                .SetProperty(x => x.Version, x => x.Version + 1),
                        token
                    );
            }
            s.Db.ChangeTracker.Clear();
        }
        var expired = await s
            .Db.Memberships.Where(x =>
                new[] { "Active", "Inactive" }.Contains(x.Status)
                && x.EndsAt != null
                && x.EndsAt <= DateTimeOffset.UtcNow
            )
            .Select(x => x.Id)
            .Take(100)
            .ToListAsync(token);
        foreach (var id in expired)
        {
            await using var tx = await s.Db.Database.BeginTransactionAsync(token);
            var tenant = await s
                .Db.Memberships.Where(x => x.Id == id)
                .Select(x => x.TenantId)
                .SingleAsync(token);
            await s.Lock(tenant);
            await s.Lock(id);
            var m = await s
                .Db.Memberships.Include(x => x.Person)
                .SingleAsync(x => x.Id == id, token);
            if (
                new[] { "Active", "Inactive" }.Contains(m.Status)
                && m.EndsAt <= DateTimeOffset.UtcNow
            )
            {
                await s.ChangeStatus(m, "Expired", "Validity period ended", Guid.Empty);
                await s.Db.SaveChangesAsync(token);
            }
            await tx.CommitAsync(token);
            s.Db.ChangeTracker.Clear();
        }
    }

    private async Task Imports(IServiceProvider sp, CancellationToken token)
    {
        var s = sp.GetRequiredService<MembershipService>();
        MemberImportJob? job;
        var lease = Guid.NewGuid();
        await using (var tx = await s.Db.Database.BeginTransactionAsync(token))
        {
            job = await s
                .Db.MemberImportJobs.FromSqlRaw(
                    "SELECT * FROM \"MemberImportJobs\" WHERE \"Status\" IN ('Validating','Queued','Importing') AND (\"LeaseUntil\" IS NULL OR \"LeaseUntil\"<now()) ORDER BY \"CreatedAt\" FOR UPDATE SKIP LOCKED LIMIT 1"
                )
                .SingleOrDefaultAsync(token);
            if (job == null)
                return;
            job.LeaseId = lease;
            job.LeaseUntil = DateTimeOffset.UtcNow.AddMinutes(2);
            if (job.Status == "Queued")
                job.Status = "Importing";
            await s.Db.SaveChangesAsync(token);
            await tx.CommitAsync(token);
        }
        var id = job.Id;
        try
        {
            if (job.Status == "Validating")
                await Validate(id, lease, token);
            else
                await Commit(id, lease, token);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            using var fresh = scopes.CreateScope();
            var db = fresh.ServiceProvider.GetRequiredService<Db>();
            var message =
                e is ApiError
                    ? e.Message
                    : "Import processing failed ("
                        + e.GetType().Name
                        + "). The atomic transaction was rolled back; review input and retry.";
            await db
                .MemberImportJobs.Where(x =>
                    x.Id == id
                    && x.LeaseId == lease
                    && x.Status != "Cancelled"
                    && x.Status != "Completed"
                )
                .ExecuteUpdateAsync(
                    u =>
                        u.SetProperty(x => x.Status, "Failed")
                            .SetProperty(x => x.Error, message)
                            .SetProperty(x => x.LeaseUntil, (DateTimeOffset?)null),
                    token
                );
            log.LogWarning("Import {JobId} failed ({Type})", id, e.GetType().Name);
        }
    }

    private async Task<Assignment> Authorized(MemberImportJob job, MembershipService s)
    {
        var w = await s.Db.Assignments.SingleOrDefaultAsync(x =>
            x.Id == job.ScopeAssignmentId && x.UserId == job.CreatorId && x.TenantId == job.TenantId
        );
        Check(
            w != null
                && await s.Access.Effective(w)
                && (await s.Access.Grants(w)).Contains("members.import"),
            "The importing officer no longer holds the required scoped permission.",
            403
        );
        Check(
            await s.Access.Visible(w).AnyAsync(x => x.Id == job.ClubId),
            "The import club is no longer authorized.",
            403
        );
        return w;
    }

    private async Task Validate(Guid id, Guid lease, CancellationToken token)
    {
        using var read = scopes.CreateScope();
        var s = read.ServiceProvider.GetRequiredService<MembershipService>();
        var job = await s.Db.MemberImportJobs.AsNoTracking().SingleAsync(x => x.Id == id, token);
        var w = await Authorized(job, s);
        var table = MembershipImport.Parse(job.Payload, job.FileName);
        var mapping = JsonSerializer.Deserialize<Dictionary<string, string>>(job.Mapping)!;
        Check(table.Rows.Count > 0, "There are no data rows to import.");
        var data = table
            .Rows.Select(row => MembershipImport.Values(table.Headers, row, mapping))
            .ToList();
        var codes = data.Select(x => x.GetValueOrDefault("clubCode", "").ToUpperInvariant())
            .Where(x => x != "")
            .Distinct()
            .ToArray();
        var clubs = await s
            .Access.Visible(w)
            .Where(x => x.Id == job.ClubId || codes.Contains(x.Code))
            .Where(x => x.Status == "Active" && s.Db.Levels.Any(l => l.Id == x.LevelId && l.IsClub))
            .Select(x => new { x.Id, x.Code })
            .ToListAsync(token);
        var categories = await s
            .Db.MembershipCategories.Where(x => x.TenantId == job.TenantId && x.Active)
            .Select(x => new { x.Id, x.Code })
            .ToListAsync(token);
        var contacts = data.SelectMany(x =>
                new[]
                {
                    MembershipService.Normalize(x.GetValueOrDefault("email", ""), "Email"),
                    MembershipService.Normalize(x.GetValueOrDefault("phone", ""), "Phone"),
                }
            )
            .Where(x => x != "")
            .Distinct()
            .ToArray();
        var visiblePeople = s.Scoped(w).Select(x => x.PersonId);
        var duplicateContacts = (
            await s
                .Db.PersonContacts.Where(x =>
                    contacts.Contains(x.NormalizedValue) && visiblePeople.Contains(x.PersonId)
                )
                .Select(x => x.NormalizedValue)
                .Distinct()
                .ToListAsync(token)
        ).ToHashSet();
        var names = data.Select(x => x.GetValueOrDefault("name", "").ToLowerInvariant())
            .Distinct()
            .ToArray();
        var duplicateNames = (
            await s
                .Db.Persons.Where(x =>
                    visiblePeople.Contains(x.Id) && names.Contains(x.LegalName.ToLower())
                )
                .Select(x => x.LegalName.ToLower())
                .Distinct()
                .ToListAsync(token)
        ).ToHashSet();
        var fileContacts = new HashSet<string>();
        var fileNames = new HashSet<string>();
        var validated = new List<MemberImportRow>();
        for (int i = 0; i < data.Count; i++)
        {
            var v = data[i];
            var row = new MemberImportRow
            {
                TenantId = job.TenantId,
                JobId = id,
                RowNumber = i + 1,
            };
            try
            {
                s.Validate(MembershipImport.Person(v));
                var clubCode = v.GetValueOrDefault("clubCode", "").ToUpperInvariant();
                var club =
                    clubCode == ""
                        ? clubs.SingleOrDefault(x => x.Id == job.ClubId)
                        : clubs.SingleOrDefault(x => x.Code == clubCode);
                Check(club != null, "Club code is unknown or outside authorized scope.");
                var catCode = v.GetValueOrDefault("categoryCode", "").ToUpperInvariant();
                var category =
                    catCode == ""
                        ? categories.SingleOrDefault(x => x.Id == job.CategoryId)
                        : categories.SingleOrDefault(x => x.Code == catCode);
                Check(category != null, "Unknown category code.");
                var date = v.GetValueOrDefault("joinedAt", "");
                if (date != "")
                    Check(
                        DateTimeOffset.TryParse(date, out var joined)
                            && joined <= DateTimeOffset.UtcNow
                            && joined.Year >= 1900,
                        "Joining date must be a past ISO date."
                    );
                v["clubId"] = club.Id.ToString();
                v["categoryId"] = category.Id.ToString();
                var name = v.GetValueOrDefault("name", "").ToLowerInvariant();
                var email = MembershipService.Normalize(v.GetValueOrDefault("email", ""), "Email");
                var phone = MembershipService.Normalize(v.GetValueOrDefault("phone", ""), "Phone");
                var duplicate =
                    duplicateNames.Contains(name)
                    || fileNames.Contains(name)
                    || (
                        email != ""
                        && (duplicateContacts.Contains(email) || fileContacts.Contains(email))
                    )
                    || (
                        phone != ""
                        && (duplicateContacts.Contains(phone) || fileContacts.Contains(phone))
                    );
                if (duplicate)
                {
                    row.Status = "NeedsReview";
                    row.Error =
                        "Potential duplicate/shared contact. Explicitly choose a separate identity or an authorized existing person.";
                }
                fileNames.Add(name);
                if (email != "")
                    fileContacts.Add(email);
                if (phone != "")
                    fileContacts.Add(phone);
            }
            catch (ApiError e)
            {
                row.Status = "Invalid";
                row.Error = e.Message;
            }
            row.Data = JsonSerializer.Serialize(v);
            validated.Add(row);
        }
        for (int start = job.ProcessedRows; start < validated.Count; start += 200)
        {
            using var batch = scopes.CreateScope();
            var db = batch.ServiceProvider.GetRequiredService<Db>();
            await using var tx = await db.Database.BeginTransactionAsync(token);
            var current = await db.MemberImportJobs.SingleAsync(x => x.Id == id, token);
            if (current.Status != "Validating" || current.LeaseId != lease)
                return;
            var rows = validated.Skip(start).Take(200).ToList();
            db.MemberImportRows.AddRange(rows);
            current.ProcessedRows = start + rows.Count;
            current.TotalRows = validated.Count;
            current.LeaseUntil = DateTimeOffset.UtcNow.AddMinutes(2);
            if (current.ProcessedRows == validated.Count)
            {
                current.Status =
                    validated.Any(x => x.Status == "Invalid") ? "NeedsReview"
                    : validated.Any(x => x.Status == "NeedsReview") ? "NeedsReview"
                    : "Ready";
                current.LeaseUntil = null;
            }
            await db.SaveChangesAsync(token);
            await tx.CommitAsync(token);
        }
    }

    private async Task Commit(Guid id, Guid lease, CancellationToken token)
    {
        using var scope = scopes.CreateScope();
        var s = scope.ServiceProvider.GetRequiredService<MembershipService>();
        var db = s.Db;
        await using var tx = await db.Database.BeginTransactionAsync(token);
        var job = await db
            .MemberImportJobs.FromSqlInterpolated(
                $"SELECT * FROM \"MemberImportJobs\" WHERE \"Id\"={id} FOR UPDATE"
            )
            .SingleAsync(token);
        Check(
            job.Status == "Importing" && job.LeaseId == lease,
            "Import lease is no longer valid.",
            409
        );
        var w = await Authorized(job, s);
        await s.Lock(job.TenantId);
        var rows = await db
            .MemberImportRows.AsNoTracking()
            .Where(x => x.JobId == id)
            .OrderBy(x => x.RowNumber)
            .ToListAsync(token);
        Check(
            rows.Count > 0 && rows.All(x => x.Status == "Valid"),
            "Import rows are not fully validated."
        );
        var values = rows.Select(x =>
                JsonSerializer.Deserialize<Dictionary<string, string>>(x.Data)!
            )
            .ToList();
        var clubIds = values.Select(v => Guid.Parse(v["clubId"])).Distinct().ToArray();
        var categoryIds = values.Select(v => Guid.Parse(v["categoryId"])).Distinct().ToArray();
        var clubs = await s
            .Access.Visible(w)
            .Where(x =>
                clubIds.Contains(x.Id)
                && x.Status == "Active"
                && db.Levels.Any(l => l.Id == x.LevelId && l.IsClub)
            )
            .ToDictionaryAsync(x => x.Id, token);
        Check(
            clubs.Count == clubIds.Length,
            "Some target clubs are no longer authorized or active.",
            403
        );
        var categories = await db
            .MembershipCategories.Where(x =>
                x.TenantId == job.TenantId && x.Active && categoryIds.Contains(x.Id)
            )
            .ToDictionaryAsync(x => x.Id, token);
        Check(categories.Count == categoryIds.Length, "Some categories are no longer active.");
        var existingIds = rows.Where(x => x.ExistingPersonId != null)
            .Select(x => x.ExistingPersonId!.Value)
            .Distinct()
            .ToArray();
        var people = await db
            .Persons.Where(x =>
                existingIds.Contains(x.Id) && s.Scoped(w).Any(m => m.PersonId == x.Id)
            )
            .Select(x => new Person
            {
                Id = x.Id,
                UserId = x.UserId,
                LegalName = x.LegalName,
                DisplayName = x.DisplayName,
            })
            .ToDictionaryAsync(x => x.Id, token);
        Check(
            people.Count == existingIds.Length,
            "An existing-person resolution is no longer authorized.",
            403
        );
        var policy = await db.MembershipPolicies.SingleAsync(
            x => x.TenantId == job.TenantId,
            token
        );
        var affiliations = await db
            .Memberships.Where(x =>
                x.TenantId == job.TenantId
                && existingIds.Contains(x.PersonId)
                && new[] { "Active", "Inactive", "Suspended", "Expired" }.Contains(x.Status)
            )
            .Select(x => new
            {
                x.PersonId,
                x.ClubId,
                x.CategoryId,
            })
            .ToListAsync(token);
        var keys = affiliations.Select(x => (x.PersonId, x.ClubId, x.CategoryId)).ToHashSet();
        var attached = affiliations.Select(x => (x.PersonId, x.ClubId)).ToHashSet();
        var numbers = await db
            .Database.SqlQuery<long>(
                $"SELECT nextval('membership_numbers') AS \"Value\" FROM generate_series(1,{rows.Count})"
            )
            .ToArrayAsync(token);
        var users = people
            .Values.Where(x => x.UserId != null)
            .Select(x => x.UserId!.Value)
            .ToArray();
        var role = await db.Roles.SingleOrDefaultAsync(
            x => x.TenantId == job.TenantId && x.Name == "Club Member",
            token
        );
        if (role == null)
        {
            role = new()
            {
                TenantId = job.TenantId,
                Name = "Club Member",
                Permissions = ["organization.view", "directory.view"],
            };
            db.Roles.Add(role);
        }
        var assignments = (
            await db
                .Assignments.Where(x =>
                    x.TenantId == job.TenantId && users.Contains(x.UserId) && x.RoleId == role.Id
                )
                .Select(x => new { x.UserId, x.OrgId })
                .ToListAsync(token)
        )
            .Select(x => (x.UserId, x.OrgId))
            .ToHashSet();
        for (int i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            var v = values[i];
            var person =
                row.ExistingPersonId == null
                    ? s.NewPerson(MembershipImport.Person(v))
                    : people[row.ExistingPersonId.Value];
            var club = clubs[Guid.Parse(v["clubId"])];
            var category = categories[Guid.Parse(v["categoryId"])];
            Check(
                keys.Add((person.Id, club.Id, category.Id)),
                "Import would duplicate a current club/category affiliation.",
                409
            );
            if (!policy.AllowMultipleClubs)
                Check(
                    !attached.Any(x => x.PersonId == person.Id && x.ClubId != club.Id),
                    "Import violates the association's single-club policy.",
                    409
                );
            attached.Add((person.Id, club.Id));
            var joined = string.IsNullOrEmpty(v.GetValueOrDefault("joinedAt"))
                ? DateTimeOffset.UtcNow
                : DateTimeOffset.Parse(v["joinedAt"]);
            var m = new Membership
            {
                TenantId = job.TenantId,
                PersonId = person.Id,
                ClubId = club.Id,
                CategoryId = category.Id,
                Number = "CS-" + numbers[i].ToString("D9"),
                JoinedAt = joined,
                EndsAt =
                    category.ValidityMonths == null
                        ? null
                        : joined.AddMonths(category.ValidityMonths.Value),
                ImportRowId = row.Id,
            };
            if (m.EndsAt < DateTimeOffset.UtcNow)
                m.Status = "Expired";
            db.Memberships.Add(m);
            s.History(m, "", m.Status, "Reviewed bulk import", job.CreatorId, joined);
            s.Card(m);
            s.Activity(m, job.CreatorId, "membership.imported", job.Id.ToString());
            if (
                person.UserId != null
                && m.Status == "Active"
                && assignments.Add((person.UserId.Value, club.Id))
            )
                db.Assignments.Add(
                    new()
                    {
                        TenantId = job.TenantId,
                        UserId = person.UserId.Value,
                        RoleId = role.Id,
                        OrgId = club.Id,
                        MembershipId = m.Id,
                    }
                );
            db.MemberImportRows.Attach(row);
            row.Status = "Imported";
            if ((i + 1) % 200 == 0)
            {
                await db.SaveChangesAsync(token);
                db.ChangeTracker.Clear();
            }
        }
        if (db.Entry(job).State == EntityState.Detached)
            db.MemberImportJobs.Attach(job);
        job.Status = "Completed";
        job.ImportedRows = rows.Count;
        job.CompletedAt = DateTimeOffset.UtcNow;
        job.Payload = [];
        job.LeaseUntil = null;
        await scope
            .ServiceProvider.GetRequiredService<NotificationService>()
            .User(
                job.CreatorId,
                "import.completed",
                rows.Count + " reviewed membership rows were imported atomically.",
                "/app/imports",
                job.TenantId,
                job.ClubId
            );
        await Authorized(job, s); // Recheck real effective dates before committing a potentially long import.
        await s.Access.Log(w, "member.import.completed", job.Id.ToString(), rows.Count.ToString());
        await tx.CommitAsync(token);
    }
}
