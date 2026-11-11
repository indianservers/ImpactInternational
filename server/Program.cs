using System.Threading.RateLimiting;
using Clubs;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using static Clubs.Access;

PdfSharp.Fonts.GlobalFontSettings.FontResolver = new EventFontResolver();
var b = WebApplication.CreateBuilder(args);
b.Logging.ClearProviders();
b.Logging.AddJsonConsole();
b.Logging.AddFilter("Microsoft.AspNetCore.Hosting.Diagnostics", LogLevel.Warning);
b.Logging.AddFilter("Microsoft.EntityFrameworkCore.Database.Command", LogLevel.Warning);
b.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = 12582912);
if (
    !b.Environment.IsDevelopment()
    && string.IsNullOrEmpty(b.Configuration.GetConnectionString("Database"))
)
    throw new InvalidOperationException("Production requires ConnectionStrings__Database.");
var protection = b.Services.AddDataProtection().SetApplicationName("ClubSphere");
if (b.Configuration["DataProtection:Path"] is string keyPath)
    protection.PersistKeysToFileSystem(new DirectoryInfo(keyPath));

b.Services.AddDbContext<Db>(o =>
    o.UseNpgsql(
        b.Configuration.GetConnectionString("Database")
            ?? "Host=localhost;Port=55432;Database=clubs;Username=clubs",
        n => n.CommandTimeout(15)
    )
);
b.Services.AddHostedService<MailWorker>();
b.Services.AddHostedService<MembershipWorker>();
b.Services.AddHostedService<MaintenanceWorker>();
b.Services.AddHttpContextAccessor();
b.Services.AddScoped<Access>();
b.Services.AddScoped<MembershipService>();
b.Services.AddScoped<NotificationService>();
b.Services.AddScoped<GovernanceService>();
b.Services.AddScoped<EventService>();
b.Services.AddScoped<OperationsService>();
b.Services.AddHostedService<OperationsWorker>();
b.Services.AddScoped<CommunicationService>();
b.Services.AddSingleton<CommunicationStorage>();
b.Services.AddHttpClient("communications", c => c.Timeout = TimeSpan.FromSeconds(20));
b.Services.AddHttpClient("communication-media", c => c.Timeout = TimeSpan.FromSeconds(20))
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
b.Services.AddTransient<MetaCommunicationProvider>();
b.Services.AddTransient<TwilioCommunicationProvider>();
b.Services.AddTransient<ICommunicationProvider, MetaCommunicationProvider>();
b.Services.AddTransient<ICommunicationProvider, TwilioCommunicationProvider>();
b.Services.AddHostedService<CommunicationWorker>();
b.Services.AddHostedService<CommunicationMediaWorker>();
b.Services.AddHostedService<EventWorker>();
b.Services.AddHostedService<GovernanceWorker>();
b.Services.AddSingleton<IPasswordHasher<User>, PasswordHasher<User>>();
b.Services.AddAntiforgery(o => o.HeaderName = "X-CSRF");
b.Services.AddOpenApi();
b.Services.AddProblemDetails();
b.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = 429;
    o.AddPolicy(
        "auth",
        c =>
            RateLimitPartition.GetFixedWindowLimiter(
                c.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new() { PermitLimit = 30, Window = TimeSpan.FromMinutes(1) }
            )
    );
});
b.Services.AddRateLimiter(o =>
    o.AddPolicy(
        "cards",
        c =>
            RateLimitPartition.GetFixedWindowLimiter(
                c.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new() { PermitLimit = 60, Window = TimeSpan.FromMinutes(1) }
            )
    )
);
b.Services.AddRateLimiter(o =>
    o.AddPolicy(
        "events",
        c =>
            RateLimitPartition.GetFixedWindowLimiter(
                c.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new() { PermitLimit = 6000, Window = TimeSpan.FromMinutes(1) }
            )
    )
);
var app = b.Build();
app.UseWebSockets(new() { KeepAliveInterval = TimeSpan.FromSeconds(20) });
app.UseExceptionHandler();
app.UseRateLimiter();
if (!app.Environment.IsDevelopment())
    app.UseHsts();
