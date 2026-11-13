using Microsoft.EntityFrameworkCore;
using static Clubs.Access;

namespace Clubs;

public static class IntelligenceTelemetry
{
    static long requests,
        failures;
    static readonly DateTimeOffset started = DateTimeOffset.UtcNow;

    public static void Record(int status)
    {
        Interlocked.Increment(ref requests);
        if (status >= 500)
            Interlocked.Increment(ref failures);
    }

    public static object Snapshot() =>
        new
        {
            scope = "This process only; not distributed monitoring",
            startedAt = started,
            requests = Interlocked.Read(ref requests),
            serverErrors = Interlocked.Read(ref failures),
        };
}

public static partial class IntelligenceEndpoints
{
    static void MapOperations(WebApplication app, RouteGroupBuilder api)
    {
        app.MapGet("/health/live", () => Results.Ok(new { status = "live" }));
        app.MapGet(
            "/health/ready",
            async (
                Db db,
                CommunicationStorage storage,
                IConfiguration config,
                IHostEnvironment env
            ) =>
            {
                var database = false;
                var objects = false;
                try
                {
                    database = await db.Database.CanConnectAsync();
                    Directory.CreateDirectory(storage.Root);
                    var path = Path.Combine(
                        storage.Root,
                        ".health-" + Guid.NewGuid().ToString("N")
                    );
                    using var probe = new FileStream(
                        path,
                        FileMode.CreateNew,
                        FileAccess.Write,
                        FileShare.None,
                        1,
                        FileOptions.DeleteOnClose
                    );
                    probe.WriteByte(1);
                    objects = true;
                }
                catch { }
                var persistentKeys =
                    env.IsDevelopment()
                    || config["DataProtection:Path"] is string keys && Directory.Exists(keys);
                return Results.Json(
                    new
                    {
                        status = database && objects && persistentKeys ? "ready" : "not-ready",
                        database,
                        privateObjectStorage = objects,
                        persistentKeys,
                    },
                    statusCode: database && objects && persistentKeys ? 200 : 503
                );
            }
        );
        api.MapGet(
            "/operations",
            async (Access a, IConfiguration config, IHostEnvironment env) =>
            {
                Check((await a.User()).PlatformAdmin, "Platform operator authority required.", 403);
                var now = DateTimeOffset.UtcNow;
                return new
                {
                    telemetry = IntelligenceTelemetry.Snapshot(),
                    environment = env.EnvironmentName,
                    migrations = await a.Db.Database.GetAppliedMigrationsAsync(),
                    queues = new
                    {
                        pendingMail = await a.Db.Mails.CountAsync(m =>
                            m.SentAt == null && !m.Suppressed && !m.DeadLetter
                        ),
                        deadLetters = await a.Db.Mails.CountAsync(m => m.DeadLetter),
                        billingFailures = await a.Db.FinanceJobs.CountAsync(j =>
                            j.Status == "Failed"
                        ),
                        workflowFailures = await a.Db.CommunicationExecutions.CountAsync(e =>
                            e.Status == "Failed"
                        ),
                        expiredKnowledge = await a.Db.KnowledgePassages.CountAsync(p =>
                            a.Db.GovernanceDocuments.Any(d =>
                                d.Id == p.DocumentId && d.RetainUntil < now
                            )
                        ),
                    },
                    gates = new
                    {
                        productionReady = false,
                        localHealth = "See /health/ready",
                        mfa = "Optional: MFA is not required for login or production access; enrollment/challenges are not implemented",
                        tls = "Target ingress/TLS must be externally verified",
                        recovery = "Run the isolated recovery drill; same-host verification is not off-host recovery",
                        providers = "Credentials, merchant/sender authorization and external sandbox certification required",
                        deployment = "Container/TLS/failover deployment is not proven by API build",
                        scaleOut = "Per-process rate limit/telemetry; shared enforcement required before scale-out",
                    },
                    configuration = new
                    {
                        persistentKeyPathConfigured = config["DataProtection:Path"]
                            is { Length: > 0 },
                        publicOriginConfigured = config["PublicOrigin"] is { Length: > 0 },
                        remoteModelConfigured = config["Intelligence:OpenAI:ApiKey"]
                            is { Length: > 0 },
                        provisionedDomainCount = config
                            .GetSection("Domains:ProvisionedHosts")
                            .Get<string[]>()
                            ?.Length
                            ?? 0,
                    },
                };
            }
        );
    }
}
