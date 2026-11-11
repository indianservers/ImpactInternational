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

START TRANSACTION;
ALTER TABLE "UserProfiles" ADD "Locale" text NOT NULL DEFAULT '';

ALTER TABLE "UserProfiles" ADD "TimeZone" text NOT NULL DEFAULT '';

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261110174353_AccountPreferences', '10.0.0');

COMMIT;

START TRANSACTION;
ALTER TABLE "UserProfiles" ADD "Theme" text NOT NULL DEFAULT '';

ALTER TABLE "Mails" ALTER COLUMN "UserId" DROP NOT NULL;

ALTER TABLE "Mails" ADD "Body" text NOT NULL DEFAULT '';

ALTER TABLE "Mails" ADD "OrgId" uuid;

ALTER TABLE "Mails" ADD "Recipient" text NOT NULL DEFAULT '';

ALTER TABLE "Mails" ADD "Subject" text NOT NULL DEFAULT '';

ALTER TABLE "Mails" ADD "Suppressed" boolean NOT NULL DEFAULT FALSE;

ALTER TABLE "Mails" ADD "TenantId" uuid;

ALTER TABLE "Assignments" ADD "MembershipId" uuid;

CREATE TABLE "MailDeliveryAttempts" (
    "Id" uuid NOT NULL,
    "MailId" uuid NOT NULL,
    "Succeeded" boolean NOT NULL,
    "ErrorCode" text NOT NULL,
    "At" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_MailDeliveryAttempts" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_MailDeliveryAttempts_Mails_MailId" FOREIGN KEY ("MailId") REFERENCES "Mails" ("Id") ON DELETE CASCADE
);

CREATE TABLE "MemberSavedViews" (
    "Id" uuid NOT NULL,
    "UserId" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "Name" text NOT NULL,
    "Configuration" text NOT NULL,
    CONSTRAINT "PK_MemberSavedViews" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_MemberSavedViews_UserProfiles_UserId" FOREIGN KEY ("UserId") REFERENCES "UserProfiles" ("Id") ON DELETE CASCADE
);

CREATE TABLE "MembershipCategories" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "Name" text NOT NULL,
    "Code" text NOT NULL,
    "ValidityMonths" integer,
    "Active" boolean NOT NULL,
    CONSTRAINT "PK_MembershipCategories" PRIMARY KEY ("Id"),
    CONSTRAINT "AK_MembershipCategories_TenantId_Id" UNIQUE ("TenantId", "Id"),
    CONSTRAINT "FK_MembershipCategories_Associations_TenantId" FOREIGN KEY ("TenantId") REFERENCES "Associations" ("Id") ON DELETE RESTRICT
);

CREATE TABLE "MembershipPolicies" (
    "TenantId" uuid NOT NULL,
    "AllowMultipleClubs" boolean NOT NULL,
    "SourceReviewRequired" boolean NOT NULL,
    "AssociationReviewRequired" boolean NOT NULL,
    "FinancialClearanceRequired" boolean NOT NULL,
    CONSTRAINT "PK_MembershipPolicies" PRIMARY KEY ("TenantId"),
    CONSTRAINT "FK_MembershipPolicies_Associations_TenantId" FOREIGN KEY ("TenantId") REFERENCES "Associations" ("Id") ON DELETE RESTRICT
);

CREATE TABLE "MembershipWorkflows" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "Version" integer NOT NULL,
    "Active" boolean NOT NULL,
    CONSTRAINT "PK_MembershipWorkflows" PRIMARY KEY ("Id"),
    CONSTRAINT "AK_MembershipWorkflows_TenantId_Id" UNIQUE ("TenantId", "Id"),
    CONSTRAINT "FK_MembershipWorkflows_Associations_TenantId" FOREIGN KEY ("TenantId") REFERENCES "Associations" ("Id") ON DELETE RESTRICT
);

CREATE TABLE "NotificationTemplates" (
    "Id" text NOT NULL,
    "Subject" text NOT NULL,
    "Body" text NOT NULL,
    CONSTRAINT "PK_NotificationTemplates" PRIMARY KEY ("Id")
);

CREATE TABLE "Persons" (
    "Id" uuid NOT NULL,
    "UserId" uuid,
    "LegalName" text NOT NULL,
    "DisplayName" text NOT NULL,
    "SearchName" text NOT NULL,
    "BirthDate" date,
    "Gender" text NOT NULL,
    "Anniversary" date,
    "Company" text NOT NULL,
    "Designation" text NOT NULL,
    "Industry" text NOT NULL,
    "Profession" text NOT NULL,
    "BusinessCategory" text NOT NULL,
    "WorkLocation" text NOT NULL,
    "Website" text NOT NULL,
    "Networking" text NOT NULL,
    "Photo" bytea,
    "PhotoType" text NOT NULL,
    "Version" bigint NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_Persons" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_Persons_UserProfiles_UserId" FOREIGN KEY ("UserId") REFERENCES "UserProfiles" ("Id") ON DELETE RESTRICT
);

CREATE TABLE "MemberImportJobs" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "ScopeAssignmentId" uuid NOT NULL,
    "CreatorId" uuid NOT NULL,
    "ClubId" uuid NOT NULL,
    "CategoryId" uuid NOT NULL,
    "FileName" text NOT NULL,
    "Payload" bytea NOT NULL,
    "Hash" text NOT NULL,
    "Mapping" text NOT NULL,
    "Headers" text NOT NULL,
    "Status" text NOT NULL,
    "TotalRows" integer NOT NULL,
    "ProcessedRows" integer NOT NULL,
    "ImportedRows" integer NOT NULL,
    "Error" text NOT NULL,
    "RequestKey" text NOT NULL,
    "LeaseId" uuid,
    "LeaseUntil" timestamp with time zone,
    "CreatedAt" timestamp with time zone NOT NULL,
    "CompletedAt" timestamp with time zone,
    CONSTRAINT "PK_MemberImportJobs" PRIMARY KEY ("Id"),
    CONSTRAINT "AK_MemberImportJobs_TenantId_Id" UNIQUE ("TenantId", "Id"),
    CONSTRAINT "FK_MemberImportJobs_Associations_TenantId" FOREIGN KEY ("TenantId") REFERENCES "Associations" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_MemberImportJobs_MembershipCategories_TenantId_CategoryId" FOREIGN KEY ("TenantId", "CategoryId") REFERENCES "MembershipCategories" ("TenantId", "Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_MemberImportJobs_Orgs_TenantId_ClubId" FOREIGN KEY ("TenantId", "ClubId") REFERENCES "Orgs" ("TenantId", "Id") ON DELETE RESTRICT
);

CREATE TABLE "MembershipInvitations" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "ClubId" uuid NOT NULL,
    "CategoryId" uuid NOT NULL,
    "InviterId" uuid NOT NULL,
    "Email" text NOT NULL,
    "TokenHash" text NOT NULL,
    "ProtectedToken" text NOT NULL,
    "Kind" text NOT NULL,
    "PersonId" uuid,
    "ExpiresAt" timestamp with time zone NOT NULL,
    "UsageLimit" integer NOT NULL,
    "Uses" integer NOT NULL,
    "Revoked" boolean NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_MembershipInvitations" PRIMARY KEY ("Id"),
    CONSTRAINT "AK_MembershipInvitations_TenantId_Id" UNIQUE ("TenantId", "Id"),
    CONSTRAINT "FK_MembershipInvitations_Associations_TenantId" FOREIGN KEY ("TenantId") REFERENCES "Associations" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_MembershipInvitations_MembershipCategories_TenantId_Categor~" FOREIGN KEY ("TenantId", "CategoryId") REFERENCES "MembershipCategories" ("TenantId", "Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_MembershipInvitations_Orgs_TenantId_ClubId" FOREIGN KEY ("TenantId", "ClubId") REFERENCES "Orgs" ("TenantId", "Id") ON DELETE RESTRICT
);

CREATE TABLE "MembershipApprovalStages" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "WorkflowId" uuid NOT NULL,
    "Name" text NOT NULL,
    "Sequence" integer NOT NULL,
    "RequiredApprovals" integer NOT NULL,
    CONSTRAINT "PK_MembershipApprovalStages" PRIMARY KEY ("Id"),
    CONSTRAINT "AK_MembershipApprovalStages_TenantId_Id" UNIQUE ("TenantId", "Id"),
    CONSTRAINT "FK_MembershipApprovalStages_Associations_TenantId" FOREIGN KEY ("TenantId") REFERENCES "Associations" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_MembershipApprovalStages_MembershipWorkflows_TenantId_Workf~" FOREIGN KEY ("TenantId", "WorkflowId") REFERENCES "MembershipWorkflows" ("TenantId", "Id") ON DELETE RESTRICT
);

CREATE TABLE "MemberActivities" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "PersonId" uuid NOT NULL,
    "ClubId" uuid NOT NULL,
    "ActorId" uuid NOT NULL,
    "Action" text NOT NULL,
    "Detail" text NOT NULL,
    "At" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_MemberActivities" PRIMARY KEY ("Id"),
    CONSTRAINT "AK_MemberActivities_TenantId_Id" UNIQUE ("TenantId", "Id"),
    CONSTRAINT "FK_MemberActivities_Associations_TenantId" FOREIGN KEY ("TenantId") REFERENCES "Associations" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_MemberActivities_Orgs_TenantId_ClubId" FOREIGN KEY ("TenantId", "ClubId") REFERENCES "Orgs" ("TenantId", "Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_MemberActivities_Persons_PersonId" FOREIGN KEY ("PersonId") REFERENCES "Persons" ("Id") ON DELETE RESTRICT
);

CREATE TABLE "MemberRelationships" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "PersonId" uuid NOT NULL,
    "RelatedPersonId" uuid NOT NULL,
    "Relationship" text NOT NULL,
    CONSTRAINT "PK_MemberRelationships" PRIMARY KEY ("Id"),
    CONSTRAINT "AK_MemberRelationships_TenantId_Id" UNIQUE ("TenantId", "Id"),
    CONSTRAINT "FK_MemberRelationships_Associations_TenantId" FOREIGN KEY ("TenantId") REFERENCES "Associations" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_MemberRelationships_Persons_PersonId" FOREIGN KEY ("PersonId") REFERENCES "Persons" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_MemberRelationships_Persons_RelatedPersonId" FOREIGN KEY ("RelatedPersonId") REFERENCES "Persons" ("Id") ON DELETE RESTRICT
);

CREATE TABLE "PersonAddresses" (
    "Id" uuid NOT NULL,
    "PersonId" uuid NOT NULL,
    "Country" text NOT NULL,
    "State" text NOT NULL,
    "City" text NOT NULL,
    "PostalCode" text NOT NULL,
    "Address" text NOT NULL,
    CONSTRAINT "PK_PersonAddresses" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_PersonAddresses_Persons_PersonId" FOREIGN KEY ("PersonId") REFERENCES "Persons" ("Id") ON DELETE CASCADE
);

CREATE TABLE "PersonContacts" (
    "Id" uuid NOT NULL,
    "PersonId" uuid NOT NULL,
    "Kind" text NOT NULL,
    "Label" text NOT NULL,
    "Value" text NOT NULL,
    "NormalizedValue" text NOT NULL,
    "Verified" boolean NOT NULL,
    CONSTRAINT "PK_PersonContacts" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_PersonContacts_Persons_PersonId" FOREIGN KEY ("PersonId") REFERENCES "Persons" ("Id") ON DELETE CASCADE
);

CREATE TABLE "PersonPrivacy" (
    "PersonId" uuid NOT NULL,
    "DirectoryVisible" boolean NOT NULL,
    "DisplayEmail" boolean NOT NULL,
    "DisplayPhone" boolean NOT NULL,
    "ProfessionalVisible" boolean NOT NULL,
    "BirthdayAnnouncements" boolean NOT NULL,
    "Marketing" boolean NOT NULL,
    "PhotoVisible" boolean NOT NULL,
    "AllowContactRequests" boolean NOT NULL,
    "CardNameVisible" boolean NOT NULL,
    "TransactionalEmail" boolean NOT NULL,
    "Version" bigint NOT NULL,
    CONSTRAINT "PK_PersonPrivacy" PRIMARY KEY ("PersonId"),
    CONSTRAINT "FK_PersonPrivacy_Persons_PersonId" FOREIGN KEY ("PersonId") REFERENCES "Persons" ("Id") ON DELETE CASCADE
);

CREATE TABLE "PersonTags" (
    "Id" uuid NOT NULL,
    "PersonId" uuid NOT NULL,
    "Kind" text NOT NULL,
    "Value" text NOT NULL,
    CONSTRAINT "PK_PersonTags" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_PersonTags_Persons_PersonId" FOREIGN KEY ("PersonId") REFERENCES "Persons" ("Id") ON DELETE CASCADE
);

CREATE TABLE "MemberImportRows" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "JobId" uuid NOT NULL,
    "RowNumber" integer NOT NULL,
    "Data" text NOT NULL,
    "Status" text NOT NULL,
    "Error" text NOT NULL,
    "Candidates" text NOT NULL,
    "ExistingPersonId" uuid,
    "CreateSeparate" boolean NOT NULL,
    CONSTRAINT "PK_MemberImportRows" PRIMARY KEY ("Id"),
    CONSTRAINT "AK_MemberImportRows_TenantId_Id" UNIQUE ("TenantId", "Id"),
    CONSTRAINT "FK_MemberImportRows_Associations_TenantId" FOREIGN KEY ("TenantId") REFERENCES "Associations" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_MemberImportRows_MemberImportJobs_TenantId_JobId" FOREIGN KEY ("TenantId", "JobId") REFERENCES "MemberImportJobs" ("TenantId", "Id") ON DELETE CASCADE
);

CREATE TABLE "MembershipApplications" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "PersonId" uuid NOT NULL,
    "ApplicantUserId" uuid NOT NULL,
    "ClubId" uuid NOT NULL,
    "CategoryId" uuid NOT NULL,
    "WorkflowId" uuid,
    "Statement" text NOT NULL,
    "Status" text NOT NULL,
    "Revision" integer NOT NULL,
    "CurrentSequence" integer NOT NULL,
    "Version" bigint NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "SubmittedAt" timestamp with time zone,
    "UpdatedAt" timestamp with time zone NOT NULL,
    "InvitationId" uuid,
    CONSTRAINT "PK_MembershipApplications" PRIMARY KEY ("Id"),
    CONSTRAINT "AK_MembershipApplications_TenantId_Id" UNIQUE ("TenantId", "Id"),
    CONSTRAINT "FK_MembershipApplications_Associations_TenantId" FOREIGN KEY ("TenantId") REFERENCES "Associations" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_MembershipApplications_MembershipCategories_TenantId_Catego~" FOREIGN KEY ("TenantId", "CategoryId") REFERENCES "MembershipCategories" ("TenantId", "Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_MembershipApplications_MembershipInvitations_TenantId_Invit~" FOREIGN KEY ("TenantId", "InvitationId") REFERENCES "MembershipInvitations" ("TenantId", "Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_MembershipApplications_MembershipWorkflows_TenantId_Workflo~" FOREIGN KEY ("TenantId", "WorkflowId") REFERENCES "MembershipWorkflows" ("TenantId", "Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_MembershipApplications_Orgs_TenantId_ClubId" FOREIGN KEY ("TenantId", "ClubId") REFERENCES "Orgs" ("TenantId", "Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_MembershipApplications_Persons_PersonId" FOREIGN KEY ("PersonId") REFERENCES "Persons" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_MembershipApplications_UserProfiles_ApplicantUserId" FOREIGN KEY ("ApplicantUserId") REFERENCES "UserProfiles" ("Id") ON DELETE RESTRICT
);

CREATE TABLE "PersonLinkRequests" (
    "Id" uuid NOT NULL,
    "InvitationId" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "PersonId" uuid NOT NULL,
    "UserId" uuid NOT NULL,
    "ClubId" uuid NOT NULL,
    "Status" text NOT NULL,
    "ReviewReason" text NOT NULL,
    "ReviewerId" uuid,
    "At" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_PersonLinkRequests" PRIMARY KEY ("Id"),
    CONSTRAINT "AK_PersonLinkRequests_TenantId_Id" UNIQUE ("TenantId", "Id"),
    CONSTRAINT "FK_PersonLinkRequests_Associations_TenantId" FOREIGN KEY ("TenantId") REFERENCES "Associations" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_PersonLinkRequests_MembershipInvitations_TenantId_Invitatio~" FOREIGN KEY ("TenantId", "InvitationId") REFERENCES "MembershipInvitations" ("TenantId", "Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_PersonLinkRequests_Orgs_TenantId_ClubId" FOREIGN KEY ("TenantId", "ClubId") REFERENCES "Orgs" ("TenantId", "Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_PersonLinkRequests_Persons_PersonId" FOREIGN KEY ("PersonId") REFERENCES "Persons" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_PersonLinkRequests_UserProfiles_UserId" FOREIGN KEY ("UserId") REFERENCES "UserProfiles" ("Id") ON DELETE RESTRICT
);

CREATE TABLE "MembershipStageRoles" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "StageId" uuid NOT NULL,
    "RoleId" uuid NOT NULL,
    CONSTRAINT "PK_MembershipStageRoles" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_MembershipStageRoles_MembershipApprovalStages_TenantId_Stag~" FOREIGN KEY ("TenantId", "StageId") REFERENCES "MembershipApprovalStages" ("TenantId", "Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_MembershipStageRoles_Roles_TenantId_RoleId" FOREIGN KEY ("TenantId", "RoleId") REFERENCES "Roles" ("TenantId", "Id") ON DELETE RESTRICT
);

CREATE TABLE "MembershipApprovals" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "ApplicationId" uuid NOT NULL,
    "StageId" uuid NOT NULL,
    "Revision" integer NOT NULL,
    "ActorId" uuid NOT NULL,
    "Decision" text NOT NULL,
    "Comment" text NOT NULL,
    "At" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_MembershipApprovals" PRIMARY KEY ("Id"),
    CONSTRAINT "AK_MembershipApprovals_TenantId_Id" UNIQUE ("TenantId", "Id"),
    CONSTRAINT "FK_MembershipApprovals_Associations_TenantId" FOREIGN KEY ("TenantId") REFERENCES "Associations" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_MembershipApprovals_MembershipApplications_TenantId_Applica~" FOREIGN KEY ("TenantId", "ApplicationId") REFERENCES "MembershipApplications" ("TenantId", "Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_MembershipApprovals_MembershipApprovalStages_TenantId_Stage~" FOREIGN KEY ("TenantId", "StageId") REFERENCES "MembershipApprovalStages" ("TenantId", "Id") ON DELETE RESTRICT
);

CREATE TABLE "Memberships" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "PersonId" uuid NOT NULL,
    "ClubId" uuid NOT NULL,
    "CategoryId" uuid NOT NULL,
    "Number" text NOT NULL,
    "Status" text NOT NULL,
    "JoinedAt" timestamp with time zone NOT NULL,
    "EndsAt" timestamp with time zone,
    "UpdatedAt" timestamp with time zone NOT NULL,
    "FinancialHold" boolean NOT NULL,
    "Version" bigint NOT NULL,
    "SourceApplicationId" uuid,
    "ImportRowId" uuid,
    CONSTRAINT "PK_Memberships" PRIMARY KEY ("Id"),
    CONSTRAINT "AK_Memberships_TenantId_Id" UNIQUE ("TenantId", "Id"),
    CONSTRAINT "FK_Memberships_Associations_TenantId" FOREIGN KEY ("TenantId") REFERENCES "Associations" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_Memberships_MemberImportRows_TenantId_ImportRowId" FOREIGN KEY ("TenantId", "ImportRowId") REFERENCES "MemberImportRows" ("TenantId", "Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_Memberships_MembershipApplications_TenantId_SourceApplicati~" FOREIGN KEY ("TenantId", "SourceApplicationId") REFERENCES "MembershipApplications" ("TenantId", "Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_Memberships_MembershipCategories_TenantId_CategoryId" FOREIGN KEY ("TenantId", "CategoryId") REFERENCES "MembershipCategories" ("TenantId", "Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_Memberships_Orgs_TenantId_ClubId" FOREIGN KEY ("TenantId", "ClubId") REFERENCES "Orgs" ("TenantId", "Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_Memberships_Persons_PersonId" FOREIGN KEY ("PersonId") REFERENCES "Persons" ("Id") ON DELETE RESTRICT
);

CREATE TABLE "DigitalCards" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "MembershipId" uuid NOT NULL,
    "TokenHash" text NOT NULL,
    "ProtectedToken" text NOT NULL,
    "IssuedAt" timestamp with time zone NOT NULL,
    "RevokedAt" timestamp with time zone,
    CONSTRAINT "PK_DigitalCards" PRIMARY KEY ("Id"),
    CONSTRAINT "AK_DigitalCards_TenantId_Id" UNIQUE ("TenantId", "Id"),
    CONSTRAINT "FK_DigitalCards_Associations_TenantId" FOREIGN KEY ("TenantId") REFERENCES "Associations" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_DigitalCards_Memberships_TenantId_MembershipId" FOREIGN KEY ("TenantId", "MembershipId") REFERENCES "Memberships" ("TenantId", "Id") ON DELETE RESTRICT
);

CREATE TABLE "MemberContactRequests" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "MembershipId" uuid NOT NULL,
    "RequesterId" uuid NOT NULL,
    "Message" text NOT NULL,
    "Status" text NOT NULL,
    "At" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_MemberContactRequests" PRIMARY KEY ("Id"),
    CONSTRAINT "AK_MemberContactRequests_TenantId_Id" UNIQUE ("TenantId", "Id"),
    CONSTRAINT "FK_MemberContactRequests_Associations_TenantId" FOREIGN KEY ("TenantId") REFERENCES "Associations" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_MemberContactRequests_Memberships_TenantId_MembershipId" FOREIGN KEY ("TenantId", "MembershipId") REFERENCES "Memberships" ("TenantId", "Id") ON DELETE RESTRICT
);

CREATE TABLE "MembershipDocuments" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "MembershipId" uuid NOT NULL,
    "Kind" text NOT NULL,
    "FileName" text NOT NULL,
    "ContentType" text NOT NULL,
    "Content" bytea NOT NULL,
    "Sha256" text NOT NULL,
    "Status" text NOT NULL,
    "UploaderId" uuid NOT NULL,
    "At" timestamp with time zone NOT NULL,
    "RetainUntil" timestamp with time zone,
    CONSTRAINT "PK_MembershipDocuments" PRIMARY KEY ("Id"),
    CONSTRAINT "AK_MembershipDocuments_TenantId_Id" UNIQUE ("TenantId", "Id"),
    CONSTRAINT "FK_MembershipDocuments_Associations_TenantId" FOREIGN KEY ("TenantId") REFERENCES "Associations" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_MembershipDocuments_Memberships_TenantId_MembershipId" FOREIGN KEY ("TenantId", "MembershipId") REFERENCES "Memberships" ("TenantId", "Id") ON DELETE RESTRICT
);

CREATE TABLE "MembershipHistory" (
    "Id" uuid NOT NULL,
    "CategoryId" uuid,
    "EndsAt" timestamp with time zone,
    "TenantId" uuid NOT NULL,
    "MembershipId" uuid NOT NULL,
    "OldStatus" text NOT NULL,
    "NewStatus" text NOT NULL,
    "Reason" text NOT NULL,
    "EffectiveAt" timestamp with time zone NOT NULL,
    "RecordedAt" timestamp with time zone NOT NULL,
    "ActorId" uuid NOT NULL,
    "ApprovalId" uuid,
    CONSTRAINT "PK_MembershipHistory" PRIMARY KEY ("Id"),
    CONSTRAINT "AK_MembershipHistory_TenantId_Id" UNIQUE ("TenantId", "Id"),
    CONSTRAINT "FK_MembershipHistory_Associations_TenantId" FOREIGN KEY ("TenantId") REFERENCES "Associations" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_MembershipHistory_Memberships_TenantId_MembershipId" FOREIGN KEY ("TenantId", "MembershipId") REFERENCES "Memberships" ("TenantId", "Id") ON DELETE RESTRICT
);

CREATE TABLE "MembershipRenewals" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "MembershipId" uuid NOT NULL,
    "ActorId" uuid NOT NULL,
    "RequestKey" text NOT NULL,
    "PreviousEnd" timestamp with time zone,
    "NewEnd" timestamp with time zone NOT NULL,
    "At" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_MembershipRenewals" PRIMARY KEY ("Id"),
    CONSTRAINT "AK_MembershipRenewals_TenantId_Id" UNIQUE ("TenantId", "Id"),
    CONSTRAINT "FK_MembershipRenewals_Associations_TenantId" FOREIGN KEY ("TenantId") REFERENCES "Associations" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_MembershipRenewals_Memberships_TenantId_MembershipId" FOREIGN KEY ("TenantId", "MembershipId") REFERENCES "Memberships" ("TenantId", "Id") ON DELETE RESTRICT
);

CREATE TABLE "MembershipTransfers" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "MembershipId" uuid NOT NULL,
    "DestinationClubId" uuid NOT NULL,
    "CategoryId" uuid NOT NULL,
    "RequesterId" uuid NOT NULL,
    "DestinationMembershipId" uuid,
    "Status" text NOT NULL,
    "Reason" text NOT NULL,
    "EffectiveAt" timestamp with time zone NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "Version" bigint NOT NULL,
    "SourceRequired" boolean NOT NULL,
    "AssociationRequired" boolean NOT NULL,
    "ClearanceRequired" boolean NOT NULL,
    "FinancialCleared" boolean NOT NULL,
    CONSTRAINT "PK_MembershipTransfers" PRIMARY KEY ("Id"),
    CONSTRAINT "AK_MembershipTransfers_TenantId_Id" UNIQUE ("TenantId", "Id"),
    CONSTRAINT "FK_MembershipTransfers_Associations_TenantId" FOREIGN KEY ("TenantId") REFERENCES "Associations" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_MembershipTransfers_MembershipCategories_TenantId_CategoryId" FOREIGN KEY ("TenantId", "CategoryId") REFERENCES "MembershipCategories" ("TenantId", "Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_MembershipTransfers_Memberships_TenantId_DestinationMembers~" FOREIGN KEY ("TenantId", "DestinationMembershipId") REFERENCES "Memberships" ("TenantId", "Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_MembershipTransfers_Memberships_TenantId_MembershipId" FOREIGN KEY ("TenantId", "MembershipId") REFERENCES "Memberships" ("TenantId", "Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_MembershipTransfers_Orgs_TenantId_DestinationClubId" FOREIGN KEY ("TenantId", "DestinationClubId") REFERENCES "Orgs" ("TenantId", "Id") ON DELETE RESTRICT
);

CREATE TABLE "DigitalCardVerifications" (
    "Id" uuid NOT NULL,
    "CardId" uuid,
    "Valid" boolean NOT NULL,
    "At" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_DigitalCardVerifications" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_DigitalCardVerifications_DigitalCards_CardId" FOREIGN KEY ("CardId") REFERENCES "DigitalCards" ("Id") ON DELETE RESTRICT
);

CREATE TABLE "TransferDecisions" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "TransferId" uuid NOT NULL,
    "Stage" text NOT NULL,
    "Decision" text NOT NULL,
    "Comment" text NOT NULL,
    "ActorId" uuid NOT NULL,
    "At" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_TransferDecisions" PRIMARY KEY ("Id"),
    CONSTRAINT "AK_TransferDecisions_TenantId_Id" UNIQUE ("TenantId", "Id"),
    CONSTRAINT "FK_TransferDecisions_Associations_TenantId" FOREIGN KEY ("TenantId") REFERENCES "Associations" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_TransferDecisions_MembershipTransfers_TenantId_TransferId" FOREIGN KEY ("TenantId", "TransferId") REFERENCES "MembershipTransfers" ("TenantId", "Id") ON DELETE RESTRICT
);

INSERT INTO "NotificationTemplates" ("Id", "Body", "Subject")
VALUES ('application.approved', '{detail}', 'ClubSphere · application approved');
INSERT INTO "NotificationTemplates" ("Id", "Body", "Subject")
VALUES ('application.changes', '{detail}', 'ClubSphere · application changes');
INSERT INTO "NotificationTemplates" ("Id", "Body", "Subject")
VALUES ('application.rejected', '{detail}', 'ClubSphere · application rejected');
INSERT INTO "NotificationTemplates" ("Id", "Body", "Subject")
VALUES ('application.submitted', '{detail}', 'ClubSphere · application submitted');
INSERT INTO "NotificationTemplates" ("Id", "Body", "Subject")
VALUES ('approval.request', '{detail}', 'ClubSphere · approval request');
INSERT INTO "NotificationTemplates" ("Id", "Body", "Subject")
VALUES ('contact.request', '{detail}', 'ClubSphere · contact request');
INSERT INTO "NotificationTemplates" ("Id", "Body", "Subject")
VALUES ('contact.response', '{detail}', 'ClubSphere · contact response');
INSERT INTO "NotificationTemplates" ("Id", "Body", "Subject")
VALUES ('import.completed', '{detail}', 'ClubSphere · import completed');
INSERT INTO "NotificationTemplates" ("Id", "Body", "Subject")
VALUES ('membership.activated', '{detail}', 'ClubSphere · membership activated');
INSERT INTO "NotificationTemplates" ("Id", "Body", "Subject")
VALUES ('membership.invitation', '{detail}', 'ClubSphere · membership invitation');
INSERT INTO "NotificationTemplates" ("Id", "Body", "Subject")
VALUES ('membership.renewed', '{detail}', 'ClubSphere · membership renewed');
INSERT INTO "NotificationTemplates" ("Id", "Body", "Subject")
VALUES ('transfer.status', '{detail}', 'ClubSphere · transfer status');

INSERT INTO "Permissions" ("Id")
VALUES ('directory.view');
INSERT INTO "Permissions" ("Id")
VALUES ('members.approve');
INSERT INTO "Permissions" ("Id")
VALUES ('members.cards');
INSERT INTO "Permissions" ("Id")
VALUES ('members.contacts.view');
INSERT INTO "Permissions" ("Id")
VALUES ('members.documents');
INSERT INTO "Permissions" ("Id")
VALUES ('members.export');
INSERT INTO "Permissions" ("Id")
VALUES ('members.import');
INSERT INTO "Permissions" ("Id")
VALUES ('members.invite');
INSERT INTO "Permissions" ("Id")
VALUES ('members.link');
INSERT INTO "Permissions" ("Id")
VALUES ('members.manage');
INSERT INTO "Permissions" ("Id")
VALUES ('members.personal.view');
INSERT INTO "Permissions" ("Id")
VALUES ('members.transfer.approve');
INSERT INTO "Permissions" ("Id")
VALUES ('members.view');
INSERT INTO "Permissions" ("Id")
VALUES ('members.workflow.manage');

CREATE INDEX "IX_Assignments_TenantId_MembershipId" ON "Assignments" ("TenantId", "MembershipId");

CREATE UNIQUE INDEX "IX_DigitalCards_MembershipId" ON "DigitalCards" ("MembershipId") WHERE "RevokedAt" IS NULL;

CREATE INDEX "IX_DigitalCards_TenantId_MembershipId" ON "DigitalCards" ("TenantId", "MembershipId");

CREATE UNIQUE INDEX "IX_DigitalCards_TokenHash" ON "DigitalCards" ("TokenHash");

CREATE INDEX "IX_DigitalCardVerifications_At" ON "DigitalCardVerifications" ("At");

CREATE INDEX "IX_DigitalCardVerifications_CardId" ON "DigitalCardVerifications" ("CardId");

CREATE INDEX "IX_MailDeliveryAttempts_MailId" ON "MailDeliveryAttempts" ("MailId");

CREATE INDEX "IX_MemberActivities_PersonId" ON "MemberActivities" ("PersonId");

CREATE INDEX "IX_MemberActivities_TenantId_ClubId" ON "MemberActivities" ("TenantId", "ClubId");

CREATE INDEX "IX_MemberActivities_TenantId_PersonId_At" ON "MemberActivities" ("TenantId", "PersonId", "At");

CREATE INDEX "IX_MemberContactRequests_TenantId_MembershipId" ON "MemberContactRequests" ("TenantId", "MembershipId");

CREATE INDEX "IX_MemberImportJobs_Status_LeaseUntil" ON "MemberImportJobs" ("Status", "LeaseUntil");

CREATE INDEX "IX_MemberImportJobs_TenantId_CategoryId" ON "MemberImportJobs" ("TenantId", "CategoryId");

CREATE INDEX "IX_MemberImportJobs_TenantId_ClubId" ON "MemberImportJobs" ("TenantId", "ClubId");

CREATE UNIQUE INDEX "IX_MemberImportJobs_TenantId_CreatorId_RequestKey" ON "MemberImportJobs" ("TenantId", "CreatorId", "RequestKey");

CREATE UNIQUE INDEX "IX_MemberImportRows_JobId_RowNumber" ON "MemberImportRows" ("JobId", "RowNumber");

CREATE INDEX "IX_MemberImportRows_TenantId_JobId" ON "MemberImportRows" ("TenantId", "JobId");

CREATE INDEX "IX_MemberRelationships_PersonId" ON "MemberRelationships" ("PersonId");

CREATE INDEX "IX_MemberRelationships_RelatedPersonId" ON "MemberRelationships" ("RelatedPersonId");

CREATE UNIQUE INDEX "IX_MemberSavedViews_TenantId_UserId_Name" ON "MemberSavedViews" ("TenantId", "UserId", "Name");

CREATE INDEX "IX_MemberSavedViews_UserId" ON "MemberSavedViews" ("UserId");

CREATE INDEX "IX_MembershipApplications_ApplicantUserId" ON "MembershipApplications" ("ApplicantUserId");

CREATE UNIQUE INDEX "IX_MembershipApplications_InvitationId_ApplicantUserId" ON "MembershipApplications" ("InvitationId", "ApplicantUserId") WHERE "InvitationId" IS NOT NULL;

CREATE UNIQUE INDEX "IX_MembershipApplications_PersonId_ClubId_CategoryId" ON "MembershipApplications" ("PersonId", "ClubId", "CategoryId") WHERE "Status" IN ('Draft','Submitted','Under Review','Changes Requested');

CREATE INDEX "IX_MembershipApplications_TenantId_CategoryId" ON "MembershipApplications" ("TenantId", "CategoryId");

CREATE INDEX "IX_MembershipApplications_TenantId_ClubId_Status_CurrentSequen~" ON "MembershipApplications" ("TenantId", "ClubId", "Status", "CurrentSequence");

CREATE INDEX "IX_MembershipApplications_TenantId_InvitationId" ON "MembershipApplications" ("TenantId", "InvitationId");

CREATE INDEX "IX_MembershipApplications_TenantId_WorkflowId" ON "MembershipApplications" ("TenantId", "WorkflowId");

CREATE UNIQUE INDEX "IX_MembershipApprovals_ApplicationId_Revision_StageId_ActorId" ON "MembershipApprovals" ("ApplicationId", "Revision", "StageId", "ActorId");

CREATE INDEX "IX_MembershipApprovals_TenantId_ApplicationId" ON "MembershipApprovals" ("TenantId", "ApplicationId");

CREATE INDEX "IX_MembershipApprovals_TenantId_StageId" ON "MembershipApprovals" ("TenantId", "StageId");

CREATE INDEX "IX_MembershipApprovalStages_TenantId_WorkflowId" ON "MembershipApprovalStages" ("TenantId", "WorkflowId");

CREATE UNIQUE INDEX "IX_MembershipApprovalStages_WorkflowId_Sequence" ON "MembershipApprovalStages" ("WorkflowId", "Sequence");

CREATE UNIQUE INDEX "IX_MembershipCategories_TenantId_Code" ON "MembershipCategories" ("TenantId", "Code");

CREATE INDEX "IX_MembershipDocuments_TenantId_MembershipId_At" ON "MembershipDocuments" ("TenantId", "MembershipId", "At");

CREATE INDEX "IX_MembershipHistory_TenantId_MembershipId_EffectiveAt" ON "MembershipHistory" ("TenantId", "MembershipId", "EffectiveAt");

CREATE INDEX "IX_MembershipInvitations_TenantId_CategoryId" ON "MembershipInvitations" ("TenantId", "CategoryId");

CREATE INDEX "IX_MembershipInvitations_TenantId_ClubId" ON "MembershipInvitations" ("TenantId", "ClubId");

CREATE UNIQUE INDEX "IX_MembershipInvitations_TokenHash" ON "MembershipInvitations" ("TokenHash");

CREATE UNIQUE INDEX "IX_MembershipRenewals_MembershipId_RequestKey" ON "MembershipRenewals" ("MembershipId", "RequestKey");

CREATE INDEX "IX_MembershipRenewals_TenantId_MembershipId" ON "MembershipRenewals" ("TenantId", "MembershipId");

CREATE UNIQUE INDEX "IX_Memberships_ImportRowId" ON "Memberships" ("ImportRowId") WHERE "ImportRowId" IS NOT NULL;

CREATE UNIQUE INDEX "IX_Memberships_Number" ON "Memberships" ("Number");

CREATE INDEX "IX_Memberships_PersonId" ON "Memberships" ("PersonId");

CREATE UNIQUE INDEX "IX_Memberships_SourceApplicationId" ON "Memberships" ("SourceApplicationId") WHERE "SourceApplicationId" IS NOT NULL;

CREATE INDEX "IX_Memberships_TenantId_CategoryId" ON "Memberships" ("TenantId", "CategoryId");

CREATE INDEX "IX_Memberships_TenantId_ClubId_Status_Id" ON "Memberships" ("TenantId", "ClubId", "Status", "Id");

CREATE INDEX "IX_Memberships_TenantId_ImportRowId" ON "Memberships" ("TenantId", "ImportRowId");

CREATE INDEX "IX_Memberships_TenantId_JoinedAt" ON "Memberships" ("TenantId", "JoinedAt");

CREATE UNIQUE INDEX "IX_Memberships_TenantId_PersonId_ClubId_CategoryId" ON "Memberships" ("TenantId", "PersonId", "ClubId", "CategoryId") WHERE "Status" IN ('Active','Inactive','Suspended','Expired');

CREATE INDEX "IX_Memberships_TenantId_SourceApplicationId" ON "Memberships" ("TenantId", "SourceApplicationId");

CREATE INDEX "IX_Memberships_TenantId_Status_EndsAt" ON "Memberships" ("TenantId", "Status", "EndsAt");

CREATE UNIQUE INDEX "IX_MembershipStageRoles_StageId_RoleId" ON "MembershipStageRoles" ("StageId", "RoleId");

CREATE INDEX "IX_MembershipStageRoles_TenantId_RoleId" ON "MembershipStageRoles" ("TenantId", "RoleId");

CREATE INDEX "IX_MembershipStageRoles_TenantId_StageId" ON "MembershipStageRoles" ("TenantId", "StageId");

CREATE UNIQUE INDEX "IX_MembershipTransfers_MembershipId" ON "MembershipTransfers" ("MembershipId") WHERE "Status" IN ('PendingSource','PendingDestination','PendingAssociation','Scheduled');

CREATE INDEX "IX_MembershipTransfers_TenantId_CategoryId" ON "MembershipTransfers" ("TenantId", "CategoryId");

CREATE INDEX "IX_MembershipTransfers_TenantId_DestinationClubId" ON "MembershipTransfers" ("TenantId", "DestinationClubId");

CREATE INDEX "IX_MembershipTransfers_TenantId_DestinationMembershipId" ON "MembershipTransfers" ("TenantId", "DestinationMembershipId");

CREATE INDEX "IX_MembershipTransfers_TenantId_MembershipId" ON "MembershipTransfers" ("TenantId", "MembershipId");

CREATE UNIQUE INDEX "IX_MembershipWorkflows_TenantId" ON "MembershipWorkflows" ("TenantId") WHERE "Active";

CREATE INDEX "IX_PersonAddresses_City_PersonId" ON "PersonAddresses" ("City", "PersonId");

CREATE INDEX "IX_PersonAddresses_PersonId" ON "PersonAddresses" ("PersonId");

CREATE INDEX "IX_PersonContacts_NormalizedValue" ON "PersonContacts" ("NormalizedValue");

CREATE INDEX "IX_PersonContacts_PersonId" ON "PersonContacts" ("PersonId");

CREATE UNIQUE INDEX "IX_PersonLinkRequests_InvitationId_UserId" ON "PersonLinkRequests" ("InvitationId", "UserId");

CREATE UNIQUE INDEX "IX_PersonLinkRequests_PersonId_UserId" ON "PersonLinkRequests" ("PersonId", "UserId") WHERE "Status"='Pending';

CREATE INDEX "IX_PersonLinkRequests_TenantId_ClubId" ON "PersonLinkRequests" ("TenantId", "ClubId");

CREATE INDEX "IX_PersonLinkRequests_TenantId_InvitationId" ON "PersonLinkRequests" ("TenantId", "InvitationId");

CREATE INDEX "IX_PersonLinkRequests_UserId" ON "PersonLinkRequests" ("UserId");

CREATE UNIQUE INDEX "IX_Persons_UserId" ON "Persons" ("UserId") WHERE "UserId" IS NOT NULL;

CREATE UNIQUE INDEX "IX_PersonTags_PersonId_Kind_Value" ON "PersonTags" ("PersonId", "Kind", "Value");

CREATE INDEX "IX_TransferDecisions_TenantId_TransferId" ON "TransferDecisions" ("TenantId", "TransferId");

CREATE UNIQUE INDEX "IX_TransferDecisions_TransferId_Stage" ON "TransferDecisions" ("TransferId", "Stage");

ALTER TABLE "Assignments" ADD CONSTRAINT "FK_Assignments_Memberships_TenantId_MembershipId" FOREIGN KEY ("TenantId", "MembershipId") REFERENCES "Memberships" ("TenantId", "Id") ON DELETE RESTRICT;


CREATE SEQUENCE membership_numbers;
UPDATE "UserProfiles" SET "Theme"='light' WHERE "Theme"='';
CREATE INDEX "IX_Persons_SearchName_Trigram" ON "Persons" USING gin ("SearchName" gin_trgm_ops);
CREATE INDEX "IX_Persons_LegalName_Normalized" ON "Persons" (lower("LegalName"));
ALTER TABLE "Persons" ADD CONSTRAINT "CK_Person_Name" CHECK(length(trim("LegalName")) BETWEEN 2 AND 200 AND length("DisplayName") BETWEEN 1 AND 200);
ALTER TABLE "Memberships" ADD CONSTRAINT "CK_Membership_Status" CHECK("Status" IN ('Active','Inactive','Suspended','Expired','Transferred','Resigned','Terminated','Deceased'));
ALTER TABLE "Memberships" ADD CONSTRAINT "CK_Membership_Dates" CHECK("EndsAt" IS NULL OR "EndsAt">"JoinedAt");
ALTER TABLE "MembershipApplications" ADD CONSTRAINT "CK_Application_Status" CHECK("Status" IN ('Draft','Submitted','Under Review','Changes Requested','Approved','Rejected','Withdrawn','Cancelled'));
ALTER TABLE "MembershipApprovalStages" ADD CONSTRAINT "CK_Stage_Quorum" CHECK("RequiredApprovals" BETWEEN 1 AND 20 AND "Sequence">0);
ALTER TABLE "MembershipInvitations" ADD CONSTRAINT "CK_Invitation_Limits" CHECK("UsageLimit" BETWEEN 1 AND 20 AND "Uses" BETWEEN 0 AND "UsageLimit" AND "ExpiresAt">"CreatedAt");
ALTER TABLE "MembershipDocuments" ADD CONSTRAINT "CK_Document_Size" CHECK(octet_length("Content") BETWEEN 1 AND 5242880 AND "ContentType" IN ('application/pdf','image/png','image/jpeg'));
ALTER TABLE "MemberImportJobs" ADD CONSTRAINT "CK_Import_Size" CHECK(octet_length("Payload")<=10485760);
CREATE FUNCTION clubs_membership_guard() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
 PERFORM pg_advisory_xact_lock(hashtext(NEW."PersonId"::text));
 IF NOT EXISTS(SELECT 1 FROM "Orgs" o JOIN "Levels" l ON o."LevelId"=l."Id" AND o."TenantId"=l."TenantId" WHERE o."Id"=NEW."ClubId" AND o."TenantId"=NEW."TenantId" AND l."IsClub") THEN RAISE EXCEPTION 'Membership target must be a club in its association' USING ERRCODE='23514'; END IF;
 IF TG_OP='UPDATE' AND (NEW."TenantId"<>OLD."TenantId" OR NEW."PersonId"<>OLD."PersonId" OR NEW."ClubId"<>OLD."ClubId") THEN RAISE EXCEPTION 'Affiliation identity is immutable; use a transfer' USING ERRCODE='23514'; END IF;
 IF NEW."Status" IN ('Active','Inactive','Suspended','Expired') AND EXISTS(SELECT 1 FROM "MembershipPolicies" p WHERE p."TenantId"=NEW."TenantId" AND NOT p."AllowMultipleClubs") AND EXISTS(SELECT 1 FROM "Memberships" m WHERE m."TenantId"=NEW."TenantId" AND m."PersonId"=NEW."PersonId" AND m."ClubId"<>NEW."ClubId" AND m."Id"<>NEW."Id" AND m."Status" IN ('Active','Inactive','Suspended','Expired')) THEN RAISE EXCEPTION 'Single-club association policy violated' USING ERRCODE='23514'; END IF;
 RETURN NEW;
END $$;
CREATE TRIGGER membership_integrity_guard BEFORE INSERT OR UPDATE ON "Memberships" FOR EACH ROW EXECUTE FUNCTION clubs_membership_guard();
CREATE FUNCTION clubs_membership_history_guard() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RAISE EXCEPTION 'Membership decisions and effective-dated history are append-only' USING ERRCODE='23514'; END $$;
CREATE TRIGGER membership_history_immutable BEFORE UPDATE OR DELETE ON "MembershipHistory" FOR EACH ROW EXECUTE FUNCTION clubs_membership_history_guard();
CREATE TRIGGER membership_approval_immutable BEFORE UPDATE OR DELETE ON "MembershipApprovals" FOR EACH ROW EXECUTE FUNCTION clubs_membership_history_guard();
CREATE TRIGGER transfer_decision_immutable BEFORE UPDATE OR DELETE ON "TransferDecisions" FOR EACH ROW EXECUTE FUNCTION clubs_membership_history_guard();
INSERT INTO "MembershipPolicies" ("TenantId","AllowMultipleClubs","SourceReviewRequired","AssociationReviewRequired","FinancialClearanceRequired") SELECT "Id",true,true,false,false FROM "Tenants";
INSERT INTO "RolePermissions" ("Id","RoleId","PermissionId") SELECT gen_random_uuid(),r."Id",p."Id" FROM "Roles" r CROSS JOIN "Permissions" p WHERE ((EXISTS(SELECT 1 FROM "RolePermissions" g WHERE g."RoleId"=r."Id" AND g."PermissionId"='roles.manage') AND EXISTS(SELECT 1 FROM "RolePermissions" g WHERE g."RoleId"=r."Id" AND g."PermissionId"='settings.manage') AND EXISTS(SELECT 1 FROM "RolePermissions" g WHERE g."RoleId"=r."Id" AND g."PermissionId"='roles.assign')) AND (p."Id" LIKE 'members.%' OR p."Id"='directory.view')) OR (r."Name" IN ('Club Member','Guest / Applicant') AND p."Id"='directory.view') OR (r."Name" IN ('Club Secretary','Club President','Club Manager','Club Treasurer','International Administrator','National Administrator','District Administrator','Regional Administrator','Zone Administrator') AND p."Id" IN ('members.view','members.manage','members.contacts.view','members.export','members.import','members.invite','members.transfer.approve','members.link','members.documents','members.cards','directory.view')) OR (r."Name" IN ('Club Secretary','Club President') AND p."Id"='members.approve') ON CONFLICT ("RoleId","PermissionId") DO NOTHING;


INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261110182649_MembershipFoundation', '10.0.0');

COMMIT;

START TRANSACTION;
CREATE TABLE "RoleAssignmentHistory" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "AssignmentId" uuid NOT NULL,
    "UserId" uuid NOT NULL,
    "RoleId" uuid NOT NULL,
    "OrgId" uuid NOT NULL,
    "RoleName" text NOT NULL,
    "OrganizationName" text NOT NULL,
    "Action" text NOT NULL,
    "At" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_RoleAssignmentHistory" PRIMARY KEY ("Id"),
    CONSTRAINT "AK_RoleAssignmentHistory_TenantId_Id" UNIQUE ("TenantId", "Id"),
    CONSTRAINT "FK_RoleAssignmentHistory_Associations_TenantId" FOREIGN KEY ("TenantId") REFERENCES "Associations" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_RoleAssignmentHistory_Orgs_TenantId_OrgId" FOREIGN KEY ("TenantId", "OrgId") REFERENCES "Orgs" ("TenantId", "Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_RoleAssignmentHistory_Roles_TenantId_RoleId" FOREIGN KEY ("TenantId", "RoleId") REFERENCES "Roles" ("TenantId", "Id") ON DELETE RESTRICT
);

CREATE INDEX "IX_RoleAssignmentHistory_TenantId_OrgId" ON "RoleAssignmentHistory" ("TenantId", "OrgId");

CREATE INDEX "IX_RoleAssignmentHistory_TenantId_RoleId" ON "RoleAssignmentHistory" ("TenantId", "RoleId");

CREATE INDEX "IX_RoleAssignmentHistory_UserId_TenantId_At" ON "RoleAssignmentHistory" ("UserId", "TenantId", "At");

CREATE OR REPLACE FUNCTION clubs_membership_guard() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
 PERFORM pg_advisory_xact_lock(hashtext(NEW."TenantId"::text));
 IF NOT EXISTS(SELECT 1 FROM "Orgs" o JOIN "Levels" l ON o."LevelId"=l."Id" AND o."TenantId"=l."TenantId" WHERE o."Id"=NEW."ClubId" AND o."TenantId"=NEW."TenantId" AND l."IsClub") THEN RAISE EXCEPTION 'Membership target must be a club in its association' USING ERRCODE='23514'; END IF;
 IF TG_OP='UPDATE' AND (NEW."TenantId"<>OLD."TenantId" OR NEW."PersonId"<>OLD."PersonId" OR NEW."ClubId"<>OLD."ClubId") THEN RAISE EXCEPTION 'Affiliation identity is immutable; use a transfer' USING ERRCODE='23514'; END IF;
 IF NEW."Status" IN ('Active','Inactive','Suspended','Expired') AND EXISTS(SELECT 1 FROM "MembershipPolicies" p WHERE p."TenantId"=NEW."TenantId" AND NOT p."AllowMultipleClubs") AND EXISTS(SELECT 1 FROM "Memberships" m WHERE m."TenantId"=NEW."TenantId" AND m."PersonId"=NEW."PersonId" AND m."ClubId"<>NEW."ClubId" AND m."Id"<>NEW."Id" AND m."Status" IN ('Active','Inactive','Suspended','Expired')) THEN RAISE EXCEPTION 'Single-club association policy violated' USING ERRCODE='23514'; END IF;
 RETURN NEW;
END $$;
CREATE INDEX "IX_Persons_DisplayName_Id" ON "Persons" ("DisplayName","Id");
CREATE INDEX "IX_Membership_Expiry_Due" ON "Memberships" ("EndsAt","Id") WHERE "Status" IN ('Active','Inactive') AND "EndsAt" IS NOT NULL;
CREATE FUNCTION clubs_role_assignment_history() RETURNS trigger LANGUAGE plpgsql AS $$
DECLARE item record; verb text;
BEGIN
 IF TG_OP='DELETE' THEN item:=OLD;verb:='Revoked';ELSE item:=NEW;verb:='Granted';END IF;
 INSERT INTO "RoleAssignmentHistory" ("Id","TenantId","AssignmentId","UserId","RoleId","OrgId","RoleName","OrganizationName","Action","At") SELECT gen_random_uuid(),item."TenantId",item."Id",item."UserId",item."RoleId",item."OrgId",r."Name",o."Name",verb,now() FROM "Roles" r JOIN "Orgs" o ON o."Id"=item."OrgId" AND o."TenantId"=item."TenantId" WHERE r."Id"=item."RoleId";
 RETURN NULL;
END $$;
CREATE TRIGGER role_assignment_history AFTER INSERT OR DELETE ON "Assignments" FOR EACH ROW EXECUTE FUNCTION clubs_role_assignment_history();
INSERT INTO "RoleAssignmentHistory" ("Id","TenantId","AssignmentId","UserId","RoleId","OrgId","RoleName","OrganizationName","Action","At") SELECT gen_random_uuid(),a."TenantId",a."Id",a."UserId",a."RoleId",a."OrgId",r."Name",o."Name",'Existing assignment snapshot',now() FROM "Assignments" a JOIN "Roles" r ON r."Id"=a."RoleId" JOIN "Orgs" o ON o."Id"=a."OrgId";
CREATE TRIGGER role_assignment_history_immutable BEFORE UPDATE OR DELETE ON "RoleAssignmentHistory" FOR EACH ROW EXECUTE FUNCTION clubs_membership_history_guard();


INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261110184942_MembershipRoleHistory', '10.0.0');

COMMIT;

START TRANSACTION;
CREATE INDEX "IX_Memberships_Number_Search" ON "Memberships" USING gin ("Number" gin_trgm_ops);
UPDATE "UserProfiles" SET "Locale"='en' WHERE "Locale"='';
UPDATE "UserProfiles" SET "TimeZone"='UTC' WHERE "TimeZone"='';

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261110193404_MembershipSearchAndPreferenceDefaults', '10.0.0');

COMMIT;

START TRANSACTION;
CREATE INDEX IF NOT EXISTS "IX_Membership_Directory_Cover" ON "Memberships" ("TenantId","ClubId") INCLUDE ("PersonId","Status","EndsAt");

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261110193859_MembershipDirectoryReadIndex', '10.0.0');

COMMIT;

START TRANSACTION;
ALTER TABLE "MembershipDocuments" DROP CONSTRAINT "CK_Document_Size";
ALTER TABLE "MembershipDocuments" ADD CONSTRAINT "CK_Document_Size" CHECK(
  (("Status"='Expired' AND octet_length("Content")=0)
    OR ("Status"<>'Expired' AND octet_length("Content") BETWEEN 1 AND 5242880))
  AND "ContentType" IN ('application/pdf','image/png','image/jpeg'));

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261110195901_MembershipDocumentRetention', '10.0.0');

COMMIT;

START TRANSACTION;
DROP INDEX "IX_MembershipWorkflows_TenantId";

ALTER TABLE "Orgs" ADD "BrandingColor" text NOT NULL DEFAULT '';

ALTER TABLE "Orgs" ADD "ClubType" text NOT NULL DEFAULT '';

ALTER TABLE "Orgs" ADD "Description" text NOT NULL DEFAULT '';

ALTER TABLE "Orgs" ADD "FiscalStartMonth" integer NOT NULL DEFAULT 0;

ALTER TABLE "Orgs" ADD "Latitude" double precision;

ALTER TABLE "Orgs" ADD "Longitude" double precision;

ALTER TABLE "Orgs" ADD "MeetingDay" text NOT NULL DEFAULT '';

ALTER TABLE "Orgs" ADD "MeetingSchedule" text NOT NULL DEFAULT '';

ALTER TABLE "Orgs" ADD "MeetingVenue" text NOT NULL DEFAULT '';

ALTER TABLE "Orgs" ADD "OperatingPolicies" text NOT NULL DEFAULT '';

ALTER TABLE "Orgs" ADD "PostalCode" text NOT NULL DEFAULT '';

ALTER TABLE "Orgs" ADD "PreferredLanguage" text NOT NULL DEFAULT '';

ALTER TABLE "Orgs" ADD "Registration" text NOT NULL DEFAULT '';

ALTER TABLE "MembershipWorkflows" ADD "Kind" text NOT NULL DEFAULT '';

ALTER TABLE "MembershipApprovalStages" ADD "AllowDelegation" boolean NOT NULL DEFAULT FALSE;

ALTER TABLE "MembershipApprovals" ALTER COLUMN "ApplicationId" DROP NOT NULL;

ALTER TABLE "MembershipApprovals" ADD "GovernanceRequestId" uuid;

ALTER TABLE "MembershipApprovals" ADD "PrincipalUserId" uuid;

ALTER TABLE "Assignments" ADD "AppointmentId" uuid;

CREATE TABLE "ClubHealthConfigurations" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "LeadershipWeight" integer NOT NULL,
    "MembershipWeight" integer NOT NULL,
    "GovernanceWeight" integer NOT NULL,
    "DataWeight" integer NOT NULL,
    "MinimumActiveMembers" integer NOT NULL,
    "Version" bigint NOT NULL,
    "RequireClubChangeApproval" boolean NOT NULL,
    CONSTRAINT "PK_ClubHealthConfigurations" PRIMARY KEY ("Id"),
    CONSTRAINT "AK_ClubHealthConfigurations_TenantId_Id" UNIQUE ("TenantId", "Id"),
    CONSTRAINT "FK_ClubHealthConfigurations_Associations_TenantId" FOREIGN KEY ("TenantId") REFERENCES "Associations" ("Id") ON DELETE RESTRICT
);

CREATE TABLE "CommitteeRoles" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "Name" text NOT NULL,
    "CanManage" boolean NOT NULL,
    "Active" boolean NOT NULL,
    CONSTRAINT "PK_CommitteeRoles" PRIMARY KEY ("Id"),
    CONSTRAINT "AK_CommitteeRoles_TenantId_Id" UNIQUE ("TenantId", "Id"),
    CONSTRAINT "FK_CommitteeRoles_Associations_TenantId" FOREIGN KEY ("TenantId") REFERENCES "Associations" ("Id") ON DELETE RESTRICT
);

CREATE TABLE "Committees" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "OrgId" uuid NOT NULL,
    "Name" text NOT NULL,
    "Code" text NOT NULL,
    "Description" text NOT NULL,
    "Purpose" text NOT NULL,
    "Category" text NOT NULL,
    "Notes" text NOT NULL,
    "StartsAt" timestamp with time zone NOT NULL,
    "EndsAt" timestamp with time zone NOT NULL,
    "Status" text NOT NULL,
    "Version" bigint NOT NULL,
    CONSTRAINT "PK_Committees" PRIMARY KEY ("Id"),
    CONSTRAINT "AK_Committees_TenantId_Id" UNIQUE ("TenantId", "Id"),
    CONSTRAINT "FK_Committees_Associations_TenantId" FOREIGN KEY ("TenantId") REFERENCES "Associations" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_Committees_Orgs_TenantId_OrgId" FOREIGN KEY ("TenantId", "OrgId") REFERENCES "Orgs" ("TenantId", "Id") ON DELETE RESTRICT
);

CREATE TABLE "GovernanceDelegations" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "OrgId" uuid NOT NULL,
    "RoleId" uuid NOT NULL,
    "DelegatorId" uuid NOT NULL,
    "DelegateId" uuid NOT NULL,
    "StartsAt" timestamp with time zone NOT NULL,
    "EndsAt" timestamp with time zone NOT NULL,
    "Revoked" boolean NOT NULL,
    CONSTRAINT "PK_GovernanceDelegations" PRIMARY KEY ("Id"),
    CONSTRAINT "AK_GovernanceDelegations_TenantId_Id" UNIQUE ("TenantId", "Id"),
    CONSTRAINT "FK_GovernanceDelegations_Associations_TenantId" FOREIGN KEY ("TenantId") REFERENCES "Associations" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_GovernanceDelegations_Orgs_TenantId_OrgId" FOREIGN KEY ("TenantId", "OrgId") REFERENCES "Orgs" ("TenantId", "Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_GovernanceDelegations_Roles_TenantId_RoleId" FOREIGN KEY ("TenantId", "RoleId") REFERENCES "Roles" ("TenantId", "Id") ON DELETE RESTRICT
);

CREATE TABLE "GovernanceResolutions" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "OrgId" uuid NOT NULL,
    "Number" text NOT NULL,
    "Title" text NOT NULL,
    "Subject" text NOT NULL,
    "Text" text NOT NULL,
    "Date" timestamp with time zone NOT NULL,
    "EffectiveAt" timestamp with time zone NOT NULL,
    "ProposerId" uuid NOT NULL,
    "Status" text NOT NULL,
    "Version" bigint NOT NULL,
    CONSTRAINT "PK_GovernanceResolutions" PRIMARY KEY ("Id"),
    CONSTRAINT "AK_GovernanceResolutions_TenantId_Id" UNIQUE ("TenantId", "Id"),
    CONSTRAINT "FK_GovernanceResolutions_Associations_TenantId" FOREIGN KEY ("TenantId") REFERENCES "Associations" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_GovernanceResolutions_Orgs_TenantId_OrgId" FOREIGN KEY ("TenantId", "OrgId") REFERENCES "Orgs" ("TenantId", "Id") ON DELETE RESTRICT
);

CREATE TABLE "LeadershipTerms" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "OrgId" uuid NOT NULL,
    "Name" text NOT NULL,
    "Calendar" text NOT NULL,
    "TimeZone" text NOT NULL,
    "StartsAt" timestamp with time zone NOT NULL,
    "EndsAt" timestamp with time zone NOT NULL,
    "Version" bigint NOT NULL,
    CONSTRAINT "PK_LeadershipTerms" PRIMARY KEY ("Id"),
    CONSTRAINT "AK_LeadershipTerms_TenantId_Id" UNIQUE ("TenantId", "Id"),
    CONSTRAINT "FK_LeadershipTerms_Associations_TenantId" FOREIGN KEY ("TenantId") REFERENCES "Associations" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_LeadershipTerms_Orgs_TenantId_OrgId" FOREIGN KEY ("TenantId", "OrgId") REFERENCES "Orgs" ("TenantId", "Id") ON DELETE RESTRICT
);

CREATE TABLE "OfficerPositions" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "LevelId" uuid NOT NULL,
    "Code" text NOT NULL,
    "Title" text NOT NULL,
    "Category" text NOT NULL,
    "DefaultMonths" integer NOT NULL,
    "MaximumHolders" integer,
    "Mandatory" boolean NOT NULL,
    "RequireActiveMembership" boolean NOT NULL,
    "MinimumMembershipDays" integer NOT NULL,
    "ApprovalRequired" boolean NOT NULL,
    "ReportsToId" uuid,
    "GrantRoleId" uuid,
    "Active" boolean NOT NULL,
    "Version" bigint NOT NULL,
    CONSTRAINT "PK_OfficerPositions" PRIMARY KEY ("Id"),
    CONSTRAINT "AK_OfficerPositions_TenantId_Id" UNIQUE ("TenantId", "Id"),
    CONSTRAINT "FK_OfficerPositions_Associations_TenantId" FOREIGN KEY ("TenantId") REFERENCES "Associations" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_OfficerPositions_Levels_TenantId_LevelId" FOREIGN KEY ("TenantId", "LevelId") REFERENCES "Levels" ("TenantId", "Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_OfficerPositions_OfficerPositions_TenantId_ReportsToId" FOREIGN KEY ("TenantId", "ReportsToId") REFERENCES "OfficerPositions" ("TenantId", "Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_OfficerPositions_Roles_TenantId_GrantRoleId" FOREIGN KEY ("TenantId", "GrantRoleId") REFERENCES "Roles" ("TenantId", "Id") ON DELETE RESTRICT
);

CREATE TABLE "OrganizationHistory" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "OrgId" uuid NOT NULL,
    "Before" text NOT NULL,
    "After" text NOT NULL,
    "ActorId" uuid,
    "At" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_OrganizationHistory" PRIMARY KEY ("Id"),
    CONSTRAINT "AK_OrganizationHistory_TenantId_Id" UNIQUE ("TenantId", "Id"),
    CONSTRAINT "FK_OrganizationHistory_Associations_TenantId" FOREIGN KEY ("TenantId") REFERENCES "Associations" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_OrganizationHistory_Orgs_TenantId_OrgId" FOREIGN KEY ("TenantId", "OrgId") REFERENCES "Orgs" ("TenantId", "Id") ON DELETE RESTRICT
);

CREATE TABLE "CommitteeActivities" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "CommitteeId" uuid NOT NULL,
    "ActorId" uuid NOT NULL,
    "Action" text NOT NULL,
    "Detail" text NOT NULL,
    "At" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_CommitteeActivities" PRIMARY KEY ("Id"),
    CONSTRAINT "AK_CommitteeActivities_TenantId_Id" UNIQUE ("TenantId", "Id"),
    CONSTRAINT "FK_CommitteeActivities_Associations_TenantId" FOREIGN KEY ("TenantId") REFERENCES "Associations" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_CommitteeActivities_Committees_TenantId_CommitteeId" FOREIGN KEY ("TenantId", "CommitteeId") REFERENCES "Committees" ("TenantId", "Id") ON DELETE RESTRICT
);

CREATE TABLE "CommitteeMemberships" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "CommitteeId" uuid NOT NULL,
    "PersonId" uuid NOT NULL,
    "RoleId" uuid NOT NULL,
    "Responsibilities" text NOT NULL,
    "StartsAt" timestamp with time zone NOT NULL,
    "EndsAt" timestamp with time zone NOT NULL,
    "Status" text NOT NULL,
    "Version" bigint NOT NULL,
    CONSTRAINT "PK_CommitteeMemberships" PRIMARY KEY ("Id"),
    CONSTRAINT "AK_CommitteeMemberships_TenantId_Id" UNIQUE ("TenantId", "Id"),
    CONSTRAINT "FK_CommitteeMemberships_Associations_TenantId" FOREIGN KEY ("TenantId") REFERENCES "Associations" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_CommitteeMemberships_CommitteeRoles_TenantId_RoleId" FOREIGN KEY ("TenantId", "RoleId") REFERENCES "CommitteeRoles" ("TenantId", "Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_CommitteeMemberships_Committees_TenantId_CommitteeId" FOREIGN KEY ("TenantId", "CommitteeId") REFERENCES "Committees" ("TenantId", "Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_CommitteeMemberships_Persons_PersonId" FOREIGN KEY ("PersonId") REFERENCES "Persons" ("Id") ON DELETE RESTRICT
);

CREATE TABLE "GovernanceDocuments" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "OrgId" uuid NOT NULL,
    "CommitteeId" uuid,
    "Kind" text NOT NULL,
    "Title" text NOT NULL,
    "FileName" text NOT NULL,
    "ContentType" text NOT NULL,
    "Content" bytea NOT NULL,
    "Sha256" text NOT NULL,
    "UploaderId" uuid NOT NULL,
    "ShareWithSuccessors" boolean NOT NULL,
    "At" timestamp with time zone NOT NULL,
    "RetainUntil" timestamp with time zone,
    "Status" text NOT NULL,
    CONSTRAINT "PK_GovernanceDocuments" PRIMARY KEY ("Id"),
    CONSTRAINT "AK_GovernanceDocuments_TenantId_Id" UNIQUE ("TenantId", "Id"),
    CONSTRAINT "FK_GovernanceDocuments_Associations_TenantId" FOREIGN KEY ("TenantId") REFERENCES "Associations" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_GovernanceDocuments_Committees_TenantId_CommitteeId" FOREIGN KEY ("TenantId", "CommitteeId") REFERENCES "Committees" ("TenantId", "Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_GovernanceDocuments_Orgs_TenantId_OrgId" FOREIGN KEY ("TenantId", "OrgId") REFERENCES "Orgs" ("TenantId", "Id") ON DELETE RESTRICT
);

CREATE TABLE "OfficerAppointments" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "OrgId" uuid NOT NULL,
    "PersonId" uuid NOT NULL,
    "PositionId" uuid NOT NULL,
    "TermId" uuid,
    "MembershipId" uuid,
    "GrantRoleId" uuid,
    "Method" text NOT NULL,
    "Status" text NOT NULL,
    "StartsAt" timestamp with time zone NOT NULL,
    "EndsAt" timestamp with time zone NOT NULL,
    "EffectiveAt" timestamp with time zone NOT NULL,
    "ProposerId" uuid NOT NULL,
    "ApproverId" uuid,
    "DocumentId" uuid,
    "Notes" text NOT NULL,
    "Version" bigint NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_OfficerAppointments" PRIMARY KEY ("Id"),
    CONSTRAINT "AK_OfficerAppointments_TenantId_Id" UNIQUE ("TenantId", "Id"),
    CONSTRAINT "FK_OfficerAppointments_Associations_TenantId" FOREIGN KEY ("TenantId") REFERENCES "Associations" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_OfficerAppointments_GovernanceDocuments_TenantId_DocumentId" FOREIGN KEY ("TenantId", "DocumentId") REFERENCES "GovernanceDocuments" ("TenantId", "Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_OfficerAppointments_LeadershipTerms_TenantId_TermId" FOREIGN KEY ("TenantId", "TermId") REFERENCES "LeadershipTerms" ("TenantId", "Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_OfficerAppointments_Memberships_TenantId_MembershipId" FOREIGN KEY ("TenantId", "MembershipId") REFERENCES "Memberships" ("TenantId", "Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_OfficerAppointments_OfficerPositions_TenantId_PositionId" FOREIGN KEY ("TenantId", "PositionId") REFERENCES "OfficerPositions" ("TenantId", "Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_OfficerAppointments_Orgs_TenantId_OrgId" FOREIGN KEY ("TenantId", "OrgId") REFERENCES "Orgs" ("TenantId", "Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_OfficerAppointments_Persons_PersonId" FOREIGN KEY ("PersonId") REFERENCES "Persons" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_OfficerAppointments_Roles_TenantId_GrantRoleId" FOREIGN KEY ("TenantId", "GrantRoleId") REFERENCES "Roles" ("TenantId", "Id") ON DELETE RESTRICT
);

CREATE TABLE "LeadershipHandovers" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "OrgId" uuid NOT NULL,
    "OutgoingAppointmentId" uuid NOT NULL,
    "IncomingAppointmentId" uuid NOT NULL,
    "Notes" text NOT NULL,
    "Status" text NOT NULL,
    "CreatorId" uuid NOT NULL,
    "AcceptedAt" timestamp with time zone,
    "Snapshot" text NOT NULL,
    "Version" bigint NOT NULL,
    "ArchivedAt" timestamp with time zone,
    CONSTRAINT "PK_LeadershipHandovers" PRIMARY KEY ("Id"),
    CONSTRAINT "AK_LeadershipHandovers_TenantId_Id" UNIQUE ("TenantId", "Id"),
    CONSTRAINT "FK_LeadershipHandovers_Associations_TenantId" FOREIGN KEY ("TenantId") REFERENCES "Associations" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_LeadershipHandovers_OfficerAppointments_TenantId_IncomingAp~" FOREIGN KEY ("TenantId", "IncomingAppointmentId") REFERENCES "OfficerAppointments" ("TenantId", "Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_LeadershipHandovers_OfficerAppointments_TenantId_OutgoingAp~" FOREIGN KEY ("TenantId", "OutgoingAppointmentId") REFERENCES "OfficerAppointments" ("TenantId", "Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_LeadershipHandovers_Orgs_TenantId_OrgId" FOREIGN KEY ("TenantId", "OrgId") REFERENCES "Orgs" ("TenantId", "Id") ON DELETE RESTRICT
);

CREATE TABLE "OfficerAppointmentHistory" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "AppointmentId" uuid NOT NULL,
    "Status" text NOT NULL,
    "Detail" text NOT NULL,
    "ActorId" uuid,
    "At" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_OfficerAppointmentHistory" PRIMARY KEY ("Id"),
    CONSTRAINT "AK_OfficerAppointmentHistory_TenantId_Id" UNIQUE ("TenantId", "Id"),
    CONSTRAINT "FK_OfficerAppointmentHistory_Associations_TenantId" FOREIGN KEY ("TenantId") REFERENCES "Associations" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_OfficerAppointmentHistory_OfficerAppointments_TenantId_Appo~" FOREIGN KEY ("TenantId", "AppointmentId") REFERENCES "OfficerAppointments" ("TenantId", "Id") ON DELETE RESTRICT
);

CREATE TABLE "GovernanceRequests" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "OrgId" uuid NOT NULL,
    "Kind" text NOT NULL,
    "Title" text NOT NULL,
    "Payload" text NOT NULL,
    "Status" text NOT NULL,
    "CreatorId" uuid NOT NULL,
    "SubjectUserId" uuid,
    "WorkflowId" uuid,
    "CurrentSequence" integer NOT NULL,
    "Revision" integer NOT NULL,
    "AppointmentId" uuid,
    "CommitteeId" uuid,
    "HandoverId" uuid,
    "ResolutionId" uuid,
    "DocumentId" uuid,
    "ResultId" uuid,
    "Version" bigint NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_GovernanceRequests" PRIMARY KEY ("Id"),
    CONSTRAINT "AK_GovernanceRequests_TenantId_Id" UNIQUE ("TenantId", "Id"),
    CONSTRAINT "FK_GovernanceRequests_Associations_TenantId" FOREIGN KEY ("TenantId") REFERENCES "Associations" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_GovernanceRequests_Committees_TenantId_CommitteeId" FOREIGN KEY ("TenantId", "CommitteeId") REFERENCES "Committees" ("TenantId", "Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_GovernanceRequests_GovernanceDocuments_TenantId_DocumentId" FOREIGN KEY ("TenantId", "DocumentId") REFERENCES "GovernanceDocuments" ("TenantId", "Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_GovernanceRequests_GovernanceResolutions_TenantId_Resolutio~" FOREIGN KEY ("TenantId", "ResolutionId") REFERENCES "GovernanceResolutions" ("TenantId", "Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_GovernanceRequests_LeadershipHandovers_TenantId_HandoverId" FOREIGN KEY ("TenantId", "HandoverId") REFERENCES "LeadershipHandovers" ("TenantId", "Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_GovernanceRequests_MembershipWorkflows_TenantId_WorkflowId" FOREIGN KEY ("TenantId", "WorkflowId") REFERENCES "MembershipWorkflows" ("TenantId", "Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_GovernanceRequests_OfficerAppointments_TenantId_Appointment~" FOREIGN KEY ("TenantId", "AppointmentId") REFERENCES "OfficerAppointments" ("TenantId", "Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_GovernanceRequests_Orgs_TenantId_OrgId" FOREIGN KEY ("TenantId", "OrgId") REFERENCES "Orgs" ("TenantId", "Id") ON DELETE RESTRICT
);

CREATE TABLE "HandoverChecklistItems" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "HandoverId" uuid NOT NULL,
    "Label" text NOT NULL,
    "Notes" text NOT NULL,
    "Completed" boolean NOT NULL,
    "DocumentId" uuid,
    CONSTRAINT "PK_HandoverChecklistItems" PRIMARY KEY ("Id"),
    CONSTRAINT "AK_HandoverChecklistItems_TenantId_Id" UNIQUE ("TenantId", "Id"),
    CONSTRAINT "FK_HandoverChecklistItems_Associations_TenantId" FOREIGN KEY ("TenantId") REFERENCES "Associations" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_HandoverChecklistItems_GovernanceDocuments_TenantId_Documen~" FOREIGN KEY ("TenantId", "DocumentId") REFERENCES "GovernanceDocuments" ("TenantId", "Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_HandoverChecklistItems_LeadershipHandovers_TenantId_Handove~" FOREIGN KEY ("TenantId", "HandoverId") REFERENCES "LeadershipHandovers" ("TenantId", "Id") ON DELETE RESTRICT
);

INSERT INTO "Permissions" ("Id")
VALUES ('clubs.manage');
INSERT INTO "Permissions" ("Id")
VALUES ('clubs.view');
INSERT INTO "Permissions" ("Id")
VALUES ('committees.manage');
INSERT INTO "Permissions" ("Id")
VALUES ('committees.view');
INSERT INTO "Permissions" ("Id")
VALUES ('governance.approve');
INSERT INTO "Permissions" ("Id")
VALUES ('governance.configure');
INSERT INTO "Permissions" ("Id")
VALUES ('governance.documents');
INSERT INTO "Permissions" ("Id")
VALUES ('governance.reports');
INSERT INTO "Permissions" ("Id")
VALUES ('governance.restructure');
INSERT INTO "Permissions" ("Id")
VALUES ('leadership.manage');
INSERT INTO "Permissions" ("Id")
VALUES ('leadership.view');

CREATE UNIQUE INDEX "IX_MembershipWorkflows_TenantId_Kind" ON "MembershipWorkflows" ("TenantId", "Kind") WHERE "Active";

CREATE UNIQUE INDEX "IX_MembershipApprovals_GovernanceRequestId_Revision_StageId_Ac~" ON "MembershipApprovals" ("GovernanceRequestId", "Revision", "StageId", "ActorId");

CREATE UNIQUE INDEX "IX_MembershipApprovals_GovernanceRequestId_Revision_StageId_Pr~" ON "MembershipApprovals" ("GovernanceRequestId", "Revision", "StageId", "PrincipalUserId");

CREATE INDEX "IX_MembershipApprovals_TenantId_GovernanceRequestId" ON "MembershipApprovals" ("TenantId", "GovernanceRequestId");

CREATE INDEX "IX_Assignments_TenantId_AppointmentId" ON "Assignments" ("TenantId", "AppointmentId");

CREATE UNIQUE INDEX "IX_ClubHealthConfigurations_TenantId" ON "ClubHealthConfigurations" ("TenantId");

CREATE INDEX "IX_CommitteeActivities_TenantId_CommitteeId" ON "CommitteeActivities" ("TenantId", "CommitteeId");

CREATE INDEX "IX_CommitteeMemberships_CommitteeId_Status_PersonId" ON "CommitteeMemberships" ("CommitteeId", "Status", "PersonId");

CREATE INDEX "IX_CommitteeMemberships_PersonId" ON "CommitteeMemberships" ("PersonId");

CREATE INDEX "IX_CommitteeMemberships_TenantId_CommitteeId" ON "CommitteeMemberships" ("TenantId", "CommitteeId");

CREATE INDEX "IX_CommitteeMemberships_TenantId_RoleId" ON "CommitteeMemberships" ("TenantId", "RoleId");

CREATE UNIQUE INDEX "IX_CommitteeRoles_TenantId_Name" ON "CommitteeRoles" ("TenantId", "Name");

CREATE INDEX "IX_Committees_TenantId_OrgId" ON "Committees" ("TenantId", "OrgId");

CREATE UNIQUE INDEX "IX_Committees_TenantId_OrgId_Code" ON "Committees" ("TenantId", "OrgId", "Code");

CREATE INDEX "IX_GovernanceDelegations_TenantId_OrgId" ON "GovernanceDelegations" ("TenantId", "OrgId");

CREATE INDEX "IX_GovernanceDelegations_TenantId_RoleId" ON "GovernanceDelegations" ("TenantId", "RoleId");

CREATE INDEX "IX_GovernanceDocuments_TenantId_CommitteeId" ON "GovernanceDocuments" ("TenantId", "CommitteeId");

CREATE INDEX "IX_GovernanceDocuments_TenantId_OrgId" ON "GovernanceDocuments" ("TenantId", "OrgId");

CREATE INDEX "IX_GovernanceRequests_TenantId_AppointmentId" ON "GovernanceRequests" ("TenantId", "AppointmentId");

CREATE INDEX "IX_GovernanceRequests_TenantId_CommitteeId" ON "GovernanceRequests" ("TenantId", "CommitteeId");

CREATE INDEX "IX_GovernanceRequests_TenantId_DocumentId" ON "GovernanceRequests" ("TenantId", "DocumentId");

CREATE INDEX "IX_GovernanceRequests_TenantId_HandoverId" ON "GovernanceRequests" ("TenantId", "HandoverId");

CREATE INDEX "IX_GovernanceRequests_TenantId_OrgId" ON "GovernanceRequests" ("TenantId", "OrgId");

CREATE INDEX "IX_GovernanceRequests_TenantId_OrgId_Status_CreatedAt" ON "GovernanceRequests" ("TenantId", "OrgId", "Status", "CreatedAt");

CREATE INDEX "IX_GovernanceRequests_TenantId_ResolutionId" ON "GovernanceRequests" ("TenantId", "ResolutionId");

CREATE INDEX "IX_GovernanceRequests_TenantId_WorkflowId" ON "GovernanceRequests" ("TenantId", "WorkflowId");

CREATE INDEX "IX_GovernanceResolutions_TenantId_OrgId" ON "GovernanceResolutions" ("TenantId", "OrgId");

CREATE UNIQUE INDEX "IX_GovernanceResolutions_TenantId_OrgId_Number" ON "GovernanceResolutions" ("TenantId", "OrgId", "Number");

CREATE INDEX "IX_HandoverChecklistItems_TenantId_DocumentId" ON "HandoverChecklistItems" ("TenantId", "DocumentId");

CREATE INDEX "IX_HandoverChecklistItems_TenantId_HandoverId" ON "HandoverChecklistItems" ("TenantId", "HandoverId");

CREATE INDEX "IX_LeadershipHandovers_TenantId_IncomingAppointmentId" ON "LeadershipHandovers" ("TenantId", "IncomingAppointmentId");

CREATE INDEX "IX_LeadershipHandovers_TenantId_OrgId" ON "LeadershipHandovers" ("TenantId", "OrgId");

CREATE INDEX "IX_LeadershipHandovers_TenantId_OutgoingAppointmentId" ON "LeadershipHandovers" ("TenantId", "OutgoingAppointmentId");

CREATE INDEX "IX_LeadershipTerms_TenantId_OrgId" ON "LeadershipTerms" ("TenantId", "OrgId");

CREATE INDEX "IX_OfficerAppointmentHistory_TenantId_AppointmentId" ON "OfficerAppointmentHistory" ("TenantId", "AppointmentId");

CREATE INDEX "IX_OfficerAppointments_PersonId_StartsAt_EndsAt" ON "OfficerAppointments" ("PersonId", "StartsAt", "EndsAt");

CREATE INDEX "IX_OfficerAppointments_TenantId_DocumentId" ON "OfficerAppointments" ("TenantId", "DocumentId");

CREATE INDEX "IX_OfficerAppointments_TenantId_GrantRoleId" ON "OfficerAppointments" ("TenantId", "GrantRoleId");

CREATE INDEX "IX_OfficerAppointments_TenantId_MembershipId" ON "OfficerAppointments" ("TenantId", "MembershipId");

CREATE INDEX "IX_OfficerAppointments_TenantId_OrgId" ON "OfficerAppointments" ("TenantId", "OrgId");

CREATE INDEX "IX_OfficerAppointments_TenantId_OrgId_PositionId_StartsAt_Ends~" ON "OfficerAppointments" ("TenantId", "OrgId", "PositionId", "StartsAt", "EndsAt");

CREATE INDEX "IX_OfficerAppointments_TenantId_OrgId_Status_EndsAt" ON "OfficerAppointments" ("TenantId", "OrgId", "Status", "EndsAt");

CREATE INDEX "IX_OfficerAppointments_TenantId_PositionId" ON "OfficerAppointments" ("TenantId", "PositionId");

CREATE INDEX "IX_OfficerAppointments_TenantId_TermId" ON "OfficerAppointments" ("TenantId", "TermId");

CREATE UNIQUE INDEX "IX_OfficerPositions_TenantId_Code" ON "OfficerPositions" ("TenantId", "Code");

CREATE INDEX "IX_OfficerPositions_TenantId_GrantRoleId" ON "OfficerPositions" ("TenantId", "GrantRoleId");

CREATE INDEX "IX_OfficerPositions_TenantId_LevelId" ON "OfficerPositions" ("TenantId", "LevelId");

CREATE INDEX "IX_OfficerPositions_TenantId_ReportsToId" ON "OfficerPositions" ("TenantId", "ReportsToId");

CREATE INDEX "IX_OrganizationHistory_TenantId_OrgId" ON "OrganizationHistory" ("TenantId", "OrgId");

ALTER TABLE "Assignments" ADD CONSTRAINT "FK_Assignments_OfficerAppointments_TenantId_AppointmentId" FOREIGN KEY ("TenantId", "AppointmentId") REFERENCES "OfficerAppointments" ("TenantId", "Id") ON DELETE RESTRICT;

ALTER TABLE "MembershipApprovals" ADD CONSTRAINT "FK_MembershipApprovals_GovernanceRequests_TenantId_GovernanceR~" FOREIGN KEY ("TenantId", "GovernanceRequestId") REFERENCES "GovernanceRequests" ("TenantId", "Id") ON DELETE RESTRICT;

ALTER TABLE "Orgs" DROP CONSTRAINT "CK_Org_Identity";
ALTER TABLE "Orgs" ADD CONSTRAINT "CK_Org_Identity" CHECK(length(trim("Name")) BETWEEN 2 AND 200 AND length("Code") BETWEEN 1 AND 40 AND "Status" IN ('Active','Pending','Archived','Proposed','Pending approval','Suspended','Inactive','Merged','Closed') AND "Id" IS DISTINCT FROM "ParentId");
ALTER TABLE "MembershipApprovals" ADD CONSTRAINT "CK_Approval_Target" CHECK(("ApplicationId" IS NULL) <> ("GovernanceRequestId" IS NULL));
ALTER TABLE "OfficerPositions" ADD CONSTRAINT "CK_Position_Rules" CHECK("DefaultMonths" BETWEEN 1 AND 120 AND ("MaximumHolders" IS NULL OR "MaximumHolders" BETWEEN 1 AND 100) AND "MinimumMembershipDays">=0 AND length("Title") BETWEEN 1 AND 120);
ALTER TABLE "OfficerAppointments" ADD CONSTRAINT "CK_Appointment_Term" CHECK("StartsAt"<"EndsAt" AND "EffectiveAt">="StartsAt" AND "EffectiveAt"<"EndsAt" AND "Status" IN ('Proposed','Pending approval','Approved','Scheduled','Active','Completed','Revoked','Cancelled'));
ALTER TABLE "LeadershipTerms" ADD CONSTRAINT "CK_Leadership_Term" CHECK("StartsAt"<"EndsAt" AND "Calendar" IN ('Calendar','Fiscal','Custom'));
ALTER TABLE "Committees" ADD CONSTRAINT "CK_Committee_Term" CHECK("StartsAt"<"EndsAt" AND "Status" IN ('Proposed','Pending approval','Active','Completed','Closed','Cancelled'));
ALTER TABLE "CommitteeMemberships" ADD CONSTRAINT "CK_Committee_Member_Term" CHECK("StartsAt"<"EndsAt" AND "Status" IN ('Active','Completed','Removed'));
ALTER TABLE "GovernanceRequests" ADD CONSTRAINT "CK_Governance_Status" CHECK("Status" IN ('Draft','Submitted','Under Review','Changes Requested','Approved','Rejected','Withdrawn','Cancelled'));
ALTER TABLE "GovernanceDocuments" ADD CONSTRAINT "CK_Governance_Document" CHECK(octet_length("Content")<=5242880 AND (octet_length("Content")>0 OR "Status"='Expired') AND "ContentType" IN ('application/pdf','image/png','image/jpeg'));
ALTER TABLE "ClubHealthConfigurations" ADD CONSTRAINT "CK_Health_Weights" CHECK("LeadershipWeight">=0 AND "MembershipWeight">=0 AND "GovernanceWeight">=0 AND "DataWeight">=0 AND "LeadershipWeight"+"MembershipWeight"+"GovernanceWeight"+"DataWeight"=100 AND "MinimumActiveMembers">0);
CREATE FUNCTION clubs_org_history() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN
 INSERT INTO "OrganizationHistory" ("Id","TenantId","OrgId","Before","After","ActorId","At") VALUES(gen_random_uuid(),NEW."TenantId",NEW."Id",CASE WHEN TG_OP='UPDATE' THEN to_jsonb(OLD)::text ELSE '{}' END,to_jsonb(NEW)::text,NULLIF(current_setting('clubs.actor',true),'')::uuid,now()); RETURN NULL; END $$;
CREATE TRIGGER org_change_history AFTER INSERT OR UPDATE ON "Orgs" FOR EACH ROW EXECUTE FUNCTION clubs_org_history();
CREATE TRIGGER org_history_immutable BEFORE UPDATE OR DELETE ON "OrganizationHistory" FOR EACH ROW EXECUTE FUNCTION clubs_membership_history_guard();
CREATE TRIGGER appointment_history_immutable BEFORE UPDATE OR DELETE ON "OfficerAppointmentHistory" FOR EACH ROW EXECUTE FUNCTION clubs_membership_history_guard();
CREATE TRIGGER committee_activity_immutable BEFORE UPDATE OR DELETE ON "CommitteeActivities" FOR EACH ROW EXECUTE FUNCTION clubs_membership_history_guard();
CREATE FUNCTION clubs_appointment_guard() RETURNS trigger LANGUAGE plpgsql AS $$
DECLARE maximum integer;
BEGIN
 PERFORM pg_advisory_xact_lock(hashtext(NEW."TenantId"::text));
 IF TG_OP='UPDATE' AND (NEW."PersonId"<>OLD."PersonId" OR NEW."OrgId"<>OLD."OrgId" OR NEW."TenantId"<>OLD."TenantId" OR NEW."PositionId"<>OLD."PositionId") THEN RAISE EXCEPTION 'Appointment identity is immutable' USING ERRCODE='23514'; END IF;
 IF NOT EXISTS(SELECT 1 FROM "OfficerPositions" p JOIN "Orgs" o ON o."Id"=NEW."OrgId" AND o."TenantId"=p."TenantId" AND o."LevelId"=p."LevelId" WHERE p."Id"=NEW."PositionId" AND p."TenantId"=NEW."TenantId") THEN RAISE EXCEPTION 'Position level mismatch' USING ERRCODE='23514'; END IF;
 IF NEW."MembershipId" IS NOT NULL AND NOT EXISTS(SELECT 1 FROM "Memberships" m WHERE m."Id"=NEW."MembershipId" AND m."PersonId"=NEW."PersonId" AND m."TenantId"=NEW."TenantId") THEN RAISE EXCEPTION 'Eligibility identity mismatch' USING ERRCODE='23514'; END IF;
 IF NEW."Status" IN ('Approved','Scheduled','Active') THEN
 SELECT "MaximumHolders" INTO maximum FROM "OfficerPositions" WHERE "Id"=NEW."PositionId";
 IF maximum IS NOT NULL AND EXISTS(SELECT 1 FROM (SELECT NEW."StartsAt" AS point UNION SELECT a."StartsAt" FROM "OfficerAppointments" a WHERE a."Id"<>NEW."Id" AND a."TenantId"=NEW."TenantId" AND a."OrgId"=NEW."OrgId" AND a."PositionId"=NEW."PositionId" AND a."Status" IN ('Approved','Scheduled','Active') AND a."StartsAt">NEW."StartsAt" AND a."StartsAt"<NEW."EndsAt") points WHERE (SELECT count(*) FROM "OfficerAppointments" a WHERE a."Id"<>NEW."Id" AND a."TenantId"=NEW."TenantId" AND a."OrgId"=NEW."OrgId" AND a."PositionId"=NEW."PositionId" AND a."Status" IN ('Approved','Scheduled','Active') AND a."StartsAt"<=points.point AND a."EndsAt">points.point)>=maximum) THEN RAISE EXCEPTION 'Office capacity exceeded' USING ERRCODE='23514'; END IF;
 END IF; RETURN NEW;
END $$;
CREATE TRIGGER appointment_integrity BEFORE INSERT OR UPDATE ON "OfficerAppointments" FOR EACH ROW EXECUTE FUNCTION clubs_appointment_guard();
CREATE FUNCTION clubs_office_assignment_guard() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN
 IF NEW."AppointmentId" IS NOT NULL AND (NEW."MembershipId" IS NOT NULL OR NOT EXISTS(SELECT 1 FROM "OfficerAppointments" a JOIN "Persons" p ON p."Id"=a."PersonId" WHERE a."Id"=NEW."AppointmentId" AND a."TenantId"=NEW."TenantId" AND a."OrgId"=NEW."OrgId" AND a."GrantRoleId"=NEW."RoleId" AND p."UserId"=NEW."UserId" AND a."Status"='Active' AND a."StartsAt"<=now() AND a."EffectiveAt"<=now() AND a."EndsAt">now())) THEN RAISE EXCEPTION 'Invalid appointment-sourced access' USING ERRCODE='23514'; END IF; RETURN NEW; END $$;
CREATE TRIGGER office_assignment_integrity BEFORE INSERT OR UPDATE ON "Assignments" FOR EACH ROW EXECUTE FUNCTION clubs_office_assignment_guard();
CREATE INDEX "IX_Appointment_Current" ON "OfficerAppointments" ("TenantId","OrgId","PositionId","EndsAt","StartsAt") WHERE "Status"='Active';
CREATE INDEX "IX_Appointment_Worker" ON "OfficerAppointments" ("Status","EffectiveAt","EndsAt") WHERE "Status" IN ('Approved','Scheduled','Active');
INSERT INTO "RolePermissions" ("Id","RoleId","PermissionId") SELECT gen_random_uuid(),r."Id",p."Id" FROM "Roles" r CROSS JOIN "Permissions" p WHERE p."Id" IN ('clubs.view','clubs.manage','leadership.view','leadership.manage','governance.configure','governance.approve','governance.restructure','governance.documents','committees.view','committees.manage','governance.reports') AND (r."Name"='Association Super Admin' OR r."Name" IN ('Club President','Club Secretary','Club Manager') AND p."Id" NOT IN ('governance.configure','governance.restructure') AND (p."Id"<>'governance.approve' OR r."Name" IN ('Club President','Club Secretary'))) ON CONFLICT DO NOTHING;


INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261110202022_GovernanceFoundation', '10.0.0');

COMMIT;

START TRANSACTION;
ALTER TABLE "OfficerAppointments" ADD "OrganizationName" text NOT NULL DEFAULT '';

ALTER TABLE "OfficerAppointments" ADD "PositionTitle" text NOT NULL DEFAULT '';

INSERT INTO "NotificationTemplates" ("Id", "Body", "Subject")
VALUES ('governance.review', '{detail}', 'ClubSphere · governance review');
INSERT INTO "NotificationTemplates" ("Id", "Body", "Subject")
VALUES ('governance.update', '{detail}', 'ClubSphere · governance update');
INSERT INTO "NotificationTemplates" ("Id", "Body", "Subject")
VALUES ('leadership.transition', '{detail}', 'ClubSphere · leadership transition');

UPDATE "OfficerAppointments" a SET "PositionTitle"=COALESCE((SELECT au."Changes" FROM "Audits" au WHERE au."Action"='appointment.proposed' AND au."Resource"=a."Id"::text ORDER BY au."At" LIMIT 1),p."Title"), "OrganizationName"=o."Name" FROM "OfficerPositions" p,"Orgs" o WHERE p."Id"=a."PositionId" AND o."Id"=a."OrgId";
CREATE INDEX "IX_Orgs_Code_Search" ON "Orgs" USING gin ("Code" gin_trgm_ops);
CREATE FUNCTION clubs_office_snapshot_guard() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN
 IF (OLD."PositionTitle"<>'' AND NEW."PositionTitle"<>OLD."PositionTitle") OR (OLD."OrganizationName"<>'' AND NEW."OrganizationName"<>OLD."OrganizationName") THEN RAISE EXCEPTION 'Historical office labels are immutable' USING ERRCODE='23514'; END IF; RETURN NEW; END $$;
CREATE TRIGGER appointment_snapshot_integrity BEFORE UPDATE ON "OfficerAppointments" FOR EACH ROW EXECUTE FUNCTION clubs_office_snapshot_guard();


INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261110204615_GovernanceHistoryHardening', '10.0.0');

COMMIT;

START TRANSACTION;
UPDATE "MembershipWorkflows" old SET "Active"=false WHERE old."Kind"='' AND old."Active" AND EXISTS(SELECT 1 FROM "MembershipWorkflows" modern WHERE modern."TenantId"=old."TenantId" AND modern."Kind"='Membership' AND modern."Active");
UPDATE "MembershipWorkflows" SET "Kind"='Membership' WHERE "Kind"='';
ALTER TABLE "MembershipWorkflows" ALTER COLUMN "Kind" SET DEFAULT 'Membership';
UPDATE "Orgs" SET "FiscalStartMonth"=CASE WHEN "FiscalStartMonth"=0 THEN 1 ELSE "FiscalStartMonth" END,
  "BrandingColor"=CASE WHEN "BrandingColor"='' THEN '#2563eb' ELSE "BrandingColor" END,
  "PreferredLanguage"=CASE WHEN "PreferredLanguage"='' THEN 'en' ELSE "PreferredLanguage" END,
  "ClubType"=CASE WHEN "ClubType"='' THEN 'Community' ELSE "ClubType" END
WHERE "FiscalStartMonth"=0 OR "BrandingColor"='' OR "PreferredLanguage"='' OR "ClubType"='';
ALTER TABLE "Orgs" ALTER COLUMN "FiscalStartMonth" SET DEFAULT 1;
ALTER TABLE "Orgs" ALTER COLUMN "BrandingColor" SET DEFAULT '#2563eb';
ALTER TABLE "Orgs" ALTER COLUMN "PreferredLanguage" SET DEFAULT 'en';
ALTER TABLE "Orgs" ALTER COLUMN "ClubType" SET DEFAULT 'Community';

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261110210002_GovernanceLegacyDefaults', '10.0.0');

COMMIT;

START TRANSACTION;
ALTER TABLE "GovernanceDocuments" ADD "EventId" uuid;

CREATE TABLE "EventSavedFilters" (
    "Id" uuid NOT NULL,
    "UserId" uuid NOT NULL,
    "Name" text NOT NULL,
    "Filters" text NOT NULL,
    CONSTRAINT "PK_EventSavedFilters" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_EventSavedFilters_UserProfiles_UserId" FOREIGN KEY ("UserId") REFERENCES "UserProfiles" ("Id") ON DELETE CASCADE
);

CREATE TABLE "EventTypes" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "Name" text NOT NULL,
    "Active" boolean NOT NULL,
    "DefaultCapacity" integer NOT NULL,
    CONSTRAINT "PK_EventTypes" PRIMARY KEY ("Id"),
    CONSTRAINT "AK_EventTypes_TenantId_Id" UNIQUE ("TenantId", "Id"),
    CONSTRAINT "FK_EventTypes_Associations_TenantId" FOREIGN KEY ("TenantId") REFERENCES "Associations" ("Id") ON DELETE RESTRICT
);

CREATE TABLE "Events" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "OrgId" uuid NOT NULL,
    "TypeId" uuid NOT NULL,
    "CommitteeId" uuid,
    "CreatorId" uuid NOT NULL,
    "Title" text NOT NULL,
    "Description" text NOT NULL,
    "TimeZone" text NOT NULL,
    "Language" text NOT NULL,
    "StartsAt" timestamp with time zone NOT NULL,
    "EndsAt" timestamp with time zone NOT NULL,
    "VenueType" text NOT NULL,
    "Venue" text NOT NULL,
    "OnlineUrl" text NOT NULL,
    "Contact" text NOT NULL,
    "Visibility" text NOT NULL,
    "Approval" text NOT NULL,
    "Publication" text NOT NULL,
    "PublishedAt" timestamp with time zone,
    "Lifecycle" text NOT NULL,
    "RegistrationEnabled" boolean NOT NULL,
    "RsvpRequired" boolean NOT NULL,
    "RegistrationStartsAt" timestamp with time zone NOT NULL,
    "RegistrationEndsAt" timestamp with time zone NOT NULL,
    "RsvpDeadline" timestamp with time zone NOT NULL,
    "Capacity" integer NOT NULL,
    "Waitlist" boolean NOT NULL,
    "WaitlistPolicy" text NOT NULL,
    "OfferMinutes" integer NOT NULL,
    "AllowGuests" boolean NOT NULL,
    "MaxCompanions" integer NOT NULL,
    "RegistrationApproval" boolean NOT NULL,
    "Paid" boolean NOT NULL,
    "CancellationPolicy" text NOT NULL,
    "Fields" text NOT NULL,
    "ApprovalRequestId" uuid,
    "ReminderAt" timestamp with time zone,
    "ReminderSent" boolean NOT NULL,
    "CompletionSent" boolean NOT NULL,
    "Version" bigint NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_Events" PRIMARY KEY ("Id"),
    CONSTRAINT "AK_Events_TenantId_Id" UNIQUE ("TenantId", "Id"),
    CONSTRAINT "FK_Events_Associations_TenantId" FOREIGN KEY ("TenantId") REFERENCES "Associations" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_Events_Committees_TenantId_CommitteeId" FOREIGN KEY ("TenantId", "CommitteeId") REFERENCES "Committees" ("TenantId", "Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_Events_EventTypes_TenantId_TypeId" FOREIGN KEY ("TenantId", "TypeId") REFERENCES "EventTypes" ("TenantId", "Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_Events_Orgs_TenantId_OrgId" FOREIGN KEY ("TenantId", "OrgId") REFERENCES "Orgs" ("TenantId", "Id") ON DELETE RESTRICT
);

CREATE TABLE "EventAudiences" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "EventId" uuid NOT NULL,
    "OrgId" uuid,
    "CommitteeId" uuid,
    "PersonId" uuid,
    "CategoryId" uuid,
    "RoleId" uuid,
    "City" text NOT NULL,
    "Country" text NOT NULL,
    "Status" text NOT NULL,
    "RegistrationStatus" text NOT NULL,
    CONSTRAINT "PK_EventAudiences" PRIMARY KEY ("Id"),
    CONSTRAINT "AK_EventAudiences_TenantId_Id" UNIQUE ("TenantId", "Id"),
    CONSTRAINT "FK_EventAudiences_Associations_TenantId" FOREIGN KEY ("TenantId") REFERENCES "Associations" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_EventAudiences_Committees_TenantId_CommitteeId" FOREIGN KEY ("TenantId", "CommitteeId") REFERENCES "Committees" ("TenantId", "Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_EventAudiences_Events_TenantId_EventId" FOREIGN KEY ("TenantId", "EventId") REFERENCES "Events" ("TenantId", "Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_EventAudiences_MembershipCategories_TenantId_CategoryId" FOREIGN KEY ("TenantId", "CategoryId") REFERENCES "MembershipCategories" ("TenantId", "Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_EventAudiences_Orgs_TenantId_OrgId" FOREIGN KEY ("TenantId", "OrgId") REFERENCES "Orgs" ("TenantId", "Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_EventAudiences_Persons_PersonId" FOREIGN KEY ("PersonId") REFERENCES "Persons" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_EventAudiences_Roles_TenantId_RoleId" FOREIGN KEY ("TenantId", "RoleId") REFERENCES "Roles" ("TenantId", "Id") ON DELETE RESTRICT
);

CREATE TABLE "EventCohosts" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "EventId" uuid NOT NULL,
    "OrgId" uuid NOT NULL,
    CONSTRAINT "PK_EventCohosts" PRIMARY KEY ("Id"),
    CONSTRAINT "AK_EventCohosts_TenantId_Id" UNIQUE ("TenantId", "Id"),
    CONSTRAINT "FK_EventCohosts_Associations_TenantId" FOREIGN KEY ("TenantId") REFERENCES "Associations" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_EventCohosts_Events_TenantId_EventId" FOREIGN KEY ("TenantId", "EventId") REFERENCES "Events" ("TenantId", "Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_EventCohosts_Orgs_TenantId_OrgId" FOREIGN KEY ("TenantId", "OrgId") REFERENCES "Orgs" ("TenantId", "Id") ON DELETE RESTRICT
);

CREATE TABLE "EventFeedbackForms" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "EventId" uuid NOT NULL,
    "Questions" text NOT NULL,
    "Anonymous" boolean NOT NULL,
    "Deadline" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_EventFeedbackForms" PRIMARY KEY ("Id"),
    CONSTRAINT "AK_EventFeedbackForms_TenantId_Id" UNIQUE ("TenantId", "Id"),
    CONSTRAINT "FK_EventFeedbackForms_Associations_TenantId" FOREIGN KEY ("TenantId") REFERENCES "Associations" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_EventFeedbackForms_Events_TenantId_EventId" FOREIGN KEY ("TenantId", "EventId") REFERENCES "Events" ("TenantId", "Id") ON DELETE RESTRICT
);

CREATE TABLE "EventInvitations" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "EventId" uuid NOT NULL,
    "PersonId" uuid,
    "InviterId" uuid NOT NULL,
    "RecipientKey" text NOT NULL,
    "GuestName" text NOT NULL,
    "Email" text NOT NULL,
    "TokenHash" text NOT NULL,
    "ProtectedToken" text NOT NULL,
    "Context" text NOT NULL,
    "Message" text NOT NULL,
    "Status" text NOT NULL,
    "SendAt" timestamp with time zone NOT NULL,
    "SentAt" timestamp with time zone,
    "ExpiresAt" timestamp with time zone NOT NULL,
    "MailId" uuid,
    "Rsvp" text NOT NULL,
    "RespondedAt" timestamp with time zone,
    CONSTRAINT "PK_EventInvitations" PRIMARY KEY ("Id"),
    CONSTRAINT "AK_EventInvitations_TenantId_Id" UNIQUE ("TenantId", "Id"),
    CONSTRAINT "FK_EventInvitations_Associations_TenantId" FOREIGN KEY ("TenantId") REFERENCES "Associations" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_EventInvitations_Events_TenantId_EventId" FOREIGN KEY ("TenantId", "EventId") REFERENCES "Events" ("TenantId", "Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_EventInvitations_Persons_PersonId" FOREIGN KEY ("PersonId") REFERENCES "Persons" ("Id") ON DELETE RESTRICT
);

CREATE TABLE "EventSessions" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "EventId" uuid NOT NULL,
    "Title" text NOT NULL,
    "Description" text NOT NULL,
    "Speaker" text NOT NULL,
    "Room" text NOT NULL,
    "StartsAt" timestamp with time zone NOT NULL,
    "EndsAt" timestamp with time zone NOT NULL,
    "Capacity" integer NOT NULL,
    "RegistrationRequired" boolean NOT NULL,
    "AttendanceRequired" boolean NOT NULL,
    "MembersOnly" boolean NOT NULL,
    "Status" text NOT NULL,
    CONSTRAINT "PK_EventSessions" PRIMARY KEY ("Id"),
    CONSTRAINT "AK_EventSessions_TenantId_Id" UNIQUE ("TenantId", "Id"),
    CONSTRAINT "FK_EventSessions_Associations_TenantId" FOREIGN KEY ("TenantId") REFERENCES "Associations" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_EventSessions_Events_TenantId_EventId" FOREIGN KEY ("TenantId", "EventId") REFERENCES "Events" ("TenantId", "Id") ON DELETE RESTRICT
);

CREATE TABLE "EventStaff" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "EventId" uuid NOT NULL,
    "PersonId" uuid NOT NULL,
    "Role" text NOT NULL,
    "Capability" text NOT NULL,
    "Responsibilities" text NOT NULL,
    "Tasks" text NOT NULL,
    "TaskStatus" text NOT NULL,
    "Notes" text NOT NULL,
    "StartsAt" timestamp with time zone NOT NULL,
    "EndsAt" timestamp with time zone NOT NULL,
    "Active" boolean NOT NULL,
    "ShiftCheckinAt" timestamp with time zone,
    "Version" bigint NOT NULL,
    CONSTRAINT "PK_EventStaff" PRIMARY KEY ("Id"),
    CONSTRAINT "AK_EventStaff_TenantId_Id" UNIQUE ("TenantId", "Id"),
    CONSTRAINT "FK_EventStaff_Associations_TenantId" FOREIGN KEY ("TenantId") REFERENCES "Associations" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_EventStaff_Events_TenantId_EventId" FOREIGN KEY ("TenantId", "EventId") REFERENCES "Events" ("TenantId", "Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_EventStaff_Persons_PersonId" FOREIGN KEY ("PersonId") REFERENCES "Persons" ("Id") ON DELETE RESTRICT
);

CREATE TABLE "EventRegistrations" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "EventId" uuid NOT NULL,
    "PersonId" uuid,
    "MembershipId" uuid,
    "InvitationId" uuid,
    "ParticipantKey" text NOT NULL,
    "Name" text NOT NULL,
    "Email" text NOT NULL,
    "Category" text NOT NULL,
    "Status" text NOT NULL,
    "Companions" integer NOT NULL,
    "GuestNames" text NOT NULL,
    "Responses" text NOT NULL,
    "Accessibility" text NOT NULL,
    "Dietary" text NOT NULL,
    "Emergency" text NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "OfferExpiresAt" timestamp with time zone,
    "FeedbackSubmitted" boolean NOT NULL,
    "ApprovalRequestId" uuid,
    "Version" bigint NOT NULL,
    CONSTRAINT "PK_EventRegistrations" PRIMARY KEY ("Id"),
    CONSTRAINT "AK_EventRegistrations_TenantId_Id" UNIQUE ("TenantId", "Id"),
    CONSTRAINT "FK_EventRegistrations_Associations_TenantId" FOREIGN KEY ("TenantId") REFERENCES "Associations" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_EventRegistrations_EventInvitations_TenantId_InvitationId" FOREIGN KEY ("TenantId", "InvitationId") REFERENCES "EventInvitations" ("TenantId", "Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_EventRegistrations_Events_TenantId_EventId" FOREIGN KEY ("TenantId", "EventId") REFERENCES "Events" ("TenantId", "Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_EventRegistrations_Memberships_TenantId_MembershipId" FOREIGN KEY ("TenantId", "MembershipId") REFERENCES "Memberships" ("TenantId", "Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_EventRegistrations_Persons_PersonId" FOREIGN KEY ("PersonId") REFERENCES "Persons" ("Id") ON DELETE RESTRICT
);

CREATE TABLE "EventRsvpHistory" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "InvitationId" uuid NOT NULL,
    "Response" text NOT NULL,
    "At" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_EventRsvpHistory" PRIMARY KEY ("Id"),
    CONSTRAINT "AK_EventRsvpHistory_TenantId_Id" UNIQUE ("TenantId", "Id"),
    CONSTRAINT "FK_EventRsvpHistory_Associations_TenantId" FOREIGN KEY ("TenantId") REFERENCES "Associations" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_EventRsvpHistory_EventInvitations_TenantId_InvitationId" FOREIGN KEY ("TenantId", "InvitationId") REFERENCES "EventInvitations" ("TenantId", "Id") ON DELETE RESTRICT
);

CREATE TABLE "EventCheckins" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "EventId" uuid NOT NULL,
    "RegistrationId" uuid NOT NULL,
    "SessionId" uuid,
    "OperatorId" uuid NOT NULL,
    "Device" text NOT NULL,
    "EntryPoint" text NOT NULL,
    "At" timestamp with time zone NOT NULL,
    "Voided" boolean NOT NULL,
    "CorrectionReason" text NOT NULL,
    CONSTRAINT "PK_EventCheckins" PRIMARY KEY ("Id"),
    CONSTRAINT "AK_EventCheckins_TenantId_Id" UNIQUE ("TenantId", "Id"),
    CONSTRAINT "FK_EventCheckins_Associations_TenantId" FOREIGN KEY ("TenantId") REFERENCES "Associations" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_EventCheckins_EventRegistrations_TenantId_RegistrationId" FOREIGN KEY ("TenantId", "RegistrationId") REFERENCES "EventRegistrations" ("TenantId", "Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_EventCheckins_EventSessions_TenantId_SessionId" FOREIGN KEY ("TenantId", "SessionId") REFERENCES "EventSessions" ("TenantId", "Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_EventCheckins_Events_TenantId_EventId" FOREIGN KEY ("TenantId", "EventId") REFERENCES "Events" ("TenantId", "Id") ON DELETE RESTRICT
);

CREATE TABLE "EventFeedbackResponses" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "EventId" uuid NOT NULL,
    "RegistrationId" uuid,
    "Answers" text NOT NULL,
    "Day" date NOT NULL,
    CONSTRAINT "PK_EventFeedbackResponses" PRIMARY KEY ("Id"),
    CONSTRAINT "AK_EventFeedbackResponses_TenantId_Id" UNIQUE ("TenantId", "Id"),
    CONSTRAINT "FK_EventFeedbackResponses_Associations_TenantId" FOREIGN KEY ("TenantId") REFERENCES "Associations" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_EventFeedbackResponses_EventRegistrations_TenantId_Registra~" FOREIGN KEY ("TenantId", "RegistrationId") REFERENCES "EventRegistrations" ("TenantId", "Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_EventFeedbackResponses_Events_TenantId_EventId" FOREIGN KEY ("TenantId", "EventId") REFERENCES "Events" ("TenantId", "Id") ON DELETE RESTRICT
);

CREATE TABLE "EventSessionBookings" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "EventId" uuid NOT NULL,
    "SessionId" uuid NOT NULL,
    "RegistrationId" uuid NOT NULL,
    "Active" boolean NOT NULL,
    CONSTRAINT "PK_EventSessionBookings" PRIMARY KEY ("Id"),
    CONSTRAINT "AK_EventSessionBookings_TenantId_Id" UNIQUE ("TenantId", "Id"),
    CONSTRAINT "FK_EventSessionBookings_Associations_TenantId" FOREIGN KEY ("TenantId") REFERENCES "Associations" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_EventSessionBookings_EventRegistrations_TenantId_Registrati~" FOREIGN KEY ("TenantId", "RegistrationId") REFERENCES "EventRegistrations" ("TenantId", "Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_EventSessionBookings_EventSessions_TenantId_SessionId" FOREIGN KEY ("TenantId", "SessionId") REFERENCES "EventSessions" ("TenantId", "Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_EventSessionBookings_Events_TenantId_EventId" FOREIGN KEY ("TenantId", "EventId") REFERENCES "Events" ("TenantId", "Id") ON DELETE RESTRICT
);

CREATE TABLE "EventTickets" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "EventId" uuid NOT NULL,
    "RegistrationId" uuid NOT NULL,
    "TokenHash" text NOT NULL,
    "ProtectedToken" text NOT NULL,
    "Revoked" boolean NOT NULL,
    "IssuedAt" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_EventTickets" PRIMARY KEY ("Id"),
    CONSTRAINT "AK_EventTickets_TenantId_Id" UNIQUE ("TenantId", "Id"),
    CONSTRAINT "FK_EventTickets_Associations_TenantId" FOREIGN KEY ("TenantId") REFERENCES "Associations" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_EventTickets_EventRegistrations_TenantId_RegistrationId" FOREIGN KEY ("TenantId", "RegistrationId") REFERENCES "EventRegistrations" ("TenantId", "Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_EventTickets_Events_TenantId_EventId" FOREIGN KEY ("TenantId", "EventId") REFERENCES "Events" ("TenantId", "Id") ON DELETE RESTRICT
);

INSERT INTO "Permissions" ("Id")
VALUES ('events.checkin');
INSERT INTO "Permissions" ("Id")
VALUES ('events.configure');
INSERT INTO "Permissions" ("Id")
VALUES ('events.manage');
INSERT INTO "Permissions" ("Id")
VALUES ('events.reports');
INSERT INTO "Permissions" ("Id")
VALUES ('events.view');

CREATE INDEX "IX_GovernanceDocuments_TenantId_EventId" ON "GovernanceDocuments" ("TenantId", "EventId");

CREATE INDEX "IX_EventAudiences_PersonId" ON "EventAudiences" ("PersonId");

CREATE INDEX "IX_EventAudiences_TenantId_CategoryId" ON "EventAudiences" ("TenantId", "CategoryId");

CREATE INDEX "IX_EventAudiences_TenantId_CommitteeId" ON "EventAudiences" ("TenantId", "CommitteeId");

CREATE INDEX "IX_EventAudiences_TenantId_EventId" ON "EventAudiences" ("TenantId", "EventId");

CREATE INDEX "IX_EventAudiences_TenantId_OrgId" ON "EventAudiences" ("TenantId", "OrgId");

CREATE INDEX "IX_EventAudiences_TenantId_RoleId" ON "EventAudiences" ("TenantId", "RoleId");

CREATE INDEX "IX_EventCheckins_EventId_SessionId_Voided" ON "EventCheckins" ("EventId", "SessionId", "Voided");

CREATE UNIQUE INDEX "IX_EventCheckins_RegistrationId_SessionId" ON "EventCheckins" ("RegistrationId", "SessionId") NULLS NOT DISTINCT;

CREATE INDEX "IX_EventCheckins_TenantId_EventId" ON "EventCheckins" ("TenantId", "EventId");

CREATE INDEX "IX_EventCheckins_TenantId_RegistrationId" ON "EventCheckins" ("TenantId", "RegistrationId");

CREATE INDEX "IX_EventCheckins_TenantId_SessionId" ON "EventCheckins" ("TenantId", "SessionId");

CREATE INDEX "IX_EventCohosts_TenantId_EventId" ON "EventCohosts" ("TenantId", "EventId");

CREATE INDEX "IX_EventCohosts_TenantId_OrgId" ON "EventCohosts" ("TenantId", "OrgId");

CREATE UNIQUE INDEX "IX_EventFeedbackForms_EventId" ON "EventFeedbackForms" ("EventId");

CREATE INDEX "IX_EventFeedbackForms_TenantId_EventId" ON "EventFeedbackForms" ("TenantId", "EventId");

CREATE UNIQUE INDEX "IX_EventFeedbackResponses_EventId_RegistrationId" ON "EventFeedbackResponses" ("EventId", "RegistrationId");

CREATE INDEX "IX_EventFeedbackResponses_TenantId_EventId" ON "EventFeedbackResponses" ("TenantId", "EventId");

CREATE INDEX "IX_EventFeedbackResponses_TenantId_RegistrationId" ON "EventFeedbackResponses" ("TenantId", "RegistrationId");

CREATE UNIQUE INDEX "IX_EventInvitations_EventId_RecipientKey" ON "EventInvitations" ("EventId", "RecipientKey");

CREATE INDEX "IX_EventInvitations_PersonId_SendAt" ON "EventInvitations" ("PersonId", "SendAt");

CREATE INDEX "IX_EventInvitations_Status_SendAt" ON "EventInvitations" ("Status", "SendAt");

CREATE INDEX "IX_EventInvitations_TenantId_EventId" ON "EventInvitations" ("TenantId", "EventId");

CREATE UNIQUE INDEX "IX_EventInvitations_TokenHash" ON "EventInvitations" ("TokenHash");

CREATE UNIQUE INDEX "IX_EventRegistrations_EventId_ParticipantKey" ON "EventRegistrations" ("EventId", "ParticipantKey");

CREATE INDEX "IX_EventRegistrations_EventId_Status_CreatedAt_Id" ON "EventRegistrations" ("EventId", "Status", "CreatedAt", "Id");

CREATE INDEX "IX_EventRegistrations_PersonId_EventId" ON "EventRegistrations" ("PersonId", "EventId");

CREATE INDEX "IX_EventRegistrations_TenantId_EventId" ON "EventRegistrations" ("TenantId", "EventId");

CREATE INDEX "IX_EventRegistrations_TenantId_InvitationId" ON "EventRegistrations" ("TenantId", "InvitationId");

CREATE INDEX "IX_EventRegistrations_TenantId_MembershipId" ON "EventRegistrations" ("TenantId", "MembershipId");

CREATE INDEX "IX_EventRsvpHistory_TenantId_InvitationId" ON "EventRsvpHistory" ("TenantId", "InvitationId");

CREATE INDEX "IX_Events_Publication_StartsAt_Id" ON "Events" ("Publication", "StartsAt", "Id");

CREATE INDEX "IX_Events_TenantId_CommitteeId" ON "Events" ("TenantId", "CommitteeId");

CREATE INDEX "IX_Events_TenantId_OrgId_StartsAt_Id" ON "Events" ("TenantId", "OrgId", "StartsAt", "Id");

CREATE INDEX "IX_Events_TenantId_TypeId" ON "Events" ("TenantId", "TypeId");

CREATE INDEX "IX_EventSavedFilters_UserId" ON "EventSavedFilters" ("UserId");

CREATE UNIQUE INDEX "IX_EventSessionBookings_RegistrationId_SessionId" ON "EventSessionBookings" ("RegistrationId", "SessionId");

CREATE INDEX "IX_EventSessionBookings_TenantId_EventId" ON "EventSessionBookings" ("TenantId", "EventId");

CREATE INDEX "IX_EventSessionBookings_TenantId_RegistrationId" ON "EventSessionBookings" ("TenantId", "RegistrationId");

CREATE INDEX "IX_EventSessionBookings_TenantId_SessionId" ON "EventSessionBookings" ("TenantId", "SessionId");

CREATE INDEX "IX_EventSessions_TenantId_EventId" ON "EventSessions" ("TenantId", "EventId");

CREATE UNIQUE INDEX "IX_EventStaff_EventId_PersonId" ON "EventStaff" ("EventId", "PersonId");

CREATE INDEX "IX_EventStaff_PersonId_Active" ON "EventStaff" ("PersonId", "Active");

CREATE INDEX "IX_EventStaff_TenantId_EventId" ON "EventStaff" ("TenantId", "EventId");

CREATE INDEX "IX_EventTickets_RegistrationId_Revoked" ON "EventTickets" ("RegistrationId", "Revoked");

CREATE INDEX "IX_EventTickets_TenantId_EventId" ON "EventTickets" ("TenantId", "EventId");

CREATE INDEX "IX_EventTickets_TenantId_RegistrationId" ON "EventTickets" ("TenantId", "RegistrationId");

CREATE UNIQUE INDEX "IX_EventTickets_TokenHash" ON "EventTickets" ("TokenHash");

CREATE UNIQUE INDEX "IX_EventTypes_TenantId_Name" ON "EventTypes" ("TenantId", "Name");

ALTER TABLE "GovernanceDocuments" ADD CONSTRAINT "FK_GovernanceDocuments_Events_TenantId_EventId" FOREIGN KEY ("TenantId", "EventId") REFERENCES "Events" ("TenantId", "Id") ON DELETE RESTRICT;

INSERT INTO "RolePermissions" ("Id","RoleId","PermissionId") SELECT gen_random_uuid(),r."Id",p."Id" FROM "Roles" r CROSS JOIN "Permissions" p WHERE p."Id" LIKE 'events.%' AND (r."Name"='Association Super Admin' OR (r."Name" IN ('Club Manager','Club President','Club Secretary') AND p."Id"<>'events.configure') OR (r."Name" IN ('Club Member','Guest / Applicant') AND p."Id"='events.view')) ON CONFLICT DO NOTHING;
INSERT INTO "NotificationTemplates" ("Id","Subject","Body") VALUES ('event.update','ClubSphere event update','{detail}'),('event.invitation','You are invited � ClubSphere','{detail}') ON CONFLICT DO NOTHING;
ALTER TABLE "Events" ADD CONSTRAINT "CK_Event_Config" CHECK ("StartsAt"<"EndsAt" AND "RegistrationStartsAt"<"RegistrationEndsAt" AND "RegistrationEndsAt"<="EndsAt" AND "Capacity">0 AND "MaxCompanions" BETWEEN 0 AND 10 AND "Approval" IN ('Draft','Pending Approval','Changes Requested','Rejected','Approved') AND "Publication" IN ('Draft','Published') AND "Lifecycle" IN ('Scheduled','Completed','Cancelled','Archived'));
ALTER TABLE "EventSessions" ADD CONSTRAINT "CK_Event_Session" CHECK("StartsAt"<"EndsAt" AND "Capacity">0);
ALTER TABLE "EventRegistrations" ADD CONSTRAINT "CK_Event_Registration" CHECK("Companions">=0 AND "Status" IN ('Pending','Awaiting Approval','Confirmed','Waitlisted','Offered','Cancelled','Rejected'));
CREATE TRIGGER event_rsvp_history_immutable BEFORE UPDATE OR DELETE ON "EventRsvpHistory" FOR EACH ROW EXECUTE FUNCTION clubs_membership_history_guard();
CREATE FUNCTION clubs_event_registration_guard() RETURNS trigger LANGUAGE plpgsql AS $$
DECLARE ev "Events"; used integer;
BEGIN
 SELECT * INTO ev FROM "Events" WHERE "Id"=NEW."EventId" AND "TenantId"=NEW."TenantId" FOR UPDATE;
 IF NEW."Status" IN ('Confirmed','Offered') THEN
  IF ev."Lifecycle" IN ('Cancelled','Archived') OR ev."Publication"<>'Published' OR ev."Paid" THEN RAISE EXCEPTION 'Event cannot confirm registration' USING ERRCODE='23514'; END IF;
  SELECT COALESCE(sum(1+"Companions"),0) INTO used FROM "EventRegistrations" WHERE "EventId"=NEW."EventId" AND "Id"<>NEW."Id" AND "Status" IN ('Confirmed','Offered');
  IF used+1+NEW."Companions">ev."Capacity" THEN RAISE EXCEPTION 'Event capacity exceeded' USING ERRCODE='23514'; END IF;
 END IF;
 RETURN NEW;
END $$;
CREATE TRIGGER event_registration_capacity BEFORE INSERT OR UPDATE ON "EventRegistrations" FOR EACH ROW EXECUTE FUNCTION clubs_event_registration_guard();
CREATE FUNCTION clubs_event_link_guard() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN
 IF NOT EXISTS(SELECT 1 FROM "EventRegistrations" r WHERE r."Id"=NEW."RegistrationId" AND r."EventId"=NEW."EventId" AND r."TenantId"=NEW."TenantId") THEN RAISE EXCEPTION 'Wrong event registration' USING ERRCODE='23514'; END IF;
 RETURN NEW;
END $$;
CREATE TRIGGER event_ticket_link BEFORE INSERT OR UPDATE ON "EventTickets" FOR EACH ROW EXECUTE FUNCTION clubs_event_link_guard();
CREATE TRIGGER event_checkin_link BEFORE INSERT OR UPDATE ON "EventCheckins" FOR EACH ROW EXECUTE FUNCTION clubs_event_link_guard();
CREATE TRIGGER event_session_booking_link BEFORE INSERT OR UPDATE ON "EventSessionBookings" FOR EACH ROW EXECUTE FUNCTION clubs_event_link_guard();

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261110215254_EventFoundation', '10.0.0');

COMMIT;

START TRANSACTION;
CREATE TABLE "EventNotices" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "EventId" uuid NOT NULL,
    "Message" text NOT NULL,
    "Audience" text NOT NULL,
    "Cursor" uuid,
    "Completed" boolean NOT NULL,
    CONSTRAINT "PK_EventNotices" PRIMARY KEY ("Id"),
    CONSTRAINT "AK_EventNotices_TenantId_Id" UNIQUE ("TenantId", "Id"),
    CONSTRAINT "FK_EventNotices_Associations_TenantId" FOREIGN KEY ("TenantId") REFERENCES "Associations" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_EventNotices_Events_TenantId_EventId" FOREIGN KEY ("TenantId", "EventId") REFERENCES "Events" ("TenantId", "Id") ON DELETE RESTRICT
);

CREATE INDEX "IX_EventNotices_Completed_EventId" ON "EventNotices" ("Completed", "EventId");

CREATE INDEX "IX_EventNotices_TenantId_EventId" ON "EventNotices" ("TenantId", "EventId");

ALTER TABLE "Events" DROP CONSTRAINT "CK_Event_Config";
ALTER TABLE "Events" ADD CONSTRAINT "CK_Event_Config" CHECK ("StartsAt"<"EndsAt" AND "RegistrationStartsAt"<"RegistrationEndsAt" AND "RegistrationEndsAt"<="EndsAt" AND "Capacity">0 AND "MaxCompanions" BETWEEN 0 AND 10 AND "Approval" IN ('Draft','Pending Approval','Changes Requested','Rejected','Approved') AND "Publication" IN ('Draft','Published') AND "Lifecycle" IN ('Scheduled','In Progress','Completed','Cancelled','Archived'));
CREATE INDEX IF NOT EXISTS "IX_EventInvitations_EventId_PersonId" ON "EventInvitations" ("EventId","PersonId");
CREATE INDEX IF NOT EXISTS "IX_EventRegistrations_EventId_MembershipId" ON "EventRegistrations" ("EventId","MembershipId");

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261110221001_EventDeliveryJobs', '10.0.0');

COMMIT;

START TRANSACTION;
ALTER TABLE "Events" ADD "Latitude" double precision;

ALTER TABLE "Events" ADD "Longitude" double precision;

CREATE INDEX "IX_Events_CreatorId" ON "Events" ("CreatorId");

CREATE INDEX "IX_Events_TenantId_ApprovalRequestId" ON "Events" ("TenantId", "ApprovalRequestId");

CREATE INDEX "IX_EventRegistrations_TenantId_ApprovalRequestId" ON "EventRegistrations" ("TenantId", "ApprovalRequestId");

CREATE INDEX "IX_EventInvitations_InviterId" ON "EventInvitations" ("InviterId");

CREATE INDEX "IX_EventCheckins_OperatorId" ON "EventCheckins" ("OperatorId");

ALTER TABLE "EventCheckins" ADD CONSTRAINT "FK_EventCheckins_UserProfiles_OperatorId" FOREIGN KEY ("OperatorId") REFERENCES "UserProfiles" ("Id") ON DELETE RESTRICT;

ALTER TABLE "EventInvitations" ADD CONSTRAINT "FK_EventInvitations_UserProfiles_InviterId" FOREIGN KEY ("InviterId") REFERENCES "UserProfiles" ("Id") ON DELETE RESTRICT;

ALTER TABLE "EventRegistrations" ADD CONSTRAINT "FK_EventRegistrations_GovernanceRequests_TenantId_ApprovalRequ~" FOREIGN KEY ("TenantId", "ApprovalRequestId") REFERENCES "GovernanceRequests" ("TenantId", "Id") ON DELETE RESTRICT;

ALTER TABLE "Events" ADD CONSTRAINT "FK_Events_GovernanceRequests_TenantId_ApprovalRequestId" FOREIGN KEY ("TenantId", "ApprovalRequestId") REFERENCES "GovernanceRequests" ("TenantId", "Id") ON DELETE RESTRICT;

ALTER TABLE "Events" ADD CONSTRAINT "FK_Events_UserProfiles_CreatorId" FOREIGN KEY ("CreatorId") REFERENCES "UserProfiles" ("Id") ON DELETE RESTRICT;

ALTER TABLE "Events" ADD CONSTRAINT "CK_Event_Location" CHECK (("Latitude" IS NULL AND "Longitude" IS NULL) OR ("Latitude" BETWEEN -90 AND 90 AND "Longitude" BETWEEN -180 AND 180));
ALTER TABLE "EventCheckins" ADD CONSTRAINT "CK_Event_Correction" CHECK(NOT "Voided" OR length("CorrectionReason")>=5);
CREATE FUNCTION clubs_event_session_guard() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN
 IF NEW."SessionId" IS NOT NULL AND NOT EXISTS(SELECT 1 FROM "EventSessions" s WHERE s."Id"=NEW."SessionId" AND s."TenantId"=NEW."TenantId" AND s."EventId"=NEW."EventId") THEN RAISE EXCEPTION 'Session belongs to a different event' USING ERRCODE='23514'; END IF; RETURN NEW; END $$;
CREATE TRIGGER event_checkin_session_link BEFORE INSERT OR UPDATE ON "EventCheckins" FOR EACH ROW EXECUTE FUNCTION clubs_event_session_guard();
CREATE TRIGGER event_booking_session_link BEFORE INSERT OR UPDATE ON "EventSessionBookings" FOR EACH ROW EXECUTE FUNCTION clubs_event_session_guard();

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261110222437_EventLocationAndLookups', '10.0.0');

COMMIT;

START TRANSACTION;
CREATE OR REPLACE FUNCTION clubs_event_registration_guard() RETURNS trigger LANGUAGE plpgsql AS $$
DECLARE ev "Events"; used integer;
BEGIN
 IF TG_OP='UPDATE' AND NEW."EventId"=OLD."EventId" AND NEW."TenantId"=OLD."TenantId" AND NEW."Id"=OLD."Id" AND NEW."Status"=OLD."Status" AND NEW."Companions"=OLD."Companions" THEN RETURN NEW; END IF;
 SELECT * INTO ev FROM "Events" WHERE "Id"=NEW."EventId" AND "TenantId"=NEW."TenantId" FOR UPDATE;
 IF NEW."Status" IN ('Confirmed','Offered') THEN
  IF ev."Lifecycle" IN ('Cancelled','Archived','Completed') OR ev."Publication"<>'Published' OR ev."Paid" THEN RAISE EXCEPTION 'Event cannot confirm registration' USING ERRCODE='23514'; END IF;
  SELECT COALESCE(sum(1+"Companions"),0) INTO used FROM "EventRegistrations" WHERE "EventId"=NEW."EventId" AND "Id"<>NEW."Id" AND "Status" IN ('Confirmed','Offered');
  IF used+1+NEW."Companions">ev."Capacity" THEN RAISE EXCEPTION 'Event capacity exceeded' USING ERRCODE='23514'; END IF;
 END IF; RETURN NEW;
END $$;
CREATE FUNCTION clubs_event_registration_audit() RETURNS trigger LANGUAGE plpgsql AS $$
DECLARE owner_id uuid; previous text;
BEGIN
 IF TG_OP='UPDATE' THEN previous=OLD."Status"; IF NEW."Status"=OLD."Status" THEN RETURN NEW; END IF; END IF;
 SELECT "OrgId" INTO owner_id FROM "Events" WHERE "Id"=NEW."EventId";
 INSERT INTO "Audits"("Id","TenantId","OrgId","ActorId","Action","Resource","Changes","Outcome","At") VALUES(gen_random_uuid(),NEW."TenantId",owner_id,nullif(current_setting('clubs.actor',true),'')::uuid,'events.registration.status',NEW."EventId"::text,jsonb_build_object('registrationId',NEW."Id",'before',previous,'after',NEW."Status")::text,'Success',clock_timestamp()); RETURN NEW;
END $$;
CREATE TRIGGER event_registration_audit AFTER INSERT OR UPDATE ON "EventRegistrations" FOR EACH ROW EXECUTE FUNCTION clubs_event_registration_audit();
CREATE FUNCTION clubs_event_state_audit() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN
 IF NEW."Approval" IS DISTINCT FROM OLD."Approval" OR NEW."Publication" IS DISTINCT FROM OLD."Publication" OR NEW."Lifecycle" IS DISTINCT FROM OLD."Lifecycle" OR NEW."RegistrationEnabled" IS DISTINCT FROM OLD."RegistrationEnabled" THEN
 INSERT INTO "Audits"("Id","TenantId","OrgId","ActorId","Action","Resource","Changes","Outcome","At") VALUES(gen_random_uuid(),NEW."TenantId",NEW."OrgId",nullif(current_setting('clubs.actor',true),'')::uuid,'events.state',NEW."Id"::text,jsonb_build_object('before',jsonb_build_object('approval',OLD."Approval",'publication',OLD."Publication",'lifecycle',OLD."Lifecycle",'registrationEnabled',OLD."RegistrationEnabled"),'after',jsonb_build_object('approval',NEW."Approval",'publication',NEW."Publication",'lifecycle',NEW."Lifecycle",'registrationEnabled',NEW."RegistrationEnabled"))::text,'Success',clock_timestamp()); END IF;RETURN NEW;END $$;
CREATE TRIGGER event_state_audit AFTER UPDATE ON "Events" FOR EACH ROW EXECUTE FUNCTION clubs_event_state_audit();
CREATE INDEX IF NOT EXISTS "IX_Events_EndsAt_Id" ON "Events"("EndsAt","Id");

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261110223448_EventAuditAndRetention', '10.0.0');

COMMIT;

START TRANSACTION;
CREATE OR REPLACE FUNCTION clubs_event_registration_guard() RETURNS trigger LANGUAGE plpgsql AS $$
DECLARE ev "Events"; used integer;
BEGIN
 IF TG_OP='UPDATE' AND NEW."EventId"=OLD."EventId" AND NEW."TenantId"=OLD."TenantId" AND NEW."Id"=OLD."Id" AND NEW."Status"=OLD."Status" AND NEW."Companions"=OLD."Companions" THEN RETURN NEW; END IF;
 SELECT * INTO ev FROM "Events" WHERE "Id"=NEW."EventId" AND "TenantId"=NEW."TenantId" FOR NO KEY UPDATE;
 IF NEW."Status" IN ('Confirmed','Offered') THEN
  IF ev."Lifecycle" IN ('Cancelled','Archived','Completed') OR ev."Publication"<>'Published' OR ev."Paid" THEN RAISE EXCEPTION 'Event cannot confirm registration' USING ERRCODE='23514'; END IF;
  SELECT COALESCE(sum(1+"Companions"),0) INTO used FROM "EventRegistrations" WHERE "EventId"=NEW."EventId" AND "Id"<>NEW."Id" AND "Status" IN ('Confirmed','Offered');
  IF used+1+NEW."Companions">ev."Capacity" THEN RAISE EXCEPTION 'Event capacity exceeded' USING ERRCODE='23514'; END IF;
 END IF; RETURN NEW;
END $$;

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261110234127_EventNonBlockingInvitations', '10.0.0');

COMMIT;

START TRANSACTION;
ALTER TABLE "Mails" ADD "AccountId" uuid;

ALTER TABLE "Mails" ADD "Channel" text NOT NULL DEFAULT '';

ALTER TABLE "Mails" ADD "CommunicationId" uuid;

ALTER TABLE "Mails" ADD "Cost" numeric;

ALTER TABLE "Mails" ADD "CreatedAt" timestamp with time zone;

ALTER TABLE "Mails" ADD "Currency" text NOT NULL DEFAULT '';

ALTER TABLE "Mails" ADD "DeadLetter" boolean NOT NULL DEFAULT FALSE;

ALTER TABLE "Mails" ADD "DeliveryStatus" text NOT NULL DEFAULT '';

ALTER TABLE "Mails" ADD "Language" text NOT NULL DEFAULT '';

ALTER TABLE "Mails" ADD "LastError" text NOT NULL DEFAULT '';

ALTER TABLE "Mails" ADD "ProviderId" text NOT NULL DEFAULT '';

ALTER TABLE "Mails" ADD "Purpose" text NOT NULL DEFAULT '';

ALTER TABLE "Mails" ADD "SenderAssignmentId" uuid;

ALTER TABLE "Mails" ADD "SenderId" uuid;

ALTER TABLE "Mails" ADD "TemplateName" text NOT NULL DEFAULT '';

ALTER TABLE "Mails" ADD "TemplateParameters" text NOT NULL DEFAULT '';

CREATE TABLE "CommunicationAccounts" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "OrgId" uuid NOT NULL,
    "Name" text NOT NULL,
    "Channel" text NOT NULL,
    "Provider" text NOT NULL,
    "Sender" text NOT NULL,
    "Reference" text NOT NULL,
    "BusinessId" text NOT NULL,
    "Secret" text NOT NULL,
    "WebhookSecret" text NOT NULL,
    "VerifyTokenHash" text NOT NULL,
    "Enabled" boolean NOT NULL,
    "Verified" boolean NOT NULL,
    "Inbound" boolean NOT NULL,
    "PerMinute" integer NOT NULL,
    "WindowAt" timestamp with time zone NOT NULL,
    "WindowCount" integer NOT NULL,
    "CallbackUrl" text NOT NULL,
    "Version" bigint NOT NULL,
    CONSTRAINT "PK_CommunicationAccounts" PRIMARY KEY ("Id"),
    CONSTRAINT "AK_CommunicationAccounts_TenantId_Id" UNIQUE ("TenantId", "Id"),
    CONSTRAINT "FK_CommunicationAccounts_Associations_TenantId" FOREIGN KEY ("TenantId") REFERENCES "Associations" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_CommunicationAccounts_Orgs_TenantId_OrgId" FOREIGN KEY ("TenantId", "OrgId") REFERENCES "Orgs" ("TenantId", "Id") ON DELETE RESTRICT
);

CREATE TABLE "CommunicationAudiences" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "OrgId" uuid NOT NULL,
    "Name" text NOT NULL,
    "Rules" text NOT NULL,
    "Version" bigint NOT NULL,
    CONSTRAINT "PK_CommunicationAudiences" PRIMARY KEY ("Id"),
    CONSTRAINT "AK_CommunicationAudiences_TenantId_Id" UNIQUE ("TenantId", "Id"),
    CONSTRAINT "FK_CommunicationAudiences_Associations_TenantId" FOREIGN KEY ("TenantId") REFERENCES "Associations" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_CommunicationAudiences_Orgs_TenantId_OrgId" FOREIGN KEY ("TenantId", "OrgId") REFERENCES "Orgs" ("TenantId", "Id") ON DELETE RESTRICT
);

CREATE TABLE "CommunicationPolicies" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "OrgId" uuid NOT NULL,
    "MemberDirect" boolean NOT NULL,
    "ApprovalThreshold" integer NOT NULL,
    "RetentionDays" integer NOT NULL,
    CONSTRAINT "PK_CommunicationPolicies" PRIMARY KEY ("Id"),
    CONSTRAINT "AK_CommunicationPolicies_TenantId_Id" UNIQUE ("TenantId", "Id"),
    CONSTRAINT "FK_CommunicationPolicies_Associations_TenantId" FOREIGN KEY ("TenantId") REFERENCES "Associations" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_CommunicationPolicies_Orgs_TenantId_OrgId" FOREIGN KEY ("TenantId", "OrgId") REFERENCES "Orgs" ("TenantId", "Id") ON DELETE RESTRICT
);

CREATE TABLE "CommunicationPreferences" (
    "Id" uuid NOT NULL,
    "UserId" uuid NOT NULL,
    "Channel" text NOT NULL,
    "Transactional" boolean NOT NULL,
    "Promotional" boolean NOT NULL,
    "Direct" boolean NOT NULL,
    "Birthday" boolean NOT NULL,
    "QuietStart" integer NOT NULL,
    "QuietEnd" integer NOT NULL,
    "QuietEnabled" boolean NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_CommunicationPreferences" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_CommunicationPreferences_UserProfiles_UserId" FOREIGN KEY ("UserId") REFERENCES "UserProfiles" ("Id") ON DELETE RESTRICT
);

CREATE TABLE "CommunicationRevisions" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "ResourceId" uuid NOT NULL,
    "Kind" text NOT NULL,
    "Version" bigint NOT NULL,
    "Snapshot" text NOT NULL,
    "ActorId" uuid NOT NULL,
    "At" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_CommunicationRevisions" PRIMARY KEY ("Id"),
    CONSTRAINT "AK_CommunicationRevisions_TenantId_Id" UNIQUE ("TenantId", "Id"),
    CONSTRAINT "FK_CommunicationRevisions_Associations_TenantId" FOREIGN KEY ("TenantId") REFERENCES "Associations" ("Id") ON DELETE RESTRICT
);

CREATE TABLE "CommunicationSuppressions" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "Channel" text NOT NULL,
    "AddressHash" text NOT NULL,
    "Reason" text NOT NULL,
    "At" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_CommunicationSuppressions" PRIMARY KEY ("Id"),
    CONSTRAINT "AK_CommunicationSuppressions_TenantId_Id" UNIQUE ("TenantId", "Id"),
    CONSTRAINT "FK_CommunicationSuppressions_Associations_TenantId" FOREIGN KEY ("TenantId") REFERENCES "Associations" ("Id") ON DELETE RESTRICT
);

CREATE TABLE "CommunicationTemplates" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "OrgId" uuid NOT NULL,
    "Name" text NOT NULL,
    "Channel" text NOT NULL,
    "Category" text NOT NULL,
    "Language" text NOT NULL,
    "Subject" text NOT NULL,
    "Body" text NOT NULL,
    "ProviderName" text NOT NULL,
    "ProviderStatus" text NOT NULL,
    "Active" boolean NOT NULL,
    "Version" bigint NOT NULL,
    CONSTRAINT "PK_CommunicationTemplates" PRIMARY KEY ("Id"),
    CONSTRAINT "AK_CommunicationTemplates_TenantId_Id" UNIQUE ("TenantId", "Id"),
    CONSTRAINT "FK_CommunicationTemplates_Associations_TenantId" FOREIGN KEY ("TenantId") REFERENCES "Associations" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_CommunicationTemplates_Orgs_TenantId_OrgId" FOREIGN KEY ("TenantId", "OrgId") REFERENCES "Orgs" ("TenantId", "Id") ON DELETE RESTRICT
);

CREATE TABLE "CommunicationProviderEvents" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "AccountId" uuid NOT NULL,
    "EventKey" text NOT NULL,
    "ProviderId" text NOT NULL,
    "Status" text NOT NULL,
    "Error" text NOT NULL,
    "At" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_CommunicationProviderEvents" PRIMARY KEY ("Id"),
    CONSTRAINT "AK_CommunicationProviderEvents_TenantId_Id" UNIQUE ("TenantId", "Id"),
    CONSTRAINT "FK_CommunicationProviderEvents_Associations_TenantId" FOREIGN KEY ("TenantId") REFERENCES "Associations" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_CommunicationProviderEvents_CommunicationAccounts_TenantId_~" FOREIGN KEY ("TenantId", "AccountId") REFERENCES "CommunicationAccounts" ("TenantId", "Id") ON DELETE RESTRICT
);

CREATE TABLE "Conversations" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "OrgId" uuid NOT NULL,
    "OwnerId" uuid NOT NULL,
    "AccountId" uuid,
    "CommitteeId" uuid,
    "EventId" uuid,
    "Kind" text NOT NULL,
    "Channel" text NOT NULL,
    "Title" text NOT NULL,
    "ExternalThread" text NOT NULL,
    "Status" text NOT NULL,
    "Priority" text NOT NULL,
    "Tags" text NOT NULL,
    "AssignedId" uuid,
    "At" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    "LastInboundAt" timestamp with time zone,
    "Preview" text NOT NULL,
    "Version" bigint NOT NULL,
    CONSTRAINT "PK_Conversations" PRIMARY KEY ("Id"),
    CONSTRAINT "AK_Conversations_TenantId_Id" UNIQUE ("TenantId", "Id"),
    CONSTRAINT "FK_Conversations_Associations_TenantId" FOREIGN KEY ("TenantId") REFERENCES "Associations" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_Conversations_Committees_TenantId_CommitteeId" FOREIGN KEY ("TenantId", "CommitteeId") REFERENCES "Committees" ("TenantId", "Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_Conversations_CommunicationAccounts_TenantId_AccountId" FOREIGN KEY ("TenantId", "AccountId") REFERENCES "CommunicationAccounts" ("TenantId", "Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_Conversations_Events_TenantId_EventId" FOREIGN KEY ("TenantId", "EventId") REFERENCES "Events" ("TenantId", "Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_Conversations_Orgs_TenantId_OrgId" FOREIGN KEY ("TenantId", "OrgId") REFERENCES "Orgs" ("TenantId", "Id") ON DELETE RESTRICT
);

CREATE TABLE "CommunicationAutomations" (
    "Id" uuid NOT NULL,
    "ApprovalRequestId" uuid,
    "Status" text NOT NULL,
    "ContentHash" text NOT NULL,
    "TemplateVersion" bigint NOT NULL,
    "TenantId" uuid NOT NULL,
    "OrgId" uuid NOT NULL,
    "CreatorId" uuid NOT NULL,
    "Name" text NOT NULL,
    "Trigger" text NOT NULL,
    "TemplateId" uuid NOT NULL,
    "AccountId" uuid,
    "Active" boolean NOT NULL,
    "DelayMinutes" integer NOT NULL,
    "DailyLimit" integer NOT NULL,
    "StartsAt" timestamp with time zone NOT NULL,
    "Version" bigint NOT NULL,
    CONSTRAINT "PK_CommunicationAutomations" PRIMARY KEY ("Id"),
    CONSTRAINT "AK_CommunicationAutomations_TenantId_Id" UNIQUE ("TenantId", "Id"),
    CONSTRAINT "FK_CommunicationAutomations_Associations_TenantId" FOREIGN KEY ("TenantId") REFERENCES "Associations" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_CommunicationAutomations_CommunicationAccounts_TenantId_Acc~" FOREIGN KEY ("TenantId", "AccountId") REFERENCES "CommunicationAccounts" ("TenantId", "Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_CommunicationAutomations_CommunicationTemplates_TenantId_Te~" FOREIGN KEY ("TenantId", "TemplateId") REFERENCES "CommunicationTemplates" ("TenantId", "Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_CommunicationAutomations_GovernanceRequests_TenantId_Approv~" FOREIGN KEY ("TenantId", "ApprovalRequestId") REFERENCES "GovernanceRequests" ("TenantId", "Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_CommunicationAutomations_Orgs_TenantId_OrgId" FOREIGN KEY ("TenantId", "OrgId") REFERENCES "Orgs" ("TenantId", "Id") ON DELETE RESTRICT
);

CREATE TABLE "CommunicationBroadcasts" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "OrgId" uuid NOT NULL,
    "CreatorId" uuid NOT NULL,
    "AccountId" uuid,
    "TemplateId" uuid,
    "EventId" uuid,
    "ApprovalRequestId" uuid,
    "Kind" text NOT NULL,
    "Category" text NOT NULL,
    "Channel" text NOT NULL,
    "Purpose" text NOT NULL,
    "Title" text NOT NULL,
    "Subject" text NOT NULL,
    "Body" text NOT NULL,
    "Rules" text NOT NULL,
    "ContentHash" text NOT NULL,
    "Status" text NOT NULL,
    "Acknowledgment" boolean NOT NULL,
    "Pinned" boolean NOT NULL,
    "ExpiresAt" timestamp with time zone,
    "SendAt" timestamp with time zone NOT NULL,
    "TimeZone" text NOT NULL,
    "RepeatDays" integer NOT NULL,
    "Cycles" integer NOT NULL,
    "CurrentCycle" integer NOT NULL,
    "CompletedAt" timestamp with time zone,
    "Version" bigint NOT NULL,
    "At" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_CommunicationBroadcasts" PRIMARY KEY ("Id"),
    CONSTRAINT "AK_CommunicationBroadcasts_TenantId_Id" UNIQUE ("TenantId", "Id"),
    CONSTRAINT "FK_CommunicationBroadcasts_Associations_TenantId" FOREIGN KEY ("TenantId") REFERENCES "Associations" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_CommunicationBroadcasts_CommunicationAccounts_TenantId_Acco~" FOREIGN KEY ("TenantId", "AccountId") REFERENCES "CommunicationAccounts" ("TenantId", "Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_CommunicationBroadcasts_CommunicationTemplates_TenantId_Tem~" FOREIGN KEY ("TenantId", "TemplateId") REFERENCES "CommunicationTemplates" ("TenantId", "Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_CommunicationBroadcasts_Events_TenantId_EventId" FOREIGN KEY ("TenantId", "EventId") REFERENCES "Events" ("TenantId", "Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_CommunicationBroadcasts_GovernanceRequests_TenantId_Approva~" FOREIGN KEY ("TenantId", "ApprovalRequestId") REFERENCES "GovernanceRequests" ("TenantId", "Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_CommunicationBroadcasts_Orgs_TenantId_OrgId" FOREIGN KEY ("TenantId", "OrgId") REFERENCES "Orgs" ("TenantId", "Id") ON DELETE RESTRICT
);

CREATE TABLE "ConversationParticipants" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "ConversationId" uuid NOT NULL,
    "UserId" uuid NOT NULL,
    "Staff" boolean NOT NULL,
    "Muted" boolean NOT NULL,
    "Active" boolean NOT NULL,
    "ReadAt" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_ConversationParticipants" PRIMARY KEY ("Id"),
    CONSTRAINT "AK_ConversationParticipants_TenantId_Id" UNIQUE ("TenantId", "Id"),
    CONSTRAINT "FK_ConversationParticipants_Associations_TenantId" FOREIGN KEY ("TenantId") REFERENCES "Associations" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_ConversationParticipants_Conversations_TenantId_Conversatio~" FOREIGN KEY ("TenantId", "ConversationId") REFERENCES "Conversations" ("TenantId", "Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_ConversationParticipants_UserProfiles_UserId" FOREIGN KEY ("UserId") REFERENCES "UserProfiles" ("Id") ON DELETE RESTRICT
);

CREATE TABLE "CommunicationExecutions" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "AutomationId" uuid NOT NULL,
    "SourceKey" text NOT NULL,
    "MailId" uuid,
    "Status" text NOT NULL,
    "At" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_CommunicationExecutions" PRIMARY KEY ("Id"),
    CONSTRAINT "AK_CommunicationExecutions_TenantId_Id" UNIQUE ("TenantId", "Id"),
    CONSTRAINT "FK_CommunicationExecutions_Associations_TenantId" FOREIGN KEY ("TenantId") REFERENCES "Associations" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_CommunicationExecutions_CommunicationAutomations_TenantId_A~" FOREIGN KEY ("TenantId", "AutomationId") REFERENCES "CommunicationAutomations" ("TenantId", "Id") ON DELETE RESTRICT
);

CREATE TABLE "CommunicationMessages" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "OrgId" uuid NOT NULL,
    "ConversationId" uuid,
    "BroadcastId" uuid,
    "SenderId" uuid,
    "PersonId" uuid,
    "ReplyToId" uuid,
    "Body" text NOT NULL,
    "Subject" text NOT NULL,
    "Direction" text NOT NULL,
    "Internal" boolean NOT NULL,
    "ClientKey" text NOT NULL,
    "ProviderId" text NOT NULL,
    "At" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_CommunicationMessages" PRIMARY KEY ("Id"),
    CONSTRAINT "AK_CommunicationMessages_TenantId_Id" UNIQUE ("TenantId", "Id"),
    CONSTRAINT "FK_CommunicationMessages_Associations_TenantId" FOREIGN KEY ("TenantId") REFERENCES "Associations" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_CommunicationMessages_CommunicationBroadcasts_TenantId_Broa~" FOREIGN KEY ("TenantId", "BroadcastId") REFERENCES "CommunicationBroadcasts" ("TenantId", "Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_CommunicationMessages_Conversations_TenantId_ConversationId" FOREIGN KEY ("TenantId", "ConversationId") REFERENCES "Conversations" ("TenantId", "Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_CommunicationMessages_Orgs_TenantId_OrgId" FOREIGN KEY ("TenantId", "OrgId") REFERENCES "Orgs" ("TenantId", "Id") ON DELETE RESTRICT
);

CREATE TABLE "CommunicationRecipients" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "BroadcastId" uuid NOT NULL,
    "PersonId" uuid NOT NULL,
    "UserId" uuid,
    "Name" text NOT NULL,
    "Club" text NOT NULL,
    "MemberNumber" text NOT NULL,
    "QueuedCycle" integer NOT NULL,
    "Exclusion" text NOT NULL,
    "SeenAt" timestamp with time zone,
    "AcknowledgedAt" timestamp with time zone,
    CONSTRAINT "PK_CommunicationRecipients" PRIMARY KEY ("Id"),
    CONSTRAINT "AK_CommunicationRecipients_TenantId_Id" UNIQUE ("TenantId", "Id"),
    CONSTRAINT "FK_CommunicationRecipients_Associations_TenantId" FOREIGN KEY ("TenantId") REFERENCES "Associations" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_CommunicationRecipients_CommunicationBroadcasts_TenantId_Br~" FOREIGN KEY ("TenantId", "BroadcastId") REFERENCES "CommunicationBroadcasts" ("TenantId", "Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_CommunicationRecipients_Persons_PersonId" FOREIGN KEY ("PersonId") REFERENCES "Persons" ("Id") ON DELETE RESTRICT
);

CREATE TABLE "CommunicationAttachments" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "MessageId" uuid NOT NULL,
    "Name" text NOT NULL,
    "Type" text NOT NULL,
    "Size" bigint NOT NULL,
    "StorageKey" text NOT NULL,
    "Hash" text NOT NULL,
    "ExpiresAt" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_CommunicationAttachments" PRIMARY KEY ("Id"),
    CONSTRAINT "AK_CommunicationAttachments_TenantId_Id" UNIQUE ("TenantId", "Id"),
    CONSTRAINT "FK_CommunicationAttachments_Associations_TenantId" FOREIGN KEY ("TenantId") REFERENCES "Associations" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_CommunicationAttachments_CommunicationMessages_TenantId_Mes~" FOREIGN KEY ("TenantId", "MessageId") REFERENCES "CommunicationMessages" ("TenantId", "Id") ON DELETE RESTRICT
);

INSERT INTO "Permissions" ("Id")
VALUES ('communications.configure');
INSERT INTO "Permissions" ("Id")
VALUES ('communications.inbox');
INSERT INTO "Permissions" ("Id")
VALUES ('communications.manage');
INSERT INTO "Permissions" ("Id")
VALUES ('communications.reports');
INSERT INTO "Permissions" ("Id")
VALUES ('communications.send');
INSERT INTO "Permissions" ("Id")
VALUES ('communications.view');

CREATE UNIQUE INDEX "IX_Mails_CommunicationId" ON "Mails" ("CommunicationId");

CREATE INDEX "IX_Mails_TenantId_AccountId" ON "Mails" ("TenantId", "AccountId");

CREATE INDEX "IX_Mails_TenantId_CommunicationId" ON "Mails" ("TenantId", "CommunicationId");

CREATE INDEX "IX_CommunicationAccounts_TenantId_OrgId" ON "CommunicationAccounts" ("TenantId", "OrgId");

CREATE INDEX "IX_CommunicationAttachments_TenantId_MessageId" ON "CommunicationAttachments" ("TenantId", "MessageId");

CREATE INDEX "IX_CommunicationAudiences_TenantId_OrgId" ON "CommunicationAudiences" ("TenantId", "OrgId");

CREATE INDEX "IX_CommunicationAutomations_TenantId_AccountId" ON "CommunicationAutomations" ("TenantId", "AccountId");

CREATE INDEX "IX_CommunicationAutomations_TenantId_ApprovalRequestId" ON "CommunicationAutomations" ("TenantId", "ApprovalRequestId");

CREATE INDEX "IX_CommunicationAutomations_TenantId_OrgId" ON "CommunicationAutomations" ("TenantId", "OrgId");

CREATE INDEX "IX_CommunicationAutomations_TenantId_TemplateId" ON "CommunicationAutomations" ("TenantId", "TemplateId");

CREATE INDEX "IX_CommunicationBroadcasts_Status_SendAt" ON "CommunicationBroadcasts" ("Status", "SendAt");

CREATE INDEX "IX_CommunicationBroadcasts_TenantId_AccountId" ON "CommunicationBroadcasts" ("TenantId", "AccountId");

CREATE INDEX "IX_CommunicationBroadcasts_TenantId_ApprovalRequestId" ON "CommunicationBroadcasts" ("TenantId", "ApprovalRequestId");

CREATE INDEX "IX_CommunicationBroadcasts_TenantId_EventId" ON "CommunicationBroadcasts" ("TenantId", "EventId");

CREATE INDEX "IX_CommunicationBroadcasts_TenantId_OrgId" ON "CommunicationBroadcasts" ("TenantId", "OrgId");

CREATE INDEX "IX_CommunicationBroadcasts_TenantId_TemplateId" ON "CommunicationBroadcasts" ("TenantId", "TemplateId");

CREATE UNIQUE INDEX "IX_CommunicationExecutions_AutomationId_SourceKey" ON "CommunicationExecutions" ("AutomationId", "SourceKey");

CREATE INDEX "IX_CommunicationExecutions_TenantId_AutomationId" ON "CommunicationExecutions" ("TenantId", "AutomationId");

CREATE INDEX "IX_CommunicationMessages_BroadcastId_At" ON "CommunicationMessages" ("BroadcastId", "At");

CREATE INDEX "IX_CommunicationMessages_ConversationId_At_Id" ON "CommunicationMessages" ("ConversationId", "At", "Id");

CREATE INDEX "IX_CommunicationMessages_TenantId_BroadcastId" ON "CommunicationMessages" ("TenantId", "BroadcastId");

CREATE INDEX "IX_CommunicationMessages_TenantId_ConversationId" ON "CommunicationMessages" ("TenantId", "ConversationId");

CREATE INDEX "IX_CommunicationMessages_TenantId_OrgId" ON "CommunicationMessages" ("TenantId", "OrgId");

CREATE UNIQUE INDEX "IX_CommunicationMessages_TenantId_SenderId_ClientKey" ON "CommunicationMessages" ("TenantId", "SenderId", "ClientKey");

CREATE UNIQUE INDEX "IX_CommunicationPolicies_OrgId" ON "CommunicationPolicies" ("OrgId");

CREATE INDEX "IX_CommunicationPolicies_TenantId_OrgId" ON "CommunicationPolicies" ("TenantId", "OrgId");

CREATE UNIQUE INDEX "IX_CommunicationPreferences_UserId_Channel" ON "CommunicationPreferences" ("UserId", "Channel");

CREATE UNIQUE INDEX "IX_CommunicationProviderEvents_AccountId_EventKey" ON "CommunicationProviderEvents" ("AccountId", "EventKey");

CREATE INDEX "IX_CommunicationProviderEvents_TenantId_AccountId" ON "CommunicationProviderEvents" ("TenantId", "AccountId");

CREATE UNIQUE INDEX "IX_CommunicationRecipients_BroadcastId_PersonId" ON "CommunicationRecipients" ("BroadcastId", "PersonId");

CREATE INDEX "IX_CommunicationRecipients_BroadcastId_QueuedCycle_Id" ON "CommunicationRecipients" ("BroadcastId", "QueuedCycle", "Id");

CREATE INDEX "IX_CommunicationRecipients_PersonId" ON "CommunicationRecipients" ("PersonId");

CREATE INDEX "IX_CommunicationRecipients_TenantId_BroadcastId" ON "CommunicationRecipients" ("TenantId", "BroadcastId");

CREATE INDEX "IX_CommunicationRecipients_UserId_BroadcastId" ON "CommunicationRecipients" ("UserId", "BroadcastId");

CREATE UNIQUE INDEX "IX_CommunicationRevisions_ResourceId_Version" ON "CommunicationRevisions" ("ResourceId", "Version");

CREATE UNIQUE INDEX "IX_CommunicationSuppressions_TenantId_Channel_AddressHash" ON "CommunicationSuppressions" ("TenantId", "Channel", "AddressHash");

CREATE INDEX "IX_CommunicationTemplates_TenantId_OrgId" ON "CommunicationTemplates" ("TenantId", "OrgId");

CREATE UNIQUE INDEX "IX_ConversationParticipants_ConversationId_UserId" ON "ConversationParticipants" ("ConversationId", "UserId");

CREATE INDEX "IX_ConversationParticipants_TenantId_ConversationId" ON "ConversationParticipants" ("TenantId", "ConversationId");

CREATE INDEX "IX_ConversationParticipants_UserId_Active_ConversationId" ON "ConversationParticipants" ("UserId", "Active", "ConversationId");

CREATE UNIQUE INDEX "IX_Conversations_AccountId_ExternalThread" ON "Conversations" ("AccountId", "ExternalThread") WHERE "ExternalThread"<>'';

CREATE INDEX "IX_Conversations_TenantId_AccountId" ON "Conversations" ("TenantId", "AccountId");

CREATE INDEX "IX_Conversations_TenantId_CommitteeId" ON "Conversations" ("TenantId", "CommitteeId");

CREATE INDEX "IX_Conversations_TenantId_EventId" ON "Conversations" ("TenantId", "EventId");

CREATE INDEX "IX_Conversations_TenantId_OrgId" ON "Conversations" ("TenantId", "OrgId");

CREATE INDEX "IX_Conversations_TenantId_UpdatedAt_Id" ON "Conversations" ("TenantId", "UpdatedAt", "Id");

ALTER TABLE "Mails" ADD CONSTRAINT "FK_Mails_CommunicationAccounts_TenantId_AccountId" FOREIGN KEY ("TenantId", "AccountId") REFERENCES "CommunicationAccounts" ("TenantId", "Id") ON DELETE RESTRICT;

ALTER TABLE "Mails" ADD CONSTRAINT "FK_Mails_CommunicationMessages_TenantId_CommunicationId" FOREIGN KEY ("TenantId", "CommunicationId") REFERENCES "CommunicationMessages" ("TenantId", "Id") ON DELETE RESTRICT;


INSERT INTO "Permissions"("Id") VALUES('communications.view'),('communications.send'),('communications.manage'),('communications.inbox'),('communications.reports'),('communications.configure') ON CONFLICT DO NOTHING;
INSERT INTO "RolePermissions"("Id","RoleId","PermissionId") SELECT gen_random_uuid(),r."Id",p."Id" FROM "Roles" r CROSS JOIN "Permissions" p WHERE p."Id" LIKE 'communications.%' AND (r."Name" IN ('Association Super Admin','Platform Administrator') OR (r."Name" IN ('Club Manager','Club Secretary','Club President') AND p."Id"<>'communications.configure') OR (r."Name" IN ('Club Member','Applicant') AND p."Id"='communications.view')) ON CONFLICT DO NOTHING;
ALTER TABLE "CommunicationAccounts" ADD CONSTRAINT communication_account_channel CHECK (("Channel"='Email' AND "Provider"='SMTP') OR ("Channel"='WhatsApp' AND "Provider"='Meta') OR ("Channel"='SMS' AND "Provider"='Twilio'));
ALTER TABLE "CommunicationAccounts" ADD CONSTRAINT communication_account_rate CHECK ("PerMinute">0 AND "PerMinute"<=1000);
ALTER TABLE "CommunicationBroadcasts" ADD CONSTRAINT communication_broadcast_state CHECK ("Status" IN ('Draft','Review','Approved','Scheduled','Sending','Completed','Partially Completed','Failed','Rejected','Changes Requested','Cancelled','Archived'));
CREATE INDEX "IX_Mails_CommunicationDispatch" ON "Mails"("Channel","NextAttemptAt","Id") WHERE "SentAt" IS NULL AND NOT "Suppressed" AND NOT "DeadLetter";
CREATE INDEX "IX_CommunicationMessages_Provider" ON "CommunicationMessages"("TenantId","ProviderId") WHERE "ProviderId"<>'';
CREATE FUNCTION clubs_communication_revision_guard() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RAISE EXCEPTION 'Communication revisions are immutable' USING ERRCODE='23514';END $$;
CREATE TRIGGER communication_revision_immutable BEFORE UPDATE OR DELETE ON "CommunicationRevisions" FOR EACH ROW EXECUTE FUNCTION clubs_communication_revision_guard();
CREATE FUNCTION clubs_communication_broadcast_guard() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN
 IF OLD."Status"<>'Draft' AND (NEW."Body",NEW."Subject",NEW."Rules",NEW."Channel",NEW."OrgId",NEW."CreatorId",NEW."AccountId",NEW."Purpose",NEW."ContentHash") IS DISTINCT FROM (OLD."Body",OLD."Subject",OLD."Rules",OLD."Channel",OLD."OrgId",OLD."CreatorId",OLD."AccountId",OLD."Purpose",OLD."ContentHash") THEN RAISE EXCEPTION 'Approved broadcast identity/content is immutable; duplicate for revision' USING ERRCODE='23514';END IF;RETURN NEW;END $$;
CREATE TRIGGER communication_broadcast_immutable BEFORE UPDATE ON "CommunicationBroadcasts" FOR EACH ROW EXECUTE FUNCTION clubs_communication_broadcast_guard();
CREATE FUNCTION clubs_communication_recipient_guard() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN IF (NEW."BroadcastId",NEW."PersonId",NEW."UserId",NEW."Name",NEW."Club",NEW."MemberNumber") IS DISTINCT FROM (OLD."BroadcastId",OLD."PersonId",OLD."UserId",OLD."Name",OLD."Club",OLD."MemberNumber") THEN RAISE EXCEPTION 'Approved recipient identity snapshot is immutable' USING ERRCODE='23514';END IF;RETURN NEW;END $$;
CREATE TRIGGER communication_recipient_immutable BEFORE UPDATE ON "CommunicationRecipients" FOR EACH ROW EXECUTE FUNCTION clubs_communication_recipient_guard();


INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261111000507_CommunicationFoundation', '10.0.0');

COMMIT;

START TRANSACTION;
ALTER TABLE "Mails" ADD "TemplateVersion" bigint NOT NULL DEFAULT 0;

ALTER TABLE "CommunicationTemplates" ADD "AccountId" uuid;

ALTER TABLE "CommunicationBroadcasts" ADD "TemplateVersion" bigint NOT NULL DEFAULT 0;

CREATE INDEX "IX_CommunicationTemplates_TenantId_AccountId" ON "CommunicationTemplates" ("TenantId", "AccountId");

ALTER TABLE "CommunicationTemplates" ADD CONSTRAINT "FK_CommunicationTemplates_CommunicationAccounts_TenantId_Accou~" FOREIGN KEY ("TenantId", "AccountId") REFERENCES "CommunicationAccounts" ("TenantId", "Id") ON DELETE RESTRICT;

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261111002847_CommunicationProviderBinding', '10.0.0');

COMMIT;

START TRANSACTION;
ALTER TABLE "Mails" ALTER COLUMN "TemplateParameters" SET DEFAULT '[]';

ALTER TABLE "Mails" ALTER COLUMN "Purpose" SET DEFAULT 'Transactional';

ALTER TABLE "Mails" ALTER COLUMN "DeliveryStatus" SET DEFAULT 'Pending';

ALTER TABLE "Mails" ALTER COLUMN "Channel" SET DEFAULT 'Email';

UPDATE "Mails" SET "DeadLetter"=false,"Attempts"=0,"NextAttemptAt"=now(),"LastError"='' WHERE "Channel"='' AND "SentAt" IS NULL AND "LastError"='InvalidOperationException';
UPDATE "Mails" SET "Channel"='Email' WHERE "Channel"='';
UPDATE "Mails" SET "Purpose"='Transactional' WHERE "Purpose"='';
UPDATE "Mails" SET "DeliveryStatus"=CASE WHEN "SentAt" IS NOT NULL THEN 'Accepted' WHEN "Suppressed" THEN 'Suppressed' ELSE 'Pending' END WHERE "DeliveryStatus"='' OR ("Channel"='Email' AND "LastError"='' AND "DeliveryStatus"='Failed' AND NOT "DeadLetter");
UPDATE "Mails" SET "TemplateParameters"='[]' WHERE "TemplateParameters"='';

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261111003141_CommunicationOutboxDefaults', '10.0.0');

COMMIT;

START TRANSACTION;
ALTER TABLE "CommunicationAttachments" ADD "Attempts" integer NOT NULL DEFAULT 0;

ALTER TABLE "CommunicationAttachments" ADD "NextAttemptAt" timestamp with time zone NOT NULL DEFAULT TIMESTAMPTZ '-infinity';

ALTER TABLE "CommunicationAttachments" ADD "ProviderAccountId" uuid;

ALTER TABLE "CommunicationAttachments" ADD "ProviderMediaId" text NOT NULL DEFAULT '';

ALTER TABLE "CommunicationAttachments" ADD "Status" text NOT NULL DEFAULT 'Stored';

CREATE INDEX "IX_CommunicationAttachments_Status_NextAttemptAt" ON "CommunicationAttachments" ("Status", "NextAttemptAt");

CREATE INDEX "IX_CommunicationAttachments_TenantId_ProviderAccountId" ON "CommunicationAttachments" ("TenantId", "ProviderAccountId");

ALTER TABLE "CommunicationAttachments" ADD CONSTRAINT "FK_CommunicationAttachments_CommunicationAccounts_TenantId_Pro~" FOREIGN KEY ("TenantId", "ProviderAccountId") REFERENCES "CommunicationAccounts" ("TenantId", "Id") ON DELETE RESTRICT;

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261111005121_CommunicationInboundMedia', '10.0.0');

COMMIT;

START TRANSACTION;
ALTER TABLE "Mails" ADD "EventId" uuid;

CREATE INDEX "IX_Mails_TenantId_CreatedAt_Channel_DeliveryStatus" ON "Mails" ("TenantId", "CreatedAt", "Channel", "DeliveryStatus");

CREATE INDEX "IX_Mails_TenantId_EventId" ON "Mails" ("TenantId", "EventId");

CREATE INDEX "IX_CommunicationMessages_TenantId_SenderId_At" ON "CommunicationMessages" ("TenantId", "SenderId", "At") WHERE "ConversationId" IS NOT NULL;

ALTER TABLE "Mails" ADD CONSTRAINT "FK_Mails_Events_TenantId_EventId" FOREIGN KEY ("TenantId", "EventId") REFERENCES "Events" ("TenantId", "Id") ON DELETE RESTRICT;

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261111005801_CommunicationProvenanceAndLimits', '10.0.0');

COMMIT;

START TRANSACTION;
ALTER TABLE "GovernanceDocuments" ADD "ApprovalRequestId" uuid;

ALTER TABLE "GovernanceDocuments" ADD "CurrentVersion" integer NOT NULL DEFAULT 0;

ALTER TABLE "GovernanceDocuments" ADD "Folder" text NOT NULL DEFAULT '';

ALTER TABLE "GovernanceDocuments" ADD "ObjectKey" text NOT NULL DEFAULT '';

ALTER TABLE "GovernanceDocuments" ADD "OperationId" uuid;

ALTER TABLE "GovernanceDocuments" ADD "SharedUsers" text NOT NULL DEFAULT '';

ALTER TABLE "GovernanceDocuments" ADD "Tags" text NOT NULL DEFAULT '';

ALTER TABLE "GovernanceDocuments" ADD "Visibility" text NOT NULL DEFAULT '';

CREATE TABLE "CertificateTemplates" (
    "Id" uuid NOT NULL,
    "Name" text NOT NULL,
    "Type" text NOT NULL,
    "Layout" text NOT NULL,
    "Active" boolean NOT NULL,
    "TenantId" uuid NOT NULL,
    "OrgId" uuid NOT NULL,
    "Version" bigint NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_CertificateTemplates" PRIMARY KEY ("Id"),
    CONSTRAINT "AK_CertificateTemplates_TenantId_Id" UNIQUE ("TenantId", "Id"),
    CONSTRAINT "FK_CertificateTemplates_Associations_TenantId" FOREIGN KEY ("TenantId") REFERENCES "Associations" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_CertificateTemplates_Orgs_TenantId_OrgId" FOREIGN KEY ("TenantId", "OrgId") REFERENCES "Orgs" ("TenantId", "Id") ON DELETE RESTRICT
);

CREATE TABLE "ClubMeetings" (
    "Id" uuid NOT NULL,
    "Title" text NOT NULL,
    "Type" text NOT NULL,
    "OrganizerId" uuid NOT NULL,
    "ChairId" uuid,
    "SecretaryId" uuid,
    "CommitteeId" uuid,
    "StartsAt" timestamp with time zone NOT NULL,
    "EndsAt" timestamp with time zone NOT NULL,
    "TimeZone" text NOT NULL,
    "Location" text NOT NULL,
    "OnlineUrl" text NOT NULL,
    "Visibility" text NOT NULL,
    "Status" text NOT NULL,
    "EventId" uuid,
    "SessionId" uuid,
    "SeriesId" uuid,
    "QuorumMinimum" integer NOT NULL,
    "QuorumPercent" numeric NOT NULL,
    "VotingCategories" text NOT NULL,
    "Agenda" text NOT NULL,
    "AgendaStatus" text NOT NULL,
    "RequiredPeople" text NOT NULL,
    "OptionalPeople" text NOT NULL,
    "AgendaRequestId" uuid,
    "TenantId" uuid NOT NULL,
    "OrgId" uuid NOT NULL,
    "Version" bigint NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_ClubMeetings" PRIMARY KEY ("Id"),
    CONSTRAINT "AK_ClubMeetings_TenantId_Id" UNIQUE ("TenantId", "Id"),
    CONSTRAINT "FK_ClubMeetings_Associations_TenantId" FOREIGN KEY ("TenantId") REFERENCES "Associations" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_ClubMeetings_Committees_TenantId_CommitteeId" FOREIGN KEY ("TenantId", "CommitteeId") REFERENCES "Committees" ("TenantId", "Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_ClubMeetings_EventSessions_TenantId_SessionId" FOREIGN KEY ("TenantId", "SessionId") REFERENCES "EventSessions" ("TenantId", "Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_ClubMeetings_Events_TenantId_EventId" FOREIGN KEY ("TenantId", "EventId") REFERENCES "Events" ("TenantId", "Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_ClubMeetings_Orgs_TenantId_OrgId" FOREIGN KEY ("TenantId", "OrgId") REFERENCES "Orgs" ("TenantId", "Id") ON DELETE RESTRICT
);

CREATE TABLE "DocumentVersions" (
    "Id" uuid NOT NULL,
    "DocumentId" uuid NOT NULL,
    "Number" integer NOT NULL,
    "ObjectKey" text NOT NULL,
    "FileName" text NOT NULL,
    "ContentType" text NOT NULL,
    "Sha256" text NOT NULL,
    "Size" bigint NOT NULL,
    "UploaderId" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "OrgId" uuid NOT NULL,
    "Version" bigint NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_DocumentVersions" PRIMARY KEY ("Id"),
    CONSTRAINT "AK_DocumentVersions_TenantId_Id" UNIQUE ("TenantId", "Id"),
    CONSTRAINT "FK_DocumentVersions_Associations_TenantId" FOREIGN KEY ("TenantId") REFERENCES "Associations" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_DocumentVersions_GovernanceDocuments_TenantId_DocumentId" FOREIGN KEY ("TenantId", "DocumentId") REFERENCES "GovernanceDocuments" ("TenantId", "Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_DocumentVersions_Orgs_TenantId_OrgId" FOREIGN KEY ("TenantId", "OrgId") REFERENCES "Orgs" ("TenantId", "Id") ON DELETE RESTRICT
);

CREATE TABLE "ImpactMetrics" (
    "Id" uuid NOT NULL,
    "Name" text NOT NULL,
    "Unit" text NOT NULL,
    "Description" text NOT NULL,
    "DataType" text NOT NULL,
    "Aggregation" text NOT NULL,
    "Category" text NOT NULL,
    "EvidenceRequired" boolean NOT NULL,
    "TenantId" uuid NOT NULL,
    "OrgId" uuid NOT NULL,
    "Version" bigint NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_ImpactMetrics" PRIMARY KEY ("Id"),
    CONSTRAINT "AK_ImpactMetrics_TenantId_Id" UNIQUE ("TenantId", "Id"),
    CONSTRAINT "FK_ImpactMetrics_Associations_TenantId" FOREIGN KEY ("TenantId") REFERENCES "Associations" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_ImpactMetrics_Orgs_TenantId_OrgId" FOREIGN KEY ("TenantId", "OrgId") REFERENCES "Orgs" ("TenantId", "Id") ON DELETE RESTRICT
);

CREATE TABLE "OperationRevisions" (
    "Id" uuid NOT NULL,
    "Kind" text NOT NULL,
    "RecordId" uuid NOT NULL,
    "Body" text NOT NULL,
    "ActorId" uuid NOT NULL,
    "Status" text NOT NULL,
    "TenantId" uuid NOT NULL,
    "OrgId" uuid NOT NULL,
    "Version" bigint NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_OperationRevisions" PRIMARY KEY ("Id"),
    CONSTRAINT "AK_OperationRevisions_TenantId_Id" UNIQUE ("TenantId", "Id"),
    CONSTRAINT "FK_OperationRevisions_Associations_TenantId" FOREIGN KEY ("TenantId") REFERENCES "Associations" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_OperationRevisions_Orgs_TenantId_OrgId" FOREIGN KEY ("TenantId", "OrgId") REFERENCES "Orgs" ("TenantId", "Id") ON DELETE RESTRICT
);

CREATE TABLE "OperationTasks" (
    "Id" uuid NOT NULL,
    "Title" text NOT NULL,
    "Description" text NOT NULL,
    "SourceType" text NOT NULL,
    "SourceId" uuid,
    "PersonId" uuid,
    "CommitteeId" uuid,
    "Priority" text NOT NULL,
    "StartsAt" timestamp with time zone,
    "DueAt" timestamp with time zone,
    "Status" text NOT NULL,
    "Blocker" text NOT NULL,
    "Evidence" text NOT NULL,
    "Dependencies" text NOT NULL,
    "CreatorId" uuid NOT NULL,
    "RemindedAt" timestamp with time zone,
    "TenantId" uuid NOT NULL,
    "OrgId" uuid NOT NULL,
    "Version" bigint NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_OperationTasks" PRIMARY KEY ("Id"),
    CONSTRAINT "AK_OperationTasks_TenantId_Id" UNIQUE ("TenantId", "Id"),
    CONSTRAINT "FK_OperationTasks_Associations_TenantId" FOREIGN KEY ("TenantId") REFERENCES "Associations" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_OperationTasks_Orgs_TenantId_OrgId" FOREIGN KEY ("TenantId", "OrgId") REFERENCES "Orgs" ("TenantId", "Id") ON DELETE RESTRICT
);

CREATE TABLE "ServiceProjects" (
    "Id" uuid NOT NULL,
    "Name" text NOT NULL,
    "Category" text NOT NULL,
    "Description" text NOT NULL,
    "Objectives" text NOT NULL,
    "ManagerId" uuid NOT NULL,
    "CommitteeId" uuid,
    "StartsAt" timestamp with time zone NOT NULL,
    "EndsAt" timestamp with time zone NOT NULL,
    "Location" text NOT NULL,
    "Status" text NOT NULL,
    "TargetBeneficiaries" integer NOT NULL,
    "PlannedVolunteers" integer NOT NULL,
    "PlannedHours" numeric NOT NULL,
    "EstimatedBudget" numeric NOT NULL,
    "Currency" text NOT NULL,
    "RequestId" uuid,
    "TenantId" uuid NOT NULL,
    "OrgId" uuid NOT NULL,
    "Version" bigint NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_ServiceProjects" PRIMARY KEY ("Id"),
    CONSTRAINT "AK_ServiceProjects_TenantId_Id" UNIQUE ("TenantId", "Id"),
    CONSTRAINT "FK_ServiceProjects_Associations_TenantId" FOREIGN KEY ("TenantId") REFERENCES "Associations" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_ServiceProjects_Orgs_TenantId_OrgId" FOREIGN KEY ("TenantId", "OrgId") REFERENCES "Orgs" ("TenantId", "Id") ON DELETE RESTRICT
);

CREATE TABLE "VolunteerProfiles" (
    "Id" uuid NOT NULL,
    "PersonId" uuid NOT NULL,
    "Skills" text NOT NULL,
    "Interests" text NOT NULL,
    "Availability" text NOT NULL,
    "Training" text NOT NULL,
    "Language" text NOT NULL,
    "TenantId" uuid NOT NULL,
    "OrgId" uuid NOT NULL,
    "Version" bigint NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_VolunteerProfiles" PRIMARY KEY ("Id"),
    CONSTRAINT "AK_VolunteerProfiles_TenantId_Id" UNIQUE ("TenantId", "Id"),
    CONSTRAINT "FK_VolunteerProfiles_Associations_TenantId" FOREIGN KEY ("TenantId") REFERENCES "Associations" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_VolunteerProfiles_Orgs_TenantId_OrgId" FOREIGN KEY ("TenantId", "OrgId") REFERENCES "Orgs" ("TenantId", "Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_VolunteerProfiles_Persons_PersonId" FOREIGN KEY ("PersonId") REFERENCES "Persons" ("Id") ON DELETE RESTRICT
);

CREATE TABLE "IssuedCertificates" (
    "Id" uuid NOT NULL,
    "TemplateId" uuid NOT NULL,
    "TemplateVersion" bigint NOT NULL,
    "PersonId" uuid NOT NULL,
    "SourceType" text NOT NULL,
    "SourceId" uuid NOT NULL,
    "Number" text NOT NULL,
    "TokenHash" text NOT NULL,
    "ProtectedToken" text NOT NULL,
    "Snapshot" text NOT NULL,
    "Layout" text NOT NULL,
    "Status" text NOT NULL,
    "IssuerId" uuid NOT NULL,
    "RequestId" uuid,
    "IssuedAt" timestamp with time zone,
    "PublicName" boolean NOT NULL,
    "RevocationReason" text NOT NULL,
    "ReplacesId" uuid,
    "TenantId" uuid NOT NULL,
    "OrgId" uuid NOT NULL,
    "Version" bigint NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_IssuedCertificates" PRIMARY KEY ("Id"),
    CONSTRAINT "AK_IssuedCertificates_TenantId_Id" UNIQUE ("TenantId", "Id"),
    CONSTRAINT "FK_IssuedCertificates_Associations_TenantId" FOREIGN KEY ("TenantId") REFERENCES "Associations" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_IssuedCertificates_CertificateTemplates_TenantId_TemplateId" FOREIGN KEY ("TenantId", "TemplateId") REFERENCES "CertificateTemplates" ("TenantId", "Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_IssuedCertificates_Orgs_TenantId_OrgId" FOREIGN KEY ("TenantId", "OrgId") REFERENCES "Orgs" ("TenantId", "Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_IssuedCertificates_Persons_PersonId" FOREIGN KEY ("PersonId") REFERENCES "Persons" ("Id") ON DELETE RESTRICT
);

CREATE TABLE "MeetingAttendances" (
    "Id" uuid NOT NULL,
    "MeetingId" uuid NOT NULL,
    "PersonId" uuid NOT NULL,
    "Status" text NOT NULL,
    "Rsvp" text NOT NULL,
    "Mode" text NOT NULL,
    "Reason" text NOT NULL,
    "ActorId" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "OrgId" uuid NOT NULL,
    "Version" bigint NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_MeetingAttendances" PRIMARY KEY ("Id"),
    CONSTRAINT "AK_MeetingAttendances_TenantId_Id" UNIQUE ("TenantId", "Id"),
    CONSTRAINT "FK_MeetingAttendances_Associations_TenantId" FOREIGN KEY ("TenantId") REFERENCES "Associations" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_MeetingAttendances_ClubMeetings_TenantId_MeetingId" FOREIGN KEY ("TenantId", "MeetingId") REFERENCES "ClubMeetings" ("TenantId", "Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_MeetingAttendances_Orgs_TenantId_OrgId" FOREIGN KEY ("TenantId", "OrgId") REFERENCES "Orgs" ("TenantId", "Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_MeetingAttendances_Persons_PersonId" FOREIGN KEY ("PersonId") REFERENCES "Persons" ("Id") ON DELETE RESTRICT
);

CREATE TABLE "MeetingMinutes" (
    "Id" uuid NOT NULL,
    "MeetingId" uuid NOT NULL,
    "Body" text NOT NULL,
    "Status" text NOT NULL,
    "PreparedBy" uuid NOT NULL,
    "RequestId" uuid,
    "Revision" integer NOT NULL,
    "TenantId" uuid NOT NULL,
    "OrgId" uuid NOT NULL,
    "Version" bigint NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_MeetingMinutes" PRIMARY KEY ("Id"),
    CONSTRAINT "AK_MeetingMinutes_TenantId_Id" UNIQUE ("TenantId", "Id"),
    CONSTRAINT "FK_MeetingMinutes_Associations_TenantId" FOREIGN KEY ("TenantId") REFERENCES "Associations" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_MeetingMinutes_ClubMeetings_TenantId_MeetingId" FOREIGN KEY ("TenantId", "MeetingId") REFERENCES "ClubMeetings" ("TenantId", "Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_MeetingMinutes_Orgs_TenantId_OrgId" FOREIGN KEY ("TenantId", "OrgId") REFERENCES "Orgs" ("TenantId", "Id") ON DELETE RESTRICT
);

CREATE TABLE "OperationComments" (
    "Id" uuid NOT NULL,
    "TaskId" uuid NOT NULL,
    "AuthorId" uuid NOT NULL,
    "Body" text NOT NULL,
    "TenantId" uuid NOT NULL,
    "OrgId" uuid NOT NULL,
    "Version" bigint NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_OperationComments" PRIMARY KEY ("Id"),
    CONSTRAINT "AK_OperationComments_TenantId_Id" UNIQUE ("TenantId", "Id"),
    CONSTRAINT "FK_OperationComments_Associations_TenantId" FOREIGN KEY ("TenantId") REFERENCES "Associations" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_OperationComments_OperationTasks_TenantId_TaskId" FOREIGN KEY ("TenantId", "TaskId") REFERENCES "OperationTasks" ("TenantId", "Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_OperationComments_Orgs_TenantId_OrgId" FOREIGN KEY ("TenantId", "OrgId") REFERENCES "Orgs" ("TenantId", "Id") ON DELETE RESTRICT
);

CREATE TABLE "ProjectActivities" (
    "Id" uuid NOT NULL,
    "ProjectId" uuid NOT NULL,
    "Title" text NOT NULL,
    "Description" text NOT NULL,
    "StartsAt" timestamp with time zone NOT NULL,
    "EndsAt" timestamp with time zone NOT NULL,
    "Status" text NOT NULL,
    "TenantId" uuid NOT NULL,
    "OrgId" uuid NOT NULL,
    "Version" bigint NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_ProjectActivities" PRIMARY KEY ("Id"),
    CONSTRAINT "AK_ProjectActivities_TenantId_Id" UNIQUE ("TenantId", "Id"),
    CONSTRAINT "FK_ProjectActivities_Associations_TenantId" FOREIGN KEY ("TenantId") REFERENCES "Associations" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_ProjectActivities_Orgs_TenantId_OrgId" FOREIGN KEY ("TenantId", "OrgId") REFERENCES "Orgs" ("TenantId", "Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_ProjectActivities_ServiceProjects_TenantId_ProjectId" FOREIGN KEY ("TenantId", "ProjectId") REFERENCES "ServiceProjects" ("TenantId", "Id") ON DELETE RESTRICT
);

CREATE TABLE "ProjectImpacts" (
    "Id" uuid NOT NULL,
    "ProjectId" uuid NOT NULL,
    "MetricId" uuid NOT NULL,
    "SourceKey" text NOT NULL,
    "Value" numeric NOT NULL,
    "Estimate" boolean NOT NULL,
    "Evidence" text NOT NULL,
    "Status" text NOT NULL,
    "SubmittedBy" uuid NOT NULL,
    "RequestId" uuid,
    "TenantId" uuid NOT NULL,
    "OrgId" uuid NOT NULL,
    "Version" bigint NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_ProjectImpacts" PRIMARY KEY ("Id"),
    CONSTRAINT "AK_ProjectImpacts_TenantId_Id" UNIQUE ("TenantId", "Id"),
    CONSTRAINT "FK_ProjectImpacts_Associations_TenantId" FOREIGN KEY ("TenantId") REFERENCES "Associations" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_ProjectImpacts_ImpactMetrics_TenantId_MetricId" FOREIGN KEY ("TenantId", "MetricId") REFERENCES "ImpactMetrics" ("TenantId", "Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_ProjectImpacts_Orgs_TenantId_OrgId" FOREIGN KEY ("TenantId", "OrgId") REFERENCES "Orgs" ("TenantId", "Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_ProjectImpacts_ServiceProjects_TenantId_ProjectId" FOREIGN KEY ("TenantId", "ProjectId") REFERENCES "ServiceProjects" ("TenantId", "Id") ON DELETE RESTRICT
);

CREATE TABLE "ProjectOrganizations" (
    "Id" uuid NOT NULL,
    "ProjectId" uuid NOT NULL,
    "Contribution" text NOT NULL,
    "EstimatedContribution" numeric NOT NULL,
    "TenantId" uuid NOT NULL,
    "OrgId" uuid NOT NULL,
    "Version" bigint NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_ProjectOrganizations" PRIMARY KEY ("Id"),
    CONSTRAINT "AK_ProjectOrganizations_TenantId_Id" UNIQUE ("TenantId", "Id"),
    CONSTRAINT "FK_ProjectOrganizations_Associations_TenantId" FOREIGN KEY ("TenantId") REFERENCES "Associations" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_ProjectOrganizations_Orgs_TenantId_OrgId" FOREIGN KEY ("TenantId", "OrgId") REFERENCES "Orgs" ("TenantId", "Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_ProjectOrganizations_ServiceProjects_TenantId_ProjectId" FOREIGN KEY ("TenantId", "ProjectId") REFERENCES "ServiceProjects" ("TenantId", "Id") ON DELETE RESTRICT
);

CREATE TABLE "VolunteerShifts" (
    "Id" uuid NOT NULL,
    "ProjectId" uuid NOT NULL,
    "ActivityId" uuid,
    "Title" text NOT NULL,
    "Location" text NOT NULL,
    "StartsAt" timestamp with time zone NOT NULL,
    "EndsAt" timestamp with time zone NOT NULL,
    "Deadline" timestamp with time zone NOT NULL,
    "SupervisorId" uuid NOT NULL,
    "Capacity" integer NOT NULL,
    "RequiredTraining" text NOT NULL,
    "ApprovalRequired" boolean NOT NULL,
    "TenantId" uuid NOT NULL,
    "OrgId" uuid NOT NULL,
    "Version" bigint NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_VolunteerShifts" PRIMARY KEY ("Id"),
    CONSTRAINT "AK_VolunteerShifts_TenantId_Id" UNIQUE ("TenantId", "Id"),
    CONSTRAINT "FK_VolunteerShifts_Associations_TenantId" FOREIGN KEY ("TenantId") REFERENCES "Associations" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_VolunteerShifts_Orgs_TenantId_OrgId" FOREIGN KEY ("TenantId", "OrgId") REFERENCES "Orgs" ("TenantId", "Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_VolunteerShifts_ProjectActivities_TenantId_ActivityId" FOREIGN KEY ("TenantId", "ActivityId") REFERENCES "ProjectActivities" ("TenantId", "Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_VolunteerShifts_ServiceProjects_TenantId_ProjectId" FOREIGN KEY ("TenantId", "ProjectId") REFERENCES "ServiceProjects" ("TenantId", "Id") ON DELETE RESTRICT
);

CREATE TABLE "VolunteerAssignments" (
    "Id" uuid NOT NULL,
    "ShiftId" uuid NOT NULL,
    "PersonId" uuid NOT NULL,
    "Status" text NOT NULL,
    "CheckIn" timestamp with time zone,
    "CheckOut" timestamp with time zone,
    "BreakMinutes" integer NOT NULL,
    "TenantId" uuid NOT NULL,
    "OrgId" uuid NOT NULL,
    "Version" bigint NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_VolunteerAssignments" PRIMARY KEY ("Id"),
    CONSTRAINT "AK_VolunteerAssignments_TenantId_Id" UNIQUE ("TenantId", "Id"),
    CONSTRAINT "FK_VolunteerAssignments_Associations_TenantId" FOREIGN KEY ("TenantId") REFERENCES "Associations" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_VolunteerAssignments_Orgs_TenantId_OrgId" FOREIGN KEY ("TenantId", "OrgId") REFERENCES "Orgs" ("TenantId", "Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_VolunteerAssignments_Persons_PersonId" FOREIGN KEY ("PersonId") REFERENCES "Persons" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_VolunteerAssignments_VolunteerShifts_TenantId_ShiftId" FOREIGN KEY ("TenantId", "ShiftId") REFERENCES "VolunteerShifts" ("TenantId", "Id") ON DELETE RESTRICT
);

CREATE TABLE "VolunteerHours" (
    "Id" uuid NOT NULL,
    "AssignmentId" uuid NOT NULL,
    "PersonId" uuid NOT NULL,
    "ProjectId" uuid NOT NULL,
    "StartsAt" timestamp with time zone NOT NULL,
    "EndsAt" timestamp with time zone NOT NULL,
    "BreakMinutes" integer NOT NULL,
    "Hours" numeric(10,2) NOT NULL,
    "Reason" text NOT NULL,
    "Status" text NOT NULL,
    "SubmittedBy" uuid NOT NULL,
    "VerifiedBy" uuid,
    "TenantId" uuid NOT NULL,
    "OrgId" uuid NOT NULL,
    "Version" bigint NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_VolunteerHours" PRIMARY KEY ("Id"),
    CONSTRAINT "AK_VolunteerHours_TenantId_Id" UNIQUE ("TenantId", "Id"),
    CONSTRAINT "FK_VolunteerHours_Associations_TenantId" FOREIGN KEY ("TenantId") REFERENCES "Associations" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_VolunteerHours_Orgs_TenantId_OrgId" FOREIGN KEY ("TenantId", "OrgId") REFERENCES "Orgs" ("TenantId", "Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_VolunteerHours_Persons_PersonId" FOREIGN KEY ("PersonId") REFERENCES "Persons" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_VolunteerHours_ServiceProjects_TenantId_ProjectId" FOREIGN KEY ("TenantId", "ProjectId") REFERENCES "ServiceProjects" ("TenantId", "Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_VolunteerHours_VolunteerAssignments_TenantId_AssignmentId" FOREIGN KEY ("TenantId", "AssignmentId") REFERENCES "VolunteerAssignments" ("TenantId", "Id") ON DELETE RESTRICT
);

INSERT INTO "Permissions" ("Id")
VALUES ('operations.certificates');
INSERT INTO "Permissions" ("Id")
VALUES ('operations.documents');
INSERT INTO "Permissions" ("Id")
VALUES ('operations.manage');
INSERT INTO "Permissions" ("Id")
VALUES ('operations.verify');
INSERT INTO "Permissions" ("Id")
VALUES ('operations.view');

CREATE INDEX "IX_CertificateTemplates_TenantId_OrgId_UpdatedAt" ON "CertificateTemplates" ("TenantId", "OrgId", "UpdatedAt");

CREATE UNIQUE INDEX "IX_ClubMeetings_SeriesId_StartsAt" ON "ClubMeetings" ("SeriesId", "StartsAt");

CREATE INDEX "IX_ClubMeetings_TenantId_CommitteeId" ON "ClubMeetings" ("TenantId", "CommitteeId");

CREATE INDEX "IX_ClubMeetings_TenantId_EventId" ON "ClubMeetings" ("TenantId", "EventId");

CREATE INDEX "IX_ClubMeetings_TenantId_OrgId_StartsAt" ON "ClubMeetings" ("TenantId", "OrgId", "StartsAt");

CREATE INDEX "IX_ClubMeetings_TenantId_OrgId_UpdatedAt" ON "ClubMeetings" ("TenantId", "OrgId", "UpdatedAt");

CREATE INDEX "IX_ClubMeetings_TenantId_SessionId" ON "ClubMeetings" ("TenantId", "SessionId");

CREATE UNIQUE INDEX "IX_DocumentVersions_DocumentId_Number" ON "DocumentVersions" ("DocumentId", "Number");

CREATE INDEX "IX_DocumentVersions_TenantId_DocumentId" ON "DocumentVersions" ("TenantId", "DocumentId");

CREATE INDEX "IX_DocumentVersions_TenantId_OrgId_UpdatedAt" ON "DocumentVersions" ("TenantId", "OrgId", "UpdatedAt");

CREATE INDEX "IX_ImpactMetrics_TenantId_OrgId_UpdatedAt" ON "ImpactMetrics" ("TenantId", "OrgId", "UpdatedAt");

CREATE INDEX "IX_IssuedCertificates_PersonId" ON "IssuedCertificates" ("PersonId");

CREATE UNIQUE INDEX "IX_IssuedCertificates_TenantId_Number" ON "IssuedCertificates" ("TenantId", "Number");

CREATE INDEX "IX_IssuedCertificates_TenantId_OrgId_UpdatedAt" ON "IssuedCertificates" ("TenantId", "OrgId", "UpdatedAt");

CREATE INDEX "IX_IssuedCertificates_TenantId_TemplateId" ON "IssuedCertificates" ("TenantId", "TemplateId");

CREATE UNIQUE INDEX "IX_IssuedCertificates_TokenHash" ON "IssuedCertificates" ("TokenHash");

CREATE UNIQUE INDEX "IX_MeetingAttendances_MeetingId_PersonId" ON "MeetingAttendances" ("MeetingId", "PersonId");

CREATE INDEX "IX_MeetingAttendances_PersonId" ON "MeetingAttendances" ("PersonId");

CREATE INDEX "IX_MeetingAttendances_TenantId_MeetingId" ON "MeetingAttendances" ("TenantId", "MeetingId");

CREATE INDEX "IX_MeetingAttendances_TenantId_OrgId_UpdatedAt" ON "MeetingAttendances" ("TenantId", "OrgId", "UpdatedAt");

CREATE UNIQUE INDEX "IX_MeetingMinutes_MeetingId_Revision" ON "MeetingMinutes" ("MeetingId", "Revision");

CREATE INDEX "IX_MeetingMinutes_TenantId_MeetingId" ON "MeetingMinutes" ("TenantId", "MeetingId");

CREATE INDEX "IX_MeetingMinutes_TenantId_OrgId_UpdatedAt" ON "MeetingMinutes" ("TenantId", "OrgId", "UpdatedAt");

CREATE INDEX "IX_OperationComments_TenantId_OrgId_UpdatedAt" ON "OperationComments" ("TenantId", "OrgId", "UpdatedAt");

CREATE INDEX "IX_OperationComments_TenantId_TaskId" ON "OperationComments" ("TenantId", "TaskId");

CREATE INDEX "IX_OperationRevisions_TenantId_OrgId_UpdatedAt" ON "OperationRevisions" ("TenantId", "OrgId", "UpdatedAt");

CREATE INDEX "IX_OperationTasks_TenantId_OrgId_UpdatedAt" ON "OperationTasks" ("TenantId", "OrgId", "UpdatedAt");

CREATE INDEX "IX_OperationTasks_TenantId_PersonId_Status_DueAt" ON "OperationTasks" ("TenantId", "PersonId", "Status", "DueAt");

CREATE INDEX "IX_ProjectActivities_TenantId_OrgId_UpdatedAt" ON "ProjectActivities" ("TenantId", "OrgId", "UpdatedAt");

CREATE INDEX "IX_ProjectActivities_TenantId_ProjectId" ON "ProjectActivities" ("TenantId", "ProjectId");

CREATE UNIQUE INDEX "IX_ProjectImpacts_ProjectId_MetricId_SourceKey" ON "ProjectImpacts" ("ProjectId", "MetricId", "SourceKey");

CREATE INDEX "IX_ProjectImpacts_TenantId_MetricId" ON "ProjectImpacts" ("TenantId", "MetricId");

CREATE INDEX "IX_ProjectImpacts_TenantId_OrgId_UpdatedAt" ON "ProjectImpacts" ("TenantId", "OrgId", "UpdatedAt");

CREATE INDEX "IX_ProjectImpacts_TenantId_ProjectId" ON "ProjectImpacts" ("TenantId", "ProjectId");

CREATE UNIQUE INDEX "IX_ProjectOrganizations_ProjectId_OrgId" ON "ProjectOrganizations" ("ProjectId", "OrgId");

CREATE INDEX "IX_ProjectOrganizations_TenantId_OrgId_UpdatedAt" ON "ProjectOrganizations" ("TenantId", "OrgId", "UpdatedAt");

CREATE INDEX "IX_ProjectOrganizations_TenantId_ProjectId" ON "ProjectOrganizations" ("TenantId", "ProjectId");

CREATE INDEX "IX_ServiceProjects_TenantId_OrgId_UpdatedAt" ON "ServiceProjects" ("TenantId", "OrgId", "UpdatedAt");

CREATE INDEX "IX_VolunteerAssignments_PersonId" ON "VolunteerAssignments" ("PersonId");

CREATE UNIQUE INDEX "IX_VolunteerAssignments_ShiftId_PersonId" ON "VolunteerAssignments" ("ShiftId", "PersonId");

CREATE INDEX "IX_VolunteerAssignments_TenantId_OrgId_UpdatedAt" ON "VolunteerAssignments" ("TenantId", "OrgId", "UpdatedAt");

CREATE INDEX "IX_VolunteerAssignments_TenantId_ShiftId" ON "VolunteerAssignments" ("TenantId", "ShiftId");

CREATE INDEX "IX_VolunteerHours_PersonId" ON "VolunteerHours" ("PersonId");

CREATE INDEX "IX_VolunteerHours_TenantId_AssignmentId" ON "VolunteerHours" ("TenantId", "AssignmentId");

CREATE INDEX "IX_VolunteerHours_TenantId_OrgId_UpdatedAt" ON "VolunteerHours" ("TenantId", "OrgId", "UpdatedAt");

CREATE INDEX "IX_VolunteerHours_TenantId_PersonId_StartsAt_EndsAt" ON "VolunteerHours" ("TenantId", "PersonId", "StartsAt", "EndsAt");

CREATE INDEX "IX_VolunteerHours_TenantId_ProjectId" ON "VolunteerHours" ("TenantId", "ProjectId");

CREATE INDEX "IX_VolunteerProfiles_PersonId" ON "VolunteerProfiles" ("PersonId");

CREATE INDEX "IX_VolunteerProfiles_TenantId_OrgId_UpdatedAt" ON "VolunteerProfiles" ("TenantId", "OrgId", "UpdatedAt");

CREATE UNIQUE INDEX "IX_VolunteerProfiles_TenantId_PersonId" ON "VolunteerProfiles" ("TenantId", "PersonId");

CREATE INDEX "IX_VolunteerShifts_TenantId_ActivityId" ON "VolunteerShifts" ("TenantId", "ActivityId");

CREATE INDEX "IX_VolunteerShifts_TenantId_OrgId_UpdatedAt" ON "VolunteerShifts" ("TenantId", "OrgId", "UpdatedAt");

CREATE INDEX "IX_VolunteerShifts_TenantId_ProjectId" ON "VolunteerShifts" ("TenantId", "ProjectId");

UPDATE "GovernanceDocuments" SET "CurrentVersion"=1,"Visibility"='Organization',"SharedUsers"='[]',"Folder"='General' WHERE "ObjectKey"='';
INSERT INTO "RolePermissions"("Id","RoleId","PermissionId")
SELECT gen_random_uuid(),r."Id",p."Id" FROM "Roles" r CROSS JOIN "Permissions" p
WHERE p."Id" LIKE 'operations.%' AND (r."Name" IN ('Association Super Admin','Club President','Club Secretary','Club Manager') OR (p."Id"='operations.view' AND r."Name" IN ('Club Member','Applicant')))
AND NOT EXISTS(SELECT 1 FROM "RolePermissions" x WHERE x."RoleId"=r."Id" AND x."PermissionId"=p."Id");
ALTER TABLE "ClubMeetings" ADD CONSTRAINT "CK_MeetingDates" CHECK ("EndsAt">"StartsAt" AND "QuorumMinimum">=0 AND "QuorumPercent" BETWEEN 0 AND 100);
ALTER TABLE "VolunteerHours" ADD CONSTRAINT "CK_HourInterval" CHECK ("EndsAt">"StartsAt" AND "Hours">=0 AND "BreakMinutes">=0);
ALTER TABLE "VolunteerShifts" ADD CONSTRAINT "CK_ShiftCapacity" CHECK ("Capacity">0 AND "EndsAt">"StartsAt");
ALTER TABLE "ProjectImpacts" ADD CONSTRAINT "CK_ImpactValue" CHECK ("Value">=0);
CREATE FUNCTION guard_operation_history() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RAISE EXCEPTION 'Operational revisions are immutable'; END $$;
CREATE TRIGGER operation_history_immutable BEFORE UPDATE OR DELETE ON "OperationRevisions" FOR EACH ROW EXECUTE FUNCTION guard_operation_history();
CREATE FUNCTION guard_issued_certificate() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN
  IF OLD."Snapshot" IS DISTINCT FROM NEW."Snapshot" OR OLD."Layout" IS DISTINCT FROM NEW."Layout" OR OLD."PersonId" IS DISTINCT FROM NEW."PersonId" OR OLD."Number" IS DISTINCT FROM NEW."Number" OR OLD."TokenHash" IS DISTINCT FROM NEW."TokenHash" THEN RAISE EXCEPTION 'Certificate identity and content are immutable'; END IF; RETURN NEW; END $$;
CREATE TRIGGER certificate_content_immutable BEFORE UPDATE ON "IssuedCertificates" FOR EACH ROW EXECUTE FUNCTION guard_issued_certificate();
CREATE FUNCTION guard_minutes_body() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN IF OLD."Status" IN ('Review','Published') AND OLD."Body" IS DISTINCT FROM NEW."Body" THEN RAISE EXCEPTION 'Reviewed minutes require a new revision'; END IF; RETURN NEW; END $$;
CREATE TRIGGER minutes_body_immutable BEFORE UPDATE ON "MeetingMinutes" FOR EACH ROW EXECUTE FUNCTION guard_minutes_body();

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261111014651_ClubOperations', '10.0.0');

COMMIT;

START TRANSACTION;
ALTER TABLE "GovernanceDocuments" DROP CONSTRAINT "CK_Governance_Document";
ALTER TABLE "GovernanceDocuments" ADD CONSTRAINT "CK_Governance_Document" CHECK(octet_length("Content")<=5242880 AND (octet_length("Content")>0 OR "ObjectKey" ~ '^[0-9a-f]{32}$' OR "Status"='Expired') AND "ContentType" IN ('application/pdf','image/png','image/jpeg'));

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261111020039_OperationsObjectStorageGuard', '10.0.0');

COMMIT;

START TRANSACTION;
DROP INDEX "IX_VolunteerAssignments_TenantId_ShiftId";

DROP INDEX "IX_IssuedCertificates_TenantId_TemplateId";

CREATE INDEX "IX_VolunteerHours_TenantId_OrgId_Status_StartsAt" ON "VolunteerHours" ("TenantId", "OrgId", "Status", "StartsAt");

CREATE INDEX "IX_VolunteerAssignments_TenantId_ShiftId_Status" ON "VolunteerAssignments" ("TenantId", "ShiftId", "Status");

CREATE INDEX "IX_ServiceProjects_TenantId_OrgId_Status_StartsAt" ON "ServiceProjects" ("TenantId", "OrgId", "Status", "StartsAt");

CREATE INDEX "IX_OperationTasks_TenantId_OrgId_Status_DueAt" ON "OperationTasks" ("TenantId", "OrgId", "Status", "DueAt");

CREATE INDEX "IX_OperationRevisions_TenantId_RecordId_UpdatedAt" ON "OperationRevisions" ("TenantId", "RecordId", "UpdatedAt");

CREATE INDEX "IX_IssuedCertificates_TenantId_TemplateId_PersonId_SourceId_So~" ON "IssuedCertificates" ("TenantId", "TemplateId", "PersonId", "SourceId", "SourceType", "TemplateVersion");

CREATE INDEX "IX_GovernanceDocuments_TenantId_OrgId_Id" ON "GovernanceDocuments" ("TenantId", "OrgId", "Id");

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261111021932_OperationsIndexes', '10.0.0');

COMMIT;

START TRANSACTION;
CREATE TABLE "FinanceEntitys" (
    "Id" uuid NOT NULL,
    "Name" text NOT NULL,
    "Country" text NOT NULL,
    "Currency" text NOT NULL,
    "Decimals" integer NOT NULL,
    "FiscalMonth" integer NOT NULL,
    "TaxRegistration" text NOT NULL,
    "Address" text NOT NULL,
    "InvoicePrefix" text NOT NULL,
    "ReceiptPrefix" text NOT NULL,
    "InvoiceSequence" bigint NOT NULL,
    "ReceiptSequence" bigint NOT NULL,
    "AccountingMethod" text NOT NULL,
    "Active" boolean NOT NULL,
    "GatewayKey" text NOT NULL,
    "CollectionAuthority" text NOT NULL,
    "TenantId" uuid NOT NULL,
    "OrgId" uuid NOT NULL,
    "EntityId" uuid NOT NULL,
    "Version" bigint NOT NULL,
    CONSTRAINT "PK_FinanceEntitys" PRIMARY KEY ("Id"),
    CONSTRAINT "AK_FinanceEntitys_TenantId_EntityId_Id" UNIQUE ("TenantId", "EntityId", "Id"),
    CONSTRAINT "AK_FinanceEntitys_TenantId_Id" UNIQUE ("TenantId", "Id"),
    CONSTRAINT "FK_FinanceEntitys_Associations_TenantId" FOREIGN KEY ("TenantId") REFERENCES "Associations" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_FinanceEntitys_Orgs_TenantId_OrgId" FOREIGN KEY ("TenantId", "OrgId") REFERENCES "Orgs" ("TenantId", "Id") ON DELETE RESTRICT
);

CREATE TABLE "DuesPlans" (
    "Id" uuid NOT NULL,
    "CreatorId" uuid NOT NULL,
    "Name" text NOT NULL,
    "CategoryId" uuid,
    "BillingOrgId" uuid NOT NULL,
    "Purpose" text NOT NULL,
    "EffectiveFrom" date NOT NULL,
    "EffectiveTo" date NOT NULL,
    "Months" integer NOT NULL,
    "GraceDays" integer NOT NULL,
    "Prorate" boolean NOT NULL,
    "Once" boolean NOT NULL,
    "AutoBill" boolean NOT NULL,
    "NextBillOn" date NOT NULL,
    "Components" text NOT NULL,
    "TenantId" uuid NOT NULL,
    "OrgId" uuid NOT NULL,
    "EntityId" uuid NOT NULL,
    "Version" bigint NOT NULL,
    CONSTRAINT "PK_DuesPlans" PRIMARY KEY ("Id"),
    CONSTRAINT "AK_DuesPlans_TenantId_EntityId_Id" UNIQUE ("TenantId", "EntityId", "Id"),
    CONSTRAINT "AK_DuesPlans_TenantId_Id" UNIQUE ("TenantId", "Id"),
    CONSTRAINT "FK_DuesPlans_Associations_TenantId" FOREIGN KEY ("TenantId") REFERENCES "Associations" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_DuesPlans_FinanceEntitys_TenantId_EntityId" FOREIGN KEY ("TenantId", "EntityId") REFERENCES "FinanceEntitys" ("TenantId", "Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_DuesPlans_MembershipCategories_TenantId_CategoryId" FOREIGN KEY ("TenantId", "CategoryId") REFERENCES "MembershipCategories" ("TenantId", "Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_DuesPlans_Orgs_TenantId_BillingOrgId" FOREIGN KEY ("TenantId", "BillingOrgId") REFERENCES "Orgs" ("TenantId", "Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_DuesPlans_Orgs_TenantId_OrgId" FOREIGN KEY ("TenantId", "OrgId") REFERENCES "Orgs" ("TenantId", "Id") ON DELETE RESTRICT
);

CREATE TABLE "FinanceAccounts" (
    "Id" uuid NOT NULL,
    "Code" text NOT NULL,
    "Name" text NOT NULL,
    "Kind" text NOT NULL,
    "Active" boolean NOT NULL,
    "TenantId" uuid NOT NULL,
    "OrgId" uuid NOT NULL,
    "EntityId" uuid NOT NULL,
    "Version" bigint NOT NULL,
    CONSTRAINT "PK_FinanceAccounts" PRIMARY KEY ("Id"),
    CONSTRAINT "AK_FinanceAccounts_TenantId_EntityId_Id" UNIQUE ("TenantId", "EntityId", "Id"),
    CONSTRAINT "AK_FinanceAccounts_TenantId_Id" UNIQUE ("TenantId", "Id"),
    CONSTRAINT "FK_FinanceAccounts_Associations_TenantId" FOREIGN KEY ("TenantId") REFERENCES "Associations" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_FinanceAccounts_FinanceEntitys_TenantId_EntityId" FOREIGN KEY ("TenantId", "EntityId") REFERENCES "FinanceEntitys" ("TenantId", "Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_FinanceAccounts_Orgs_TenantId_OrgId" FOREIGN KEY ("TenantId", "OrgId") REFERENCES "Orgs" ("TenantId", "Id") ON DELETE RESTRICT
);

CREATE TABLE "FinanceAudits" (
    "Id" uuid NOT NULL,
    "ActorId" uuid NOT NULL,
    "Action" text NOT NULL,
    "Detail" text NOT NULL,
    "At" timestamp with time zone NOT NULL,
    "TenantId" uuid NOT NULL,
    "OrgId" uuid NOT NULL,
    "EntityId" uuid NOT NULL,
    "Version" bigint NOT NULL,
    CONSTRAINT "PK_FinanceAudits" PRIMARY KEY ("Id"),
    CONSTRAINT "AK_FinanceAudits_TenantId_EntityId_Id" UNIQUE ("TenantId", "EntityId", "Id"),
    CONSTRAINT "AK_FinanceAudits_TenantId_Id" UNIQUE ("TenantId", "Id"),
    CONSTRAINT "FK_FinanceAudits_Associations_TenantId" FOREIGN KEY ("TenantId") REFERENCES "Associations" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_FinanceAudits_FinanceEntitys_TenantId_EntityId" FOREIGN KEY ("TenantId", "EntityId") REFERENCES "FinanceEntitys" ("TenantId", "Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_FinanceAudits_Orgs_TenantId_OrgId" FOREIGN KEY ("TenantId", "OrgId") REFERENCES "Orgs" ("TenantId", "Id") ON DELETE RESTRICT
);

CREATE TABLE "FinanceBanks" (
    "Id" uuid NOT NULL,
    "Name" text NOT NULL,
    "MaskedNumber" text NOT NULL,
    "ProtectedDetails" text NOT NULL,
    "AccountCode" text NOT NULL,
    "TenantId" uuid NOT NULL,
    "OrgId" uuid NOT NULL,
    "EntityId" uuid NOT NULL,
    "Version" bigint NOT NULL,
    CONSTRAINT "PK_FinanceBanks" PRIMARY KEY ("Id"),
    CONSTRAINT "AK_FinanceBanks_TenantId_EntityId_Id" UNIQUE ("TenantId", "EntityId", "Id"),
    CONSTRAINT "AK_FinanceBanks_TenantId_Id" UNIQUE ("TenantId", "Id"),
    CONSTRAINT "FK_FinanceBanks_Associations_TenantId" FOREIGN KEY ("TenantId") REFERENCES "Associations" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_FinanceBanks_FinanceEntitys_TenantId_EntityId" FOREIGN KEY ("TenantId", "EntityId") REFERENCES "FinanceEntitys" ("TenantId", "Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_FinanceBanks_Orgs_TenantId_OrgId" FOREIGN KEY ("TenantId", "OrgId") REFERENCES "Orgs" ("TenantId", "Id") ON DELETE RESTRICT
);

CREATE TABLE "FinanceBudgets" (
    "Id" uuid NOT NULL,
    "Name" text NOT NULL,
    "Kind" text NOT NULL,
    "EventId" uuid,
    "ProjectId" uuid,
    "Revision" integer NOT NULL,
    "ReplacesId" uuid,
    "StartsOn" date NOT NULL,
    "EndsOn" date NOT NULL,
    "Lines" text NOT NULL,
    "Amount" numeric(24,8) NOT NULL,
    "Status" text NOT NULL,
    "CreatorId" uuid NOT NULL,
    "RequestId" uuid,
    "TenantId" uuid NOT NULL,
    "OrgId" uuid NOT NULL,
    "EntityId" uuid NOT NULL,
    "Version" bigint NOT NULL,
    CONSTRAINT "PK_FinanceBudgets" PRIMARY KEY ("Id"),
    CONSTRAINT "AK_FinanceBudgets_TenantId_EntityId_Id" UNIQUE ("TenantId", "EntityId", "Id"),
    CONSTRAINT "AK_FinanceBudgets_TenantId_Id" UNIQUE ("TenantId", "Id"),
    CONSTRAINT "FK_FinanceBudgets_Associations_TenantId" FOREIGN KEY ("TenantId") REFERENCES "Associations" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_FinanceBudgets_Events_TenantId_EventId" FOREIGN KEY ("TenantId", "EventId") REFERENCES "Events" ("TenantId", "Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_FinanceBudgets_FinanceBudgets_TenantId_EntityId_ReplacesId" FOREIGN KEY ("TenantId", "EntityId", "ReplacesId") REFERENCES "FinanceBudgets" ("TenantId", "EntityId", "Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_FinanceBudgets_FinanceEntitys_TenantId_EntityId" FOREIGN KEY ("TenantId", "EntityId") REFERENCES "FinanceEntitys" ("TenantId", "Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_FinanceBudgets_GovernanceRequests_TenantId_RequestId" FOREIGN KEY ("TenantId", "RequestId") REFERENCES "GovernanceRequests" ("TenantId", "Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_FinanceBudgets_Orgs_TenantId_OrgId" FOREIGN KEY ("TenantId", "OrgId") REFERENCES "Orgs" ("TenantId", "Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_FinanceBudgets_ServiceProjects_TenantId_ProjectId" FOREIGN KEY ("TenantId", "ProjectId") REFERENCES "ServiceProjects" ("TenantId", "Id") ON DELETE RESTRICT
);

CREATE TABLE "FinanceEventPrices" (
    "Id" uuid NOT NULL,
    "EventId" uuid NOT NULL,
    "Category" text NOT NULL,
    "Amount" numeric(24,8) NOT NULL,
    "TenantId" uuid NOT NULL,
    "OrgId" uuid NOT NULL,
    "EntityId" uuid NOT NULL,
    "Version" bigint NOT NULL,
    CONSTRAINT "PK_FinanceEventPrices" PRIMARY KEY ("Id"),
    CONSTRAINT "AK_FinanceEventPrices_TenantId_Id" UNIQUE ("TenantId", "Id"),
    CONSTRAINT "FK_FinanceEventPrices_Associations_TenantId" FOREIGN KEY ("TenantId") REFERENCES "Associations" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_FinanceEventPrices_Events_TenantId_EventId" FOREIGN KEY ("TenantId", "EventId") REFERENCES "Events" ("TenantId", "Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_FinanceEventPrices_FinanceEntitys_TenantId_EntityId" FOREIGN KEY ("TenantId", "EntityId") REFERENCES "FinanceEntitys" ("TenantId", "Id") ON DELETE RESTRICT
);

CREATE TABLE "FinanceExchangeRates" (
    "Id" uuid NOT NULL,
    "Currency" text NOT NULL,
    "Rate" numeric(24,8) NOT NULL,
    "Date" date NOT NULL,
    "Source" text NOT NULL,
    "TenantId" uuid NOT NULL,
    "OrgId" uuid NOT NULL,
    "EntityId" uuid NOT NULL,
    "Version" bigint NOT NULL,
    CONSTRAINT "PK_FinanceExchangeRates" PRIMARY KEY ("Id"),
    CONSTRAINT "AK_FinanceExchangeRates_TenantId_EntityId_Id" UNIQUE ("TenantId", "EntityId", "Id"),
    CONSTRAINT "AK_FinanceExchangeRates_TenantId_Id" UNIQUE ("TenantId", "Id"),
    CONSTRAINT "FK_FinanceExchangeRates_Associations_TenantId" FOREIGN KEY ("TenantId") REFERENCES "Associations" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_FinanceExchangeRates_FinanceEntitys_TenantId_EntityId" FOREIGN KEY ("TenantId", "EntityId") REFERENCES "FinanceEntitys" ("TenantId", "Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_FinanceExchangeRates_Orgs_TenantId_OrgId" FOREIGN KEY ("TenantId", "OrgId") REFERENCES "Orgs" ("TenantId", "Id") ON DELETE RESTRICT
);

CREATE TABLE "FinanceGrants" (
    "Id" uuid NOT NULL,
    "UserId" uuid NOT NULL,
    "Role" text NOT NULL,
    "TenantId" uuid NOT NULL,
    "OrgId" uuid NOT NULL,
    "EntityId" uuid NOT NULL,
    "Version" bigint NOT NULL,
    CONSTRAINT "PK_FinanceGrants" PRIMARY KEY ("Id"),
    CONSTRAINT "AK_FinanceGrants_TenantId_EntityId_Id" UNIQUE ("TenantId", "EntityId", "Id"),
    CONSTRAINT "AK_FinanceGrants_TenantId_Id" UNIQUE ("TenantId", "Id"),
    CONSTRAINT "FK_FinanceGrants_Associations_TenantId" FOREIGN KEY ("TenantId") REFERENCES "Associations" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_FinanceGrants_FinanceEntitys_TenantId_EntityId" FOREIGN KEY ("TenantId", "EntityId") REFERENCES "FinanceEntitys" ("TenantId", "Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_FinanceGrants_Orgs_TenantId_OrgId" FOREIGN KEY ("TenantId", "OrgId") REFERENCES "Orgs" ("TenantId", "Id") ON DELETE RESTRICT
);

CREATE TABLE "FinanceOrganizations" (
    "Id" uuid NOT NULL,
    "ServedOrgId" uuid NOT NULL,
    "Authority" text NOT NULL,
    "TenantId" uuid NOT NULL,
    "OrgId" uuid NOT NULL,
    "EntityId" uuid NOT NULL,
    "Version" bigint NOT NULL,
    CONSTRAINT "PK_FinanceOrganizations" PRIMARY KEY ("Id"),
    CONSTRAINT "AK_FinanceOrganizations_TenantId_EntityId_Id" UNIQUE ("TenantId", "EntityId", "Id"),
    CONSTRAINT "AK_FinanceOrganizations_TenantId_Id" UNIQUE ("TenantId", "Id"),
    CONSTRAINT "FK_FinanceOrganizations_Associations_TenantId" FOREIGN KEY ("TenantId") REFERENCES "Associations" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_FinanceOrganizations_FinanceEntitys_TenantId_EntityId" FOREIGN KEY ("TenantId", "EntityId") REFERENCES "FinanceEntitys" ("TenantId", "Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_FinanceOrganizations_Orgs_TenantId_OrgId" FOREIGN KEY ("TenantId", "OrgId") REFERENCES "Orgs" ("TenantId", "Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_FinanceOrganizations_Orgs_TenantId_ServedOrgId" FOREIGN KEY ("TenantId", "ServedOrgId") REFERENCES "Orgs" ("TenantId", "Id") ON DELETE RESTRICT
);

CREATE TABLE "FinancePeriods" (
    "Id" uuid NOT NULL,
    "Name" text NOT NULL,
    "StartsOn" date NOT NULL,
    "EndsOn" date NOT NULL,
    "Closed" boolean NOT NULL,
    "TenantId" uuid NOT NULL,
    "OrgId" uuid NOT NULL,
    "EntityId" uuid NOT NULL,
    "Version" bigint NOT NULL,
    CONSTRAINT "PK_FinancePeriods" PRIMARY KEY ("Id"),
    CONSTRAINT "AK_FinancePeriods_TenantId_EntityId_Id" UNIQUE ("TenantId", "EntityId", "Id"),
    CONSTRAINT "AK_FinancePeriods_TenantId_Id" UNIQUE ("TenantId", "Id"),
    CONSTRAINT "FK_FinancePeriods_Associations_TenantId" FOREIGN KEY ("TenantId") REFERENCES "Associations" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_FinancePeriods_FinanceEntitys_TenantId_EntityId" FOREIGN KEY ("TenantId", "EntityId") REFERENCES "FinanceEntitys" ("TenantId", "Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_FinancePeriods_Orgs_TenantId_OrgId" FOREIGN KEY ("TenantId", "OrgId") REFERENCES "Orgs" ("TenantId", "Id") ON DELETE RESTRICT
);

CREATE TABLE "FinancePledges" (
    "Id" uuid NOT NULL,
    "PersonId" uuid,
    "Contributor" text NOT NULL,
    "Kind" text NOT NULL,
    "Description" text NOT NULL,
    "ProjectId" uuid,
    "EventId" uuid,
    "Amount" numeric(24,8) NOT NULL,
    "Status" text NOT NULL,
    "InvoiceId" uuid,
    "CreatorId" uuid NOT NULL,
    "Date" date NOT NULL,
    "TenantId" uuid NOT NULL,
    "OrgId" uuid NOT NULL,
    "EntityId" uuid NOT NULL,
    "Version" bigint NOT NULL,
    CONSTRAINT "PK_FinancePledges" PRIMARY KEY ("Id"),
    CONSTRAINT "AK_FinancePledges_TenantId_EntityId_Id" UNIQUE ("TenantId", "EntityId", "Id"),
    CONSTRAINT "AK_FinancePledges_TenantId_Id" UNIQUE ("TenantId", "Id"),
    CONSTRAINT "FK_FinancePledges_Associations_TenantId" FOREIGN KEY ("TenantId") REFERENCES "Associations" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_FinancePledges_Events_TenantId_EventId" FOREIGN KEY ("TenantId", "EventId") REFERENCES "Events" ("TenantId", "Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_FinancePledges_FinanceEntitys_TenantId_EntityId" FOREIGN KEY ("TenantId", "EntityId") REFERENCES "FinanceEntitys" ("TenantId", "Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_FinancePledges_Orgs_TenantId_OrgId" FOREIGN KEY ("TenantId", "OrgId") REFERENCES "Orgs" ("TenantId", "Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_FinancePledges_Persons_PersonId" FOREIGN KEY ("PersonId") REFERENCES "Persons" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_FinancePledges_ServiceProjects_TenantId_ProjectId" FOREIGN KEY ("TenantId", "ProjectId") REFERENCES "ServiceProjects" ("TenantId", "Id") ON DELETE RESTRICT
);

CREATE TABLE "FinanceWebhooks" (
    "Id" uuid NOT NULL,
    "ProviderEventId" text NOT NULL,
    "PayloadHash" text NOT NULL,
    "Type" text NOT NULL,
    "ReceivedAt" timestamp with time zone NOT NULL,
    "TenantId" uuid NOT NULL,
    "OrgId" uuid NOT NULL,
    "EntityId" uuid NOT NULL,
    "Version" bigint NOT NULL,
    CONSTRAINT "PK_FinanceWebhooks" PRIMARY KEY ("Id"),
    CONSTRAINT "AK_FinanceWebhooks_TenantId_EntityId_Id" UNIQUE ("TenantId", "EntityId", "Id"),
    CONSTRAINT "AK_FinanceWebhooks_TenantId_Id" UNIQUE ("TenantId", "Id"),
    CONSTRAINT "FK_FinanceWebhooks_Associations_TenantId" FOREIGN KEY ("TenantId") REFERENCES "Associations" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_FinanceWebhooks_FinanceEntitys_TenantId_EntityId" FOREIGN KEY ("TenantId", "EntityId") REFERENCES "FinanceEntitys" ("TenantId", "Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_FinanceWebhooks_Orgs_TenantId_OrgId" FOREIGN KEY ("TenantId", "OrgId") REFERENCES "Orgs" ("TenantId", "Id") ON DELETE RESTRICT
);

CREATE TABLE "FinanceJobs" (
    "Id" uuid NOT NULL,
    "Kind" text NOT NULL,
    "PlanId" uuid NOT NULL,
    "PeriodStart" date NOT NULL,
    "PeriodEnd" date NOT NULL,
    "AfterMembershipId" uuid,
    "Status" text NOT NULL,
    "Processed" integer NOT NULL,
    "Attempts" integer NOT NULL,
    "Error" text NOT NULL,
    "CreatorId" uuid NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "TenantId" uuid NOT NULL,
    "OrgId" uuid NOT NULL,
    "EntityId" uuid NOT NULL,
    "Version" bigint NOT NULL,
    CONSTRAINT "PK_FinanceJobs" PRIMARY KEY ("Id"),
    CONSTRAINT "AK_FinanceJobs_TenantId_EntityId_Id" UNIQUE ("TenantId", "EntityId", "Id"),
    CONSTRAINT "AK_FinanceJobs_TenantId_Id" UNIQUE ("TenantId", "Id"),
    CONSTRAINT "FK_FinanceJobs_Associations_TenantId" FOREIGN KEY ("TenantId") REFERENCES "Associations" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_FinanceJobs_DuesPlans_TenantId_EntityId_PlanId" FOREIGN KEY ("TenantId", "EntityId", "PlanId") REFERENCES "DuesPlans" ("TenantId", "EntityId", "Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_FinanceJobs_FinanceEntitys_TenantId_EntityId" FOREIGN KEY ("TenantId", "EntityId") REFERENCES "FinanceEntitys" ("TenantId", "Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_FinanceJobs_Orgs_TenantId_OrgId" FOREIGN KEY ("TenantId", "OrgId") REFERENCES "Orgs" ("TenantId", "Id") ON DELETE RESTRICT
);

CREATE TABLE "FinancePayments" (
    "Id" uuid NOT NULL,
    "PersonId" uuid,
    "Payer" text NOT NULL,
    "Reference" text NOT NULL,
    "Method" text NOT NULL,
    "Status" text NOT NULL,
    "SettlementStatus" text NOT NULL,
    "Currency" text NOT NULL,
    "Amount" numeric(24,8) NOT NULL,
    "Allocated" numeric(24,8) NOT NULL,
    "Refunded" numeric(24,8) NOT NULL,
    "Fee" numeric(24,8) NOT NULL,
    "Date" date NOT NULL,
    "RecorderId" uuid NOT NULL,
    "VerifierId" uuid,
    "ReceiptNumber" text NOT NULL,
    "Evidence" text NOT NULL,
    "IdempotencyKey" text NOT NULL,
    "BankAccountId" uuid,
    "SettledOn" date,
    "TenantId" uuid NOT NULL,
    "OrgId" uuid NOT NULL,
    "EntityId" uuid NOT NULL,
    "Version" bigint NOT NULL,
    CONSTRAINT "PK_FinancePayments" PRIMARY KEY ("Id"),
    CONSTRAINT "AK_FinancePayments_TenantId_EntityId_Id" UNIQUE ("TenantId", "EntityId", "Id"),
    CONSTRAINT "AK_FinancePayments_TenantId_Id" UNIQUE ("TenantId", "Id"),
    CONSTRAINT "FK_FinancePayments_Associations_TenantId" FOREIGN KEY ("TenantId") REFERENCES "Associations" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_FinancePayments_FinanceBanks_TenantId_EntityId_BankAccountId" FOREIGN KEY ("TenantId", "EntityId", "BankAccountId") REFERENCES "FinanceBanks" ("TenantId", "EntityId", "Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_FinancePayments_FinanceEntitys_TenantId_EntityId" FOREIGN KEY ("TenantId", "EntityId") REFERENCES "FinanceEntitys" ("TenantId", "Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_FinancePayments_Orgs_TenantId_OrgId" FOREIGN KEY ("TenantId", "OrgId") REFERENCES "Orgs" ("TenantId", "Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_FinancePayments_Persons_PersonId" FOREIGN KEY ("PersonId") REFERENCES "Persons" ("Id") ON DELETE RESTRICT
);

CREATE TABLE "FinanceExpenses" (
    "Id" uuid NOT NULL,
    "PersonId" uuid NOT NULL,
    "Payee" text NOT NULL,
    "Description" text NOT NULL,
    "Category" text NOT NULL,
    "Lines" text NOT NULL,
    "Evidence" text NOT NULL,
    "EvidenceHash" text NOT NULL,
    "EventId" uuid,
    "ProjectId" uuid,
    "BudgetId" uuid,
    "AccountCode" text NOT NULL,
    "Amount" numeric(24,8) NOT NULL,
    "Paid" numeric(24,8) NOT NULL,
    "Status" text NOT NULL,
    "CreatorId" uuid NOT NULL,
    "RequestId" uuid,
    "Date" date NOT NULL,
    "TenantId" uuid NOT NULL,
    "OrgId" uuid NOT NULL,
    "EntityId" uuid NOT NULL,
    "Version" bigint NOT NULL,
    CONSTRAINT "PK_FinanceExpenses" PRIMARY KEY ("Id"),
    CONSTRAINT "AK_FinanceExpenses_TenantId_EntityId_Id" UNIQUE ("TenantId", "EntityId", "Id"),
    CONSTRAINT "AK_FinanceExpenses_TenantId_Id" UNIQUE ("TenantId", "Id"),
    CONSTRAINT "FK_FinanceExpenses_Associations_TenantId" FOREIGN KEY ("TenantId") REFERENCES "Associations" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_FinanceExpenses_Events_TenantId_EventId" FOREIGN KEY ("TenantId", "EventId") REFERENCES "Events" ("TenantId", "Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_FinanceExpenses_FinanceBudgets_TenantId_EntityId_BudgetId" FOREIGN KEY ("TenantId", "EntityId", "BudgetId") REFERENCES "FinanceBudgets" ("TenantId", "EntityId", "Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_FinanceExpenses_FinanceEntitys_TenantId_EntityId" FOREIGN KEY ("TenantId", "EntityId") REFERENCES "FinanceEntitys" ("TenantId", "Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_FinanceExpenses_GovernanceRequests_TenantId_RequestId" FOREIGN KEY ("TenantId", "RequestId") REFERENCES "GovernanceRequests" ("TenantId", "Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_FinanceExpenses_Orgs_TenantId_OrgId" FOREIGN KEY ("TenantId", "OrgId") REFERENCES "Orgs" ("TenantId", "Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_FinanceExpenses_Persons_PersonId" FOREIGN KEY ("PersonId") REFERENCES "Persons" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_FinanceExpenses_ServiceProjects_TenantId_ProjectId" FOREIGN KEY ("TenantId", "ProjectId") REFERENCES "ServiceProjects" ("TenantId", "Id") ON DELETE RESTRICT
);

CREATE TABLE "FinanceJournals" (
    "Id" uuid NOT NULL,
    "Date" date NOT NULL,
    "PeriodId" uuid NOT NULL,
    "Source" text NOT NULL,
    "Narration" text NOT NULL,
    "CreatorId" uuid NOT NULL,
    "ReversesId" uuid,
    "PostedAt" timestamp with time zone NOT NULL,
    "TenantId" uuid NOT NULL,
    "OrgId" uuid NOT NULL,
    "EntityId" uuid NOT NULL,
    "Version" bigint NOT NULL,
    CONSTRAINT "PK_FinanceJournals" PRIMARY KEY ("Id"),
    CONSTRAINT "AK_FinanceJournals_TenantId_EntityId_Id" UNIQUE ("TenantId", "EntityId", "Id"),
    CONSTRAINT "AK_FinanceJournals_TenantId_Id" UNIQUE ("TenantId", "Id"),
    CONSTRAINT "FK_FinanceJournals_Associations_TenantId" FOREIGN KEY ("TenantId") REFERENCES "Associations" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_FinanceJournals_FinanceEntitys_TenantId_EntityId" FOREIGN KEY ("TenantId", "EntityId") REFERENCES "FinanceEntitys" ("TenantId", "Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_FinanceJournals_FinanceJournals_TenantId_EntityId_ReversesId" FOREIGN KEY ("TenantId", "EntityId", "ReversesId") REFERENCES "FinanceJournals" ("TenantId", "EntityId", "Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_FinanceJournals_FinancePeriods_TenantId_EntityId_PeriodId" FOREIGN KEY ("TenantId", "EntityId", "PeriodId") REFERENCES "FinancePeriods" ("TenantId", "EntityId", "Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_FinanceJournals_Orgs_TenantId_OrgId" FOREIGN KEY ("TenantId", "OrgId") REFERENCES "Orgs" ("TenantId", "Id") ON DELETE RESTRICT
);

CREATE TABLE "FinanceInvoices" (
    "Id" uuid NOT NULL,
    "Number" text NOT NULL,
    "PersonId" uuid,
    "MembershipId" uuid,
    "PlanId" uuid,
    "EventId" uuid,
    "RegistrationId" uuid,
    "ProjectId" uuid,
    "PledgeId" uuid,
    "Customer" text NOT NULL,
    "CustomerAddress" text NOT NULL,
    "IssuerSnapshot" text NOT NULL,
    "Purpose" text NOT NULL,
    "Date" date NOT NULL,
    "DueOn" date NOT NULL,
    "PeriodStart" date,
    "PeriodEnd" date,
    "State" text NOT NULL,
    "Currency" text NOT NULL,
    "Subtotal" numeric(24,8) NOT NULL,
    "Tax" numeric(24,8) NOT NULL,
    "Total" numeric(24,8) NOT NULL,
    "Paid" numeric(24,8) NOT NULL,
    "Credited" numeric(24,8) NOT NULL,
    "IdempotencyKey" text NOT NULL,
    "Notes" text NOT NULL,
    "CreatorId" uuid NOT NULL,
    "RemindedAt" timestamp with time zone,
    "TenantId" uuid NOT NULL,
    "OrgId" uuid NOT NULL,
    "EntityId" uuid NOT NULL,
    "Version" bigint NOT NULL,
    CONSTRAINT "PK_FinanceInvoices" PRIMARY KEY ("Id"),
    CONSTRAINT "AK_FinanceInvoices_TenantId_EntityId_Id" UNIQUE ("TenantId", "EntityId", "Id"),
    CONSTRAINT "AK_FinanceInvoices_TenantId_Id" UNIQUE ("TenantId", "Id"),
    CONSTRAINT "FK_FinanceInvoices_Associations_TenantId" FOREIGN KEY ("TenantId") REFERENCES "Associations" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_FinanceInvoices_DuesPlans_TenantId_EntityId_PlanId" FOREIGN KEY ("TenantId", "EntityId", "PlanId") REFERENCES "DuesPlans" ("TenantId", "EntityId", "Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_FinanceInvoices_EventRegistrations_TenantId_RegistrationId" FOREIGN KEY ("TenantId", "RegistrationId") REFERENCES "EventRegistrations" ("TenantId", "Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_FinanceInvoices_Events_TenantId_EventId" FOREIGN KEY ("TenantId", "EventId") REFERENCES "Events" ("TenantId", "Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_FinanceInvoices_FinanceEntitys_TenantId_EntityId" FOREIGN KEY ("TenantId", "EntityId") REFERENCES "FinanceEntitys" ("TenantId", "Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_FinanceInvoices_FinancePledges_TenantId_EntityId_PledgeId" FOREIGN KEY ("TenantId", "EntityId", "PledgeId") REFERENCES "FinancePledges" ("TenantId", "EntityId", "Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_FinanceInvoices_Memberships_TenantId_MembershipId" FOREIGN KEY ("TenantId", "MembershipId") REFERENCES "Memberships" ("TenantId", "Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_FinanceInvoices_Orgs_TenantId_OrgId" FOREIGN KEY ("TenantId", "OrgId") REFERENCES "Orgs" ("TenantId", "Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_FinanceInvoices_Persons_PersonId" FOREIGN KEY ("PersonId") REFERENCES "Persons" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_FinanceInvoices_ServiceProjects_TenantId_ProjectId" FOREIGN KEY ("TenantId", "ProjectId") REFERENCES "ServiceProjects" ("TenantId", "Id") ON DELETE RESTRICT
);

CREATE TABLE "FinanceBankTransactions" (
    "Id" uuid NOT NULL,
    "BankAccountId" uuid NOT NULL,
    "Date" date NOT NULL,
    "Amount" numeric(24,8) NOT NULL,
    "Reference" text NOT NULL,
    "ImportKey" text NOT NULL,
    "PaymentId" uuid,
    "Status" text NOT NULL,
    "TenantId" uuid NOT NULL,
    "OrgId" uuid NOT NULL,
    "EntityId" uuid NOT NULL,
    "Version" bigint NOT NULL,
    CONSTRAINT "PK_FinanceBankTransactions" PRIMARY KEY ("Id"),
    CONSTRAINT "AK_FinanceBankTransactions_TenantId_EntityId_Id" UNIQUE ("TenantId", "EntityId", "Id"),
    CONSTRAINT "AK_FinanceBankTransactions_TenantId_Id" UNIQUE ("TenantId", "Id"),
    CONSTRAINT "FK_FinanceBankTransactions_Associations_TenantId" FOREIGN KEY ("TenantId") REFERENCES "Associations" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_FinanceBankTransactions_FinanceBanks_TenantId_EntityId_Bank~" FOREIGN KEY ("TenantId", "EntityId", "BankAccountId") REFERENCES "FinanceBanks" ("TenantId", "EntityId", "Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_FinanceBankTransactions_FinanceEntitys_TenantId_EntityId" FOREIGN KEY ("TenantId", "EntityId") REFERENCES "FinanceEntitys" ("TenantId", "Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_FinanceBankTransactions_FinancePayments_TenantId_EntityId_P~" FOREIGN KEY ("TenantId", "EntityId", "PaymentId") REFERENCES "FinancePayments" ("TenantId", "EntityId", "Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_FinanceBankTransactions_Orgs_TenantId_OrgId" FOREIGN KEY ("TenantId", "OrgId") REFERENCES "Orgs" ("TenantId", "Id") ON DELETE RESTRICT
);

CREATE TABLE "FinanceReimbursements" (
    "Id" uuid NOT NULL,
    "ExpenseId" uuid NOT NULL,
    "Amount" numeric(24,8) NOT NULL,
    "Reference" text NOT NULL,
    "CreatorId" uuid NOT NULL,
    "Date" date NOT NULL,
    "TenantId" uuid NOT NULL,
    "OrgId" uuid NOT NULL,
    "EntityId" uuid NOT NULL,
    "Version" bigint NOT NULL,
    CONSTRAINT "PK_FinanceReimbursements" PRIMARY KEY ("Id"),
    CONSTRAINT "AK_FinanceReimbursements_TenantId_EntityId_Id" UNIQUE ("TenantId", "EntityId", "Id"),
    CONSTRAINT "AK_FinanceReimbursements_TenantId_Id" UNIQUE ("TenantId", "Id"),
    CONSTRAINT "FK_FinanceReimbursements_Associations_TenantId" FOREIGN KEY ("TenantId") REFERENCES "Associations" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_FinanceReimbursements_FinanceEntitys_TenantId_EntityId" FOREIGN KEY ("TenantId", "EntityId") REFERENCES "FinanceEntitys" ("TenantId", "Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_FinanceReimbursements_FinanceExpenses_TenantId_EntityId_Exp~" FOREIGN KEY ("TenantId", "EntityId", "ExpenseId") REFERENCES "FinanceExpenses" ("TenantId", "EntityId", "Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_FinanceReimbursements_Orgs_TenantId_OrgId" FOREIGN KEY ("TenantId", "OrgId") REFERENCES "Orgs" ("TenantId", "Id") ON DELETE RESTRICT
);

CREATE TABLE "FinanceJournalLines" (
    "Id" uuid NOT NULL,
    "JournalId" uuid NOT NULL,
    "AccountId" uuid NOT NULL,
    "Debit" numeric(24,8) NOT NULL,
    "Credit" numeric(24,8) NOT NULL,
    "PersonId" uuid,
    "EventId" uuid,
    "ProjectId" uuid,
    "TenantId" uuid NOT NULL,
    "OrgId" uuid NOT NULL,
    "EntityId" uuid NOT NULL,
    "Version" bigint NOT NULL,
    CONSTRAINT "PK_FinanceJournalLines" PRIMARY KEY ("Id"),
    CONSTRAINT "AK_FinanceJournalLines_TenantId_EntityId_Id" UNIQUE ("TenantId", "EntityId", "Id"),
    CONSTRAINT "AK_FinanceJournalLines_TenantId_Id" UNIQUE ("TenantId", "Id"),
    CONSTRAINT "FK_FinanceJournalLines_Associations_TenantId" FOREIGN KEY ("TenantId") REFERENCES "Associations" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_FinanceJournalLines_Events_TenantId_EventId" FOREIGN KEY ("TenantId", "EventId") REFERENCES "Events" ("TenantId", "Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_FinanceJournalLines_FinanceAccounts_TenantId_EntityId_Accou~" FOREIGN KEY ("TenantId", "EntityId", "AccountId") REFERENCES "FinanceAccounts" ("TenantId", "EntityId", "Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_FinanceJournalLines_FinanceEntitys_TenantId_EntityId" FOREIGN KEY ("TenantId", "EntityId") REFERENCES "FinanceEntitys" ("TenantId", "Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_FinanceJournalLines_FinanceJournals_TenantId_EntityId_Journ~" FOREIGN KEY ("TenantId", "EntityId", "JournalId") REFERENCES "FinanceJournals" ("TenantId", "EntityId", "Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_FinanceJournalLines_Orgs_TenantId_OrgId" FOREIGN KEY ("TenantId", "OrgId") REFERENCES "Orgs" ("TenantId", "Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_FinanceJournalLines_Persons_PersonId" FOREIGN KEY ("PersonId") REFERENCES "Persons" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_FinanceJournalLines_ServiceProjects_TenantId_ProjectId" FOREIGN KEY ("TenantId", "ProjectId") REFERENCES "ServiceProjects" ("TenantId", "Id") ON DELETE RESTRICT
);

CREATE TABLE "FinanceAllocations" (
    "Id" uuid NOT NULL,
    "Key" text NOT NULL,
    "PaymentId" uuid NOT NULL,
    "InvoiceId" uuid NOT NULL,
    "Amount" numeric(24,8) NOT NULL,
    "TenantId" uuid NOT NULL,
    "OrgId" uuid NOT NULL,
    "EntityId" uuid NOT NULL,
    "Version" bigint NOT NULL,
    CONSTRAINT "PK_FinanceAllocations" PRIMARY KEY ("Id"),
    CONSTRAINT "AK_FinanceAllocations_TenantId_EntityId_Id" UNIQUE ("TenantId", "EntityId", "Id"),
    CONSTRAINT "AK_FinanceAllocations_TenantId_Id" UNIQUE ("TenantId", "Id"),
    CONSTRAINT "FK_FinanceAllocations_Associations_TenantId" FOREIGN KEY ("TenantId") REFERENCES "Associations" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_FinanceAllocations_FinanceEntitys_TenantId_EntityId" FOREIGN KEY ("TenantId", "EntityId") REFERENCES "FinanceEntitys" ("TenantId", "Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_FinanceAllocations_FinanceInvoices_TenantId_EntityId_Invoic~" FOREIGN KEY ("TenantId", "EntityId", "InvoiceId") REFERENCES "FinanceInvoices" ("TenantId", "EntityId", "Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_FinanceAllocations_FinancePayments_TenantId_EntityId_Paymen~" FOREIGN KEY ("TenantId", "EntityId", "PaymentId") REFERENCES "FinancePayments" ("TenantId", "EntityId", "Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_FinanceAllocations_Orgs_TenantId_OrgId" FOREIGN KEY ("TenantId", "OrgId") REFERENCES "Orgs" ("TenantId", "Id") ON DELETE RESTRICT
);

CREATE TABLE "FinanceCredits" (
    "Id" uuid NOT NULL,
    "InvoiceId" uuid NOT NULL,
    "Amount" numeric(24,8) NOT NULL,
    "Reason" text NOT NULL,
    "Key" text NOT NULL,
    "CreatorId" uuid NOT NULL,
    "Date" date NOT NULL,
    "TenantId" uuid NOT NULL,
    "OrgId" uuid NOT NULL,
    "EntityId" uuid NOT NULL,
    "Version" bigint NOT NULL,
    CONSTRAINT "PK_FinanceCredits" PRIMARY KEY ("Id"),
    CONSTRAINT "AK_FinanceCredits_TenantId_EntityId_Id" UNIQUE ("TenantId", "EntityId", "Id"),
    CONSTRAINT "AK_FinanceCredits_TenantId_Id" UNIQUE ("TenantId", "Id"),
    CONSTRAINT "FK_FinanceCredits_Associations_TenantId" FOREIGN KEY ("TenantId") REFERENCES "Associations" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_FinanceCredits_FinanceEntitys_TenantId_EntityId" FOREIGN KEY ("TenantId", "EntityId") REFERENCES "FinanceEntitys" ("TenantId", "Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_FinanceCredits_FinanceInvoices_TenantId_EntityId_InvoiceId" FOREIGN KEY ("TenantId", "EntityId", "InvoiceId") REFERENCES "FinanceInvoices" ("TenantId", "EntityId", "Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_FinanceCredits_Orgs_TenantId_OrgId" FOREIGN KEY ("TenantId", "OrgId") REFERENCES "Orgs" ("TenantId", "Id") ON DELETE RESTRICT
);

CREATE TABLE "FinanceEntitlements" (
    "Id" uuid NOT NULL,
    "InvoiceId" uuid NOT NULL,
    "BeneficiaryEntityId" uuid NOT NULL,
    "Amount" numeric(24,8) NOT NULL,
    "Settled" numeric(24,8) NOT NULL,
    "TenantId" uuid NOT NULL,
    "OrgId" uuid NOT NULL,
    "EntityId" uuid NOT NULL,
    "Version" bigint NOT NULL,
    CONSTRAINT "PK_FinanceEntitlements" PRIMARY KEY ("Id"),
    CONSTRAINT "AK_FinanceEntitlements_TenantId_EntityId_Id" UNIQUE ("TenantId", "EntityId", "Id"),
    CONSTRAINT "AK_FinanceEntitlements_TenantId_Id" UNIQUE ("TenantId", "Id"),
    CONSTRAINT "FK_FinanceEntitlements_Associations_TenantId" FOREIGN KEY ("TenantId") REFERENCES "Associations" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_FinanceEntitlements_FinanceEntitys_TenantId_EntityId" FOREIGN KEY ("TenantId", "EntityId") REFERENCES "FinanceEntitys" ("TenantId", "Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_FinanceEntitlements_FinanceInvoices_TenantId_EntityId_Invoi~" FOREIGN KEY ("TenantId", "EntityId", "InvoiceId") REFERENCES "FinanceInvoices" ("TenantId", "EntityId", "Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_FinanceEntitlements_Orgs_TenantId_OrgId" FOREIGN KEY ("TenantId", "OrgId") REFERENCES "Orgs" ("TenantId", "Id") ON DELETE RESTRICT
);

CREATE TABLE "FinanceIntents" (
    "Id" uuid NOT NULL,
    "InvoiceId" uuid NOT NULL,
    "Provider" text NOT NULL,
    "ProviderId" text NOT NULL,
    "CheckoutUrl" text NOT NULL,
    "Key" text NOT NULL,
    "State" text NOT NULL,
    "Amount" numeric(24,8) NOT NULL,
    "PaymentId" uuid,
    "TenantId" uuid NOT NULL,
    "OrgId" uuid NOT NULL,
    "EntityId" uuid NOT NULL,
    "Version" bigint NOT NULL,
    CONSTRAINT "PK_FinanceIntents" PRIMARY KEY ("Id"),
    CONSTRAINT "AK_FinanceIntents_TenantId_EntityId_Id" UNIQUE ("TenantId", "EntityId", "Id"),
    CONSTRAINT "AK_FinanceIntents_TenantId_Id" UNIQUE ("TenantId", "Id"),
    CONSTRAINT "FK_FinanceIntents_Associations_TenantId" FOREIGN KEY ("TenantId") REFERENCES "Associations" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_FinanceIntents_FinanceEntitys_TenantId_EntityId" FOREIGN KEY ("TenantId", "EntityId") REFERENCES "FinanceEntitys" ("TenantId", "Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_FinanceIntents_FinanceInvoices_TenantId_EntityId_InvoiceId" FOREIGN KEY ("TenantId", "EntityId", "InvoiceId") REFERENCES "FinanceInvoices" ("TenantId", "EntityId", "Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_FinanceIntents_FinancePayments_TenantId_EntityId_PaymentId" FOREIGN KEY ("TenantId", "EntityId", "PaymentId") REFERENCES "FinancePayments" ("TenantId", "EntityId", "Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_FinanceIntents_Orgs_TenantId_OrgId" FOREIGN KEY ("TenantId", "OrgId") REFERENCES "Orgs" ("TenantId", "Id") ON DELETE RESTRICT
);

CREATE TABLE "FinanceInvoiceLines" (
    "Id" uuid NOT NULL,
    "InvoiceId" uuid NOT NULL,
    "Description" text NOT NULL,
    "Quantity" numeric(24,8) NOT NULL,
    "Rate" numeric(24,8) NOT NULL,
    "Discount" numeric(24,8) NOT NULL,
    "TaxPercent" numeric(24,8) NOT NULL,
    "Net" numeric(24,8) NOT NULL,
    "Tax" numeric(24,8) NOT NULL,
    "Total" numeric(24,8) NOT NULL,
    "AccountCode" text NOT NULL,
    "BeneficiaryEntityId" uuid,
    "TenantId" uuid NOT NULL,
    "OrgId" uuid NOT NULL,
    "EntityId" uuid NOT NULL,
    "Version" bigint NOT NULL,
    CONSTRAINT "PK_FinanceInvoiceLines" PRIMARY KEY ("Id"),
    CONSTRAINT "AK_FinanceInvoiceLines_TenantId_EntityId_Id" UNIQUE ("TenantId", "EntityId", "Id"),
    CONSTRAINT "AK_FinanceInvoiceLines_TenantId_Id" UNIQUE ("TenantId", "Id"),
    CONSTRAINT "FK_FinanceInvoiceLines_Associations_TenantId" FOREIGN KEY ("TenantId") REFERENCES "Associations" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_FinanceInvoiceLines_FinanceEntitys_TenantId_EntityId" FOREIGN KEY ("TenantId", "EntityId") REFERENCES "FinanceEntitys" ("TenantId", "Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_FinanceInvoiceLines_FinanceInvoices_TenantId_EntityId_Invoi~" FOREIGN KEY ("TenantId", "EntityId", "InvoiceId") REFERENCES "FinanceInvoices" ("TenantId", "EntityId", "Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_FinanceInvoiceLines_Orgs_TenantId_OrgId" FOREIGN KEY ("TenantId", "OrgId") REFERENCES "Orgs" ("TenantId", "Id") ON DELETE RESTRICT
);

CREATE TABLE "FinanceRefunds" (
    "Id" uuid NOT NULL,
    "PaymentId" uuid NOT NULL,
    "InvoiceId" uuid,
    "Amount" numeric(24,8) NOT NULL,
    "Kind" text NOT NULL,
    "Reason" text NOT NULL,
    "Reference" text NOT NULL,
    "CreatorId" uuid NOT NULL,
    "Date" date NOT NULL,
    "TenantId" uuid NOT NULL,
    "OrgId" uuid NOT NULL,
    "EntityId" uuid NOT NULL,
    "Version" bigint NOT NULL,
    CONSTRAINT "PK_FinanceRefunds" PRIMARY KEY ("Id"),
    CONSTRAINT "AK_FinanceRefunds_TenantId_EntityId_Id" UNIQUE ("TenantId", "EntityId", "Id"),
    CONSTRAINT "AK_FinanceRefunds_TenantId_Id" UNIQUE ("TenantId", "Id"),
    CONSTRAINT "FK_FinanceRefunds_Associations_TenantId" FOREIGN KEY ("TenantId") REFERENCES "Associations" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_FinanceRefunds_FinanceEntitys_TenantId_EntityId" FOREIGN KEY ("TenantId", "EntityId") REFERENCES "FinanceEntitys" ("TenantId", "Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_FinanceRefunds_FinanceInvoices_TenantId_EntityId_InvoiceId" FOREIGN KEY ("TenantId", "EntityId", "InvoiceId") REFERENCES "FinanceInvoices" ("TenantId", "EntityId", "Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_FinanceRefunds_FinancePayments_TenantId_EntityId_PaymentId" FOREIGN KEY ("TenantId", "EntityId", "PaymentId") REFERENCES "FinancePayments" ("TenantId", "EntityId", "Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_FinanceRefunds_Orgs_TenantId_OrgId" FOREIGN KEY ("TenantId", "OrgId") REFERENCES "Orgs" ("TenantId", "Id") ON DELETE RESTRICT
);

CREATE TABLE "FinanceTransfers" (
    "Id" uuid NOT NULL,
    "BeneficiaryEntityId" uuid NOT NULL,
    "EntitlementId" uuid NOT NULL,
    "Amount" numeric(24,8) NOT NULL,
    "Reference" text NOT NULL,
    "Status" text NOT NULL,
    "CreatorId" uuid NOT NULL,
    "Date" date NOT NULL,
    "TenantId" uuid NOT NULL,
    "OrgId" uuid NOT NULL,
    "EntityId" uuid NOT NULL,
    "Version" bigint NOT NULL,
    CONSTRAINT "PK_FinanceTransfers" PRIMARY KEY ("Id"),
    CONSTRAINT "AK_FinanceTransfers_TenantId_EntityId_Id" UNIQUE ("TenantId", "EntityId", "Id"),
    CONSTRAINT "AK_FinanceTransfers_TenantId_Id" UNIQUE ("TenantId", "Id"),
    CONSTRAINT "FK_FinanceTransfers_Associations_TenantId" FOREIGN KEY ("TenantId") REFERENCES "Associations" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_FinanceTransfers_FinanceEntitlements_TenantId_EntityId_Enti~" FOREIGN KEY ("TenantId", "EntityId", "EntitlementId") REFERENCES "FinanceEntitlements" ("TenantId", "EntityId", "Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_FinanceTransfers_FinanceEntitys_TenantId_EntityId" FOREIGN KEY ("TenantId", "EntityId") REFERENCES "FinanceEntitys" ("TenantId", "Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_FinanceTransfers_Orgs_TenantId_OrgId" FOREIGN KEY ("TenantId", "OrgId") REFERENCES "Orgs" ("TenantId", "Id") ON DELETE RESTRICT
);

INSERT INTO "Permissions" ("Id")
VALUES ('finance.approve');
INSERT INTO "Permissions" ("Id")
VALUES ('finance.configure');
INSERT INTO "Permissions" ("Id")
VALUES ('finance.manage');
INSERT INTO "Permissions" ("Id")
VALUES ('finance.post');
INSERT INTO "Permissions" ("Id")
VALUES ('finance.reconcile');
INSERT INTO "Permissions" ("Id")
VALUES ('finance.self');
INSERT INTO "Permissions" ("Id")
VALUES ('finance.view');

CREATE INDEX "IX_DuesPlans_EntityId_AutoBill_NextBillOn" ON "DuesPlans" ("EntityId", "AutoBill", "NextBillOn");

CREATE INDEX "IX_DuesPlans_EntityId_Id" ON "DuesPlans" ("EntityId", "Id");

CREATE INDEX "IX_DuesPlans_TenantId_BillingOrgId" ON "DuesPlans" ("TenantId", "BillingOrgId");

CREATE INDEX "IX_DuesPlans_TenantId_CategoryId" ON "DuesPlans" ("TenantId", "CategoryId");

CREATE INDEX "IX_DuesPlans_TenantId_OrgId" ON "DuesPlans" ("TenantId", "OrgId");

CREATE UNIQUE INDEX "IX_FinanceAccounts_EntityId_Code" ON "FinanceAccounts" ("EntityId", "Code");

CREATE INDEX "IX_FinanceAccounts_EntityId_Id" ON "FinanceAccounts" ("EntityId", "Id");

CREATE INDEX "IX_FinanceAccounts_TenantId_OrgId" ON "FinanceAccounts" ("TenantId", "OrgId");

CREATE INDEX "IX_FinanceAllocations_EntityId_Id" ON "FinanceAllocations" ("EntityId", "Id");

CREATE UNIQUE INDEX "IX_FinanceAllocations_EntityId_Key" ON "FinanceAllocations" ("EntityId", "Key");

CREATE INDEX "IX_FinanceAllocations_TenantId_EntityId_InvoiceId" ON "FinanceAllocations" ("TenantId", "EntityId", "InvoiceId");

CREATE INDEX "IX_FinanceAllocations_TenantId_EntityId_PaymentId" ON "FinanceAllocations" ("TenantId", "EntityId", "PaymentId");

CREATE INDEX "IX_FinanceAllocations_TenantId_OrgId" ON "FinanceAllocations" ("TenantId", "OrgId");

CREATE INDEX "IX_FinanceAudits_EntityId_Id" ON "FinanceAudits" ("EntityId", "Id");

CREATE INDEX "IX_FinanceAudits_TenantId_OrgId" ON "FinanceAudits" ("TenantId", "OrgId");

CREATE INDEX "IX_FinanceBanks_EntityId_Id" ON "FinanceBanks" ("EntityId", "Id");

CREATE INDEX "IX_FinanceBanks_TenantId_OrgId" ON "FinanceBanks" ("TenantId", "OrgId");

CREATE INDEX "IX_FinanceBankTransactions_EntityId_Id" ON "FinanceBankTransactions" ("EntityId", "Id");

CREATE UNIQUE INDEX "IX_FinanceBankTransactions_EntityId_ImportKey" ON "FinanceBankTransactions" ("EntityId", "ImportKey");

CREATE INDEX "IX_FinanceBankTransactions_TenantId_EntityId_BankAccountId" ON "FinanceBankTransactions" ("TenantId", "EntityId", "BankAccountId");

CREATE INDEX "IX_FinanceBankTransactions_TenantId_EntityId_PaymentId" ON "FinanceBankTransactions" ("TenantId", "EntityId", "PaymentId");

CREATE INDEX "IX_FinanceBankTransactions_TenantId_OrgId" ON "FinanceBankTransactions" ("TenantId", "OrgId");

CREATE INDEX "IX_FinanceBudgets_EntityId_Id" ON "FinanceBudgets" ("EntityId", "Id");

CREATE INDEX "IX_FinanceBudgets_TenantId_EntityId_ReplacesId" ON "FinanceBudgets" ("TenantId", "EntityId", "ReplacesId");

CREATE INDEX "IX_FinanceBudgets_TenantId_EventId" ON "FinanceBudgets" ("TenantId", "EventId");

CREATE INDEX "IX_FinanceBudgets_TenantId_OrgId" ON "FinanceBudgets" ("TenantId", "OrgId");

CREATE INDEX "IX_FinanceBudgets_TenantId_ProjectId" ON "FinanceBudgets" ("TenantId", "ProjectId");

CREATE INDEX "IX_FinanceBudgets_TenantId_RequestId" ON "FinanceBudgets" ("TenantId", "RequestId");

CREATE INDEX "IX_FinanceCredits_EntityId_Id" ON "FinanceCredits" ("EntityId", "Id");

CREATE UNIQUE INDEX "IX_FinanceCredits_EntityId_Key" ON "FinanceCredits" ("EntityId", "Key");

CREATE INDEX "IX_FinanceCredits_TenantId_EntityId_InvoiceId" ON "FinanceCredits" ("TenantId", "EntityId", "InvoiceId");

CREATE INDEX "IX_FinanceCredits_TenantId_OrgId" ON "FinanceCredits" ("TenantId", "OrgId");

CREATE INDEX "IX_FinanceEntitlements_EntityId_Id" ON "FinanceEntitlements" ("EntityId", "Id");

CREATE UNIQUE INDEX "IX_FinanceEntitlements_EntityId_InvoiceId_BeneficiaryEntityId" ON "FinanceEntitlements" ("EntityId", "InvoiceId", "BeneficiaryEntityId");

CREATE INDEX "IX_FinanceEntitlements_TenantId_EntityId_InvoiceId" ON "FinanceEntitlements" ("TenantId", "EntityId", "InvoiceId");

CREATE INDEX "IX_FinanceEntitlements_TenantId_OrgId" ON "FinanceEntitlements" ("TenantId", "OrgId");

CREATE INDEX "IX_FinanceEntitys_EntityId_Id" ON "FinanceEntitys" ("EntityId", "Id");

CREATE INDEX "IX_FinanceEntitys_TenantId_OrgId" ON "FinanceEntitys" ("TenantId", "OrgId");

CREATE UNIQUE INDEX "IX_FinanceEventPrices_EventId_Category" ON "FinanceEventPrices" ("EventId", "Category");

CREATE INDEX "IX_FinanceEventPrices_TenantId_EntityId" ON "FinanceEventPrices" ("TenantId", "EntityId");

CREATE INDEX "IX_FinanceEventPrices_TenantId_EventId" ON "FinanceEventPrices" ("TenantId", "EventId");

CREATE INDEX "IX_FinanceExchangeRates_EntityId_Id" ON "FinanceExchangeRates" ("EntityId", "Id");

CREATE INDEX "IX_FinanceExchangeRates_TenantId_OrgId" ON "FinanceExchangeRates" ("TenantId", "OrgId");

CREATE INDEX "IX_FinanceExpenses_EntityId_Id" ON "FinanceExpenses" ("EntityId", "Id");

CREATE INDEX "IX_FinanceExpenses_EntityId_Status_Date" ON "FinanceExpenses" ("EntityId", "Status", "Date");

CREATE INDEX "IX_FinanceExpenses_PersonId" ON "FinanceExpenses" ("PersonId");

CREATE INDEX "IX_FinanceExpenses_TenantId_EntityId_BudgetId" ON "FinanceExpenses" ("TenantId", "EntityId", "BudgetId");

CREATE INDEX "IX_FinanceExpenses_TenantId_EventId" ON "FinanceExpenses" ("TenantId", "EventId");

CREATE INDEX "IX_FinanceExpenses_TenantId_OrgId" ON "FinanceExpenses" ("TenantId", "OrgId");

CREATE INDEX "IX_FinanceExpenses_TenantId_ProjectId" ON "FinanceExpenses" ("TenantId", "ProjectId");

CREATE INDEX "IX_FinanceExpenses_TenantId_RequestId" ON "FinanceExpenses" ("TenantId", "RequestId");

CREATE INDEX "IX_FinanceGrants_EntityId_Id" ON "FinanceGrants" ("EntityId", "Id");

CREATE UNIQUE INDEX "IX_FinanceGrants_EntityId_UserId" ON "FinanceGrants" ("EntityId", "UserId");

CREATE INDEX "IX_FinanceGrants_TenantId_OrgId" ON "FinanceGrants" ("TenantId", "OrgId");

CREATE INDEX "IX_FinanceIntents_EntityId_Id" ON "FinanceIntents" ("EntityId", "Id");

CREATE UNIQUE INDEX "IX_FinanceIntents_EntityId_Key" ON "FinanceIntents" ("EntityId", "Key");

CREATE UNIQUE INDEX "IX_FinanceIntents_EntityId_ProviderId" ON "FinanceIntents" ("EntityId", "ProviderId") WHERE "ProviderId" <> '';

CREATE INDEX "IX_FinanceIntents_TenantId_EntityId_InvoiceId" ON "FinanceIntents" ("TenantId", "EntityId", "InvoiceId");

CREATE INDEX "IX_FinanceIntents_TenantId_EntityId_PaymentId" ON "FinanceIntents" ("TenantId", "EntityId", "PaymentId");

CREATE INDEX "IX_FinanceIntents_TenantId_OrgId" ON "FinanceIntents" ("TenantId", "OrgId");

CREATE INDEX "IX_FinanceInvoiceLines_EntityId_Id" ON "FinanceInvoiceLines" ("EntityId", "Id");

CREATE INDEX "IX_FinanceInvoiceLines_TenantId_EntityId_InvoiceId" ON "FinanceInvoiceLines" ("TenantId", "EntityId", "InvoiceId");

CREATE INDEX "IX_FinanceInvoiceLines_TenantId_OrgId" ON "FinanceInvoiceLines" ("TenantId", "OrgId");

CREATE INDEX "IX_FinanceInvoices_EntityId_Id" ON "FinanceInvoices" ("EntityId", "Id");

CREATE UNIQUE INDEX "IX_FinanceInvoices_EntityId_IdempotencyKey" ON "FinanceInvoices" ("EntityId", "IdempotencyKey");

CREATE UNIQUE INDEX "IX_FinanceInvoices_EntityId_Number" ON "FinanceInvoices" ("EntityId", "Number") WHERE "Number" <> '';

CREATE INDEX "IX_FinanceInvoices_EntityId_PersonId_Date" ON "FinanceInvoices" ("EntityId", "PersonId", "Date");

CREATE INDEX "IX_FinanceInvoices_PersonId" ON "FinanceInvoices" ("PersonId");

CREATE INDEX "IX_FinanceInvoices_TenantId_EntityId_PlanId" ON "FinanceInvoices" ("TenantId", "EntityId", "PlanId");

CREATE INDEX "IX_FinanceInvoices_TenantId_EntityId_PledgeId" ON "FinanceInvoices" ("TenantId", "EntityId", "PledgeId");

CREATE INDEX "IX_FinanceInvoices_TenantId_EventId" ON "FinanceInvoices" ("TenantId", "EventId");

CREATE INDEX "IX_FinanceInvoices_TenantId_MembershipId" ON "FinanceInvoices" ("TenantId", "MembershipId");

CREATE INDEX "IX_FinanceInvoices_TenantId_OrgId" ON "FinanceInvoices" ("TenantId", "OrgId");

CREATE INDEX "IX_FinanceInvoices_TenantId_ProjectId" ON "FinanceInvoices" ("TenantId", "ProjectId");

CREATE INDEX "IX_FinanceInvoices_TenantId_RegistrationId" ON "FinanceInvoices" ("TenantId", "RegistrationId");

CREATE INDEX "IX_FinanceJobs_EntityId_Id" ON "FinanceJobs" ("EntityId", "Id");

CREATE INDEX "IX_FinanceJobs_EntityId_Status_CreatedAt" ON "FinanceJobs" ("EntityId", "Status", "CreatedAt");

CREATE UNIQUE INDEX "IX_FinanceJobs_PlanId_PeriodStart_PeriodEnd" ON "FinanceJobs" ("PlanId", "PeriodStart", "PeriodEnd");

CREATE INDEX "IX_FinanceJobs_TenantId_EntityId_PlanId" ON "FinanceJobs" ("TenantId", "EntityId", "PlanId");

CREATE INDEX "IX_FinanceJobs_TenantId_OrgId" ON "FinanceJobs" ("TenantId", "OrgId");

CREATE INDEX "IX_FinanceJournalLines_EntityId_AccountId_JournalId" ON "FinanceJournalLines" ("EntityId", "AccountId", "JournalId");

CREATE INDEX "IX_FinanceJournalLines_EntityId_Id" ON "FinanceJournalLines" ("EntityId", "Id");

CREATE INDEX "IX_FinanceJournalLines_PersonId" ON "FinanceJournalLines" ("PersonId");

CREATE INDEX "IX_FinanceJournalLines_TenantId_EntityId_AccountId" ON "FinanceJournalLines" ("TenantId", "EntityId", "AccountId");

CREATE INDEX "IX_FinanceJournalLines_TenantId_EntityId_JournalId" ON "FinanceJournalLines" ("TenantId", "EntityId", "JournalId");

CREATE INDEX "IX_FinanceJournalLines_TenantId_EventId" ON "FinanceJournalLines" ("TenantId", "EventId");

CREATE INDEX "IX_FinanceJournalLines_TenantId_OrgId" ON "FinanceJournalLines" ("TenantId", "OrgId");

CREATE INDEX "IX_FinanceJournalLines_TenantId_ProjectId" ON "FinanceJournalLines" ("TenantId", "ProjectId");

CREATE INDEX "IX_FinanceJournals_EntityId_Date" ON "FinanceJournals" ("EntityId", "Date");

CREATE INDEX "IX_FinanceJournals_EntityId_Id" ON "FinanceJournals" ("EntityId", "Id");

CREATE UNIQUE INDEX "IX_FinanceJournals_EntityId_Source" ON "FinanceJournals" ("EntityId", "Source");

CREATE UNIQUE INDEX "IX_FinanceJournals_ReversesId" ON "FinanceJournals" ("ReversesId");

CREATE INDEX "IX_FinanceJournals_TenantId_EntityId_PeriodId" ON "FinanceJournals" ("TenantId", "EntityId", "PeriodId");

CREATE INDEX "IX_FinanceJournals_TenantId_EntityId_ReversesId" ON "FinanceJournals" ("TenantId", "EntityId", "ReversesId");

CREATE INDEX "IX_FinanceJournals_TenantId_OrgId" ON "FinanceJournals" ("TenantId", "OrgId");

CREATE INDEX "IX_FinanceOrganizations_EntityId_Id" ON "FinanceOrganizations" ("EntityId", "Id");

CREATE UNIQUE INDEX "IX_FinanceOrganizations_EntityId_ServedOrgId" ON "FinanceOrganizations" ("EntityId", "ServedOrgId");

CREATE INDEX "IX_FinanceOrganizations_TenantId_OrgId" ON "FinanceOrganizations" ("TenantId", "OrgId");

CREATE INDEX "IX_FinanceOrganizations_TenantId_ServedOrgId" ON "FinanceOrganizations" ("TenantId", "ServedOrgId");

CREATE INDEX "IX_FinancePayments_EntityId_Date" ON "FinancePayments" ("EntityId", "Date");

CREATE INDEX "IX_FinancePayments_EntityId_Id" ON "FinancePayments" ("EntityId", "Id");

CREATE UNIQUE INDEX "IX_FinancePayments_EntityId_IdempotencyKey" ON "FinancePayments" ("EntityId", "IdempotencyKey");

CREATE UNIQUE INDEX "IX_FinancePayments_EntityId_ReceiptNumber" ON "FinancePayments" ("EntityId", "ReceiptNumber") WHERE "ReceiptNumber" <> '';

CREATE UNIQUE INDEX "IX_FinancePayments_EntityId_Reference" ON "FinancePayments" ("EntityId", "Reference");

CREATE INDEX "IX_FinancePayments_PersonId" ON "FinancePayments" ("PersonId");

CREATE INDEX "IX_FinancePayments_TenantId_EntityId_BankAccountId" ON "FinancePayments" ("TenantId", "EntityId", "BankAccountId");

CREATE INDEX "IX_FinancePayments_TenantId_OrgId" ON "FinancePayments" ("TenantId", "OrgId");

CREATE INDEX "IX_FinancePeriods_EntityId_Id" ON "FinancePeriods" ("EntityId", "Id");

CREATE INDEX "IX_FinancePeriods_TenantId_OrgId" ON "FinancePeriods" ("TenantId", "OrgId");

CREATE INDEX "IX_FinancePledges_EntityId_Id" ON "FinancePledges" ("EntityId", "Id");

CREATE INDEX "IX_FinancePledges_PersonId" ON "FinancePledges" ("PersonId");

CREATE INDEX "IX_FinancePledges_TenantId_EventId" ON "FinancePledges" ("TenantId", "EventId");

CREATE INDEX "IX_FinancePledges_TenantId_OrgId" ON "FinancePledges" ("TenantId", "OrgId");

CREATE INDEX "IX_FinancePledges_TenantId_ProjectId" ON "FinancePledges" ("TenantId", "ProjectId");

CREATE INDEX "IX_FinanceRefunds_EntityId_Id" ON "FinanceRefunds" ("EntityId", "Id");

CREATE UNIQUE INDEX "IX_FinanceRefunds_EntityId_Reference" ON "FinanceRefunds" ("EntityId", "Reference");

CREATE INDEX "IX_FinanceRefunds_TenantId_EntityId_InvoiceId" ON "FinanceRefunds" ("TenantId", "EntityId", "InvoiceId");

CREATE INDEX "IX_FinanceRefunds_TenantId_EntityId_PaymentId" ON "FinanceRefunds" ("TenantId", "EntityId", "PaymentId");

CREATE INDEX "IX_FinanceRefunds_TenantId_OrgId" ON "FinanceRefunds" ("TenantId", "OrgId");

CREATE INDEX "IX_FinanceReimbursements_EntityId_Id" ON "FinanceReimbursements" ("EntityId", "Id");

CREATE UNIQUE INDEX "IX_FinanceReimbursements_EntityId_Reference" ON "FinanceReimbursements" ("EntityId", "Reference");

CREATE INDEX "IX_FinanceReimbursements_TenantId_EntityId_ExpenseId" ON "FinanceReimbursements" ("TenantId", "EntityId", "ExpenseId");

CREATE INDEX "IX_FinanceReimbursements_TenantId_OrgId" ON "FinanceReimbursements" ("TenantId", "OrgId");

CREATE INDEX "IX_FinanceTransfers_EntityId_Id" ON "FinanceTransfers" ("EntityId", "Id");

CREATE UNIQUE INDEX "IX_FinanceTransfers_EntityId_Reference" ON "FinanceTransfers" ("EntityId", "Reference");

CREATE INDEX "IX_FinanceTransfers_TenantId_EntityId_EntitlementId" ON "FinanceTransfers" ("TenantId", "EntityId", "EntitlementId");

CREATE INDEX "IX_FinanceTransfers_TenantId_OrgId" ON "FinanceTransfers" ("TenantId", "OrgId");

CREATE INDEX "IX_FinanceWebhooks_EntityId_Id" ON "FinanceWebhooks" ("EntityId", "Id");

CREATE UNIQUE INDEX "IX_FinanceWebhooks_EntityId_ProviderEventId" ON "FinanceWebhooks" ("EntityId", "ProviderEventId");

CREATE INDEX "IX_FinanceWebhooks_TenantId_OrgId" ON "FinanceWebhooks" ("TenantId", "OrgId");


CREATE OR REPLACE FUNCTION finance_immutable() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RAISE EXCEPTION 'Final financial history is append only'; END $$;
CREATE TRIGGER finance_immutable BEFORE UPDATE OR DELETE ON "FinanceJournals" FOR EACH ROW EXECUTE FUNCTION finance_immutable();
CREATE TRIGGER finance_immutable BEFORE UPDATE OR DELETE ON "FinanceJournalLines" FOR EACH ROW EXECUTE FUNCTION finance_immutable();
CREATE TRIGGER finance_immutable BEFORE UPDATE OR DELETE ON "FinanceCredits" FOR EACH ROW EXECUTE FUNCTION finance_immutable();
CREATE TRIGGER finance_immutable BEFORE UPDATE OR DELETE ON "FinanceAllocations" FOR EACH ROW EXECUTE FUNCTION finance_immutable();
CREATE TRIGGER finance_immutable BEFORE UPDATE OR DELETE ON "FinanceRefunds" FOR EACH ROW EXECUTE FUNCTION finance_immutable();
CREATE TRIGGER finance_immutable BEFORE UPDATE OR DELETE ON "FinanceReimbursements" FOR EACH ROW EXECUTE FUNCTION finance_immutable();
CREATE TRIGGER finance_immutable BEFORE UPDATE OR DELETE ON "FinanceTransfers" FOR EACH ROW EXECUTE FUNCTION finance_immutable();
CREATE TRIGGER finance_immutable BEFORE UPDATE OR DELETE ON "FinanceWebhooks" FOR EACH ROW EXECUTE FUNCTION finance_immutable();
CREATE TRIGGER finance_immutable BEFORE UPDATE OR DELETE ON "FinanceAudits" FOR EACH ROW EXECUTE FUNCTION finance_immutable();
CREATE TRIGGER finance_immutable BEFORE UPDATE OR DELETE ON "FinanceExchangeRates" FOR EACH ROW EXECUTE FUNCTION finance_immutable();

CREATE OR REPLACE FUNCTION finance_balance_check() RETURNS trigger LANGUAGE plpgsql AS $$
DECLARE jid uuid; debit numeric; credit numeric; n integer; locked boolean; edate date; pstart date; pend date;
BEGIN
 IF TG_TABLE_NAME='FinanceJournals' THEN jid:=NEW."Id"; ELSE jid:=NEW."JournalId"; END IF;
 SELECT SUM("Debit"),SUM("Credit"),COUNT(*) INTO debit,credit,n FROM "FinanceJournalLines" WHERE "JournalId"=jid;
 IF n<2 OR debit<=0 OR debit<>credit THEN RAISE EXCEPTION 'Journal must balance with at least two positive lines'; END IF;
 SELECT p."Closed",j."Date",p."StartsOn",p."EndsOn" INTO locked,edate,pstart,pend FROM "FinanceJournals" j JOIN "FinancePeriods" p ON p."Id"=j."PeriodId" WHERE j."Id"=jid FOR SHARE OF p;
 IF locked OR edate<pstart OR edate>pend THEN RAISE EXCEPTION 'Posting period is closed or date is outside period'; END IF;
 RETURN NULL;
END $$;
CREATE CONSTRAINT TRIGGER finance_journal_balance AFTER INSERT ON "FinanceJournals" DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION finance_balance_check();
CREATE CONSTRAINT TRIGGER finance_line_balance AFTER INSERT ON "FinanceJournalLines" DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION finance_balance_check();
ALTER TABLE "FinanceJournalLines" ADD CONSTRAINT finance_one_side CHECK ("Debit">=0 AND "Credit">=0 AND (("Debit">0 AND "Credit"=0) OR ("Credit">0 AND "Debit"=0)));
ALTER TABLE "FinanceEntitys" ADD CONSTRAINT finance_entity_policy CHECK ("Decimals" BETWEEN 0 AND 4 AND "FiscalMonth" BETWEEN 1 AND 12 AND "Currency" ~ '^[A-Z]{3}$' AND "AccountingMethod"='Accrual' AND "EntityId"="Id");
ALTER TABLE "FinanceInvoices" ADD CONSTRAINT finance_invoice_balances CHECK ("Total">0 AND "Paid">=0 AND "Credited">=0 AND "Paid"+"Credited"<="Total" AND "State" IN ('Draft','Issued','Cancelled'));
ALTER TABLE "FinancePayments" ADD CONSTRAINT finance_payment_balances CHECK ("Amount">0 AND "Allocated">=0 AND "Refunded">=0 AND "Fee">=0 AND "Allocated"+"Refunded"<="Amount" AND "Fee"<="Amount" AND "Status" IN ('Pending','Verified'));
ALTER TABLE "FinanceExpenses" ADD CONSTRAINT finance_expense_amount CHECK ("Amount">0 AND "Paid">=0 AND "Paid"<="Amount");
ALTER TABLE "FinancePeriods" ADD CONSTRAINT finance_period_dates CHECK ("StartsOn"<="EndsOn");
ALTER TABLE "FinanceEntitlements" ADD CONSTRAINT finance_entitlement_balance CHECK ("Amount">=0 AND "Settled">=0 AND "Settled"<="Amount");
CREATE OR REPLACE FUNCTION finance_document_guard() RETURNS trigger LANGUAGE plpgsql AS $$
DECLARE invoice_state text;
BEGIN
 IF TG_OP='DELETE' THEN RAISE EXCEPTION 'Financial records cannot be deleted'; END IF;
 IF TG_TABLE_NAME='FinanceInvoices' AND (to_jsonb(OLD)->>'State')<>'Draft' AND (to_jsonb(NEW)-ARRAY['Paid','Credited','RemindedAt','Version']) IS DISTINCT FROM (to_jsonb(OLD)-ARRAY['Paid','Credited','RemindedAt','Version']) THEN RAISE EXCEPTION 'Issued invoice content is immutable'; END IF;
 IF TG_TABLE_NAME='FinancePayments' AND (to_jsonb(OLD)->>'Status')='Verified' AND (to_jsonb(NEW)-ARRAY['Allocated','Refunded','Fee','SettlementStatus','BankAccountId','SettledOn','Version']) IS DISTINCT FROM (to_jsonb(OLD)-ARRAY['Allocated','Refunded','Fee','SettlementStatus','BankAccountId','SettledOn','Version']) THEN RAISE EXCEPTION 'Verified payment identity is immutable'; END IF;
 RETURN NEW;
END $$;
CREATE TRIGGER finance_document_guard BEFORE UPDATE OR DELETE ON "FinanceInvoices" FOR EACH ROW EXECUTE FUNCTION finance_document_guard();
CREATE TRIGGER finance_document_guard BEFORE UPDATE OR DELETE ON "FinancePayments" FOR EACH ROW EXECUTE FUNCTION finance_document_guard();
CREATE OR REPLACE FUNCTION finance_invoice_line_guard() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN
 IF EXISTS(SELECT 1 FROM "FinanceInvoices" WHERE "Id"=COALESCE(NEW."InvoiceId",OLD."InvoiceId") AND "State"<>'Draft') THEN RAISE EXCEPTION 'Finalized invoice lines are immutable'; END IF;
 RETURN COALESCE(NEW,OLD); END $$;
CREATE TRIGGER finance_invoice_line_guard BEFORE INSERT OR UPDATE OR DELETE ON "FinanceInvoiceLines" FOR EACH ROW EXECUTE FUNCTION finance_invoice_line_guard();
CREATE OR REPLACE FUNCTION finance_review_guard() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN
 IF TG_OP='DELETE' THEN RAISE EXCEPTION 'Financial review history cannot be deleted'; END IF;
 IF (to_jsonb(OLD)->>'Status')<>'Draft' AND (to_jsonb(NEW)-ARRAY['Status','Paid','Version']) IS DISTINCT FROM (to_jsonb(OLD)-ARRAY['Status','Paid','Version']) THEN RAISE EXCEPTION 'Reviewed financial content is immutable'; END IF; RETURN NEW; END $$;
CREATE TRIGGER finance_review_guard BEFORE UPDATE OR DELETE ON "FinanceExpenses" FOR EACH ROW EXECUTE FUNCTION finance_review_guard();
CREATE TRIGGER finance_review_guard BEFORE UPDATE OR DELETE ON "FinanceBudgets" FOR EACH ROW EXECUTE FUNCTION finance_review_guard();
CREATE OR REPLACE FUNCTION finance_plan_guard() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN
 IF TG_OP='DELETE' OR (to_jsonb(NEW)-ARRAY['AutoBill','NextBillOn','Version']) IS DISTINCT FROM (to_jsonb(OLD)-ARRAY['AutoBill','NextBillOn','Version']) THEN RAISE EXCEPTION 'Fee schedule definitions are immutable; create a new effective-dated plan'; END IF;RETURN NEW;END $$;
CREATE TRIGGER finance_plan_guard BEFORE UPDATE OR DELETE ON "DuesPlans" FOR EACH ROW EXECUTE FUNCTION finance_plan_guard();
CREATE OR REPLACE FUNCTION finance_entity_guard() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN
 IF NEW."Currency"<>OLD."Currency" OR NEW."Decimals"<>OLD."Decimals" OR NEW."TenantId"<>OLD."TenantId" OR NEW."OrgId"<>OLD."OrgId" THEN RAISE EXCEPTION 'Entity accounting identity is immutable'; END IF;RETURN NEW;END $$;
CREATE TRIGGER finance_entity_guard BEFORE UPDATE ON "FinanceEntitys" FOR EACH ROW EXECUTE FUNCTION finance_entity_guard();
CREATE OR REPLACE FUNCTION finance_money_guard() RETURNS trigger LANGUAGE plpgsql AS $$
DECLARE currency_decimals integer; v jsonb; field text;
BEGIN
 SELECT "Decimals" INTO currency_decimals FROM "FinanceEntitys" WHERE "Id"=NEW."EntityId";
 v:=to_jsonb(NEW);
 FOREACH field IN ARRAY ARRAY['Amount','Paid','Credited','Allocated','Refunded','Fee','Subtotal','Tax','Total','Debit','Credit','Net','Discount','Rate','Settled'] LOOP
 IF v ? field AND (TG_TABLE_NAME<>'FinanceExchangeRates' OR field<>'Rate') AND (v->>field)::numeric <> ROUND((v->>field)::numeric,currency_decimals) THEN RAISE EXCEPTION 'Amount exceeds native currency precision';END IF;
 END LOOP;
 IF v ? 'Currency' AND TG_TABLE_NAME NOT IN ('FinanceExchangeRates') AND v->>'Currency'<>(SELECT "Currency" FROM "FinanceEntitys" WHERE "Id"=NEW."EntityId") THEN RAISE EXCEPTION 'Native financial currency mismatch';END IF;RETURN NEW;END $$;
CREATE TRIGGER finance_money_guard BEFORE INSERT OR UPDATE ON "FinanceInvoices" FOR EACH ROW EXECUTE FUNCTION finance_money_guard();
CREATE TRIGGER finance_money_guard BEFORE INSERT OR UPDATE ON "FinanceInvoiceLines" FOR EACH ROW EXECUTE FUNCTION finance_money_guard();
CREATE TRIGGER finance_money_guard BEFORE INSERT OR UPDATE ON "FinancePayments" FOR EACH ROW EXECUTE FUNCTION finance_money_guard();
CREATE TRIGGER finance_money_guard BEFORE INSERT OR UPDATE ON "FinanceAllocations" FOR EACH ROW EXECUTE FUNCTION finance_money_guard();
CREATE TRIGGER finance_money_guard BEFORE INSERT OR UPDATE ON "FinanceCredits" FOR EACH ROW EXECUTE FUNCTION finance_money_guard();
CREATE TRIGGER finance_money_guard BEFORE INSERT OR UPDATE ON "FinanceRefunds" FOR EACH ROW EXECUTE FUNCTION finance_money_guard();
CREATE TRIGGER finance_money_guard BEFORE INSERT OR UPDATE ON "FinanceJournalLines" FOR EACH ROW EXECUTE FUNCTION finance_money_guard();
CREATE TRIGGER finance_money_guard BEFORE INSERT OR UPDATE ON "FinanceExpenses" FOR EACH ROW EXECUTE FUNCTION finance_money_guard();
CREATE TRIGGER finance_money_guard BEFORE INSERT OR UPDATE ON "FinanceBudgets" FOR EACH ROW EXECUTE FUNCTION finance_money_guard();
CREATE TRIGGER finance_money_guard BEFORE INSERT OR UPDATE ON "FinancePledges" FOR EACH ROW EXECUTE FUNCTION finance_money_guard();
CREATE TRIGGER finance_money_guard BEFORE INSERT OR UPDATE ON "FinanceEntitlements" FOR EACH ROW EXECUTE FUNCTION finance_money_guard();
CREATE TRIGGER finance_money_guard BEFORE INSERT OR UPDATE ON "FinanceTransfers" FOR EACH ROW EXECUTE FUNCTION finance_money_guard();
CREATE TRIGGER finance_money_guard BEFORE INSERT OR UPDATE ON "FinanceReimbursements" FOR EACH ROW EXECUTE FUNCTION finance_money_guard();
CREATE TRIGGER finance_money_guard BEFORE INSERT OR UPDATE ON "FinanceBankTransactions" FOR EACH ROW EXECUTE FUNCTION finance_money_guard();
CREATE TRIGGER finance_money_guard BEFORE INSERT OR UPDATE ON "FinanceEventPrices" FOR EACH ROW EXECUTE FUNCTION finance_money_guard();

INSERT INTO "Permissions" ("Id") VALUES ('finance.view') ON CONFLICT DO NOTHING;
INSERT INTO "RolePermissions" ("Id","RoleId","PermissionId") SELECT gen_random_uuid(),"Id",'finance.view' FROM "Roles" WHERE "Name"='Association Super Admin' ON CONFLICT DO NOTHING;
INSERT INTO "Permissions" ("Id") VALUES ('finance.manage') ON CONFLICT DO NOTHING;
INSERT INTO "RolePermissions" ("Id","RoleId","PermissionId") SELECT gen_random_uuid(),"Id",'finance.manage' FROM "Roles" WHERE "Name"='Association Super Admin' ON CONFLICT DO NOTHING;
INSERT INTO "Permissions" ("Id") VALUES ('finance.configure') ON CONFLICT DO NOTHING;
INSERT INTO "RolePermissions" ("Id","RoleId","PermissionId") SELECT gen_random_uuid(),"Id",'finance.configure' FROM "Roles" WHERE "Name"='Association Super Admin' ON CONFLICT DO NOTHING;
INSERT INTO "Permissions" ("Id") VALUES ('finance.reconcile') ON CONFLICT DO NOTHING;
INSERT INTO "RolePermissions" ("Id","RoleId","PermissionId") SELECT gen_random_uuid(),"Id",'finance.reconcile' FROM "Roles" WHERE "Name"='Association Super Admin' ON CONFLICT DO NOTHING;
INSERT INTO "Permissions" ("Id") VALUES ('finance.post') ON CONFLICT DO NOTHING;
INSERT INTO "RolePermissions" ("Id","RoleId","PermissionId") SELECT gen_random_uuid(),"Id",'finance.post' FROM "Roles" WHERE "Name"='Association Super Admin' ON CONFLICT DO NOTHING;
INSERT INTO "Permissions" ("Id") VALUES ('finance.approve') ON CONFLICT DO NOTHING;
INSERT INTO "RolePermissions" ("Id","RoleId","PermissionId") SELECT gen_random_uuid(),"Id",'finance.approve' FROM "Roles" WHERE "Name"='Association Super Admin' ON CONFLICT DO NOTHING;
INSERT INTO "Permissions" ("Id") VALUES ('finance.self') ON CONFLICT DO NOTHING;
INSERT INTO "RolePermissions" ("Id","RoleId","PermissionId") SELECT gen_random_uuid(),"Id",'finance.self' FROM "Roles" WHERE "Name"='Association Super Admin' ON CONFLICT DO NOTHING;
INSERT INTO "RolePermissions" ("Id","RoleId","PermissionId") SELECT gen_random_uuid(),"Id",'finance.self' FROM "Roles" ON CONFLICT DO NOTHING;


INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261111042130_FinancialFoundation', '10.0.0');

COMMIT;

START TRANSACTION;
ALTER TABLE "FinanceInvoices" ADD "RequestHash" text NOT NULL DEFAULT '';

ALTER TABLE "DuesPlans" ADD "ChargeCode" text NOT NULL DEFAULT '';


ALTER TABLE "DuesPlans" DISABLE TRIGGER finance_plan_guard;
UPDATE "DuesPlans" SET "ChargeCode"=upper(trim("Name")) WHERE "ChargeCode"='';
ALTER TABLE "DuesPlans" ENABLE TRIGGER finance_plan_guard;
CREATE OR REPLACE FUNCTION finance_plan_dates() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN
 PERFORM pg_advisory_xact_lock(hashtextextended(NEW."TenantId"::text,7));
 IF NEW."ChargeCode"='' OR length(NEW."ChargeCode")>150 THEN RAISE EXCEPTION 'Charge code required';END IF;
 IF EXISTS(SELECT 1 FROM "DuesPlans" p WHERE p."Id"<>NEW."Id" AND p."EntityId"=NEW."EntityId" AND p."BillingOrgId"=NEW."BillingOrgId" AND p."CategoryId" IS NOT DISTINCT FROM NEW."CategoryId" AND p."ChargeCode"=NEW."ChargeCode" AND p."EffectiveFrom"<=NEW."EffectiveTo" AND p."EffectiveTo">=NEW."EffectiveFrom") THEN RAISE EXCEPTION 'Effective fee definitions with the same charge code cannot overlap'; END IF;RETURN NEW;
END $$;
CREATE TRIGGER finance_plan_dates BEFORE INSERT ON "DuesPlans" FOR EACH ROW EXECUTE FUNCTION finance_plan_dates();
INSERT INTO "RolePermissions" ("Id","RoleId","PermissionId") SELECT gen_random_uuid(),r."Id",p."Id" FROM "Roles" r CROSS JOIN "Permissions" p WHERE r."Name"='Club Treasurer' AND (p."Id" LIKE 'finance.%' OR p."Id" IN ('governance.approve','governance.documents','operations.view','operations.documents')) ON CONFLICT DO NOTHING;


INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261111043443_FinancialBillingIdentity', '10.0.0');

COMMIT;

START TRANSACTION;
CREATE INDEX "IX_FinanceRefunds_InvoiceId" ON "FinanceRefunds" ("InvoiceId");

CREATE INDEX "IX_FinanceRefunds_PaymentId" ON "FinanceRefunds" ("PaymentId");

CREATE INDEX "IX_FinanceJournalLines_JournalId" ON "FinanceJournalLines" ("JournalId");

CREATE INDEX "IX_FinanceInvoiceLines_InvoiceId" ON "FinanceInvoiceLines" ("InvoiceId");

CREATE INDEX "IX_FinanceAllocations_InvoiceId" ON "FinanceAllocations" ("InvoiceId");

CREATE INDEX "IX_FinanceAllocations_PaymentId" ON "FinanceAllocations" ("PaymentId");


CREATE OR REPLACE FUNCTION finance_document_totals() RETURNS trigger LANGUAGE plpgsql AS $$
DECLARE iid uuid; pid uuid; inv "FinanceInvoices"%ROWTYPE; pay "FinancePayments"%ROWTYPE; paid numeric; credited numeric; total numeric; refunds numeric; allocated numeric;
BEGIN
 IF TG_TABLE_NAME='FinanceInvoices' THEN iid:=NEW."Id";
 ELSIF TG_TABLE_NAME IN ('FinanceAllocations','FinanceCredits','FinanceRefunds','FinanceInvoiceLines') THEN iid:=NEW."InvoiceId"; END IF;
 IF TG_TABLE_NAME='FinancePayments' THEN pid:=NEW."Id";
 ELSIF TG_TABLE_NAME IN ('FinanceAllocations','FinanceRefunds') THEN pid:=NEW."PaymentId"; END IF;
 IF iid IS NOT NULL THEN
 SELECT * INTO inv FROM "FinanceInvoices" WHERE "Id"=iid;
 SELECT COALESCE(SUM("Amount"),0) INTO paid FROM "FinanceAllocations" WHERE "InvoiceId"=iid;
 SELECT COALESCE(SUM("Amount"),0) INTO refunds FROM "FinanceRefunds" WHERE "InvoiceId"=iid;
 SELECT COALESCE(SUM("Amount"),0) INTO credited FROM "FinanceCredits" WHERE "InvoiceId"=iid;
 SELECT COALESCE(SUM("Total"),0) INTO total FROM "FinanceInvoiceLines" WHERE "InvoiceId"=iid;
 IF inv."Paid"<>paid-refunds OR inv."Credited"<>credited OR inv."Total"<>total THEN RAISE EXCEPTION 'Invoice totals do not reconcile to immutable source records';END IF;
 IF inv."State"='Issued' AND NOT EXISTS(SELECT 1 FROM "FinanceJournals" WHERE "EntityId"=inv."EntityId" AND "Source"='invoice:'||iid::text) THEN RAISE EXCEPTION 'Issued invoice requires posted journal';END IF;
 END IF;
 IF pid IS NOT NULL THEN
 SELECT * INTO pay FROM "FinancePayments" WHERE "Id"=pid;
 SELECT COALESCE(SUM("Amount"),0) INTO allocated FROM "FinanceAllocations" WHERE "PaymentId"=pid;
 SELECT COALESCE(SUM("Amount"),0) INTO refunds FROM "FinanceRefunds" WHERE "PaymentId"=pid;
 SELECT COALESCE(SUM("Amount"),0) INTO total FROM "FinanceRefunds" WHERE "PaymentId"=pid AND "InvoiceId" IS NOT NULL;
 IF pay."Allocated"<>allocated-total OR pay."Refunded"<>refunds THEN RAISE EXCEPTION 'Payment totals do not reconcile to immutable source records';END IF;
 IF pay."Status"='Verified' AND NOT EXISTS(SELECT 1 FROM "FinanceJournals" WHERE "EntityId"=pay."EntityId" AND "Source"='payment:'||pid::text) THEN RAISE EXCEPTION 'Verified payment requires collection journal';END IF;
 END IF; RETURN NULL;
END $$;
CREATE CONSTRAINT TRIGGER finance_document_totals AFTER INSERT OR UPDATE ON "FinanceInvoices" DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION finance_document_totals();
CREATE CONSTRAINT TRIGGER finance_document_totals AFTER INSERT OR UPDATE ON "FinanceInvoiceLines" DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION finance_document_totals();
CREATE CONSTRAINT TRIGGER finance_document_totals AFTER INSERT OR UPDATE ON "FinancePayments" DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION finance_document_totals();
CREATE CONSTRAINT TRIGGER finance_document_totals AFTER INSERT OR UPDATE ON "FinanceAllocations" DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION finance_document_totals();
CREATE CONSTRAINT TRIGGER finance_document_totals AFTER INSERT OR UPDATE ON "FinanceCredits" DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION finance_document_totals();
CREATE CONSTRAINT TRIGGER finance_document_totals AFTER INSERT OR UPDATE ON "FinanceRefunds" DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION finance_document_totals();

CREATE OR REPLACE FUNCTION finance_period_guard() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN
 PERFORM pg_advisory_xact_lock(hashtextextended(NEW."TenantId"::text,7));
 IF EXISTS(SELECT 1 FROM "FinancePeriods" p WHERE p."Id"<>NEW."Id" AND p."EntityId"=NEW."EntityId" AND p."StartsOn"<=NEW."EndsOn" AND p."EndsOn">=NEW."StartsOn") THEN RAISE EXCEPTION 'Accounting periods cannot overlap'; END IF;
 IF TG_OP='UPDATE' AND (NEW."StartsOn"<>OLD."StartsOn" OR NEW."EndsOn"<>OLD."EndsOn") AND EXISTS(SELECT 1 FROM "FinanceJournals" j WHERE j."PeriodId"=OLD."Id") THEN RAISE EXCEPTION 'Posted accounting period dates are immutable';END IF;RETURN NEW;END $$;
CREATE TRIGGER finance_period_guard BEFORE INSERT OR UPDATE ON "FinancePeriods" FOR EACH ROW EXECUTE FUNCTION finance_period_guard();


INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261111044010_FinancialPostingGuards', '10.0.0');

COMMIT;

START TRANSACTION;


CREATE OR REPLACE FUNCTION clubs_event_registration_guard() RETURNS trigger LANGUAGE plpgsql AS $$
DECLARE ev "Events"; used integer;
BEGIN
 IF TG_OP='UPDATE' AND NEW."EventId"=OLD."EventId" AND NEW."TenantId"=OLD."TenantId" AND NEW."Id"=OLD."Id" AND NEW."Status"=OLD."Status" AND NEW."Companions"=OLD."Companions" THEN RETURN NEW; END IF;
 SELECT * INTO ev FROM "Events" WHERE "Id"=NEW."EventId" AND "TenantId"=NEW."TenantId" FOR NO KEY UPDATE;
 IF NEW."Status" IN ('Confirmed','Offered') THEN
  IF ev."Lifecycle" IN ('Cancelled','Archived','Completed') OR ev."Publication"<>'Published' THEN RAISE EXCEPTION 'Event cannot confirm registration' USING ERRCODE='23514'; END IF;
  IF ev."Paid" AND NOT EXISTS(SELECT 1 FROM "FinanceInvoices" i WHERE i."RegistrationId"=NEW."Id" AND i."TenantId"=NEW."TenantId" AND i."State"='Issued' AND i."Total">0 AND i."Paid"+i."Credited">=i."Total") THEN RAISE EXCEPTION 'Paid event requires verified allocation' USING ERRCODE='23514';END IF;
  SELECT COALESCE(sum(1+"Companions"),0) INTO used FROM "EventRegistrations" WHERE "EventId"=NEW."EventId" AND "Id"<>NEW."Id" AND "Status" IN ('Confirmed','Offered');
  IF used+1+NEW."Companions">ev."Capacity" THEN RAISE EXCEPTION 'Event capacity exceeded' USING ERRCODE='23514'; END IF;
 END IF; RETURN NEW;
END $$;

ALTER TABLE "EventRegistrations" DROP CONSTRAINT "CK_Event_Registration";
ALTER TABLE "EventRegistrations" ADD CONSTRAINT "CK_Event_Registration" CHECK("Companions">=0 AND "Status" IN ('Pending','Pending Payment','Awaiting Approval','Confirmed','Waitlisted','Offered','Cancelled','Rejected'));
CREATE OR REPLACE FUNCTION finance_paid_registration() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN
 IF NEW."Status" IN ('Confirmed','Offered') AND EXISTS(SELECT 1 FROM "Events" e WHERE e."Id"=NEW."EventId" AND e."Paid") AND NOT EXISTS(SELECT 1 FROM "FinanceInvoices" i WHERE i."RegistrationId"=NEW."Id" AND i."State"='Issued' AND i."Total">0 AND i."Paid"+i."Credited">=i."Total") THEN RAISE EXCEPTION 'Paid registration requires verified allocation or authorized credit'; END IF;RETURN NULL;END $$;
CREATE CONSTRAINT TRIGGER finance_paid_registration AFTER INSERT OR UPDATE ON "EventRegistrations" DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION finance_paid_registration();


INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261111045505_PaidEventFinanceState', '10.0.0');

COMMIT;

START TRANSACTION;
ALTER TABLE "CommunicationExecutions" ADD "Attempts" integer NOT NULL DEFAULT 0;

ALTER TABLE "CommunicationExecutions" ADD "DefinitionSnapshot" text NOT NULL DEFAULT '';

ALTER TABLE "CommunicationExecutions" ADD "Error" text NOT NULL DEFAULT '';

ALTER TABLE "CommunicationExecutions" ADD "GeneratedResourceId" uuid;

ALTER TABLE "CommunicationExecutions" ADD "NextAttemptAt" timestamp with time zone NOT NULL DEFAULT TIMESTAMPTZ '-infinity';

ALTER TABLE "CommunicationExecutions" ADD "OrgId" uuid;

ALTER TABLE "CommunicationExecutions" ADD "RuleVersion" bigint NOT NULL DEFAULT 0;

ALTER TABLE "CommunicationExecutions" ADD "Step" integer NOT NULL DEFAULT 0;

ALTER TABLE "CommunicationExecutions" ADD "Trace" text NOT NULL DEFAULT '';

ALTER TABLE "CommunicationAutomations" ADD "Definition" text NOT NULL DEFAULT '';

CREATE TABLE "IntelligenceAudits" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "OrgId" uuid NOT NULL,
    "UserId" uuid NOT NULL,
    "Action" text NOT NULL,
    "QueryHash" text NOT NULL,
    "Tool" text NOT NULL,
    "Results" integer NOT NULL,
    "At" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_IntelligenceAudits" PRIMARY KEY ("Id"),
    CONSTRAINT "AK_IntelligenceAudits_TenantId_Id" UNIQUE ("TenantId", "Id"),
    CONSTRAINT "FK_IntelligenceAudits_Associations_TenantId" FOREIGN KEY ("TenantId") REFERENCES "Associations" ("Id") ON DELETE RESTRICT
);

CREATE TABLE "KnowledgePassages" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "DocumentId" uuid NOT NULL,
    "DocumentVersion" integer NOT NULL,
    "Page" integer NOT NULL,
    "SourceHash" text NOT NULL,
    "Text" text NOT NULL,
    "SearchVector" tsvector GENERATED ALWAYS AS (to_tsvector('simple', "Text")) STORED NOT NULL,
    "IndexedBy" uuid NOT NULL,
    "IndexedAt" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_KnowledgePassages" PRIMARY KEY ("Id"),
    CONSTRAINT "AK_KnowledgePassages_TenantId_Id" UNIQUE ("TenantId", "Id"),
    CONSTRAINT "FK_KnowledgePassages_Associations_TenantId" FOREIGN KEY ("TenantId") REFERENCES "Associations" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_KnowledgePassages_GovernanceDocuments_TenantId_DocumentId" FOREIGN KEY ("TenantId", "DocumentId") REFERENCES "GovernanceDocuments" ("TenantId", "Id") ON DELETE RESTRICT
);

CREATE TABLE "TenantBrandings" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "DisplayName" text NOT NULL,
    "Logo" text NOT NULL,
    "Favicon" text NOT NULL,
    "Accent" text NOT NULL,
    "ClubTerm" text NOT NULL,
    "DistrictTerm" text NOT NULL,
    "Welcome" text NOT NULL,
    "Languages" text NOT NULL,
    "DisabledFeatures" text NOT NULL,
    "RemoteAiEnabled" boolean NOT NULL,
    "AiPolicy" text NOT NULL,
    "Version" bigint NOT NULL,
    CONSTRAINT "PK_TenantBrandings" PRIMARY KEY ("Id"),
    CONSTRAINT "AK_TenantBrandings_TenantId_Id" UNIQUE ("TenantId", "Id"),
    CONSTRAINT "FK_TenantBrandings_Associations_TenantId" FOREIGN KEY ("TenantId") REFERENCES "Associations" ("Id") ON DELETE RESTRICT
);

CREATE TABLE "TenantDomains" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "Host" text NOT NULL,
    "Challenge" text NOT NULL,
    "Status" text NOT NULL,
    "CreatorId" uuid NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "VerifiedAt" timestamp with time zone,
    "LastCheckedAt" timestamp with time zone,
    "Version" bigint NOT NULL,
    CONSTRAINT "PK_TenantDomains" PRIMARY KEY ("Id"),
    CONSTRAINT "AK_TenantDomains_TenantId_Id" UNIQUE ("TenantId", "Id"),
    CONSTRAINT "FK_TenantDomains_Associations_TenantId" FOREIGN KEY ("TenantId") REFERENCES "Associations" ("Id") ON DELETE RESTRICT
);

INSERT INTO "Permissions" ("Id")
VALUES ('intelligence.assistant');
INSERT INTO "Permissions" ("Id")
VALUES ('intelligence.automations');
INSERT INTO "Permissions" ("Id")
VALUES ('intelligence.knowledge');
INSERT INTO "Permissions" ("Id")
VALUES ('intelligence.view');

CREATE INDEX "IX_CommunicationExecutions_TenantId_OrgId" ON "CommunicationExecutions" ("TenantId", "OrgId");

CREATE INDEX "IX_IntelligenceAudits_TenantId_OrgId_At" ON "IntelligenceAudits" ("TenantId", "OrgId", "At");

CREATE UNIQUE INDEX "IX_KnowledgePassages_DocumentId_DocumentVersion_Page" ON "KnowledgePassages" ("DocumentId", "DocumentVersion", "Page");

CREATE INDEX "IX_KnowledgePassages_SearchVector" ON "KnowledgePassages" USING GIN ("SearchVector");

CREATE INDEX "IX_KnowledgePassages_TenantId_DocumentId_DocumentVersion" ON "KnowledgePassages" ("TenantId", "DocumentId", "DocumentVersion");

CREATE UNIQUE INDEX "IX_TenantBrandings_TenantId" ON "TenantBrandings" ("TenantId");

CREATE UNIQUE INDEX "IX_TenantDomains_Host" ON "TenantDomains" ("Host");

ALTER TABLE "CommunicationExecutions" ADD CONSTRAINT "FK_CommunicationExecutions_Orgs_TenantId_OrgId" FOREIGN KEY ("TenantId", "OrgId") REFERENCES "Orgs" ("TenantId", "Id") ON DELETE RESTRICT;


CREATE INDEX "IX_Workflow_ExecutionDue" ON "CommunicationExecutions" ("Status","NextAttemptAt") WHERE "DefinitionSnapshot"<>'';
CREATE INDEX "IX_Workflow_GeneratedResource" ON "CommunicationExecutions" ("GeneratedResourceId") WHERE "GeneratedResourceId" IS NOT NULL;
CREATE OR REPLACE FUNCTION clubs_intelligence_immutable() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RAISE EXCEPTION 'Intelligence audit history is append only';END $$;
CREATE TRIGGER intelligence_audit_immutable BEFORE UPDATE OR DELETE ON "IntelligenceAudits" FOR EACH ROW EXECUTE FUNCTION clubs_intelligence_immutable();
ALTER TABLE "KnowledgePassages" ADD CONSTRAINT knowledge_page_bounds CHECK("Page" BETWEEN 1 AND 100 AND length("Text") BETWEEN 1 AND 30000 AND "DocumentVersion">0);
ALTER TABLE "TenantDomains" ADD CONSTRAINT domain_activation_guard CHECK("Host"=lower("Host") AND "Status" IN ('Pending','Verified','Active','Disabled') AND ("Status" NOT IN ('Verified','Active') OR "VerifiedAt" IS NOT NULL));
CREATE OR REPLACE FUNCTION clubs_workflow_guard() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN
 IF TG_OP='DELETE' AND OLD."Definition"<>'' THEN RAISE EXCEPTION 'Workflow history cannot be deleted';END IF;
 IF TG_OP='DELETE' THEN RETURN OLD;END IF;
 IF OLD."Definition"<>'' AND OLD."Status"<>'Draft' THEN
 IF NEW."Status"='Draft' THEN RAISE EXCEPTION 'Reviewed workflow requires a new revision';END IF;
 IF (to_jsonb(NEW)-ARRAY['Active','Status','Version','StartsAt']) IS DISTINCT FROM (to_jsonb(OLD)-ARRAY['Active','Status','Version','StartsAt']) THEN RAISE EXCEPTION 'Reviewed workflow content is immutable';END IF;
 END IF; RETURN NEW;END $$;
CREATE TRIGGER intelligence_workflow_guard BEFORE UPDATE OR DELETE ON "CommunicationAutomations" FOR EACH ROW EXECUTE FUNCTION clubs_workflow_guard();
CREATE OR REPLACE FUNCTION clubs_workflow_execution_guard() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN
 IF OLD."DefinitionSnapshot"<>'' AND (NEW."DefinitionSnapshot"<>OLD."DefinitionSnapshot" OR NEW."RuleVersion"<>OLD."RuleVersion" OR NEW."AutomationId"<>OLD."AutomationId" OR NEW."TenantId"<>OLD."TenantId" OR NEW."SourceKey"<>OLD."SourceKey" OR NEW."Step"<OLD."Step" OR NOT NEW."Trace"::jsonb @> OLD."Trace"::jsonb) THEN RAISE EXCEPTION 'Execution snapshot and trace are immutable';END IF;RETURN NEW;END $$;
CREATE TRIGGER intelligence_execution_guard BEFORE UPDATE ON "CommunicationExecutions" FOR EACH ROW EXECUTE FUNCTION clubs_workflow_execution_guard();
CREATE INDEX "IX_Intelligence_ClubMeetings_Title" ON "ClubMeetings" USING gin("Title" gin_trgm_ops);
CREATE INDEX "IX_Intelligence_OperationTasks_Title" ON "OperationTasks" USING gin("Title" gin_trgm_ops);
CREATE INDEX "IX_Intelligence_ServiceProjects_Name" ON "ServiceProjects" USING gin("Name" gin_trgm_ops);
CREATE INDEX "IX_Intelligence_GovernanceDocuments_Title" ON "GovernanceDocuments" USING gin("Title" gin_trgm_ops);
CREATE INDEX "IX_Intelligence_FinanceInvoices_Number" ON "FinanceInvoices" USING gin("Number" gin_trgm_ops);
CREATE INDEX "IX_Intelligence_FinanceInvoices_Customer" ON "FinanceInvoices" USING gin("Customer" gin_trgm_ops);
INSERT INTO "Permissions" ("Id") VALUES ('intelligence.view') ON CONFLICT DO NOTHING;
INSERT INTO "RolePermissions" ("Id","RoleId","PermissionId") SELECT gen_random_uuid(),"Id",'intelligence.view' FROM "Roles" WHERE "Name" IN ('Association Super Admin','Club President','Club Secretary','Club Treasurer','Club Manager') ON CONFLICT DO NOTHING;
INSERT INTO "Permissions" ("Id") VALUES ('intelligence.assistant') ON CONFLICT DO NOTHING;
INSERT INTO "RolePermissions" ("Id","RoleId","PermissionId") SELECT gen_random_uuid(),"Id",'intelligence.assistant' FROM "Roles" ON CONFLICT DO NOTHING;
INSERT INTO "Permissions" ("Id") VALUES ('intelligence.knowledge') ON CONFLICT DO NOTHING;
INSERT INTO "RolePermissions" ("Id","RoleId","PermissionId") SELECT gen_random_uuid(),"Id",'intelligence.knowledge' FROM "Roles" ON CONFLICT DO NOTHING;
INSERT INTO "Permissions" ("Id") VALUES ('intelligence.automations') ON CONFLICT DO NOTHING;
INSERT INTO "RolePermissions" ("Id","RoleId","PermissionId") SELECT gen_random_uuid(),"Id",'intelligence.automations' FROM "Roles" WHERE "Name" IN ('Association Super Admin','Club President','Club Secretary','Club Treasurer','Club Manager') ON CONFLICT DO NOTHING;


INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261111053439_IntelligenceFoundation', '10.0.0');

COMMIT;