app.Use(
    async (c, next) =>
    {
        var elapsed = System.Diagnostics.Stopwatch.StartNew();
        var bodyLimit =
            c.Request.Path.Value?.Contains("/imports") == true
            || c.Request.Path.Value?.EndsWith("/documents") == true
            || c.Request.Path.Value?.EndsWith("/photo") == true
            || c.Request.Path.Value?.EndsWith("/attachments") == true
                ? 12582912
                : 65536;
        var bodyFeature =
            c.Features.Get<Microsoft.AspNetCore.Http.Features.IHttpMaxRequestBodySizeFeature>();
        if (bodyFeature is { IsReadOnly: false })
            bodyFeature.MaxRequestBodySize = bodyLimit;
        c.Response.Headers["X-Content-Type-Options"] = "nosniff";
        c.Response.Headers["X-Frame-Options"] = "DENY";
        c.Response.Headers["Referrer-Policy"] = "no-referrer";
        c.Response.Headers["X-Correlation-ID"] = c.TraceIdentifier;
        try
        {
            if (
                c.Request.Path.StartsWithSegments("/api")
                && !HttpMethods.IsGet(c.Request.Method)
                && !c.Request.Path.StartsWithSegments("/api/v1/communications/webhooks")
                && c.Request.Path != "/api/v1/communications/unsubscribe"
            )
                await c.RequestServices.GetRequiredService<IAntiforgery>().ValidateRequestAsync(c);
            await next();
        }
        catch (AntiforgeryValidationException)
        {
            c.Response.StatusCode = 400;
            await c.Response.WriteAsJsonAsync(
                new { detail = "Invalid CSRF token. Refresh and retry." }
            );
        }
        catch (BadHttpRequestException)
        {
            c.Response.StatusCode = 400;
            await c.Response.WriteAsJsonAsync(
                new { detail = "Invalid request data.", status = 400 }
            );
        }
        catch (System.Text.Json.JsonException)
        {
            c.Response.StatusCode = 400;
            await c.Response.WriteAsJsonAsync(
                new { detail = "Invalid structured input.", status = 400 }
            );
        }
        catch (ApiError e)
        {
            if (e.Status is 401 or 403)
                await DenialAudit.Record(app.Services, c);
            c.Response.StatusCode = e.Status;
            await c.Response.WriteAsJsonAsync(new { detail = e.Message, status = e.Status });
        }
        catch (DbUpdateConcurrencyException)
        {
            c.Response.StatusCode = 409;
            await c.Response.WriteAsJsonAsync(
                new { detail = "Record changed. Refresh before saving." }
            );
        }
        catch (DbUpdateException error)
        {
            var postgres = error.InnerException as Npgsql.PostgresException;
            app.Logger.LogWarning(
                "Write rejected by database constraint {Constraint} ({SqlState})",
                postgres?.ConstraintName,
                postgres?.SqlState
            );
            c.Response.StatusCode = 409;
            await c.Response.WriteAsJsonAsync(
                new { detail = "A unique code or relationship constraint was violated." }
            );
        }
        finally
        {
            app.Logger.LogInformation(
                "HTTP {Method} {Path} returned {Status} in {ElapsedMs} ms ({CorrelationId})",
                c.Request.Method,
                c.Request.Path.StartsWithSegments("/api/v1/cards/verify")
                    ? "/api/v1/cards/verify/[redacted]"
                    : c.Request.Path.Value,
                c.Response.StatusCode,
                elapsed.Elapsed.TotalMilliseconds,
                c.TraceIdentifier
            );
        }
    }
);
app.MapOpenApi();
var api = app.MapGroup("/api/v1");
api.MapMembership();
api.MapMembershipWorkflows();
api.MapGovernance();
api.MapEvents();
api.MapCommunications();
api.MapOperations();
api.MapMembershipLifecycle();
api.MapMemberMedia();
api.MapMemberImports();
api.MapGet(
    "/auth/csrf",
    (HttpContext c, IAntiforgery a) => new { token = a.GetAndStoreTokens(c).RequestToken }
);
api.MapPost(
        "/auth/register",
        async (Register r, Db db, IPasswordHasher<User> h) =>
        {
            Check(
                r.Agreed
                    && (r.Name?.Trim().Length ?? 0) >= 2
                    && System.Net.Mail.MailAddress.TryCreate(r.Email, out _)
                    && (r.Password?.Length ?? 0) >= 12
                    && r.Password == r.Confirmation,
                "Name, valid email, agreement and matching 12-character passwords required."
            );
            var email = (r.Email ?? "").Trim().ToLowerInvariant();
            if (!await db.Users.AnyAsync(x => x.Email == email))
            {
                var u = new User
                {
                    Email = email,
                    Name = r.Name!.Trim(),
                    Phone = r.Phone,
                };
                u.PasswordHash = h.HashPassword(u, r.Password!);
                db.Users.Add(u);
                Queue(db, u, "verify");
                await db.SaveChangesAsync();
            }
            return Results.Accepted(
                value: new { message = "If eligible, verification instructions have been queued." }
            );
        }
    )
    .RequireRateLimiting("auth");
