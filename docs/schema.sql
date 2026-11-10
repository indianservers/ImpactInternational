CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
    "MigrationId" character varying(150) NOT NULL,
    "ProductVersion" character varying(32) NOT NULL,
    CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
);

START TRANSACTION;
CREATE TABLE "Audits" (
    "Id" uuid NOT NULL,
    "TenantId" uuid,
    "OrgId" uuid,
    "ActorId" uuid,
    "Action" text NOT NULL,
    "Resource" text NOT NULL,
    "Outcome" text NOT NULL,
    "At" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_Audits" PRIMARY KEY ("Id")
);

CREATE TABLE "Mails" (
    "Id" uuid NOT NULL,
    "UserId" uuid NOT NULL,
    "Kind" text NOT NULL,
    "TokenHash" text NOT NULL,
    "Link" text NOT NULL,
    "ExpiresAt" timestamp with time zone NOT NULL,
    "Used" boolean NOT NULL,
    CONSTRAINT "PK_Mails" PRIMARY KEY ("Id")
);

CREATE TABLE "Settings" (
    "Id" uuid NOT NULL,
    "TenantId" uuid,
    "OrgId" uuid,
    "Key" text NOT NULL,
    "Value" text NOT NULL,
    CONSTRAINT "PK_Settings" PRIMARY KEY ("Id")
);

CREATE TABLE "Tenants" (
    "Id" uuid NOT NULL,
    "Name" text NOT NULL,
    "Currency" text NOT NULL,
    "TimeZone" text NOT NULL,
    "Language" text NOT NULL,
    "Color" text NOT NULL,
    CONSTRAINT "PK_Tenants" PRIMARY KEY ("Id")
);

CREATE TABLE "Users" (
    "Id" uuid NOT NULL,
    "Email" text NOT NULL,
    "PasswordHash" text NOT NULL,
    "Name" text NOT NULL,
    "Phone" text NOT NULL,
    "Verified" boolean NOT NULL,
    "PlatformAdmin" boolean NOT NULL,
    "FailedAttempts" integer NOT NULL,
    "LockedUntil" timestamp with time zone,
    CONSTRAINT "PK_Users" PRIMARY KEY ("Id")
);

CREATE TABLE "Levels" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "Name" text NOT NULL,
    "ParentLevelId" uuid,
    "IsClub" boolean NOT NULL,
    CONSTRAINT "PK_Levels" PRIMARY KEY ("Id"),
    CONSTRAINT "AK_Levels_TenantId_Id" UNIQUE ("TenantId", "Id"),
    CONSTRAINT "FK_Levels_Levels_TenantId_ParentLevelId" FOREIGN KEY ("TenantId", "ParentLevelId") REFERENCES "Levels" ("TenantId", "Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_Levels_Tenants_TenantId" FOREIGN KEY ("TenantId") REFERENCES "Tenants" ("Id") ON DELETE RESTRICT
);

CREATE TABLE "Roles" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "Name" text NOT NULL,
    "Permissions" text[] NOT NULL,
    CONSTRAINT "PK_Roles" PRIMARY KEY ("Id"),
    CONSTRAINT "AK_Roles_TenantId_Id" UNIQUE ("TenantId", "Id"),
    CONSTRAINT "FK_Roles_Tenants_TenantId" FOREIGN KEY ("TenantId") REFERENCES "Tenants" ("Id") ON DELETE RESTRICT
);

CREATE TABLE "Sessions" (
    "Id" text NOT NULL,
    "UserId" uuid NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "ExpiresAt" timestamp with time zone NOT NULL,
    "WorkspaceId" uuid,
    CONSTRAINT "PK_Sessions" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_Sessions_Users_UserId" FOREIGN KEY ("UserId") REFERENCES "Users" ("Id") ON DELETE CASCADE
);

