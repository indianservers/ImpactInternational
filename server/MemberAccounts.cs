using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;

namespace Clubs;

public static class MemberAccounts
{
    public static async Task Provision(Db db, Guid[] personIds)
    {
        if (personIds.Length == 0) return;
        await db.Database.ExecuteSqlRawAsync(ProvisionSql, PersonParameter(personIds));
        await db.Database.ExecuteSqlRawAsync(GrantsSql, PersonParameter(personIds));
    }

    private static NpgsqlParameter PersonParameter(Guid[] ids) =>
        new("person_ids", NpgsqlDbType.Array | NpgsqlDbType.Uuid) { Value = ids };

    // Leave existing accounts and ambiguous addresses for the reviewed account-link workflow.
    internal const string ProvisionSql = """
        WITH member_people AS (
            SELECT person."Id", person."DisplayName"
            FROM "Persons" person
            WHERE person."UserId" IS NULL
                AND (@person_ids IS NULL OR person."Id" = ANY(@person_ids))
                AND EXISTS (
                    SELECT 1 FROM "Memberships" member JOIN "Associations" association ON association."Id" = member."TenantId"
                    WHERE member."PersonId" = person."Id" AND association."Name" = 'Impact International'
                        AND member."Status" IN ('Active', 'Inactive', 'Suspended', 'Expired')
                )
        ), email_options AS (
            SELECT person."Id", person."DisplayName", contact."NormalizedValue" AS email,
                bool_or(lower(contact."Label") LIKE 'primary%') AS is_primary
            FROM member_people person JOIN "PersonContacts" contact ON contact."PersonId" = person."Id"
            WHERE contact."Kind" = 'Email'
                AND contact."NormalizedValue" ~ '^[^[:space:]<>@]+@[^[:space:]<>@]+$'
            GROUP BY person."Id", person."DisplayName", contact."NormalizedValue"
        ), candidates AS (
            SELECT "Id", "DisplayName",
                CASE WHEN count(*) = 1 THEN min(email) ELSE min(email) FILTER (WHERE is_primary) END AS email
            FROM email_options
            GROUP BY "Id", "DisplayName"
            HAVING count(*) = 1 OR count(*) FILTER (WHERE is_primary) = 1
        ), inserted AS (
            INSERT INTO "Users" ("Id", "Email", "PasswordHash", "Verified", "PlatformAdmin", "FailedAttempts")
            SELECT gen_random_uuid(), candidate.email, '', false, false, 0 FROM candidates candidate
            WHERE candidate.email IS NOT NULL AND NOT EXISTS (
                SELECT 1 FROM "PersonContacts" other
                WHERE other."Kind" = 'Email' AND other."NormalizedValue" = candidate.email AND other."PersonId" <> candidate."Id"
            )
            ON CONFLICT ("Email") DO NOTHING
            RETURNING "Id", "Email"
        ), profiles AS (
            INSERT INTO "UserProfiles" ("Id", "Name", "Phone", "Locale", "TimeZone", "Theme")
            SELECT inserted."Id", candidate."DisplayName", '', 'en', 'Asia/Kolkata', 'light'
            FROM inserted JOIN candidates candidate ON candidate.email = inserted."Email"
        )
        UPDATE "Persons" person SET "UserId" = inserted."Id", "Version" = person."Version" + 1, "UpdatedAt" = now()
        FROM candidates candidate JOIN inserted ON inserted."Email" = candidate.email
        WHERE person."Id" = candidate."Id" AND person."UserId" IS NULL;
        """;

    internal const string GrantsSql = """
        INSERT INTO "Assignments" ("Id", "TenantId", "UserId", "OrgId", "RoleId", "MembershipId")
        SELECT DISTINCT ON (person."UserId", role."Id", member."ClubId")
            gen_random_uuid(), member."TenantId", person."UserId", member."ClubId", role."Id", member."Id"
        FROM "Memberships" member
        JOIN "Persons" person ON person."Id" = member."PersonId"
        JOIN "Associations" association ON association."Id" = member."TenantId"
        JOIN "Orgs" club ON club."Id" = member."ClubId"
        JOIN "Roles" role ON role."TenantId" = member."TenantId" AND role."Name" = 'Club Member'
        WHERE association."Name" = 'Impact International' AND person."UserId" IS NOT NULL
            AND (@person_ids IS NULL OR person."Id" = ANY(@person_ids))
            AND club."Status" = 'Active' AND member."Status" = 'Active' AND (member."EndsAt" IS NULL OR member."EndsAt" > now())
        ORDER BY person."UserId", role."Id", member."ClubId", member."JoinedAt", member."Id"
        ON CONFLICT ("UserId", "RoleId", "OrgId") DO NOTHING;
        """;
}