api.MapPost(
        "/auth/login",
        async (Login r, Db db, IPasswordHasher<User> h, HttpContext c) =>
        {
            var u = await db.Users.SingleOrDefaultAsync(x =>
                x.Email == (r.Email ?? "").Trim().ToLowerInvariant()
            );
            var valid =
                h.VerifyHashedPassword(u ?? new(), u?.PasswordHash ?? Dummy, r.Password ?? "")
                != PasswordVerificationResult.Failed;
            if (u == null || !valid || !u.Verified || u.LockedUntil > DateTimeOffset.UtcNow)
            {
                if (u != null)
                {
                    await db
                        .Users.Where(x => x.Id == u.Id)
                        .ExecuteUpdateAsync(update =>
                            update
                                .SetProperty(x => x.FailedAttempts, x => x.FailedAttempts + 1)
                                .SetProperty(
                                    x => x.LockedUntil,
                                    x =>
                                        x.FailedAttempts >= 4
                                            ? DateTimeOffset.UtcNow.AddMinutes(15)
                                            : x.LockedUntil
                                )
                        );
                }
                db.AuthenticationAudits.Add(
                    new()
                    {
                        ActorId = u?.Id,
                        Action = "auth.login",
                        Outcome = "Denied",
                    }
                );
                await db.SaveChangesAsync();
                throw new ApiError(401, "Unable to sign in with these credentials.");
            }
            u.FailedAttempts = 0;
            u.LockedUntil = null;
            var raw = Token();
            db.Sessions.Add(
                new()
                {
                    Id = Hash(raw),
                    UserId = u.Id,
                    ExpiresAt = DateTimeOffset.UtcNow.AddHours(8),
                }
            );
            db.AuthenticationAudits.Add(new() { ActorId = u.Id, Action = "auth.login" });
            await db.SaveChangesAsync();
            c.Response.Cookies.Append(
                "clubs_session",
                raw,
                new()
                {
                    HttpOnly = true,
                    Secure = !app.Environment.IsDevelopment(),
                    SameSite = SameSiteMode.Strict,
                    Path = "/",
                    MaxAge = TimeSpan.FromHours(8),
                }
            );
            return Results.Ok(new { u.Name });
        }
    )
    .RequireRateLimiting("auth");
api.MapPost(
        "/auth/resend-verification",
        async (EmailRequest r, Db db) =>
        {
            var user = await db.Users.SingleOrDefaultAsync(x =>
                x.Email == (r.Email ?? "").Trim().ToLowerInvariant()
            );
            if (user != null && !user.Verified)
            {
                Queue(db, user, "verify");
                await db.SaveChangesAsync();
            }
            return Results.Accepted(
                value: new
                {
                    message = "If the account needs verification, new instructions have been queued.",
                }
            );
        }
    )
    .RequireRateLimiting("auth");
api.MapPost(
        "/auth/forgot-password",
        async (EmailRequest r, Db db) =>
        {
            var u = await db.Users.SingleOrDefaultAsync(x =>
                x.Email == (r.Email ?? "").Trim().ToLowerInvariant()
            );
            if (u != null)
            {
                Queue(db, u, "reset");
                await db.SaveChangesAsync();
            }
            return Results.Accepted(
                value: new
                {
                    message = "If the account exists, recovery instructions have been queued.",
                }
            );
        }
    )
    .RequireRateLimiting("auth");