CREATE TABLE "Orgs" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "LevelId" uuid NOT NULL,
    "ParentId" uuid,
    "Name" text NOT NULL,
    "ShortName" text NOT NULL,
    "Code" text NOT NULL,
    "Country" text NOT NULL,
    "State" text NOT NULL,
    "City" text NOT NULL,
    "Address" text NOT NULL,
    "Email" text NOT NULL,
    "Phone" text NOT NULL,
    "Website" text NOT NULL,
    "Logo" text NOT NULL,
    "TimeZone" text NOT NULL,
    "Currency" text NOT NULL,
    "CharterDate" date,
    "Status" text NOT NULL,
    "Version" bigint NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_Orgs" PRIMARY KEY ("Id"),
    CONSTRAINT "AK_Orgs_TenantId_Id" UNIQUE ("TenantId", "Id"),
    CONSTRAINT "FK_Orgs_Levels_TenantId_LevelId" FOREIGN KEY ("TenantId", "LevelId") REFERENCES "Levels" ("TenantId", "Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_Orgs_Orgs_TenantId_ParentId" FOREIGN KEY ("TenantId", "ParentId") REFERENCES "Orgs" ("TenantId", "Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_Orgs_Tenants_TenantId" FOREIGN KEY ("TenantId") REFERENCES "Tenants" ("Id") ON DELETE RESTRICT
);

CREATE TABLE "Assignments" (
    "Id" uuid NOT NULL,
    "UserId" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "RoleId" uuid NOT NULL,
    "OrgId" uuid NOT NULL,
    CONSTRAINT "PK_Assignments" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_Assignments_Orgs_TenantId_OrgId" FOREIGN KEY ("TenantId", "OrgId") REFERENCES "Orgs" ("TenantId", "Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_Assignments_Roles_TenantId_RoleId" FOREIGN KEY ("TenantId", "RoleId") REFERENCES "Roles" ("TenantId", "Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_Assignments_Users_UserId" FOREIGN KEY ("UserId") REFERENCES "Users" ("Id") ON DELETE RESTRICT
);

CREATE INDEX "IX_Assignments_TenantId_OrgId" ON "Assignments" ("TenantId", "OrgId");

CREATE INDEX "IX_Assignments_TenantId_RoleId" ON "Assignments" ("TenantId", "RoleId");

CREATE UNIQUE INDEX "IX_Assignments_UserId_RoleId_OrgId" ON "Assignments" ("UserId", "RoleId", "OrgId");

CREATE INDEX "IX_Audits_TenantId_At" ON "Audits" ("TenantId", "At");

CREATE UNIQUE INDEX "IX_Levels_TenantId_Name" ON "Levels" ("TenantId", "Name");

CREATE INDEX "IX_Levels_TenantId_ParentLevelId" ON "Levels" ("TenantId", "ParentLevelId");

CREATE UNIQUE INDEX "IX_Mails_TokenHash" ON "Mails" ("TokenHash");

CREATE UNIQUE INDEX "IX_Orgs_TenantId_Code" ON "Orgs" ("TenantId", "Code");

CREATE INDEX "IX_Orgs_TenantId_LevelId_Status" ON "Orgs" ("TenantId", "LevelId", "Status");

CREATE INDEX "IX_Orgs_TenantId_ParentId_Name" ON "Orgs" ("TenantId", "ParentId", "Name");

CREATE UNIQUE INDEX "IX_Roles_TenantId_Name" ON "Roles" ("TenantId", "Name");

CREATE INDEX "IX_Sessions_UserId_ExpiresAt" ON "Sessions" ("UserId", "ExpiresAt");

CREATE UNIQUE INDEX "IX_Users_Email" ON "Users" ("Email");

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261110171939_InitialFoundation', '10.0.0');

COMMIT;

START TRANSACTION;
ALTER TABLE "Mails" ADD "Attempts" integer NOT NULL DEFAULT 0;

ALTER TABLE "Mails" ADD "NextAttemptAt" timestamp with time zone NOT NULL DEFAULT TIMESTAMPTZ '-infinity';

ALTER TABLE "Mails" ADD "SentAt" timestamp with time zone;

CREATE INDEX "IX_Mails_NextAttemptAt" ON "Mails" ("NextAttemptAt") WHERE "SentAt" IS NULL AND NOT "Used";

CREATE INDEX "IX_Mails_UserId" ON "Mails" ("UserId");

ALTER TABLE "Mails" ADD CONSTRAINT "FK_Mails_Users_UserId" FOREIGN KEY ("UserId") REFERENCES "Users" ("Id") ON DELETE CASCADE;

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261110172415_IntegrityAndOutbox', '10.0.0');

COMMIT;

START TRANSACTION;
CREATE TABLE "Permissions" (
    "Id" text NOT NULL,
    CONSTRAINT "PK_Permissions" PRIMARY KEY ("Id")
);

CREATE TABLE "RolePermissions" (
    "Id" uuid NOT NULL,
    "RoleId" uuid NOT NULL,
    "PermissionId" text NOT NULL,
    CONSTRAINT "PK_RolePermissions" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_RolePermissions_Permissions_PermissionId" FOREIGN KEY ("PermissionId") REFERENCES "Permissions" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_RolePermissions_Roles_RoleId" FOREIGN KEY ("RoleId") REFERENCES "Roles" ("Id") ON DELETE CASCADE
);

INSERT INTO "Permissions" ("Id")
VALUES ('audit.view');
INSERT INTO "Permissions" ("Id")
VALUES ('organization.approve');
INSERT INTO "Permissions" ("Id")
VALUES ('organization.archive');
INSERT INTO "Permissions" ("Id")
VALUES ('organization.create');
INSERT INTO "Permissions" ("Id")
VALUES ('organization.update');
INSERT INTO "Permissions" ("Id")
VALUES ('organization.view');
INSERT INTO "Permissions" ("Id")
VALUES ('reports.view');
INSERT INTO "Permissions" ("Id")
VALUES ('roles.assign');
INSERT INTO "Permissions" ("Id")
VALUES ('roles.manage');
INSERT INTO "Permissions" ("Id")
VALUES ('roles.view');
INSERT INTO "Permissions" ("Id")
VALUES ('settings.manage');
INSERT INTO "Permissions" ("Id")
VALUES ('settings.view');
INSERT INTO "Permissions" ("Id")
VALUES ('users.invite');
INSERT INTO "Permissions" ("Id")
VALUES ('users.manage');
INSERT INTO "Permissions" ("Id")
VALUES ('users.view');

CREATE INDEX "IX_RolePermissions_PermissionId" ON "RolePermissions" ("PermissionId");

CREATE UNIQUE INDEX "IX_RolePermissions_RoleId_PermissionId" ON "RolePermissions" ("RoleId", "PermissionId");

INSERT INTO "RolePermissions" ("Id","RoleId","PermissionId") SELECT gen_random_uuid(), r."Id", p FROM "Roles" r CROSS JOIN LATERAL unnest(r."Permissions") p

ALTER TABLE "Roles" DROP COLUMN "Permissions";

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261110172443_NormalizePermissions', '10.0.0');

COMMIT;

START TRANSACTION;
ALTER TABLE "Assignments" DROP CONSTRAINT "FK_Assignments_Users_UserId";

ALTER TABLE "Levels" DROP CONSTRAINT "FK_Levels_Tenants_TenantId";

ALTER TABLE "Mails" DROP CONSTRAINT "FK_Mails_Users_UserId";

ALTER TABLE "Orgs" DROP CONSTRAINT "FK_Orgs_Tenants_TenantId";

ALTER TABLE "Roles" DROP CONSTRAINT "FK_Roles_Tenants_TenantId";

ALTER TABLE "Sessions" DROP CONSTRAINT "FK_Sessions_Users_UserId";

ALTER TABLE "Tenants" ADD "CreatedAt" timestamp with time zone NOT NULL DEFAULT TIMESTAMPTZ '-infinity';

CREATE TABLE "Associations" (
    "Id" uuid NOT NULL,
    "Name" text NOT NULL,
    "Currency" text NOT NULL,
    "TimeZone" text NOT NULL,
    "Language" text NOT NULL,
    "Color" text NOT NULL,
    CONSTRAINT "PK_Associations" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_Associations_Tenants_Id" FOREIGN KEY ("Id") REFERENCES "Tenants" ("Id") ON DELETE CASCADE
);

CREATE TABLE "AuthenticationAudits" (
    "Id" uuid NOT NULL,
    "ActorId" uuid,
    "Action" text NOT NULL,
    "Outcome" text NOT NULL,
    "At" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_AuthenticationAudits" PRIMARY KEY ("Id")
);

CREATE TABLE "RegistrationRequests" (
    "Id" uuid NOT NULL,
    "Name" text NOT NULL,
    "Email" text NOT NULL,
    "Description" text NOT NULL,
    "Status" text NOT NULL,
    "At" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_RegistrationRequests" PRIMARY KEY ("Id")
);

CREATE TABLE "UserProfiles" (
    "Id" uuid NOT NULL,
    "Name" text NOT NULL,
    "Phone" text NOT NULL,
    CONSTRAINT "PK_UserProfiles" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_UserProfiles_Users_Id" FOREIGN KEY ("Id") REFERENCES "Users" ("Id") ON DELETE CASCADE
);

CREATE INDEX "IX_AuthenticationAudits_ActorId_At" ON "AuthenticationAudits" ("ActorId", "At");

INSERT INTO "UserProfiles" ("Id","Name","Phone") SELECT "Id","Name","Phone" FROM "Users"; INSERT INTO "Associations" ("Id","Name","Currency","TimeZone","Language","Color") SELECT "Id","Name","Currency","TimeZone","Language","Color" FROM "Tenants";

ALTER TABLE "Users" DROP COLUMN "Name";

ALTER TABLE "Users" DROP COLUMN "Phone";

ALTER TABLE "Tenants" DROP COLUMN "Color";

ALTER TABLE "Tenants" DROP COLUMN "Currency";

ALTER TABLE "Tenants" DROP COLUMN "Language";

ALTER TABLE "Tenants" DROP COLUMN "Name";

ALTER TABLE "Tenants" DROP COLUMN "TimeZone";

ALTER TABLE "Assignments" ADD CONSTRAINT "FK_Assignments_UserProfiles_UserId" FOREIGN KEY ("UserId") REFERENCES "UserProfiles" ("Id") ON DELETE RESTRICT;

ALTER TABLE "Levels" ADD CONSTRAINT "FK_Levels_Associations_TenantId" FOREIGN KEY ("TenantId") REFERENCES "Associations" ("Id") ON DELETE RESTRICT;

ALTER TABLE "Mails" ADD CONSTRAINT "FK_Mails_UserProfiles_UserId" FOREIGN KEY ("UserId") REFERENCES "UserProfiles" ("Id") ON DELETE CASCADE;

ALTER TABLE "Orgs" ADD CONSTRAINT "FK_Orgs_Associations_TenantId" FOREIGN KEY ("TenantId") REFERENCES "Associations" ("Id") ON DELETE RESTRICT;

ALTER TABLE "Roles" ADD CONSTRAINT "FK_Roles_Associations_TenantId" FOREIGN KEY ("TenantId") REFERENCES "Associations" ("Id") ON DELETE RESTRICT;

ALTER TABLE "Sessions" ADD CONSTRAINT "FK_Sessions_UserProfiles_UserId" FOREIGN KEY ("UserId") REFERENCES "UserProfiles" ("Id") ON DELETE CASCADE;

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261110172533_IdentityAndAssociationProfiles', '10.0.0');

COMMIT;

START TRANSACTION;

CREATE EXTENSION IF NOT EXISTS pg_trgm;
CREATE INDEX "IX_Orgs_Name_Search" ON "Orgs" USING gin ("Name" gin_trgm_ops);
ALTER TABLE "Orgs" ADD CONSTRAINT "CK_Org_Identity" CHECK(length(trim("Name")) BETWEEN 2 AND 200 AND length("Code") BETWEEN 1 AND 40 AND "Status" IN ('Active','Pending','Archived') AND "Id" IS DISTINCT FROM "ParentId");
ALTER TABLE "Orgs" ADD CONSTRAINT "CK_Org_Currency" CHECK(length("Currency")=3);
CREATE FUNCTION clubs_hierarchy_guard() RETURNS trigger LANGUAGE plpgsql AS $$
DECLARE permitted uuid; actual uuid;
BEGIN
 PERFORM pg_advisory_xact_lock(hashtext(NEW."TenantId"::text));
 IF TG_OP='UPDATE' AND (NEW."TenantId"<>OLD."TenantId" OR NEW."LevelId"<>OLD."LevelId") THEN RAISE EXCEPTION 'Tenant and level are immutable' USING ERRCODE='23514'; END IF;
 SELECT "ParentLevelId" INTO permitted FROM "Levels" WHERE "Id"=NEW."LevelId" AND "TenantId"=NEW."TenantId";
 IF NEW."ParentId" IS NULL THEN
  IF permitted IS NOT NULL THEN RAISE EXCEPTION 'Parent required' USING ERRCODE='23514'; END IF;
 ELSE
  SELECT "LevelId" INTO actual FROM "Orgs" WHERE "Id"=NEW."ParentId" AND "TenantId"=NEW."TenantId";
  IF actual IS NULL OR actual IS DISTINCT FROM permitted THEN RAISE EXCEPTION 'Invalid parent type or tenant' USING ERRCODE='23514'; END IF;
  IF EXISTS(WITH RECURSIVE ancestors AS (SELECT "Id","ParentId" FROM "Orgs" WHERE "Id"=NEW."ParentId" AND "TenantId"=NEW."TenantId" UNION SELECT o."Id",o."ParentId" FROM "Orgs" o JOIN ancestors a ON o."Id"=a."ParentId" WHERE o."TenantId"=NEW."TenantId") SELECT 1 FROM ancestors WHERE "Id"=NEW."Id") THEN RAISE EXCEPTION 'Cycle rejected' USING ERRCODE='23514'; END IF;
 END IF;
 RETURN NEW;
END $$;
CREATE TRIGGER organization_hierarchy_guard BEFORE INSERT OR UPDATE ON "Orgs" FOR EACH ROW EXECUTE FUNCTION clubs_hierarchy_guard();
CREATE FUNCTION clubs_level_guard() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
 IF TG_OP='UPDATE' AND (NEW."ParentLevelId" IS DISTINCT FROM OLD."ParentLevelId" OR NEW."TenantId"<>OLD."TenantId") THEN RAISE EXCEPTION 'Existing level relationships are immutable' USING ERRCODE='23514'; END IF;
 IF NEW."Id"=NEW."ParentLevelId" THEN RAISE EXCEPTION 'Level cycle rejected' USING ERRCODE='23514'; END IF;
 RETURN NEW;
END $$;
CREATE TRIGGER level_hierarchy_guard BEFORE INSERT OR UPDATE ON "Levels" FOR EACH ROW EXECUTE FUNCTION clubs_level_guard();


INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261110172550_HierarchyGuards', '10.0.0');

COMMIT;

START TRANSACTION;
DROP TABLE "Settings";

CREATE TABLE "OrganizationSettings" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "OrgId" uuid NOT NULL,
    "Key" text NOT NULL,
    "Value" text NOT NULL,
    CONSTRAINT "PK_OrganizationSettings" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_OrganizationSettings_Orgs_TenantId_OrgId" FOREIGN KEY ("TenantId", "OrgId") REFERENCES "Orgs" ("TenantId", "Id") ON DELETE RESTRICT
);

CREATE TABLE "SystemSettings" (
    "Key" text NOT NULL,
    "Value" text NOT NULL,
    CONSTRAINT "PK_SystemSettings" PRIMARY KEY ("Key")
);

CREATE TABLE "TenantSettings" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "Key" text NOT NULL,
    "Value" text NOT NULL,
    CONSTRAINT "PK_TenantSettings" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_TenantSettings_Associations_TenantId" FOREIGN KEY ("TenantId") REFERENCES "Associations" ("Id") ON DELETE RESTRICT
);

CREATE UNIQUE INDEX "IX_OrganizationSettings_TenantId_OrgId_Key" ON "OrganizationSettings" ("TenantId", "OrgId", "Key");

CREATE UNIQUE INDEX "IX_TenantSettings_TenantId_Key" ON "TenantSettings" ("TenantId", "Key");

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261110172659_ScopedSettings', '10.0.0');

COMMIT;

START TRANSACTION;
ALTER TABLE "Audits" ADD "Changes" text NOT NULL DEFAULT '';

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261110173351_AuditChanges', '10.0.0');

COMMIT;