api.MapPost(
        "/auth/complete",
        async (Complete r, Db db, IPasswordHasher<User> h) =>
        {
            await using var tx = await db.Database.BeginTransactionAsync();
            var hash = Hash(r.Token);
            var m = await db.Mails.SingleOrDefaultAsync(x =>
                x.TokenHash == hash && !x.Used && x.ExpiresAt > DateTimeOffset.UtcNow
            );
            Check(m != null, "Invalid or expired link.");
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT pg_advisory_xact_lock(hashtext({m.UserId.ToString()}))"
            );
            Check(
                await db
                    .Mails.Where(x => x.Id == m!.Id && !x.Used)
                    .ExecuteUpdateAsync(s =>
                        s.SetProperty(x => x.Used, true).SetProperty(x => x.Link, "")
                    ) == 1,
                "Link already used."
            );
            await db
                .Mails.Where(x => x.UserId == m!.UserId && x.Kind == m.Kind && !x.Used)
                .ExecuteUpdateAsync(s =>
                    s.SetProperty(x => x.Used, true).SetProperty(x => x.Link, "")
                );
            var u = await db.Users.SingleAsync(x => x.Id == m!.UserId);
            if (m!.Kind == "verify")
                u.Verified = true;
            else
            {
                Check((r.Password?.Length ?? 0) >= 12, "Use at least 12 characters.");
                u.PasswordHash = h.HashPassword(u, r.Password!);
                await db.Sessions.Where(x => x.UserId == u.Id).ExecuteDeleteAsync();
            }
            await db.SaveChangesAsync();
            await tx.CommitAsync();
            return Results.Ok(new { message = "Account updated. You may sign in." });
        }
    )
    .RequireRateLimiting("auth");
api.MapGet(
    "/auth/me",
    async (Access a) =>
    {
        var u = await a.User();
        var session = await a.Session();
        return new
        {
            u.Id,
            u.Name,
            u.Email,
            u.Phone,
            u.PlatformAdmin,
            u.Locale,
            u.Theme,
            u.TimeZone,
            session.WorkspaceId,
        };
    }
);
api.MapPost(
    "/auth/logout",
    async (Access a, HttpContext c) =>
    {
        a.Db.Sessions.Remove(await a.Session());
        await a.Db.SaveChangesAsync();
        c.Response.Cookies.Delete("clubs_session");
        return Results.NoContent();
    }
);
api.MapGet(
    "/auth/sessions",
    async (Access a) =>
    {
        var u = await a.User();
        var s = await a.Session();
        return await a
            .Db.Sessions.Where(x => x.UserId == u.Id)
            .Select(x => new
            {
                x.Id,
                x.CreatedAt,
                x.ExpiresAt,
                Current = x.Id == s.Id,
            })
            .ToListAsync();
    }
);
api.MapDelete(
    "/auth/sessions/{id}",
    async (string id, Access a) =>
    {
        var u = await a.User();
        await a.Db.Sessions.Where(x => x.UserId == u.Id && x.Id == id).ExecuteDeleteAsync();
        return Results.NoContent();
    }
);
api.MapPut(
    "/users/profile",
    async (Profile r, Access a) =>
    {
        Check((r.Name?.Trim().Length ?? 0) >= 2, "Name required.");
        var u = await a.User();
        u.Name = r.Name!;
        u.Phone = r.Phone;
        Check(new[] { "en", "hi" }.Contains(r.Locale), "Unsupported language preference.");
        try
        {
            TimeZoneInfo.FindSystemTimeZoneById(r.TimeZone);
        }
        catch
        {
            throw new ApiError(400, "Invalid time zone.");
        }
        Check(r.Theme is "light" or "dark", "Invalid theme.");
        u.Theme = r.Theme;
        u.Locale = r.Locale;
        u.TimeZone = r.TimeZone;
        await a.Db.SaveChangesAsync();
        return Results.NoContent();
    }
);
api.MapPost(
    "/auth/change-password",
    async (ChangePassword r, Access a, IPasswordHasher<User> h) =>
    {
        var u = await a.User();
        Check(
            (r.Password?.Length ?? 0) >= 12
                && h.VerifyHashedPassword(u, u.PasswordHash, r.Current)
                    != PasswordVerificationResult.Failed,
            "Invalid current password or new password too short."
        );
        u.PasswordHash = h.HashPassword(u, r.Password!);
        var s = await a.Session();
        await a.Db.Sessions.Where(x => x.UserId == u.Id && x.Id != s.Id).ExecuteDeleteAsync();
        await a.Db.SaveChangesAsync();
        return Results.NoContent();
    }
);
api.MapGet(
    "/workspaces",
    async (Access a) =>
    {
        var u = await a.User();
        var data = await (
            from x in a.Db.Assignments
            join o in a.Db.Orgs on x.OrgId equals o.Id
            join t in a.Db.Tenants on x.TenantId equals t.Id
            join r in a.Db.Roles on x.RoleId equals r.Id
            where x.UserId == u.Id
            select new
            {
                x.Id,
                x.OrgId,
                x.TenantId,
                Organization = o.Name,
                Association = t.Name,
                t.Color,
                RoleEntity = r,
            }
        ).ToListAsync();
        return data.Select(x => new
        {
            x.Id,
            x.OrgId,
            x.TenantId,
            x.Organization,
            x.Association,
            x.Color,
            Role = x.RoleEntity.Name,
            x.RoleEntity.Permissions,
        });
    }
);
api.MapPost(
    "/workspaces/{id:guid}/activate",
    async (Guid id, Access a) =>
    {
        var u = await a.User();
        Check(
            await a.Db.Assignments.AnyAsync(x => x.Id == id && x.UserId == u.Id),
            "Workspace denied.",
            403
        );
        (await a.Session()).WorkspaceId = id;
        await a.Db.SaveChangesAsync();
        return Results.NoContent();
    }
);
api.MapGet(
    "/dashboard",
    async (Access a) =>
    {
        var w = await a.Workspace("organization.view");
        var q = a.Visible(w);
        return new
        {
            Organizations = await q.CountAsync(),
            Clubs = await q.CountAsync(o => a.Db.Levels.Any(l => l.Id == o.LevelId && l.IsClub)),
            Pending = await q.CountAsync(x => x.Status == "Pending"),
            Recent = await a
                .Db.Audits.Where(x =>
                    x.TenantId == w.TenantId && x.OrgId != null && q.Any(o => o.Id == x.OrgId)
                )
                .OrderByDescending(x => x.At)
                .Take(6)
                .ToListAsync(),
        };
    }
);
api.MapGet(
    "/organizations",
    async (
        Access a,
        string? search,
        Guid? parentId,
        Guid? levelId,
        string? status,
        int page = 1,
        int size = 20,
        bool clubs = false,
        string sort = "name"
    ) =>
    {
        var w = await a.Workspace("organization.view");
        var q = a.Visible(w);
        if (clubs)
            q = q.Where(o => a.Db.Levels.Any(l => l.Id == o.LevelId && l.IsClub));
        if (parentId != null)
            q = q.Where(x => x.ParentId == parentId);
        if (levelId != null)
            q = q.Where(x => x.LevelId == levelId);
        if (!string.IsNullOrWhiteSpace(search))
            q = q.Where(x =>
                EF.Functions.ILike(x.Name, "%" + search + "%")
                || EF.Functions.ILike(x.Code, "%" + search + "%")
            );
        if (!string.IsNullOrEmpty(status))
            q = q.Where(x => x.Status == status);
        size = Math.Clamp(size, 1, 100);
        page = Math.Max(1, page);
        var total = await q.CountAsync();
        var ordered = sort == "code" ? q.OrderBy(x => x.Code) : q.OrderBy(x => x.Name);
        return Results.Ok(
            new
            {
                total,
                page,
                size,
                items = await ordered
                    .ThenBy(x => x.Id)
                    .Skip((page - 1) * size)
                    .Take(size)
                    .Select(x => new
                    {
                        x.Id,
                        x.Name,
                        x.Code,
                        x.ParentId,
                        x.LevelId,
                        x.City,
                        x.Country,
                        x.Status,
                        x.CharterDate,
                        x.Version,
                        Level = a
                            .Db.Levels.Where(l => l.Id == x.LevelId)
                            .Select(l => l.Name)
                            .First(),
                        Parent = a
                            .Db.Orgs.Where(p => p.Id == x.ParentId)
                            .Select(p => p.Name)
                            .FirstOrDefault(),
                        HasChildren = a.Db.Orgs.Any(p => p.ParentId == x.Id),
                    })
                    .ToListAsync(),
            }
        );
    }
);
api.MapGet(
    "/organizations/{id:guid}",
    async (Guid id, Access a) =>
    {
        var w = await a.Workspace("organization.view");
        return await a.Visible(w).AsNoTracking().SingleOrDefaultAsync(x => x.Id == id)
            ?? throw new ApiError(404, "Organization not found.");
    }
);
api.MapPost(
    "/organizations",
    async (Org r, Access a, GovernanceService g) => await Operations.Save(r, null, a, g)
);
api.MapPut(
    "/organizations/{id:guid}",
    async (Guid id, Org r, Access a, GovernanceService g) => await Operations.Save(r, id, a, g)
);
api.MapGet(
    "/levels",
    async (Access a) =>
    {
        var w = await a.Workspace("organization.view");
        return await a.Db.Levels.Where(x => x.TenantId == w.TenantId).ToListAsync();
    }
);
api.MapPost(
    "/levels",
    async (Level r, Access a) =>
    {
        var w = await a.TenantWorkspace("settings.manage");
        Check((r.Name?.Trim().Length ?? 0) > 0, "Name required.");
        if (r.ParentLevelId != null)
            Check(
                await a.Db.Levels.AnyAsync(x =>
                    x.Id == r.ParentLevelId && x.TenantId == w.TenantId
                ),
                "Invalid parent level."
            );
        r.Id = Guid.NewGuid();
        r.TenantId = w.TenantId;
        a.Db.Levels.Add(r);
        await a.Log(w, "level.create", r.Name!);
        return Results.Ok(r);
    }
);
api.MapGet(
    "/roles",
    async (Access a) =>
    {
        var w = await a.Workspace("roles.view");
        return await a.Db.Roles.Where(x => x.TenantId == w.TenantId).ToListAsync();
    }
);
api.MapGet(
    "/permissions",
    async (Access a) =>
    {
        await a.Workspace("roles.view");
        return All;
    }
);
api.MapPost(
    "/roles",
    async (Role r, Access a) =>
    {
        var w = await a.TenantWorkspace("roles.manage");
        var held = await a.Grants(w);
        Check(
            (r.Name?.Trim().Length ?? 0) > 0
                && r.Permissions.All(held.Contains)
                && r.Permissions.All(All.Contains),
            "Invalid role or excessive permissions.",
            403
        );
        r.Id = Guid.NewGuid();
        r.TenantId = w.TenantId;
        a.Db.Roles.Add(r);
        await a.Log(w, "role.create", r.Name!);
        return Results.Ok(r);
    }
);
api.MapPut(
    "/roles/{id:guid}",
    async (Guid id, Role r, Access a) =>
    {
        var w = await a.TenantWorkspace("roles.manage");
        await using var tx = await a.Mutation(w);
        var held = await a.Grants(w);
        Check(
            r.Permissions.All(held.Contains) && r.Permissions.All(All.Contains),
            "Excessive permissions.",
            403
        );
        var role = await a.Db.Roles.SingleOrDefaultAsync(x =>
            x.Id == id && x.TenantId == w.TenantId
        );
        Check(role != null, "Role missing.", 404);
        Check(
            !await a.Db.Assignments.AnyAsync(x => x.RoleId == id),
            "Assigned roles are immutable; create a replacement role."
        );
        role!.Name = r.Name;
        foreach (
            var grant in role.Grants.Where(x => !r.Permissions.Contains(x.PermissionId)).ToList()
        )
            role.Grants.Remove(grant);
        foreach (
            var permission in r.Permissions.Where(x => !role.Grants.Any(g => g.PermissionId == x))
        )
        {
            var added = new RolePermission { RoleId = role.Id, PermissionId = permission };
            role.Grants.Add(added);
            a.Db.RolePermissions.Add(added);
        }
        await a.Log(w, "role.update", r.Name);
        await tx.CommitAsync();
        return Results.NoContent();
    }
);
api.MapGet(
    "/assignments",
    async (Access a, Guid? roleId = null) =>
    {
        var w = await a.Workspace("roles.view");
        return await (
            from x in a.Db.Assignments
            join u in a.Db.Users on x.UserId equals u.Id
            join r in a.Db.Roles on x.RoleId equals r.Id
            join o in a.Visible(w) on x.OrgId equals o.Id
            where x.TenantId == w.TenantId && (roleId == null || x.RoleId == roleId)
            select new
            {
                x.Id,
                u.Email,
                User = u.Name,
                Role = r.Name,
                Organization = o.Name,
            }
        )
            .Take(100)
            .ToListAsync();
    }
);
api.MapPost(
    "/assignments",
    async (AssignRequest r, Access a) =>
    {
        var w = await a.Workspace("roles.assign");
        await using var tx = await a.Mutation(w);
        Check(await a.Visible(w).AnyAsync(x => x.Id == r.OrgId), "Scope denied.", 403);
        var role = await a.Db.Roles.SingleOrDefaultAsync(x =>
            x.Id == r.RoleId && x.TenantId == w.TenantId
        );
        var held = await a.Grants(w);
        Check(
            role != null && role.Permissions.All(held.Contains),
            "Role exceeds your permissions.",
            403
        );
        var u = await a.Db.Users.SingleOrDefaultAsync(x => x.Email == r.Email.ToLowerInvariant());
        Check(u != null && u.Verified, "Verified account required.");
        a.Db.Assignments.Add(
            new()
            {
                UserId = u!.Id,
                TenantId = w.TenantId,
                RoleId = r.RoleId,
                OrgId = r.OrgId,
            }
        );
        await a.Log(w, "role.assign", r.OrgId.ToString());
        await tx.CommitAsync();
        return Results.NoContent();
    }
);
api.MapGet(
    "/settings",
    async (Access a) =>
    {
        var w = await a.Workspace("settings.view");
        return await a.Db.Tenants.SingleAsync(x => x.Id == w.TenantId);
    }
);
api.MapPut(
    "/settings",
    async (Tenant r, Access a) =>
    {
        var w = await a.TenantWorkspace("settings.manage");
        Check(
            (r.Name?.Trim().Length ?? 0) > 0 && (r.Currency?.Length ?? 0) == 3,
            "Name and ISO currency required."
        );
        try
        {
            TimeZoneInfo.FindSystemTimeZoneById(r.TimeZone);
        }
        catch
        {
            throw new ApiError(400, "Invalid time zone.");
        }
        var t = await a.Db.Tenants.SingleAsync(x => x.Id == w.TenantId);
        t.Name = r.Name!;
        t.Currency = r.Currency!;
        t.TimeZone = r.TimeZone;
        t.Language = r.Language;
        Check(
            System.Text.RegularExpressions.Regex.IsMatch(r.Color ?? "", "^#[0-9a-fA-F]{6}$"),
            "Invalid brand color."
        );
        t.Color = r.Color!;
        await a.Log(w, "settings.update", t.Name);
        return Results.NoContent();
    }
);
api.MapGet(
    "/audit-logs",
    async (Access a, string? search, int page = 1) =>
    {
        var w = await a.Workspace("audit.view");
        var q = a.Db.Audits.Where(x =>
            x.TenantId == w.TenantId && x.OrgId != null && a.Visible(w).Any(o => o.Id == x.OrgId)
        );
        if (!string.IsNullOrEmpty(search))
            q = q.Where(x => x.Action.Contains(search));
        return new
        {
            total = await q.CountAsync(),
            items = await q.OrderByDescending(x => x.At)
                .Skip((Math.Max(1, page) - 1) * 20)
                .Take(20)
                .Select(x => new
                {
                    x.Id,
                    x.Action,
                    x.Resource,
                    x.At,
                    x.Outcome,
                    x.OrgId,
                    x.ActorId,
                    x.Changes,
                    Actor = a
                        .Db.Users.Where(u => u.Id == x.ActorId)
                        .Select(u => u.Name)
                        .FirstOrDefault(),
                    Organization = a
                        .Db.Orgs.Where(o => o.Id == x.OrgId)
                        .Select(o => o.Name)
                        .FirstOrDefault(),
                })
                .ToListAsync(),
        };
    }
);
api.MapPost(
    "/associations",
    async (AssociationRequest r, Access a) => Results.Ok(await Operations.Provision(r.Name, a))
);
api.MapPost(
    "/registration-requests/{id:guid}/provision",
    async (Guid id, Access a) =>
    {
        Check((await a.User()).PlatformAdmin, "Platform administrator required.", 403);
        await using var tx = await a.Db.Database.BeginTransactionAsync();
        await a.Db.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock(hashtext({id.ToString()}))"
        );
        var request = await a.Db.RegistrationRequests.SingleOrDefaultAsync(x => x.Id == id);
        Check(request != null, "Request not found.", 404);
        Check(request.Status == "Pending", "Request already processed.", 409);
        var tenant = await Operations.Provision(request.Name, a);
        request.Status = "Provisioned";
        await a.Db.SaveChangesAsync();
        await tx.CommitAsync();
        return Results.Ok(tenant);
    }
);
api.MapDelete(
    "/assignments/{id:guid}",
    async (Guid id, Access a) =>
    {
        var w = await a.Workspace("roles.assign");
        await using var tx = await a.Mutation(w);
        var assignment = await a.Db.Assignments.SingleOrDefaultAsync(x =>
            x.Id == id && x.TenantId == w.TenantId
        );
        Check(assignment != null, "Assignment not found.", 404);
        Check(
            assignment.UserId != w.UserId,
            "Use another administrator to revoke your own access."
        );
        Check(await a.Visible(w).AnyAsync(x => x.Id == assignment.OrgId), "Scope denied.", 403);
        var grants = await a.Grants(w);
        Check(
            (await a.Grants(assignment)).All(grants.Contains),
            "Cannot revoke access exceeding your own permissions.",
            403
        );
        a.Db.Assignments.Remove(assignment);
        await a.Log(w, "role.revoke", id.ToString());
        await tx.CommitAsync();
        return Results.NoContent();
    }
);

if (app.Environment.IsDevelopment())
    api.MapGet(
        "/development/mail",
        async (Db db) =>
            await db
                .Mails.Where(x =>
                    !x.Used
                    && x.SentAt == null
                    && x.Link != ""
                    && x.ExpiresAt > DateTimeOffset.UtcNow
                )
                .Select(x => new
                {
                    x.Kind,
                    x.Link,
                    Email = db.Users.Where(u => u.Id == x.UserId)
                        .Select(u => u.Email)
                        .FirstOrDefault()
                        ?? x.Recipient,
                })
                .ToListAsync()
    );
api.MapPost(
        "/registration-requests",
        async (RegistrationRequest r, Db db) =>
        {
            Check(
                r.Name.Length is >= 3 and <= 200
                    && System.Net.Mail.MailAddress.TryCreate(r.Email, out _)
                    && (r.Description?.Length ?? 0) <= 2000,
                "Provide an organization name, valid email, and short description."
            );
            r.Id = Guid.NewGuid();
            r.Status = "Pending";
            r.At = DateTimeOffset.UtcNow;
            db.RegistrationRequests.Add(r);
            await db.SaveChangesAsync();
            return Results.Accepted(
                value: new
                {
                    message = "Your registration request has been recorded for platform review.",
                }
            );
        }
    )
    .RequireRateLimiting("auth");
api.MapGet(
    "/registration-requests",
    async (Access a) =>
    {
        Check((await a.User()).PlatformAdmin, "Platform administrator required.", 403);
        return await a.Db.RegistrationRequests.OrderByDescending(x => x.At).Take(100).ToListAsync();
    }
);
app.MapGet(
    "/health",
    async (Db db) =>
        await db.Database.CanConnectAsync()
            ? Results.Ok(new { status = "ready" })
            : Results.StatusCode(503)
);
if (args.Contains("--seed-phase2") && !args.Contains("--migrate"))
{
    using var scope = app.Services.CreateScope();
    await MembershipSetup.Demo(scope.ServiceProvider);
    return;
}
if (args.Contains("--grant-platform-admin"))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<Db>();
    var index = Array.IndexOf(args, "--grant-platform-admin");
    Check(args.Length > index + 1, "Provide the verified operator email.");
    var email = args[index + 1].ToLowerInvariant();
    var user = await db.Users.SingleOrDefaultAsync(x => x.Email == email && x.Verified);
    Check(user != null, "A verified account must exist first.");
    user.PlatformAdmin = true;
    db.AuthenticationAudits.Add(
        new() { ActorId = user.Id, Action = "operator.platform-admin.grant" }
    );
    await db.SaveChangesAsync();
    return;
}
if (args.Contains("--migrate"))
{
    using var scope = app.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<Db>().Database.MigrateAsync();
    if (args.Contains("--seed"))
        await Seed.Run(scope.ServiceProvider);
    if (args.Contains("--seed-phase2"))
        await MembershipSetup.Demo(scope.ServiceProvider);
    if (args.Contains("--seed-phase3"))
        await GovernanceSetup.Initialize(app.Services);
    if (args.Contains("--seed-phase4"))
        await EventSetup.Initialize(app.Services);
    return;
}
if (args.Contains("--seed-phase4"))
{
    await EventSetup.Initialize(app.Services);
    return;
}
if (args.Contains("--seed-phase3"))
{
    await GovernanceSetup.Initialize(app.Services);
    return;
}
app.Run();

public record Register(
    string Name,
    string Email,
    string Phone,
    string Password,
    string Confirmation,
    bool Agreed
);

public record Login(string Email, string Password);

public record EmailRequest(string Email);

public record Complete(string Token, string Password);

public record Profile(
    string Name,
    string Phone,
    string Locale = "en",
    string TimeZone = "UTC",
    string Theme = "light"
);

public record ChangePassword(string Current, string Password);

public record AssignRequest(string Email, Guid RoleId, Guid OrgId);

public record AssociationRequest(string Name);

public partial class Program { }
