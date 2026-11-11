--
-- PostgreSQL database dump
--

\restrict Ixnd9FHFPWgSBcHYI0zZwsOwJzRTuaL6biXJ1Ikfp0XjF1Sao93HtX4vuCLdaFE

-- Dumped from database version 17.6
-- Dumped by pg_dump version 17.6

SET statement_timeout = 0;
SET lock_timeout = 0;
SET idle_in_transaction_session_timeout = 0;
SET transaction_timeout = 0;
SET client_encoding = 'UTF8';
SET standard_conforming_strings = on;
SELECT pg_catalog.set_config('search_path', '', false);
SET check_function_bodies = false;
SET xmloption = content;
SET client_min_messages = warning;
SET row_security = off;

--
-- Name: pg_trgm; Type: EXTENSION; Schema: -; Owner: -
--

CREATE EXTENSION IF NOT EXISTS pg_trgm WITH SCHEMA public;


--
-- Name: EXTENSION pg_trgm; Type: COMMENT; Schema: -; Owner: -
--

COMMENT ON EXTENSION pg_trgm IS 'text similarity measurement and index searching based on trigrams';


--
-- Name: clubs_appointment_guard(); Type: FUNCTION; Schema: public; Owner: -
--

CREATE FUNCTION public.clubs_appointment_guard() RETURNS trigger
    LANGUAGE plpgsql
    AS $$
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


--
-- Name: clubs_communication_broadcast_guard(); Type: FUNCTION; Schema: public; Owner: -
--

CREATE FUNCTION public.clubs_communication_broadcast_guard() RETURNS trigger
    LANGUAGE plpgsql
    AS $$ BEGIN
 IF OLD."Status"<>'Draft' AND (NEW."Body",NEW."Subject",NEW."Rules",NEW."Channel",NEW."OrgId",NEW."CreatorId",NEW."AccountId",NEW."Purpose",NEW."ContentHash") IS DISTINCT FROM (OLD."Body",OLD."Subject",OLD."Rules",OLD."Channel",OLD."OrgId",OLD."CreatorId",OLD."AccountId",OLD."Purpose",OLD."ContentHash") THEN RAISE EXCEPTION 'Approved broadcast identity/content is immutable; duplicate for revision' USING ERRCODE='23514';END IF;RETURN NEW;END $$;


--
-- Name: clubs_communication_recipient_guard(); Type: FUNCTION; Schema: public; Owner: -
--

CREATE FUNCTION public.clubs_communication_recipient_guard() RETURNS trigger
    LANGUAGE plpgsql
    AS $$ BEGIN IF (NEW."BroadcastId",NEW."PersonId",NEW."UserId",NEW."Name",NEW."Club",NEW."MemberNumber") IS DISTINCT FROM (OLD."BroadcastId",OLD."PersonId",OLD."UserId",OLD."Name",OLD."Club",OLD."MemberNumber") THEN RAISE EXCEPTION 'Approved recipient identity snapshot is immutable' USING ERRCODE='23514';END IF;RETURN NEW;END $$;


--
-- Name: clubs_communication_revision_guard(); Type: FUNCTION; Schema: public; Owner: -
--

CREATE FUNCTION public.clubs_communication_revision_guard() RETURNS trigger
    LANGUAGE plpgsql
    AS $$ BEGIN RAISE EXCEPTION 'Communication revisions are immutable' USING ERRCODE='23514';END $$;


--
-- Name: clubs_event_link_guard(); Type: FUNCTION; Schema: public; Owner: -
--

CREATE FUNCTION public.clubs_event_link_guard() RETURNS trigger
    LANGUAGE plpgsql
    AS $$ BEGIN
 IF NOT EXISTS(SELECT 1 FROM "EventRegistrations" r WHERE r."Id"=NEW."RegistrationId" AND r."EventId"=NEW."EventId" AND r."TenantId"=NEW."TenantId") THEN RAISE EXCEPTION 'Wrong event registration' USING ERRCODE='23514'; END IF;
 RETURN NEW;
END $$;


--
-- Name: clubs_event_registration_audit(); Type: FUNCTION; Schema: public; Owner: -
--

CREATE FUNCTION public.clubs_event_registration_audit() RETURNS trigger
    LANGUAGE plpgsql
    AS $$
DECLARE owner_id uuid; previous text;
BEGIN
 IF TG_OP='UPDATE' THEN previous=OLD."Status"; IF NEW."Status"=OLD."Status" THEN RETURN NEW; END IF; END IF;
 SELECT "OrgId" INTO owner_id FROM "Events" WHERE "Id"=NEW."EventId";
 INSERT INTO "Audits"("Id","TenantId","OrgId","ActorId","Action","Resource","Changes","Outcome","At") VALUES(gen_random_uuid(),NEW."TenantId",owner_id,nullif(current_setting('clubs.actor',true),'')::uuid,'events.registration.status',NEW."EventId"::text,jsonb_build_object('registrationId',NEW."Id",'before',previous,'after',NEW."Status")::text,'Success',clock_timestamp()); RETURN NEW;
END $$;


--
-- Name: clubs_event_registration_guard(); Type: FUNCTION; Schema: public; Owner: -
--

CREATE FUNCTION public.clubs_event_registration_guard() RETURNS trigger
    LANGUAGE plpgsql
    AS $$
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


--
-- Name: clubs_event_session_guard(); Type: FUNCTION; Schema: public; Owner: -
--

CREATE FUNCTION public.clubs_event_session_guard() RETURNS trigger
    LANGUAGE plpgsql
    AS $$ BEGIN
 IF NEW."SessionId" IS NOT NULL AND NOT EXISTS(SELECT 1 FROM "EventSessions" s WHERE s."Id"=NEW."SessionId" AND s."TenantId"=NEW."TenantId" AND s."EventId"=NEW."EventId") THEN RAISE EXCEPTION 'Session belongs to a different event' USING ERRCODE='23514'; END IF; RETURN NEW; END $$;


--
-- Name: clubs_event_state_audit(); Type: FUNCTION; Schema: public; Owner: -
--

CREATE FUNCTION public.clubs_event_state_audit() RETURNS trigger
    LANGUAGE plpgsql
    AS $$ BEGIN
 IF NEW."Approval" IS DISTINCT FROM OLD."Approval" OR NEW."Publication" IS DISTINCT FROM OLD."Publication" OR NEW."Lifecycle" IS DISTINCT FROM OLD."Lifecycle" OR NEW."RegistrationEnabled" IS DISTINCT FROM OLD."RegistrationEnabled" THEN
 INSERT INTO "Audits"("Id","TenantId","OrgId","ActorId","Action","Resource","Changes","Outcome","At") VALUES(gen_random_uuid(),NEW."TenantId",NEW."OrgId",nullif(current_setting('clubs.actor',true),'')::uuid,'events.state',NEW."Id"::text,jsonb_build_object('before',jsonb_build_object('approval',OLD."Approval",'publication',OLD."Publication",'lifecycle',OLD."Lifecycle",'registrationEnabled',OLD."RegistrationEnabled"),'after',jsonb_build_object('approval',NEW."Approval",'publication',NEW."Publication",'lifecycle',NEW."Lifecycle",'registrationEnabled',NEW."RegistrationEnabled"))::text,'Success',clock_timestamp()); END IF;RETURN NEW;END $$;


--
-- Name: clubs_hierarchy_guard(); Type: FUNCTION; Schema: public; Owner: -
--

CREATE FUNCTION public.clubs_hierarchy_guard() RETURNS trigger
    LANGUAGE plpgsql
    AS $$
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


--
-- Name: clubs_level_guard(); Type: FUNCTION; Schema: public; Owner: -
--

CREATE FUNCTION public.clubs_level_guard() RETURNS trigger
    LANGUAGE plpgsql
    AS $$
BEGIN
 IF TG_OP='UPDATE' AND (NEW."ParentLevelId" IS DISTINCT FROM OLD."ParentLevelId" OR NEW."TenantId"<>OLD."TenantId") THEN RAISE EXCEPTION 'Existing level relationships are immutable' USING ERRCODE='23514'; END IF;
 IF NEW."Id"=NEW."ParentLevelId" THEN RAISE EXCEPTION 'Level cycle rejected' USING ERRCODE='23514'; END IF;
 RETURN NEW;
END $$;


--
-- Name: clubs_membership_guard(); Type: FUNCTION; Schema: public; Owner: -
--

CREATE FUNCTION public.clubs_membership_guard() RETURNS trigger
    LANGUAGE plpgsql
    AS $$
BEGIN
 PERFORM pg_advisory_xact_lock(hashtext(NEW."TenantId"::text));
 IF NOT EXISTS(SELECT 1 FROM "Orgs" o JOIN "Levels" l ON o."LevelId"=l."Id" AND o."TenantId"=l."TenantId" WHERE o."Id"=NEW."ClubId" AND o."TenantId"=NEW."TenantId" AND l."IsClub") THEN RAISE EXCEPTION 'Membership target must be a club in its association' USING ERRCODE='23514'; END IF;
 IF TG_OP='UPDATE' AND (NEW."TenantId"<>OLD."TenantId" OR NEW."PersonId"<>OLD."PersonId" OR NEW."ClubId"<>OLD."ClubId") THEN RAISE EXCEPTION 'Affiliation identity is immutable; use a transfer' USING ERRCODE='23514'; END IF;
 IF NEW."Status" IN ('Active','Inactive','Suspended','Expired') AND EXISTS(SELECT 1 FROM "MembershipPolicies" p WHERE p."TenantId"=NEW."TenantId" AND NOT p."AllowMultipleClubs") AND EXISTS(SELECT 1 FROM "Memberships" m WHERE m."TenantId"=NEW."TenantId" AND m."PersonId"=NEW."PersonId" AND m."ClubId"<>NEW."ClubId" AND m."Id"<>NEW."Id" AND m."Status" IN ('Active','Inactive','Suspended','Expired')) THEN RAISE EXCEPTION 'Single-club association policy violated' USING ERRCODE='23514'; END IF;
 RETURN NEW;
END $$;


--
-- Name: clubs_membership_history_guard(); Type: FUNCTION; Schema: public; Owner: -
--

CREATE FUNCTION public.clubs_membership_history_guard() RETURNS trigger
    LANGUAGE plpgsql
    AS $$ BEGIN RAISE EXCEPTION 'Membership decisions and effective-dated history are append-only' USING ERRCODE='23514'; END $$;


--
-- Name: clubs_office_assignment_guard(); Type: FUNCTION; Schema: public; Owner: -
--

CREATE FUNCTION public.clubs_office_assignment_guard() RETURNS trigger
    LANGUAGE plpgsql
    AS $$ BEGIN
 IF NEW."AppointmentId" IS NOT NULL AND (NEW."MembershipId" IS NOT NULL OR NOT EXISTS(SELECT 1 FROM "OfficerAppointments" a JOIN "Persons" p ON p."Id"=a."PersonId" WHERE a."Id"=NEW."AppointmentId" AND a."TenantId"=NEW."TenantId" AND a."OrgId"=NEW."OrgId" AND a."GrantRoleId"=NEW."RoleId" AND p."UserId"=NEW."UserId" AND a."Status"='Active' AND a."StartsAt"<=now() AND a."EffectiveAt"<=now() AND a."EndsAt">now())) THEN RAISE EXCEPTION 'Invalid appointment-sourced access' USING ERRCODE='23514'; END IF; RETURN NEW; END $$;


--
-- Name: clubs_office_snapshot_guard(); Type: FUNCTION; Schema: public; Owner: -
--

CREATE FUNCTION public.clubs_office_snapshot_guard() RETURNS trigger
    LANGUAGE plpgsql
    AS $$ BEGIN
 IF (OLD."PositionTitle"<>'' AND NEW."PositionTitle"<>OLD."PositionTitle") OR (OLD."OrganizationName"<>'' AND NEW."OrganizationName"<>OLD."OrganizationName") THEN RAISE EXCEPTION 'Historical office labels are immutable' USING ERRCODE='23514'; END IF; RETURN NEW; END $$;


--
-- Name: clubs_org_history(); Type: FUNCTION; Schema: public; Owner: -
--

CREATE FUNCTION public.clubs_org_history() RETURNS trigger
    LANGUAGE plpgsql
    AS $$ BEGIN
 INSERT INTO "OrganizationHistory" ("Id","TenantId","OrgId","Before","After","ActorId","At") VALUES(gen_random_uuid(),NEW."TenantId",NEW."Id",CASE WHEN TG_OP='UPDATE' THEN to_jsonb(OLD)::text ELSE '{}' END,to_jsonb(NEW)::text,NULLIF(current_setting('clubs.actor',true),'')::uuid,now()); RETURN NULL; END $$;


--
-- Name: clubs_role_assignment_history(); Type: FUNCTION; Schema: public; Owner: -
--

CREATE FUNCTION public.clubs_role_assignment_history() RETURNS trigger
    LANGUAGE plpgsql
    AS $$
DECLARE item record; verb text;
BEGIN
 IF TG_OP='DELETE' THEN item:=OLD;verb:='Revoked';ELSE item:=NEW;verb:='Granted';END IF;
 INSERT INTO "RoleAssignmentHistory" ("Id","TenantId","AssignmentId","UserId","RoleId","OrgId","RoleName","OrganizationName","Action","At") SELECT gen_random_uuid(),item."TenantId",item."Id",item."UserId",item."RoleId",item."OrgId",r."Name",o."Name",verb,now() FROM "Roles" r JOIN "Orgs" o ON o."Id"=item."OrgId" AND o."TenantId"=item."TenantId" WHERE r."Id"=item."RoleId";
 RETURN NULL;
END $$;


--
-- Name: guard_issued_certificate(); Type: FUNCTION; Schema: public; Owner: -
--

CREATE FUNCTION public.guard_issued_certificate() RETURNS trigger
    LANGUAGE plpgsql
    AS $$ BEGIN
  IF OLD."Snapshot" IS DISTINCT FROM NEW."Snapshot" OR OLD."Layout" IS DISTINCT FROM NEW."Layout" OR OLD."PersonId" IS DISTINCT FROM NEW."PersonId" OR OLD."Number" IS DISTINCT FROM NEW."Number" OR OLD."TokenHash" IS DISTINCT FROM NEW."TokenHash" THEN RAISE EXCEPTION 'Certificate identity and content are immutable'; END IF; RETURN NEW; END $$;


--
-- Name: guard_minutes_body(); Type: FUNCTION; Schema: public; Owner: -
--

CREATE FUNCTION public.guard_minutes_body() RETURNS trigger
    LANGUAGE plpgsql
    AS $$ BEGIN IF OLD."Status" IN ('Review','Published') AND OLD."Body" IS DISTINCT FROM NEW."Body" THEN RAISE EXCEPTION 'Reviewed minutes require a new revision'; END IF; RETURN NEW; END $$;


--
-- Name: guard_operation_history(); Type: FUNCTION; Schema: public; Owner: -
--

CREATE FUNCTION public.guard_operation_history() RETURNS trigger
    LANGUAGE plpgsql
    AS $$ BEGIN RAISE EXCEPTION 'Operational revisions are immutable'; END $$;


SET default_tablespace = '';

SET default_table_access_method = heap;

--
-- Name: Assignments; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."Assignments" (
    "Id" uuid NOT NULL,
    "UserId" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "RoleId" uuid NOT NULL,
    "OrgId" uuid NOT NULL,
    "MembershipId" uuid,
    "AppointmentId" uuid
);


--
-- Name: Associations; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."Associations" (
    "Id" uuid NOT NULL,
    "Name" text NOT NULL,
    "Currency" text NOT NULL,
    "TimeZone" text NOT NULL,
    "Language" text NOT NULL,
    "Color" text NOT NULL
);


--
-- Name: Audits; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."Audits" (
    "Id" uuid NOT NULL,
    "TenantId" uuid,
    "OrgId" uuid,
    "ActorId" uuid,
    "Action" text NOT NULL,
    "Resource" text NOT NULL,
    "Outcome" text NOT NULL,
    "At" timestamp with time zone NOT NULL,
    "Changes" text DEFAULT ''::text NOT NULL
);


--
-- Name: AuthenticationAudits; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."AuthenticationAudits" (
    "Id" uuid NOT NULL,
    "ActorId" uuid,
    "Action" text NOT NULL,
    "Outcome" text NOT NULL,
    "At" timestamp with time zone NOT NULL
);


--
-- Name: CertificateTemplates; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."CertificateTemplates" (
    "Id" uuid NOT NULL,
    "Name" text NOT NULL,
    "Type" text NOT NULL,
    "Layout" text NOT NULL,
    "Active" boolean NOT NULL,
    "TenantId" uuid NOT NULL,
    "OrgId" uuid NOT NULL,
    "Version" bigint NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL
);


--
-- Name: ClubHealthConfigurations; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."ClubHealthConfigurations" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "LeadershipWeight" integer NOT NULL,
    "MembershipWeight" integer NOT NULL,
    "GovernanceWeight" integer NOT NULL,
    "DataWeight" integer NOT NULL,
    "MinimumActiveMembers" integer NOT NULL,
    "Version" bigint NOT NULL,
    "RequireClubChangeApproval" boolean NOT NULL,
    CONSTRAINT "CK_Health_Weights" CHECK ((("LeadershipWeight" >= 0) AND ("MembershipWeight" >= 0) AND ("GovernanceWeight" >= 0) AND ("DataWeight" >= 0) AND (((("LeadershipWeight" + "MembershipWeight") + "GovernanceWeight") + "DataWeight") = 100) AND ("MinimumActiveMembers" > 0)))
);


--
-- Name: ClubMeetings; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."ClubMeetings" (
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
    CONSTRAINT "CK_MeetingDates" CHECK ((("EndsAt" > "StartsAt") AND ("QuorumMinimum" >= 0) AND (("QuorumPercent" >= (0)::numeric) AND ("QuorumPercent" <= (100)::numeric))))
);


--
-- Name: CommitteeActivities; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."CommitteeActivities" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "CommitteeId" uuid NOT NULL,
    "ActorId" uuid NOT NULL,
    "Action" text NOT NULL,
    "Detail" text NOT NULL,
    "At" timestamp with time zone NOT NULL
);


--
-- Name: CommitteeMemberships; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."CommitteeMemberships" (
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
    CONSTRAINT "CK_Committee_Member_Term" CHECK ((("StartsAt" < "EndsAt") AND ("Status" = ANY (ARRAY['Active'::text, 'Completed'::text, 'Removed'::text]))))
);


--
-- Name: CommitteeRoles; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."CommitteeRoles" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "Name" text NOT NULL,
    "CanManage" boolean NOT NULL,
    "Active" boolean NOT NULL
);


--
-- Name: Committees; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."Committees" (
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
    CONSTRAINT "CK_Committee_Term" CHECK ((("StartsAt" < "EndsAt") AND ("Status" = ANY (ARRAY['Proposed'::text, 'Pending approval'::text, 'Active'::text, 'Completed'::text, 'Closed'::text, 'Cancelled'::text]))))
);


--
-- Name: CommunicationAccounts; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."CommunicationAccounts" (
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
    CONSTRAINT communication_account_channel CHECK (((("Channel" = 'Email'::text) AND ("Provider" = 'SMTP'::text)) OR (("Channel" = 'WhatsApp'::text) AND ("Provider" = 'Meta'::text)) OR (("Channel" = 'SMS'::text) AND ("Provider" = 'Twilio'::text)))),
    CONSTRAINT communication_account_rate CHECK ((("PerMinute" > 0) AND ("PerMinute" <= 1000)))
);


--
-- Name: CommunicationAttachments; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."CommunicationAttachments" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "MessageId" uuid NOT NULL,
    "Name" text NOT NULL,
    "Type" text NOT NULL,
    "Size" bigint NOT NULL,
    "StorageKey" text NOT NULL,
    "Hash" text NOT NULL,
    "ExpiresAt" timestamp with time zone NOT NULL,
    "Attempts" integer DEFAULT 0 NOT NULL,
    "NextAttemptAt" timestamp with time zone DEFAULT '-infinity'::timestamp with time zone NOT NULL,
    "ProviderAccountId" uuid,
    "ProviderMediaId" text DEFAULT ''::text NOT NULL,
    "Status" text DEFAULT 'Stored'::text NOT NULL
);


--
-- Name: CommunicationAudiences; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."CommunicationAudiences" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "OrgId" uuid NOT NULL,
    "Name" text NOT NULL,
    "Rules" text NOT NULL,
    "Version" bigint NOT NULL
);


--
-- Name: CommunicationAutomations; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."CommunicationAutomations" (
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
    "Version" bigint NOT NULL
);


--
-- Name: CommunicationBroadcasts; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."CommunicationBroadcasts" (
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
    "TemplateVersion" bigint DEFAULT 0 NOT NULL,
    CONSTRAINT communication_broadcast_state CHECK (("Status" = ANY (ARRAY['Draft'::text, 'Review'::text, 'Approved'::text, 'Scheduled'::text, 'Sending'::text, 'Completed'::text, 'Partially Completed'::text, 'Failed'::text, 'Rejected'::text, 'Changes Requested'::text, 'Cancelled'::text, 'Archived'::text])))
);


--
-- Name: CommunicationExecutions; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."CommunicationExecutions" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "AutomationId" uuid NOT NULL,
    "SourceKey" text NOT NULL,
    "MailId" uuid,
    "Status" text NOT NULL,
    "At" timestamp with time zone NOT NULL
);


--
-- Name: CommunicationMessages; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."CommunicationMessages" (
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
    "At" timestamp with time zone NOT NULL
);


--
-- Name: CommunicationPolicies; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."CommunicationPolicies" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "OrgId" uuid NOT NULL,
    "MemberDirect" boolean NOT NULL,
    "ApprovalThreshold" integer NOT NULL,
    "RetentionDays" integer NOT NULL
);


--
-- Name: CommunicationPreferences; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."CommunicationPreferences" (
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
    "UpdatedAt" timestamp with time zone NOT NULL
);


--
-- Name: CommunicationProviderEvents; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."CommunicationProviderEvents" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "AccountId" uuid NOT NULL,
    "EventKey" text NOT NULL,
    "ProviderId" text NOT NULL,
    "Status" text NOT NULL,
    "Error" text NOT NULL,
    "At" timestamp with time zone NOT NULL
);


--
-- Name: CommunicationRecipients; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."CommunicationRecipients" (
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
    "AcknowledgedAt" timestamp with time zone
);


--
-- Name: CommunicationRevisions; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."CommunicationRevisions" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "ResourceId" uuid NOT NULL,
    "Kind" text NOT NULL,
    "Version" bigint NOT NULL,
    "Snapshot" text NOT NULL,
    "ActorId" uuid NOT NULL,
    "At" timestamp with time zone NOT NULL
);


--
-- Name: CommunicationSuppressions; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."CommunicationSuppressions" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "Channel" text NOT NULL,
    "AddressHash" text NOT NULL,
    "Reason" text NOT NULL,
    "At" timestamp with time zone NOT NULL
);


--
-- Name: CommunicationTemplates; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."CommunicationTemplates" (
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
    "AccountId" uuid
);


--
-- Name: ConversationParticipants; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."ConversationParticipants" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "ConversationId" uuid NOT NULL,
    "UserId" uuid NOT NULL,
    "Staff" boolean NOT NULL,
    "Muted" boolean NOT NULL,
    "Active" boolean NOT NULL,
    "ReadAt" timestamp with time zone NOT NULL
);


--
-- Name: Conversations; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."Conversations" (
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
    "Version" bigint NOT NULL
);


--
-- Name: DigitalCardVerifications; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."DigitalCardVerifications" (
    "Id" uuid NOT NULL,
    "CardId" uuid,
    "Valid" boolean NOT NULL,
    "At" timestamp with time zone NOT NULL
);


--
-- Name: DigitalCards; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."DigitalCards" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "MembershipId" uuid NOT NULL,
    "TokenHash" text NOT NULL,
    "ProtectedToken" text NOT NULL,
    "IssuedAt" timestamp with time zone NOT NULL,
    "RevokedAt" timestamp with time zone
);


--
-- Name: DocumentVersions; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."DocumentVersions" (
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
    "UpdatedAt" timestamp with time zone NOT NULL
);


--
-- Name: EventAudiences; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."EventAudiences" (
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
    "RegistrationStatus" text NOT NULL
);


--
-- Name: EventCheckins; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."EventCheckins" (
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
    CONSTRAINT "CK_Event_Correction" CHECK (((NOT "Voided") OR (length("CorrectionReason") >= 5)))
);


--
-- Name: EventCohosts; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."EventCohosts" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "EventId" uuid NOT NULL,
    "OrgId" uuid NOT NULL
);


--
-- Name: EventFeedbackForms; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."EventFeedbackForms" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "EventId" uuid NOT NULL,
    "Questions" text NOT NULL,
    "Anonymous" boolean NOT NULL,
    "Deadline" timestamp with time zone NOT NULL
);


--
-- Name: EventFeedbackResponses; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."EventFeedbackResponses" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "EventId" uuid NOT NULL,
    "RegistrationId" uuid,
    "Answers" text NOT NULL,
    "Day" date NOT NULL
);


--
-- Name: EventInvitations; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."EventInvitations" (
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
    "RespondedAt" timestamp with time zone
);


--
-- Name: EventNotices; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."EventNotices" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "EventId" uuid NOT NULL,
    "Message" text NOT NULL,
    "Audience" text NOT NULL,
    "Cursor" uuid,
    "Completed" boolean NOT NULL
);


--
-- Name: EventRegistrations; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."EventRegistrations" (
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
    CONSTRAINT "CK_Event_Registration" CHECK ((("Companions" >= 0) AND ("Status" = ANY (ARRAY['Pending'::text, 'Awaiting Approval'::text, 'Confirmed'::text, 'Waitlisted'::text, 'Offered'::text, 'Cancelled'::text, 'Rejected'::text]))))
);


--
-- Name: EventRsvpHistory; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."EventRsvpHistory" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "InvitationId" uuid NOT NULL,
    "Response" text NOT NULL,
    "At" timestamp with time zone NOT NULL
);


--
-- Name: EventSavedFilters; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."EventSavedFilters" (
    "Id" uuid NOT NULL,
    "UserId" uuid NOT NULL,
    "Name" text NOT NULL,
    "Filters" text NOT NULL
);


--
-- Name: EventSessionBookings; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."EventSessionBookings" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "EventId" uuid NOT NULL,
    "SessionId" uuid NOT NULL,
    "RegistrationId" uuid NOT NULL,
    "Active" boolean NOT NULL
);


--
-- Name: EventSessions; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."EventSessions" (
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
    CONSTRAINT "CK_Event_Session" CHECK ((("StartsAt" < "EndsAt") AND ("Capacity" > 0)))
);


--
-- Name: EventStaff; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."EventStaff" (
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
    "Version" bigint NOT NULL
);


--
-- Name: EventTickets; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."EventTickets" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "EventId" uuid NOT NULL,
    "RegistrationId" uuid NOT NULL,
    "TokenHash" text NOT NULL,
    "ProtectedToken" text NOT NULL,
    "Revoked" boolean NOT NULL,
    "IssuedAt" timestamp with time zone NOT NULL
);


--
-- Name: EventTypes; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."EventTypes" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "Name" text NOT NULL,
    "Active" boolean NOT NULL,
    "DefaultCapacity" integer NOT NULL
);


--
-- Name: Events; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."Events" (
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
    "Latitude" double precision,
    "Longitude" double precision,
    CONSTRAINT "CK_Event_Config" CHECK ((("StartsAt" < "EndsAt") AND ("RegistrationStartsAt" < "RegistrationEndsAt") AND ("RegistrationEndsAt" <= "EndsAt") AND ("Capacity" > 0) AND (("MaxCompanions" >= 0) AND ("MaxCompanions" <= 10)) AND ("Approval" = ANY (ARRAY['Draft'::text, 'Pending Approval'::text, 'Changes Requested'::text, 'Rejected'::text, 'Approved'::text])) AND ("Publication" = ANY (ARRAY['Draft'::text, 'Published'::text])) AND ("Lifecycle" = ANY (ARRAY['Scheduled'::text, 'In Progress'::text, 'Completed'::text, 'Cancelled'::text, 'Archived'::text])))),
    CONSTRAINT "CK_Event_Location" CHECK (((("Latitude" IS NULL) AND ("Longitude" IS NULL)) OR ((("Latitude" >= ('-90'::integer)::double precision) AND ("Latitude" <= (90)::double precision)) AND (("Longitude" >= ('-180'::integer)::double precision) AND ("Longitude" <= (180)::double precision)))))
);


--
-- Name: GovernanceDelegations; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."GovernanceDelegations" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "OrgId" uuid NOT NULL,
    "RoleId" uuid NOT NULL,
    "DelegatorId" uuid NOT NULL,
    "DelegateId" uuid NOT NULL,
    "StartsAt" timestamp with time zone NOT NULL,
    "EndsAt" timestamp with time zone NOT NULL,
    "Revoked" boolean NOT NULL
);


--
-- Name: GovernanceDocuments; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."GovernanceDocuments" (
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
    "EventId" uuid,
    "ApprovalRequestId" uuid,
    "CurrentVersion" integer DEFAULT 0 NOT NULL,
    "Folder" text DEFAULT ''::text NOT NULL,
    "ObjectKey" text DEFAULT ''::text NOT NULL,
    "OperationId" uuid,
    "SharedUsers" text DEFAULT ''::text NOT NULL,
    "Tags" text DEFAULT ''::text NOT NULL,
    "Visibility" text DEFAULT ''::text NOT NULL,
    CONSTRAINT "CK_Governance_Document" CHECK (((octet_length("Content") <= 5242880) AND ((octet_length("Content") > 0) OR ("ObjectKey" ~ '^[0-9a-f]{32}$'::text) OR ("Status" = 'Expired'::text)) AND ("ContentType" = ANY (ARRAY['application/pdf'::text, 'image/png'::text, 'image/jpeg'::text]))))
);


--
-- Name: GovernanceRequests; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."GovernanceRequests" (
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
    CONSTRAINT "CK_Governance_Status" CHECK (("Status" = ANY (ARRAY['Draft'::text, 'Submitted'::text, 'Under Review'::text, 'Changes Requested'::text, 'Approved'::text, 'Rejected'::text, 'Withdrawn'::text, 'Cancelled'::text])))
);


--
-- Name: GovernanceResolutions; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."GovernanceResolutions" (
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
    "Version" bigint NOT NULL
);


--
-- Name: HandoverChecklistItems; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."HandoverChecklistItems" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "HandoverId" uuid NOT NULL,
    "Label" text NOT NULL,
    "Notes" text NOT NULL,
    "Completed" boolean NOT NULL,
    "DocumentId" uuid
);


--
-- Name: ImpactMetrics; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."ImpactMetrics" (
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
    "UpdatedAt" timestamp with time zone NOT NULL
);


--
-- Name: IssuedCertificates; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."IssuedCertificates" (
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
    "UpdatedAt" timestamp with time zone NOT NULL
);


--
-- Name: LeadershipHandovers; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."LeadershipHandovers" (
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
    "ArchivedAt" timestamp with time zone
);


--
-- Name: LeadershipTerms; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."LeadershipTerms" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "OrgId" uuid NOT NULL,
    "Name" text NOT NULL,
    "Calendar" text NOT NULL,
    "TimeZone" text NOT NULL,
    "StartsAt" timestamp with time zone NOT NULL,
    "EndsAt" timestamp with time zone NOT NULL,
    "Version" bigint NOT NULL,
    CONSTRAINT "CK_Leadership_Term" CHECK ((("StartsAt" < "EndsAt") AND ("Calendar" = ANY (ARRAY['Calendar'::text, 'Fiscal'::text, 'Custom'::text]))))
);


--
-- Name: Levels; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."Levels" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "Name" text NOT NULL,
    "ParentLevelId" uuid,
    "IsClub" boolean NOT NULL
);


--
-- Name: MailDeliveryAttempts; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."MailDeliveryAttempts" (
    "Id" uuid NOT NULL,
    "MailId" uuid NOT NULL,
    "Succeeded" boolean NOT NULL,
    "ErrorCode" text NOT NULL,
    "At" timestamp with time zone NOT NULL
);


--
-- Name: Mails; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."Mails" (
    "Id" uuid NOT NULL,
    "UserId" uuid,
    "Kind" text NOT NULL,
    "TokenHash" text NOT NULL,
    "Link" text NOT NULL,
    "ExpiresAt" timestamp with time zone NOT NULL,
    "Used" boolean NOT NULL,
    "Attempts" integer DEFAULT 0 NOT NULL,
    "NextAttemptAt" timestamp with time zone DEFAULT '-infinity'::timestamp with time zone NOT NULL,
    "SentAt" timestamp with time zone,
    "Body" text DEFAULT ''::text NOT NULL,
    "OrgId" uuid,
    "Recipient" text DEFAULT ''::text NOT NULL,
    "Subject" text DEFAULT ''::text NOT NULL,
    "Suppressed" boolean DEFAULT false NOT NULL,
    "TenantId" uuid,
    "AccountId" uuid,
    "Channel" text DEFAULT 'Email'::text NOT NULL,
    "CommunicationId" uuid,
    "Cost" numeric,
    "CreatedAt" timestamp with time zone,
    "Currency" text DEFAULT ''::text NOT NULL,
    "DeadLetter" boolean DEFAULT false NOT NULL,
    "DeliveryStatus" text DEFAULT 'Pending'::text NOT NULL,
    "Language" text DEFAULT ''::text NOT NULL,
    "LastError" text DEFAULT ''::text NOT NULL,
    "ProviderId" text DEFAULT ''::text NOT NULL,
    "Purpose" text DEFAULT 'Transactional'::text NOT NULL,
    "SenderAssignmentId" uuid,
    "SenderId" uuid,
    "TemplateName" text DEFAULT ''::text NOT NULL,
    "TemplateParameters" text DEFAULT '[]'::text NOT NULL,
    "TemplateVersion" bigint DEFAULT 0 NOT NULL,
    "EventId" uuid
);


--
-- Name: MeetingAttendances; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."MeetingAttendances" (
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
    "UpdatedAt" timestamp with time zone NOT NULL
);


--
-- Name: MeetingMinutes; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."MeetingMinutes" (
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
    "UpdatedAt" timestamp with time zone NOT NULL
);


--
-- Name: MemberActivities; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."MemberActivities" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "PersonId" uuid NOT NULL,
    "ClubId" uuid NOT NULL,
    "ActorId" uuid NOT NULL,
    "Action" text NOT NULL,
    "Detail" text NOT NULL,
    "At" timestamp with time zone NOT NULL
);


--
-- Name: MemberContactRequests; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."MemberContactRequests" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "MembershipId" uuid NOT NULL,
    "RequesterId" uuid NOT NULL,
    "Message" text NOT NULL,
    "Status" text NOT NULL,
    "At" timestamp with time zone NOT NULL
);


--
-- Name: MemberImportJobs; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."MemberImportJobs" (
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
    CONSTRAINT "CK_Import_Size" CHECK ((octet_length("Payload") <= 10485760))
);


--
-- Name: MemberImportRows; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."MemberImportRows" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "JobId" uuid NOT NULL,
    "RowNumber" integer NOT NULL,
    "Data" text NOT NULL,
    "Status" text NOT NULL,
    "Error" text NOT NULL,
    "Candidates" text NOT NULL,
    "ExistingPersonId" uuid,
    "CreateSeparate" boolean NOT NULL
);


--
-- Name: MemberRelationships; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."MemberRelationships" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "PersonId" uuid NOT NULL,
    "RelatedPersonId" uuid NOT NULL,
    "Relationship" text NOT NULL
);


--
-- Name: MemberSavedViews; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."MemberSavedViews" (
    "Id" uuid NOT NULL,
    "UserId" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "Name" text NOT NULL,
    "Configuration" text NOT NULL
);


--
-- Name: MembershipApplications; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."MembershipApplications" (
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
    CONSTRAINT "CK_Application_Status" CHECK (("Status" = ANY (ARRAY['Draft'::text, 'Submitted'::text, 'Under Review'::text, 'Changes Requested'::text, 'Approved'::text, 'Rejected'::text, 'Withdrawn'::text, 'Cancelled'::text])))
);


--
-- Name: MembershipApprovalStages; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."MembershipApprovalStages" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "WorkflowId" uuid NOT NULL,
    "Name" text NOT NULL,
    "Sequence" integer NOT NULL,
    "RequiredApprovals" integer NOT NULL,
    "AllowDelegation" boolean DEFAULT false NOT NULL,
    CONSTRAINT "CK_Stage_Quorum" CHECK (((("RequiredApprovals" >= 1) AND ("RequiredApprovals" <= 20)) AND ("Sequence" > 0)))
);


--
-- Name: MembershipApprovals; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."MembershipApprovals" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "ApplicationId" uuid,
    "StageId" uuid NOT NULL,
    "Revision" integer NOT NULL,
    "ActorId" uuid NOT NULL,
    "Decision" text NOT NULL,
    "Comment" text NOT NULL,
    "At" timestamp with time zone NOT NULL,
    "GovernanceRequestId" uuid,
    "PrincipalUserId" uuid,
    CONSTRAINT "CK_Approval_Target" CHECK ((("ApplicationId" IS NULL) <> ("GovernanceRequestId" IS NULL)))
);


--
-- Name: MembershipCategories; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."MembershipCategories" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "Name" text NOT NULL,
    "Code" text NOT NULL,
    "ValidityMonths" integer,
    "Active" boolean NOT NULL
);


--
-- Name: MembershipDocuments; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."MembershipDocuments" (
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
    CONSTRAINT "CK_Document_Size" CHECK ((((("Status" = 'Expired'::text) AND (octet_length("Content") = 0)) OR (("Status" <> 'Expired'::text) AND ((octet_length("Content") >= 1) AND (octet_length("Content") <= 5242880)))) AND ("ContentType" = ANY (ARRAY['application/pdf'::text, 'image/png'::text, 'image/jpeg'::text]))))
);


--
-- Name: MembershipHistory; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."MembershipHistory" (
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
    "ApprovalId" uuid
);


--
-- Name: MembershipInvitations; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."MembershipInvitations" (
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
    CONSTRAINT "CK_Invitation_Limits" CHECK (((("UsageLimit" >= 1) AND ("UsageLimit" <= 20)) AND (("Uses" >= 0) AND ("Uses" <= "UsageLimit")) AND ("ExpiresAt" > "CreatedAt")))
);


--
-- Name: MembershipPolicies; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."MembershipPolicies" (
    "TenantId" uuid NOT NULL,
    "AllowMultipleClubs" boolean NOT NULL,
    "SourceReviewRequired" boolean NOT NULL,
    "AssociationReviewRequired" boolean NOT NULL,
    "FinancialClearanceRequired" boolean NOT NULL
);


--
-- Name: MembershipRenewals; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."MembershipRenewals" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "MembershipId" uuid NOT NULL,
    "ActorId" uuid NOT NULL,
    "RequestKey" text NOT NULL,
    "PreviousEnd" timestamp with time zone,
    "NewEnd" timestamp with time zone NOT NULL,
    "At" timestamp with time zone NOT NULL
);


--
-- Name: MembershipStageRoles; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."MembershipStageRoles" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "StageId" uuid NOT NULL,
    "RoleId" uuid NOT NULL
);


--
-- Name: MembershipTransfers; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."MembershipTransfers" (
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
    "FinancialCleared" boolean NOT NULL
);


--
-- Name: MembershipWorkflows; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."MembershipWorkflows" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "Version" integer NOT NULL,
    "Active" boolean NOT NULL,
    "Kind" text DEFAULT 'Membership'::text NOT NULL
);


--
-- Name: Memberships; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."Memberships" (
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
    CONSTRAINT "CK_Membership_Dates" CHECK ((("EndsAt" IS NULL) OR ("EndsAt" > "JoinedAt"))),
    CONSTRAINT "CK_Membership_Status" CHECK (("Status" = ANY (ARRAY['Active'::text, 'Inactive'::text, 'Suspended'::text, 'Expired'::text, 'Transferred'::text, 'Resigned'::text, 'Terminated'::text, 'Deceased'::text])))
);


--
-- Name: NotificationTemplates; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."NotificationTemplates" (
    "Id" text NOT NULL,
    "Subject" text NOT NULL,
    "Body" text NOT NULL
);


--
-- Name: OfficerAppointmentHistory; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."OfficerAppointmentHistory" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "AppointmentId" uuid NOT NULL,
    "Status" text NOT NULL,
    "Detail" text NOT NULL,
    "ActorId" uuid,
    "At" timestamp with time zone NOT NULL
);


--
-- Name: OfficerAppointments; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."OfficerAppointments" (
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
    "OrganizationName" text DEFAULT ''::text NOT NULL,
    "PositionTitle" text DEFAULT ''::text NOT NULL,
    CONSTRAINT "CK_Appointment_Term" CHECK ((("StartsAt" < "EndsAt") AND ("EffectiveAt" >= "StartsAt") AND ("EffectiveAt" < "EndsAt") AND ("Status" = ANY (ARRAY['Proposed'::text, 'Pending approval'::text, 'Approved'::text, 'Scheduled'::text, 'Active'::text, 'Completed'::text, 'Revoked'::text, 'Cancelled'::text]))))
);


--
-- Name: OfficerPositions; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."OfficerPositions" (
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
    CONSTRAINT "CK_Position_Rules" CHECK (((("DefaultMonths" >= 1) AND ("DefaultMonths" <= 120)) AND (("MaximumHolders" IS NULL) OR (("MaximumHolders" >= 1) AND ("MaximumHolders" <= 100))) AND ("MinimumMembershipDays" >= 0) AND ((length("Title") >= 1) AND (length("Title") <= 120))))
);


--
-- Name: OperationComments; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."OperationComments" (
    "Id" uuid NOT NULL,
    "TaskId" uuid NOT NULL,
    "AuthorId" uuid NOT NULL,
    "Body" text NOT NULL,
    "TenantId" uuid NOT NULL,
    "OrgId" uuid NOT NULL,
    "Version" bigint NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL
);


--
-- Name: OperationRevisions; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."OperationRevisions" (
    "Id" uuid NOT NULL,
    "Kind" text NOT NULL,
    "RecordId" uuid NOT NULL,
    "Body" text NOT NULL,
    "ActorId" uuid NOT NULL,
    "Status" text NOT NULL,
    "TenantId" uuid NOT NULL,
    "OrgId" uuid NOT NULL,
    "Version" bigint NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL
);


--
-- Name: OperationTasks; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."OperationTasks" (
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
    "UpdatedAt" timestamp with time zone NOT NULL
);


--
-- Name: OrganizationHistory; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."OrganizationHistory" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "OrgId" uuid NOT NULL,
    "Before" text NOT NULL,
    "After" text NOT NULL,
    "ActorId" uuid,
    "At" timestamp with time zone NOT NULL
);


--
-- Name: OrganizationSettings; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."OrganizationSettings" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "OrgId" uuid NOT NULL,
    "Key" text NOT NULL,
    "Value" text NOT NULL
);


--
-- Name: Orgs; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."Orgs" (
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
    "BrandingColor" text DEFAULT '#2563eb'::text NOT NULL,
    "ClubType" text DEFAULT 'Community'::text NOT NULL,
    "Description" text DEFAULT ''::text NOT NULL,
    "FiscalStartMonth" integer DEFAULT 1 NOT NULL,
    "Latitude" double precision,
    "Longitude" double precision,
    "MeetingDay" text DEFAULT ''::text NOT NULL,
    "MeetingSchedule" text DEFAULT ''::text NOT NULL,
    "MeetingVenue" text DEFAULT ''::text NOT NULL,
    "OperatingPolicies" text DEFAULT ''::text NOT NULL,
    "PostalCode" text DEFAULT ''::text NOT NULL,
    "PreferredLanguage" text DEFAULT 'en'::text NOT NULL,
    "Registration" text DEFAULT ''::text NOT NULL,
    CONSTRAINT "CK_Org_Currency" CHECK ((length("Currency") = 3)),
    CONSTRAINT "CK_Org_Identity" CHECK ((((length(TRIM(BOTH FROM "Name")) >= 2) AND (length(TRIM(BOTH FROM "Name")) <= 200)) AND ((length("Code") >= 1) AND (length("Code") <= 40)) AND ("Status" = ANY (ARRAY['Active'::text, 'Pending'::text, 'Archived'::text, 'Proposed'::text, 'Pending approval'::text, 'Suspended'::text, 'Inactive'::text, 'Merged'::text, 'Closed'::text])) AND ("Id" IS DISTINCT FROM "ParentId")))
);


--
-- Name: Permissions; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."Permissions" (
    "Id" text NOT NULL
);


--
-- Name: PersonAddresses; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."PersonAddresses" (
    "Id" uuid NOT NULL,
    "PersonId" uuid NOT NULL,
    "Country" text NOT NULL,
    "State" text NOT NULL,
    "City" text NOT NULL,
    "PostalCode" text NOT NULL,
    "Address" text NOT NULL
);


--
-- Name: PersonContacts; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."PersonContacts" (
    "Id" uuid NOT NULL,
    "PersonId" uuid NOT NULL,
    "Kind" text NOT NULL,
    "Label" text NOT NULL,
    "Value" text NOT NULL,
    "NormalizedValue" text NOT NULL,
    "Verified" boolean NOT NULL
);


--
-- Name: PersonLinkRequests; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."PersonLinkRequests" (
    "Id" uuid NOT NULL,
    "InvitationId" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "PersonId" uuid NOT NULL,
    "UserId" uuid NOT NULL,
    "ClubId" uuid NOT NULL,
    "Status" text NOT NULL,
    "ReviewReason" text NOT NULL,
    "ReviewerId" uuid,
    "At" timestamp with time zone NOT NULL
);


--
-- Name: PersonPrivacy; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."PersonPrivacy" (
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
    "Version" bigint NOT NULL
);


--
-- Name: PersonTags; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."PersonTags" (
    "Id" uuid NOT NULL,
    "PersonId" uuid NOT NULL,
    "Kind" text NOT NULL,
    "Value" text NOT NULL
);


--
-- Name: Persons; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."Persons" (
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
    CONSTRAINT "CK_Person_Name" CHECK ((((length(TRIM(BOTH FROM "LegalName")) >= 2) AND (length(TRIM(BOTH FROM "LegalName")) <= 200)) AND ((length("DisplayName") >= 1) AND (length("DisplayName") <= 200))))
);


--
-- Name: ProjectActivities; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."ProjectActivities" (
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
    "UpdatedAt" timestamp with time zone NOT NULL
);


--
-- Name: ProjectImpacts; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."ProjectImpacts" (
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
    CONSTRAINT "CK_ImpactValue" CHECK (("Value" >= (0)::numeric))
);


--
-- Name: ProjectOrganizations; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."ProjectOrganizations" (
    "Id" uuid NOT NULL,
    "ProjectId" uuid NOT NULL,
    "Contribution" text NOT NULL,
    "EstimatedContribution" numeric NOT NULL,
    "TenantId" uuid NOT NULL,
    "OrgId" uuid NOT NULL,
    "Version" bigint NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL
);


--
-- Name: RegistrationRequests; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."RegistrationRequests" (
    "Id" uuid NOT NULL,
    "Name" text NOT NULL,
    "Email" text NOT NULL,
    "Description" text NOT NULL,
    "Status" text NOT NULL,
    "At" timestamp with time zone NOT NULL
);


--
-- Name: RoleAssignmentHistory; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."RoleAssignmentHistory" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "AssignmentId" uuid NOT NULL,
    "UserId" uuid NOT NULL,
    "RoleId" uuid NOT NULL,
    "OrgId" uuid NOT NULL,
    "RoleName" text NOT NULL,
    "OrganizationName" text NOT NULL,
    "Action" text NOT NULL,
    "At" timestamp with time zone NOT NULL
);


--
-- Name: RolePermissions; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."RolePermissions" (
    "Id" uuid NOT NULL,
    "RoleId" uuid NOT NULL,
    "PermissionId" text NOT NULL
);


--
-- Name: Roles; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."Roles" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "Name" text NOT NULL
);


--
-- Name: ServiceProjects; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."ServiceProjects" (
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
    "UpdatedAt" timestamp with time zone NOT NULL
);


--
-- Name: Sessions; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."Sessions" (
    "Id" text NOT NULL,
    "UserId" uuid NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "ExpiresAt" timestamp with time zone NOT NULL,
    "WorkspaceId" uuid
);


--
-- Name: SystemSettings; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."SystemSettings" (
    "Key" text NOT NULL,
    "Value" text NOT NULL
);


--
-- Name: TenantSettings; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."TenantSettings" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "Key" text NOT NULL,
    "Value" text NOT NULL
);


--
-- Name: Tenants; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."Tenants" (
    "Id" uuid NOT NULL,
    "CreatedAt" timestamp with time zone DEFAULT '-infinity'::timestamp with time zone NOT NULL
);


--
-- Name: TransferDecisions; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."TransferDecisions" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "TransferId" uuid NOT NULL,
    "Stage" text NOT NULL,
    "Decision" text NOT NULL,
    "Comment" text NOT NULL,
    "ActorId" uuid NOT NULL,
    "At" timestamp with time zone NOT NULL
);


--
-- Name: UserProfiles; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."UserProfiles" (
    "Id" uuid NOT NULL,
    "Name" text NOT NULL,
    "Phone" text NOT NULL,
    "Locale" text DEFAULT ''::text NOT NULL,
    "TimeZone" text DEFAULT ''::text NOT NULL,
    "Theme" text DEFAULT ''::text NOT NULL
);


--
-- Name: Users; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."Users" (
    "Id" uuid NOT NULL,
    "Email" text NOT NULL,
    "PasswordHash" text NOT NULL,
    "Verified" boolean NOT NULL,
    "PlatformAdmin" boolean NOT NULL,
    "FailedAttempts" integer NOT NULL,
    "LockedUntil" timestamp with time zone
);


--
-- Name: VolunteerAssignments; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."VolunteerAssignments" (
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
    "UpdatedAt" timestamp with time zone NOT NULL
);


--
-- Name: VolunteerHours; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."VolunteerHours" (
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
    CONSTRAINT "CK_HourInterval" CHECK ((("EndsAt" > "StartsAt") AND ("Hours" >= (0)::numeric) AND ("BreakMinutes" >= 0)))
);


--
-- Name: VolunteerProfiles; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."VolunteerProfiles" (
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
    "UpdatedAt" timestamp with time zone NOT NULL
);


--
-- Name: VolunteerShifts; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."VolunteerShifts" (
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
    CONSTRAINT "CK_ShiftCapacity" CHECK ((("Capacity" > 0) AND ("EndsAt" > "StartsAt")))
);


--
-- Name: __EFMigrationsHistory; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."__EFMigrationsHistory" (
    "MigrationId" character varying(150) NOT NULL,
    "ProductVersion" character varying(32) NOT NULL
);


--
-- Name: membership_numbers; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public.membership_numbers
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: CertificateTemplates AK_CertificateTemplates_TenantId_Id; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."CertificateTemplates"
    ADD CONSTRAINT "AK_CertificateTemplates_TenantId_Id" UNIQUE ("TenantId", "Id");


--
-- Name: ClubHealthConfigurations AK_ClubHealthConfigurations_TenantId_Id; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."ClubHealthConfigurations"
    ADD CONSTRAINT "AK_ClubHealthConfigurations_TenantId_Id" UNIQUE ("TenantId", "Id");


--
-- Name: ClubMeetings AK_ClubMeetings_TenantId_Id; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."ClubMeetings"
    ADD CONSTRAINT "AK_ClubMeetings_TenantId_Id" UNIQUE ("TenantId", "Id");


--
-- Name: CommitteeActivities AK_CommitteeActivities_TenantId_Id; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."CommitteeActivities"
    ADD CONSTRAINT "AK_CommitteeActivities_TenantId_Id" UNIQUE ("TenantId", "Id");


--
-- Name: CommitteeMemberships AK_CommitteeMemberships_TenantId_Id; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."CommitteeMemberships"
    ADD CONSTRAINT "AK_CommitteeMemberships_TenantId_Id" UNIQUE ("TenantId", "Id");


--
-- Name: CommitteeRoles AK_CommitteeRoles_TenantId_Id; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."CommitteeRoles"
    ADD CONSTRAINT "AK_CommitteeRoles_TenantId_Id" UNIQUE ("TenantId", "Id");


--
-- Name: Committees AK_Committees_TenantId_Id; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."Committees"
    ADD CONSTRAINT "AK_Committees_TenantId_Id" UNIQUE ("TenantId", "Id");


--
-- Name: CommunicationAccounts AK_CommunicationAccounts_TenantId_Id; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."CommunicationAccounts"
    ADD CONSTRAINT "AK_CommunicationAccounts_TenantId_Id" UNIQUE ("TenantId", "Id");


--
-- Name: CommunicationAttachments AK_CommunicationAttachments_TenantId_Id; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."CommunicationAttachments"
    ADD CONSTRAINT "AK_CommunicationAttachments_TenantId_Id" UNIQUE ("TenantId", "Id");


--
-- Name: CommunicationAudiences AK_CommunicationAudiences_TenantId_Id; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."CommunicationAudiences"
    ADD CONSTRAINT "AK_CommunicationAudiences_TenantId_Id" UNIQUE ("TenantId", "Id");


--
-- Name: CommunicationAutomations AK_CommunicationAutomations_TenantId_Id; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."CommunicationAutomations"
    ADD CONSTRAINT "AK_CommunicationAutomations_TenantId_Id" UNIQUE ("TenantId", "Id");


--
-- Name: CommunicationBroadcasts AK_CommunicationBroadcasts_TenantId_Id; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."CommunicationBroadcasts"
    ADD CONSTRAINT "AK_CommunicationBroadcasts_TenantId_Id" UNIQUE ("TenantId", "Id");


--
-- Name: CommunicationExecutions AK_CommunicationExecutions_TenantId_Id; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."CommunicationExecutions"
    ADD CONSTRAINT "AK_CommunicationExecutions_TenantId_Id" UNIQUE ("TenantId", "Id");


--
-- Name: CommunicationMessages AK_CommunicationMessages_TenantId_Id; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."CommunicationMessages"
    ADD CONSTRAINT "AK_CommunicationMessages_TenantId_Id" UNIQUE ("TenantId", "Id");


--
-- Name: CommunicationPolicies AK_CommunicationPolicies_TenantId_Id; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."CommunicationPolicies"
    ADD CONSTRAINT "AK_CommunicationPolicies_TenantId_Id" UNIQUE ("TenantId", "Id");


--
-- Name: CommunicationProviderEvents AK_CommunicationProviderEvents_TenantId_Id; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."CommunicationProviderEvents"
    ADD CONSTRAINT "AK_CommunicationProviderEvents_TenantId_Id" UNIQUE ("TenantId", "Id");


--
-- Name: CommunicationRecipients AK_CommunicationRecipients_TenantId_Id; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."CommunicationRecipients"
    ADD CONSTRAINT "AK_CommunicationRecipients_TenantId_Id" UNIQUE ("TenantId", "Id");


--
-- Name: CommunicationRevisions AK_CommunicationRevisions_TenantId_Id; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."CommunicationRevisions"
    ADD CONSTRAINT "AK_CommunicationRevisions_TenantId_Id" UNIQUE ("TenantId", "Id");


--
-- Name: CommunicationSuppressions AK_CommunicationSuppressions_TenantId_Id; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."CommunicationSuppressions"
    ADD CONSTRAINT "AK_CommunicationSuppressions_TenantId_Id" UNIQUE ("TenantId", "Id");


--
-- Name: CommunicationTemplates AK_CommunicationTemplates_TenantId_Id; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."CommunicationTemplates"
    ADD CONSTRAINT "AK_CommunicationTemplates_TenantId_Id" UNIQUE ("TenantId", "Id");


--
-- Name: ConversationParticipants AK_ConversationParticipants_TenantId_Id; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."ConversationParticipants"
    ADD CONSTRAINT "AK_ConversationParticipants_TenantId_Id" UNIQUE ("TenantId", "Id");


--
-- Name: Conversations AK_Conversations_TenantId_Id; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."Conversations"
    ADD CONSTRAINT "AK_Conversations_TenantId_Id" UNIQUE ("TenantId", "Id");


--
-- Name: DigitalCards AK_DigitalCards_TenantId_Id; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."DigitalCards"
    ADD CONSTRAINT "AK_DigitalCards_TenantId_Id" UNIQUE ("TenantId", "Id");


--
-- Name: DocumentVersions AK_DocumentVersions_TenantId_Id; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."DocumentVersions"
    ADD CONSTRAINT "AK_DocumentVersions_TenantId_Id" UNIQUE ("TenantId", "Id");


--
-- Name: EventAudiences AK_EventAudiences_TenantId_Id; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."EventAudiences"
    ADD CONSTRAINT "AK_EventAudiences_TenantId_Id" UNIQUE ("TenantId", "Id");


--
-- Name: EventCheckins AK_EventCheckins_TenantId_Id; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."EventCheckins"
    ADD CONSTRAINT "AK_EventCheckins_TenantId_Id" UNIQUE ("TenantId", "Id");


--
-- Name: EventCohosts AK_EventCohosts_TenantId_Id; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."EventCohosts"
    ADD CONSTRAINT "AK_EventCohosts_TenantId_Id" UNIQUE ("TenantId", "Id");


--
-- Name: EventFeedbackForms AK_EventFeedbackForms_TenantId_Id; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."EventFeedbackForms"
    ADD CONSTRAINT "AK_EventFeedbackForms_TenantId_Id" UNIQUE ("TenantId", "Id");


--
-- Name: EventFeedbackResponses AK_EventFeedbackResponses_TenantId_Id; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."EventFeedbackResponses"
    ADD CONSTRAINT "AK_EventFeedbackResponses_TenantId_Id" UNIQUE ("TenantId", "Id");


--
-- Name: EventInvitations AK_EventInvitations_TenantId_Id; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."EventInvitations"
    ADD CONSTRAINT "AK_EventInvitations_TenantId_Id" UNIQUE ("TenantId", "Id");


--
-- Name: EventNotices AK_EventNotices_TenantId_Id; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."EventNotices"
    ADD CONSTRAINT "AK_EventNotices_TenantId_Id" UNIQUE ("TenantId", "Id");


--
-- Name: EventRegistrations AK_EventRegistrations_TenantId_Id; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."EventRegistrations"
    ADD CONSTRAINT "AK_EventRegistrations_TenantId_Id" UNIQUE ("TenantId", "Id");


--
-- Name: EventRsvpHistory AK_EventRsvpHistory_TenantId_Id; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."EventRsvpHistory"
    ADD CONSTRAINT "AK_EventRsvpHistory_TenantId_Id" UNIQUE ("TenantId", "Id");


--
-- Name: EventSessionBookings AK_EventSessionBookings_TenantId_Id; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."EventSessionBookings"
    ADD CONSTRAINT "AK_EventSessionBookings_TenantId_Id" UNIQUE ("TenantId", "Id");


--
-- Name: EventSessions AK_EventSessions_TenantId_Id; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."EventSessions"
    ADD CONSTRAINT "AK_EventSessions_TenantId_Id" UNIQUE ("TenantId", "Id");


--
-- Name: EventStaff AK_EventStaff_TenantId_Id; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."EventStaff"
    ADD CONSTRAINT "AK_EventStaff_TenantId_Id" UNIQUE ("TenantId", "Id");


--
-- Name: EventTickets AK_EventTickets_TenantId_Id; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."EventTickets"
    ADD CONSTRAINT "AK_EventTickets_TenantId_Id" UNIQUE ("TenantId", "Id");


--
-- Name: EventTypes AK_EventTypes_TenantId_Id; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."EventTypes"
    ADD CONSTRAINT "AK_EventTypes_TenantId_Id" UNIQUE ("TenantId", "Id");


--
-- Name: Events AK_Events_TenantId_Id; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."Events"
    ADD CONSTRAINT "AK_Events_TenantId_Id" UNIQUE ("TenantId", "Id");


--
-- Name: GovernanceDelegations AK_GovernanceDelegations_TenantId_Id; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."GovernanceDelegations"
    ADD CONSTRAINT "AK_GovernanceDelegations_TenantId_Id" UNIQUE ("TenantId", "Id");


--
-- Name: GovernanceDocuments AK_GovernanceDocuments_TenantId_Id; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."GovernanceDocuments"
    ADD CONSTRAINT "AK_GovernanceDocuments_TenantId_Id" UNIQUE ("TenantId", "Id");


--
-- Name: GovernanceRequests AK_GovernanceRequests_TenantId_Id; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."GovernanceRequests"
    ADD CONSTRAINT "AK_GovernanceRequests_TenantId_Id" UNIQUE ("TenantId", "Id");


--
-- Name: GovernanceResolutions AK_GovernanceResolutions_TenantId_Id; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."GovernanceResolutions"
    ADD CONSTRAINT "AK_GovernanceResolutions_TenantId_Id" UNIQUE ("TenantId", "Id");


--
-- Name: HandoverChecklistItems AK_HandoverChecklistItems_TenantId_Id; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."HandoverChecklistItems"
    ADD CONSTRAINT "AK_HandoverChecklistItems_TenantId_Id" UNIQUE ("TenantId", "Id");


--
-- Name: ImpactMetrics AK_ImpactMetrics_TenantId_Id; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."ImpactMetrics"
    ADD CONSTRAINT "AK_ImpactMetrics_TenantId_Id" UNIQUE ("TenantId", "Id");


--
-- Name: IssuedCertificates AK_IssuedCertificates_TenantId_Id; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."IssuedCertificates"
    ADD CONSTRAINT "AK_IssuedCertificates_TenantId_Id" UNIQUE ("TenantId", "Id");


--
-- Name: LeadershipHandovers AK_LeadershipHandovers_TenantId_Id; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."LeadershipHandovers"
    ADD CONSTRAINT "AK_LeadershipHandovers_TenantId_Id" UNIQUE ("TenantId", "Id");


--
-- Name: LeadershipTerms AK_LeadershipTerms_TenantId_Id; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."LeadershipTerms"
    ADD CONSTRAINT "AK_LeadershipTerms_TenantId_Id" UNIQUE ("TenantId", "Id");


--
-- Name: Levels AK_Levels_TenantId_Id; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."Levels"
    ADD CONSTRAINT "AK_Levels_TenantId_Id" UNIQUE ("TenantId", "Id");


--
-- Name: MeetingAttendances AK_MeetingAttendances_TenantId_Id; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."MeetingAttendances"
    ADD CONSTRAINT "AK_MeetingAttendances_TenantId_Id" UNIQUE ("TenantId", "Id");


--
-- Name: MeetingMinutes AK_MeetingMinutes_TenantId_Id; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."MeetingMinutes"
    ADD CONSTRAINT "AK_MeetingMinutes_TenantId_Id" UNIQUE ("TenantId", "Id");


--
-- Name: MemberActivities AK_MemberActivities_TenantId_Id; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."MemberActivities"
    ADD CONSTRAINT "AK_MemberActivities_TenantId_Id" UNIQUE ("TenantId", "Id");


--
-- Name: MemberContactRequests AK_MemberContactRequests_TenantId_Id; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."MemberContactRequests"
    ADD CONSTRAINT "AK_MemberContactRequests_TenantId_Id" UNIQUE ("TenantId", "Id");


--
-- Name: MemberImportJobs AK_MemberImportJobs_TenantId_Id; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."MemberImportJobs"
    ADD CONSTRAINT "AK_MemberImportJobs_TenantId_Id" UNIQUE ("TenantId", "Id");


--
-- Name: MemberImportRows AK_MemberImportRows_TenantId_Id; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."MemberImportRows"
    ADD CONSTRAINT "AK_MemberImportRows_TenantId_Id" UNIQUE ("TenantId", "Id");


--
-- Name: MemberRelationships AK_MemberRelationships_TenantId_Id; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."MemberRelationships"
    ADD CONSTRAINT "AK_MemberRelationships_TenantId_Id" UNIQUE ("TenantId", "Id");


--
-- Name: MembershipApplications AK_MembershipApplications_TenantId_Id; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."MembershipApplications"
    ADD CONSTRAINT "AK_MembershipApplications_TenantId_Id" UNIQUE ("TenantId", "Id");


--
-- Name: MembershipApprovalStages AK_MembershipApprovalStages_TenantId_Id; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."MembershipApprovalStages"
    ADD CONSTRAINT "AK_MembershipApprovalStages_TenantId_Id" UNIQUE ("TenantId", "Id");


--
-- Name: MembershipApprovals AK_MembershipApprovals_TenantId_Id; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."MembershipApprovals"
    ADD CONSTRAINT "AK_MembershipApprovals_TenantId_Id" UNIQUE ("TenantId", "Id");


--
-- Name: MembershipCategories AK_MembershipCategories_TenantId_Id; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."MembershipCategories"
    ADD CONSTRAINT "AK_MembershipCategories_TenantId_Id" UNIQUE ("TenantId", "Id");


--
-- Name: MembershipDocuments AK_MembershipDocuments_TenantId_Id; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."MembershipDocuments"
    ADD CONSTRAINT "AK_MembershipDocuments_TenantId_Id" UNIQUE ("TenantId", "Id");


--
-- Name: MembershipHistory AK_MembershipHistory_TenantId_Id; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."MembershipHistory"
    ADD CONSTRAINT "AK_MembershipHistory_TenantId_Id" UNIQUE ("TenantId", "Id");


--
-- Name: MembershipInvitations AK_MembershipInvitations_TenantId_Id; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."MembershipInvitations"
    ADD CONSTRAINT "AK_MembershipInvitations_TenantId_Id" UNIQUE ("TenantId", "Id");


--
-- Name: MembershipRenewals AK_MembershipRenewals_TenantId_Id; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."MembershipRenewals"
    ADD CONSTRAINT "AK_MembershipRenewals_TenantId_Id" UNIQUE ("TenantId", "Id");


--
-- Name: MembershipTransfers AK_MembershipTransfers_TenantId_Id; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."MembershipTransfers"
    ADD CONSTRAINT "AK_MembershipTransfers_TenantId_Id" UNIQUE ("TenantId", "Id");


--
-- Name: MembershipWorkflows AK_MembershipWorkflows_TenantId_Id; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."MembershipWorkflows"
    ADD CONSTRAINT "AK_MembershipWorkflows_TenantId_Id" UNIQUE ("TenantId", "Id");


--
-- Name: Memberships AK_Memberships_TenantId_Id; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."Memberships"
    ADD CONSTRAINT "AK_Memberships_TenantId_Id" UNIQUE ("TenantId", "Id");


--
-- Name: OfficerAppointmentHistory AK_OfficerAppointmentHistory_TenantId_Id; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."OfficerAppointmentHistory"
    ADD CONSTRAINT "AK_OfficerAppointmentHistory_TenantId_Id" UNIQUE ("TenantId", "Id");


--
-- Name: OfficerAppointments AK_OfficerAppointments_TenantId_Id; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."OfficerAppointments"
    ADD CONSTRAINT "AK_OfficerAppointments_TenantId_Id" UNIQUE ("TenantId", "Id");


--
-- Name: OfficerPositions AK_OfficerPositions_TenantId_Id; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."OfficerPositions"
    ADD CONSTRAINT "AK_OfficerPositions_TenantId_Id" UNIQUE ("TenantId", "Id");


--
-- Name: OperationComments AK_OperationComments_TenantId_Id; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."OperationComments"
    ADD CONSTRAINT "AK_OperationComments_TenantId_Id" UNIQUE ("TenantId", "Id");


--
-- Name: OperationRevisions AK_OperationRevisions_TenantId_Id; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."OperationRevisions"
    ADD CONSTRAINT "AK_OperationRevisions_TenantId_Id" UNIQUE ("TenantId", "Id");


--
-- Name: OperationTasks AK_OperationTasks_TenantId_Id; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."OperationTasks"
    ADD CONSTRAINT "AK_OperationTasks_TenantId_Id" UNIQUE ("TenantId", "Id");


--
-- Name: OrganizationHistory AK_OrganizationHistory_TenantId_Id; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."OrganizationHistory"
    ADD CONSTRAINT "AK_OrganizationHistory_TenantId_Id" UNIQUE ("TenantId", "Id");


--
-- Name: Orgs AK_Orgs_TenantId_Id; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."Orgs"
    ADD CONSTRAINT "AK_Orgs_TenantId_Id" UNIQUE ("TenantId", "Id");


--
-- Name: PersonLinkRequests AK_PersonLinkRequests_TenantId_Id; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."PersonLinkRequests"
    ADD CONSTRAINT "AK_PersonLinkRequests_TenantId_Id" UNIQUE ("TenantId", "Id");


--
-- Name: ProjectActivities AK_ProjectActivities_TenantId_Id; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."ProjectActivities"
    ADD CONSTRAINT "AK_ProjectActivities_TenantId_Id" UNIQUE ("TenantId", "Id");


--
-- Name: ProjectImpacts AK_ProjectImpacts_TenantId_Id; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."ProjectImpacts"
    ADD CONSTRAINT "AK_ProjectImpacts_TenantId_Id" UNIQUE ("TenantId", "Id");


--
-- Name: ProjectOrganizations AK_ProjectOrganizations_TenantId_Id; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."ProjectOrganizations"
    ADD CONSTRAINT "AK_ProjectOrganizations_TenantId_Id" UNIQUE ("TenantId", "Id");


--
-- Name: RoleAssignmentHistory AK_RoleAssignmentHistory_TenantId_Id; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."RoleAssignmentHistory"
    ADD CONSTRAINT "AK_RoleAssignmentHistory_TenantId_Id" UNIQUE ("TenantId", "Id");


--
-- Name: Roles AK_Roles_TenantId_Id; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."Roles"
    ADD CONSTRAINT "AK_Roles_TenantId_Id" UNIQUE ("TenantId", "Id");


--
-- Name: ServiceProjects AK_ServiceProjects_TenantId_Id; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."ServiceProjects"
    ADD CONSTRAINT "AK_ServiceProjects_TenantId_Id" UNIQUE ("TenantId", "Id");


--
-- Name: TransferDecisions AK_TransferDecisions_TenantId_Id; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."TransferDecisions"
    ADD CONSTRAINT "AK_TransferDecisions_TenantId_Id" UNIQUE ("TenantId", "Id");


--
-- Name: VolunteerAssignments AK_VolunteerAssignments_TenantId_Id; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."VolunteerAssignments"
    ADD CONSTRAINT "AK_VolunteerAssignments_TenantId_Id" UNIQUE ("TenantId", "Id");


--
-- Name: VolunteerHours AK_VolunteerHours_TenantId_Id; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."VolunteerHours"
    ADD CONSTRAINT "AK_VolunteerHours_TenantId_Id" UNIQUE ("TenantId", "Id");


--
-- Name: VolunteerProfiles AK_VolunteerProfiles_TenantId_Id; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."VolunteerProfiles"
    ADD CONSTRAINT "AK_VolunteerProfiles_TenantId_Id" UNIQUE ("TenantId", "Id");


--
-- Name: VolunteerShifts AK_VolunteerShifts_TenantId_Id; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."VolunteerShifts"
    ADD CONSTRAINT "AK_VolunteerShifts_TenantId_Id" UNIQUE ("TenantId", "Id");


--
-- Name: Assignments PK_Assignments; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."Assignments"
    ADD CONSTRAINT "PK_Assignments" PRIMARY KEY ("Id");


--
-- Name: Associations PK_Associations; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."Associations"
    ADD CONSTRAINT "PK_Associations" PRIMARY KEY ("Id");


--
-- Name: Audits PK_Audits; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."Audits"
    ADD CONSTRAINT "PK_Audits" PRIMARY KEY ("Id");


--
-- Name: AuthenticationAudits PK_AuthenticationAudits; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."AuthenticationAudits"
    ADD CONSTRAINT "PK_AuthenticationAudits" PRIMARY KEY ("Id");


--
-- Name: CertificateTemplates PK_CertificateTemplates; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."CertificateTemplates"
    ADD CONSTRAINT "PK_CertificateTemplates" PRIMARY KEY ("Id");


--
-- Name: ClubHealthConfigurations PK_ClubHealthConfigurations; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."ClubHealthConfigurations"
    ADD CONSTRAINT "PK_ClubHealthConfigurations" PRIMARY KEY ("Id");


--
-- Name: ClubMeetings PK_ClubMeetings; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."ClubMeetings"
    ADD CONSTRAINT "PK_ClubMeetings" PRIMARY KEY ("Id");


--
-- Name: CommitteeActivities PK_CommitteeActivities; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."CommitteeActivities"
    ADD CONSTRAINT "PK_CommitteeActivities" PRIMARY KEY ("Id");


--
-- Name: CommitteeMemberships PK_CommitteeMemberships; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."CommitteeMemberships"
    ADD CONSTRAINT "PK_CommitteeMemberships" PRIMARY KEY ("Id");


--
-- Name: CommitteeRoles PK_CommitteeRoles; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."CommitteeRoles"
    ADD CONSTRAINT "PK_CommitteeRoles" PRIMARY KEY ("Id");


--
-- Name: Committees PK_Committees; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."Committees"
    ADD CONSTRAINT "PK_Committees" PRIMARY KEY ("Id");


--
-- Name: CommunicationAccounts PK_CommunicationAccounts; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."CommunicationAccounts"
    ADD CONSTRAINT "PK_CommunicationAccounts" PRIMARY KEY ("Id");


--
-- Name: CommunicationAttachments PK_CommunicationAttachments; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."CommunicationAttachments"
    ADD CONSTRAINT "PK_CommunicationAttachments" PRIMARY KEY ("Id");


--
-- Name: CommunicationAudiences PK_CommunicationAudiences; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."CommunicationAudiences"
    ADD CONSTRAINT "PK_CommunicationAudiences" PRIMARY KEY ("Id");


--
-- Name: CommunicationAutomations PK_CommunicationAutomations; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."CommunicationAutomations"
    ADD CONSTRAINT "PK_CommunicationAutomations" PRIMARY KEY ("Id");


--
-- Name: CommunicationBroadcasts PK_CommunicationBroadcasts; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."CommunicationBroadcasts"
    ADD CONSTRAINT "PK_CommunicationBroadcasts" PRIMARY KEY ("Id");


--
-- Name: CommunicationExecutions PK_CommunicationExecutions; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."CommunicationExecutions"
    ADD CONSTRAINT "PK_CommunicationExecutions" PRIMARY KEY ("Id");


--
-- Name: CommunicationMessages PK_CommunicationMessages; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."CommunicationMessages"
    ADD CONSTRAINT "PK_CommunicationMessages" PRIMARY KEY ("Id");


--
-- Name: CommunicationPolicies PK_CommunicationPolicies; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."CommunicationPolicies"
    ADD CONSTRAINT "PK_CommunicationPolicies" PRIMARY KEY ("Id");


--
-- Name: CommunicationPreferences PK_CommunicationPreferences; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."CommunicationPreferences"
    ADD CONSTRAINT "PK_CommunicationPreferences" PRIMARY KEY ("Id");


--
-- Name: CommunicationProviderEvents PK_CommunicationProviderEvents; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."CommunicationProviderEvents"
    ADD CONSTRAINT "PK_CommunicationProviderEvents" PRIMARY KEY ("Id");


--
-- Name: CommunicationRecipients PK_CommunicationRecipients; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."CommunicationRecipients"
    ADD CONSTRAINT "PK_CommunicationRecipients" PRIMARY KEY ("Id");


--
-- Name: CommunicationRevisions PK_CommunicationRevisions; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."CommunicationRevisions"
    ADD CONSTRAINT "PK_CommunicationRevisions" PRIMARY KEY ("Id");


--
-- Name: CommunicationSuppressions PK_CommunicationSuppressions; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."CommunicationSuppressions"
    ADD CONSTRAINT "PK_CommunicationSuppressions" PRIMARY KEY ("Id");


--
-- Name: CommunicationTemplates PK_CommunicationTemplates; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."CommunicationTemplates"
    ADD CONSTRAINT "PK_CommunicationTemplates" PRIMARY KEY ("Id");


--
-- Name: ConversationParticipants PK_ConversationParticipants; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."ConversationParticipants"
    ADD CONSTRAINT "PK_ConversationParticipants" PRIMARY KEY ("Id");


--
-- Name: Conversations PK_Conversations; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."Conversations"
    ADD CONSTRAINT "PK_Conversations" PRIMARY KEY ("Id");


--
-- Name: DigitalCardVerifications PK_DigitalCardVerifications; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."DigitalCardVerifications"
    ADD CONSTRAINT "PK_DigitalCardVerifications" PRIMARY KEY ("Id");


--
-- Name: DigitalCards PK_DigitalCards; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."DigitalCards"
    ADD CONSTRAINT "PK_DigitalCards" PRIMARY KEY ("Id");


--
-- Name: DocumentVersions PK_DocumentVersions; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."DocumentVersions"
    ADD CONSTRAINT "PK_DocumentVersions" PRIMARY KEY ("Id");


--
-- Name: EventAudiences PK_EventAudiences; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."EventAudiences"
    ADD CONSTRAINT "PK_EventAudiences" PRIMARY KEY ("Id");


--
-- Name: EventCheckins PK_EventCheckins; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."EventCheckins"
    ADD CONSTRAINT "PK_EventCheckins" PRIMARY KEY ("Id");


--
-- Name: EventCohosts PK_EventCohosts; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."EventCohosts"
    ADD CONSTRAINT "PK_EventCohosts" PRIMARY KEY ("Id");


--
-- Name: EventFeedbackForms PK_EventFeedbackForms; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."EventFeedbackForms"
    ADD CONSTRAINT "PK_EventFeedbackForms" PRIMARY KEY ("Id");


--
-- Name: EventFeedbackResponses PK_EventFeedbackResponses; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."EventFeedbackResponses"
    ADD CONSTRAINT "PK_EventFeedbackResponses" PRIMARY KEY ("Id");


--
-- Name: EventInvitations PK_EventInvitations; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."EventInvitations"
    ADD CONSTRAINT "PK_EventInvitations" PRIMARY KEY ("Id");


--
-- Name: EventNotices PK_EventNotices; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."EventNotices"
    ADD CONSTRAINT "PK_EventNotices" PRIMARY KEY ("Id");


--
-- Name: EventRegistrations PK_EventRegistrations; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."EventRegistrations"
    ADD CONSTRAINT "PK_EventRegistrations" PRIMARY KEY ("Id");


--
-- Name: EventRsvpHistory PK_EventRsvpHistory; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."EventRsvpHistory"
    ADD CONSTRAINT "PK_EventRsvpHistory" PRIMARY KEY ("Id");


--
-- Name: EventSavedFilters PK_EventSavedFilters; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."EventSavedFilters"
    ADD CONSTRAINT "PK_EventSavedFilters" PRIMARY KEY ("Id");


--
-- Name: EventSessionBookings PK_EventSessionBookings; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."EventSessionBookings"
    ADD CONSTRAINT "PK_EventSessionBookings" PRIMARY KEY ("Id");


--
-- Name: EventSessions PK_EventSessions; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."EventSessions"
    ADD CONSTRAINT "PK_EventSessions" PRIMARY KEY ("Id");


--
-- Name: EventStaff PK_EventStaff; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."EventStaff"
    ADD CONSTRAINT "PK_EventStaff" PRIMARY KEY ("Id");


--
-- Name: EventTickets PK_EventTickets; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."EventTickets"
    ADD CONSTRAINT "PK_EventTickets" PRIMARY KEY ("Id");


--
-- Name: EventTypes PK_EventTypes; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."EventTypes"
    ADD CONSTRAINT "PK_EventTypes" PRIMARY KEY ("Id");


--
-- Name: Events PK_Events; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."Events"
    ADD CONSTRAINT "PK_Events" PRIMARY KEY ("Id");


--
-- Name: GovernanceDelegations PK_GovernanceDelegations; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."GovernanceDelegations"
    ADD CONSTRAINT "PK_GovernanceDelegations" PRIMARY KEY ("Id");


--
-- Name: GovernanceDocuments PK_GovernanceDocuments; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."GovernanceDocuments"
    ADD CONSTRAINT "PK_GovernanceDocuments" PRIMARY KEY ("Id");


--
-- Name: GovernanceRequests PK_GovernanceRequests; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."GovernanceRequests"
    ADD CONSTRAINT "PK_GovernanceRequests" PRIMARY KEY ("Id");


--
-- Name: GovernanceResolutions PK_GovernanceResolutions; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."GovernanceResolutions"
    ADD CONSTRAINT "PK_GovernanceResolutions" PRIMARY KEY ("Id");


--
-- Name: HandoverChecklistItems PK_HandoverChecklistItems; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."HandoverChecklistItems"
    ADD CONSTRAINT "PK_HandoverChecklistItems" PRIMARY KEY ("Id");


--
-- Name: ImpactMetrics PK_ImpactMetrics; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."ImpactMetrics"
    ADD CONSTRAINT "PK_ImpactMetrics" PRIMARY KEY ("Id");


--
-- Name: IssuedCertificates PK_IssuedCertificates; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."IssuedCertificates"
    ADD CONSTRAINT "PK_IssuedCertificates" PRIMARY KEY ("Id");


--
-- Name: LeadershipHandovers PK_LeadershipHandovers; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."LeadershipHandovers"
    ADD CONSTRAINT "PK_LeadershipHandovers" PRIMARY KEY ("Id");


--
-- Name: LeadershipTerms PK_LeadershipTerms; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."LeadershipTerms"
    ADD CONSTRAINT "PK_LeadershipTerms" PRIMARY KEY ("Id");


--
-- Name: Levels PK_Levels; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."Levels"
    ADD CONSTRAINT "PK_Levels" PRIMARY KEY ("Id");


--
-- Name: MailDeliveryAttempts PK_MailDeliveryAttempts; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."MailDeliveryAttempts"
    ADD CONSTRAINT "PK_MailDeliveryAttempts" PRIMARY KEY ("Id");


--
-- Name: Mails PK_Mails; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."Mails"
    ADD CONSTRAINT "PK_Mails" PRIMARY KEY ("Id");


--
-- Name: MeetingAttendances PK_MeetingAttendances; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."MeetingAttendances"
    ADD CONSTRAINT "PK_MeetingAttendances" PRIMARY KEY ("Id");


--
-- Name: MeetingMinutes PK_MeetingMinutes; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."MeetingMinutes"
    ADD CONSTRAINT "PK_MeetingMinutes" PRIMARY KEY ("Id");


--
-- Name: MemberActivities PK_MemberActivities; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."MemberActivities"
    ADD CONSTRAINT "PK_MemberActivities" PRIMARY KEY ("Id");


--
-- Name: MemberContactRequests PK_MemberContactRequests; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."MemberContactRequests"
    ADD CONSTRAINT "PK_MemberContactRequests" PRIMARY KEY ("Id");


--
-- Name: MemberImportJobs PK_MemberImportJobs; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."MemberImportJobs"
    ADD CONSTRAINT "PK_MemberImportJobs" PRIMARY KEY ("Id");


--
-- Name: MemberImportRows PK_MemberImportRows; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."MemberImportRows"
    ADD CONSTRAINT "PK_MemberImportRows" PRIMARY KEY ("Id");


--
-- Name: MemberRelationships PK_MemberRelationships; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."MemberRelationships"
    ADD CONSTRAINT "PK_MemberRelationships" PRIMARY KEY ("Id");


--
-- Name: MemberSavedViews PK_MemberSavedViews; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."MemberSavedViews"
    ADD CONSTRAINT "PK_MemberSavedViews" PRIMARY KEY ("Id");


--
-- Name: MembershipApplications PK_MembershipApplications; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."MembershipApplications"
    ADD CONSTRAINT "PK_MembershipApplications" PRIMARY KEY ("Id");


--
-- Name: MembershipApprovalStages PK_MembershipApprovalStages; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."MembershipApprovalStages"
    ADD CONSTRAINT "PK_MembershipApprovalStages" PRIMARY KEY ("Id");


--
-- Name: MembershipApprovals PK_MembershipApprovals; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."MembershipApprovals"
    ADD CONSTRAINT "PK_MembershipApprovals" PRIMARY KEY ("Id");


--
-- Name: MembershipCategories PK_MembershipCategories; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."MembershipCategories"
    ADD CONSTRAINT "PK_MembershipCategories" PRIMARY KEY ("Id");


--
-- Name: MembershipDocuments PK_MembershipDocuments; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."MembershipDocuments"
    ADD CONSTRAINT "PK_MembershipDocuments" PRIMARY KEY ("Id");


--
-- Name: MembershipHistory PK_MembershipHistory; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."MembershipHistory"
    ADD CONSTRAINT "PK_MembershipHistory" PRIMARY KEY ("Id");


--
-- Name: MembershipInvitations PK_MembershipInvitations; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."MembershipInvitations"
    ADD CONSTRAINT "PK_MembershipInvitations" PRIMARY KEY ("Id");


--
-- Name: MembershipPolicies PK_MembershipPolicies; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."MembershipPolicies"
    ADD CONSTRAINT "PK_MembershipPolicies" PRIMARY KEY ("TenantId");


--
-- Name: MembershipRenewals PK_MembershipRenewals; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."MembershipRenewals"
    ADD CONSTRAINT "PK_MembershipRenewals" PRIMARY KEY ("Id");


--
-- Name: MembershipStageRoles PK_MembershipStageRoles; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."MembershipStageRoles"
    ADD CONSTRAINT "PK_MembershipStageRoles" PRIMARY KEY ("Id");


--
-- Name: MembershipTransfers PK_MembershipTransfers; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."MembershipTransfers"
    ADD CONSTRAINT "PK_MembershipTransfers" PRIMARY KEY ("Id");


--
-- Name: MembershipWorkflows PK_MembershipWorkflows; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."MembershipWorkflows"
    ADD CONSTRAINT "PK_MembershipWorkflows" PRIMARY KEY ("Id");


--
-- Name: Memberships PK_Memberships; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."Memberships"
    ADD CONSTRAINT "PK_Memberships" PRIMARY KEY ("Id");


--
-- Name: NotificationTemplates PK_NotificationTemplates; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."NotificationTemplates"
    ADD CONSTRAINT "PK_NotificationTemplates" PRIMARY KEY ("Id");


--
-- Name: OfficerAppointmentHistory PK_OfficerAppointmentHistory; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."OfficerAppointmentHistory"
    ADD CONSTRAINT "PK_OfficerAppointmentHistory" PRIMARY KEY ("Id");


--
-- Name: OfficerAppointments PK_OfficerAppointments; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."OfficerAppointments"
    ADD CONSTRAINT "PK_OfficerAppointments" PRIMARY KEY ("Id");


--
-- Name: OfficerPositions PK_OfficerPositions; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."OfficerPositions"
    ADD CONSTRAINT "PK_OfficerPositions" PRIMARY KEY ("Id");


--
-- Name: OperationComments PK_OperationComments; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."OperationComments"
    ADD CONSTRAINT "PK_OperationComments" PRIMARY KEY ("Id");


--
-- Name: OperationRevisions PK_OperationRevisions; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."OperationRevisions"
    ADD CONSTRAINT "PK_OperationRevisions" PRIMARY KEY ("Id");


--
-- Name: OperationTasks PK_OperationTasks; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."OperationTasks"
    ADD CONSTRAINT "PK_OperationTasks" PRIMARY KEY ("Id");


--
-- Name: OrganizationHistory PK_OrganizationHistory; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."OrganizationHistory"
    ADD CONSTRAINT "PK_OrganizationHistory" PRIMARY KEY ("Id");


--
-- Name: OrganizationSettings PK_OrganizationSettings; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."OrganizationSettings"
    ADD CONSTRAINT "PK_OrganizationSettings" PRIMARY KEY ("Id");


--
-- Name: Orgs PK_Orgs; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."Orgs"
    ADD CONSTRAINT "PK_Orgs" PRIMARY KEY ("Id");


--
-- Name: Permissions PK_Permissions; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."Permissions"
    ADD CONSTRAINT "PK_Permissions" PRIMARY KEY ("Id");


--
-- Name: PersonAddresses PK_PersonAddresses; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."PersonAddresses"
    ADD CONSTRAINT "PK_PersonAddresses" PRIMARY KEY ("Id");


--
-- Name: PersonContacts PK_PersonContacts; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."PersonContacts"
    ADD CONSTRAINT "PK_PersonContacts" PRIMARY KEY ("Id");


--
-- Name: PersonLinkRequests PK_PersonLinkRequests; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."PersonLinkRequests"
    ADD CONSTRAINT "PK_PersonLinkRequests" PRIMARY KEY ("Id");


--
-- Name: PersonPrivacy PK_PersonPrivacy; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."PersonPrivacy"
    ADD CONSTRAINT "PK_PersonPrivacy" PRIMARY KEY ("PersonId");


--
-- Name: PersonTags PK_PersonTags; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."PersonTags"
    ADD CONSTRAINT "PK_PersonTags" PRIMARY KEY ("Id");


--
-- Name: Persons PK_Persons; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."Persons"
    ADD CONSTRAINT "PK_Persons" PRIMARY KEY ("Id");


--
-- Name: ProjectActivities PK_ProjectActivities; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."ProjectActivities"
    ADD CONSTRAINT "PK_ProjectActivities" PRIMARY KEY ("Id");


--
-- Name: ProjectImpacts PK_ProjectImpacts; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."ProjectImpacts"
    ADD CONSTRAINT "PK_ProjectImpacts" PRIMARY KEY ("Id");


--
-- Name: ProjectOrganizations PK_ProjectOrganizations; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."ProjectOrganizations"
    ADD CONSTRAINT "PK_ProjectOrganizations" PRIMARY KEY ("Id");


--
-- Name: RegistrationRequests PK_RegistrationRequests; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."RegistrationRequests"
    ADD CONSTRAINT "PK_RegistrationRequests" PRIMARY KEY ("Id");


--
-- Name: RoleAssignmentHistory PK_RoleAssignmentHistory; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."RoleAssignmentHistory"
    ADD CONSTRAINT "PK_RoleAssignmentHistory" PRIMARY KEY ("Id");


--
-- Name: RolePermissions PK_RolePermissions; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."RolePermissions"
    ADD CONSTRAINT "PK_RolePermissions" PRIMARY KEY ("Id");


--
-- Name: Roles PK_Roles; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."Roles"
    ADD CONSTRAINT "PK_Roles" PRIMARY KEY ("Id");


--
-- Name: ServiceProjects PK_ServiceProjects; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."ServiceProjects"
    ADD CONSTRAINT "PK_ServiceProjects" PRIMARY KEY ("Id");


--
-- Name: Sessions PK_Sessions; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."Sessions"
    ADD CONSTRAINT "PK_Sessions" PRIMARY KEY ("Id");


--
-- Name: SystemSettings PK_SystemSettings; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."SystemSettings"
    ADD CONSTRAINT "PK_SystemSettings" PRIMARY KEY ("Key");


--
-- Name: TenantSettings PK_TenantSettings; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."TenantSettings"
    ADD CONSTRAINT "PK_TenantSettings" PRIMARY KEY ("Id");


--
-- Name: Tenants PK_Tenants; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."Tenants"
    ADD CONSTRAINT "PK_Tenants" PRIMARY KEY ("Id");


--
-- Name: TransferDecisions PK_TransferDecisions; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."TransferDecisions"
    ADD CONSTRAINT "PK_TransferDecisions" PRIMARY KEY ("Id");


--
-- Name: UserProfiles PK_UserProfiles; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."UserProfiles"
    ADD CONSTRAINT "PK_UserProfiles" PRIMARY KEY ("Id");


--
-- Name: Users PK_Users; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."Users"
    ADD CONSTRAINT "PK_Users" PRIMARY KEY ("Id");


--
-- Name: VolunteerAssignments PK_VolunteerAssignments; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."VolunteerAssignments"
    ADD CONSTRAINT "PK_VolunteerAssignments" PRIMARY KEY ("Id");


--
-- Name: VolunteerHours PK_VolunteerHours; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."VolunteerHours"
    ADD CONSTRAINT "PK_VolunteerHours" PRIMARY KEY ("Id");


--
-- Name: VolunteerProfiles PK_VolunteerProfiles; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."VolunteerProfiles"
    ADD CONSTRAINT "PK_VolunteerProfiles" PRIMARY KEY ("Id");


--
-- Name: VolunteerShifts PK_VolunteerShifts; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."VolunteerShifts"
    ADD CONSTRAINT "PK_VolunteerShifts" PRIMARY KEY ("Id");


--
-- Name: __EFMigrationsHistory PK___EFMigrationsHistory; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."__EFMigrationsHistory"
    ADD CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId");


--
-- Name: IX_Appointment_Current; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_Appointment_Current" ON public."OfficerAppointments" USING btree ("TenantId", "OrgId", "PositionId", "EndsAt", "StartsAt") WHERE ("Status" = 'Active'::text);


--
-- Name: IX_Appointment_Worker; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_Appointment_Worker" ON public."OfficerAppointments" USING btree ("Status", "EffectiveAt", "EndsAt") WHERE ("Status" = ANY (ARRAY['Approved'::text, 'Scheduled'::text, 'Active'::text]));


--
-- Name: IX_Assignments_TenantId_AppointmentId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_Assignments_TenantId_AppointmentId" ON public."Assignments" USING btree ("TenantId", "AppointmentId");


--
-- Name: IX_Assignments_TenantId_MembershipId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_Assignments_TenantId_MembershipId" ON public."Assignments" USING btree ("TenantId", "MembershipId");


--
-- Name: IX_Assignments_TenantId_OrgId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_Assignments_TenantId_OrgId" ON public."Assignments" USING btree ("TenantId", "OrgId");


--
-- Name: IX_Assignments_TenantId_RoleId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_Assignments_TenantId_RoleId" ON public."Assignments" USING btree ("TenantId", "RoleId");


--
-- Name: IX_Assignments_UserId_RoleId_OrgId; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX "IX_Assignments_UserId_RoleId_OrgId" ON public."Assignments" USING btree ("UserId", "RoleId", "OrgId");


--
-- Name: IX_Audits_TenantId_At; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_Audits_TenantId_At" ON public."Audits" USING btree ("TenantId", "At");


--
-- Name: IX_AuthenticationAudits_ActorId_At; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_AuthenticationAudits_ActorId_At" ON public."AuthenticationAudits" USING btree ("ActorId", "At");


--
-- Name: IX_CertificateTemplates_TenantId_OrgId_UpdatedAt; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_CertificateTemplates_TenantId_OrgId_UpdatedAt" ON public."CertificateTemplates" USING btree ("TenantId", "OrgId", "UpdatedAt");


--
-- Name: IX_ClubHealthConfigurations_TenantId; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX "IX_ClubHealthConfigurations_TenantId" ON public."ClubHealthConfigurations" USING btree ("TenantId");


--
-- Name: IX_ClubMeetings_SeriesId_StartsAt; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX "IX_ClubMeetings_SeriesId_StartsAt" ON public."ClubMeetings" USING btree ("SeriesId", "StartsAt");


--
-- Name: IX_ClubMeetings_TenantId_CommitteeId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_ClubMeetings_TenantId_CommitteeId" ON public."ClubMeetings" USING btree ("TenantId", "CommitteeId");


--
-- Name: IX_ClubMeetings_TenantId_EventId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_ClubMeetings_TenantId_EventId" ON public."ClubMeetings" USING btree ("TenantId", "EventId");


--
-- Name: IX_ClubMeetings_TenantId_OrgId_StartsAt; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_ClubMeetings_TenantId_OrgId_StartsAt" ON public."ClubMeetings" USING btree ("TenantId", "OrgId", "StartsAt");


--
-- Name: IX_ClubMeetings_TenantId_OrgId_UpdatedAt; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_ClubMeetings_TenantId_OrgId_UpdatedAt" ON public."ClubMeetings" USING btree ("TenantId", "OrgId", "UpdatedAt");


--
-- Name: IX_ClubMeetings_TenantId_SessionId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_ClubMeetings_TenantId_SessionId" ON public."ClubMeetings" USING btree ("TenantId", "SessionId");


--
-- Name: IX_CommitteeActivities_TenantId_CommitteeId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_CommitteeActivities_TenantId_CommitteeId" ON public."CommitteeActivities" USING btree ("TenantId", "CommitteeId");


--
-- Name: IX_CommitteeMemberships_CommitteeId_Status_PersonId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_CommitteeMemberships_CommitteeId_Status_PersonId" ON public."CommitteeMemberships" USING btree ("CommitteeId", "Status", "PersonId");


--
-- Name: IX_CommitteeMemberships_PersonId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_CommitteeMemberships_PersonId" ON public."CommitteeMemberships" USING btree ("PersonId");


--
-- Name: IX_CommitteeMemberships_TenantId_CommitteeId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_CommitteeMemberships_TenantId_CommitteeId" ON public."CommitteeMemberships" USING btree ("TenantId", "CommitteeId");


--
-- Name: IX_CommitteeMemberships_TenantId_RoleId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_CommitteeMemberships_TenantId_RoleId" ON public."CommitteeMemberships" USING btree ("TenantId", "RoleId");


--
-- Name: IX_CommitteeRoles_TenantId_Name; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX "IX_CommitteeRoles_TenantId_Name" ON public."CommitteeRoles" USING btree ("TenantId", "Name");


--
-- Name: IX_Committees_TenantId_OrgId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_Committees_TenantId_OrgId" ON public."Committees" USING btree ("TenantId", "OrgId");


--
-- Name: IX_Committees_TenantId_OrgId_Code; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX "IX_Committees_TenantId_OrgId_Code" ON public."Committees" USING btree ("TenantId", "OrgId", "Code");


--
-- Name: IX_CommunicationAccounts_TenantId_OrgId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_CommunicationAccounts_TenantId_OrgId" ON public."CommunicationAccounts" USING btree ("TenantId", "OrgId");


--
-- Name: IX_CommunicationAttachments_Status_NextAttemptAt; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_CommunicationAttachments_Status_NextAttemptAt" ON public."CommunicationAttachments" USING btree ("Status", "NextAttemptAt");


--
-- Name: IX_CommunicationAttachments_TenantId_MessageId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_CommunicationAttachments_TenantId_MessageId" ON public."CommunicationAttachments" USING btree ("TenantId", "MessageId");


--
-- Name: IX_CommunicationAttachments_TenantId_ProviderAccountId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_CommunicationAttachments_TenantId_ProviderAccountId" ON public."CommunicationAttachments" USING btree ("TenantId", "ProviderAccountId");


--
-- Name: IX_CommunicationAudiences_TenantId_OrgId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_CommunicationAudiences_TenantId_OrgId" ON public."CommunicationAudiences" USING btree ("TenantId", "OrgId");


--
-- Name: IX_CommunicationAutomations_TenantId_AccountId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_CommunicationAutomations_TenantId_AccountId" ON public."CommunicationAutomations" USING btree ("TenantId", "AccountId");


--
-- Name: IX_CommunicationAutomations_TenantId_ApprovalRequestId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_CommunicationAutomations_TenantId_ApprovalRequestId" ON public."CommunicationAutomations" USING btree ("TenantId", "ApprovalRequestId");


--
-- Name: IX_CommunicationAutomations_TenantId_OrgId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_CommunicationAutomations_TenantId_OrgId" ON public."CommunicationAutomations" USING btree ("TenantId", "OrgId");


--
-- Name: IX_CommunicationAutomations_TenantId_TemplateId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_CommunicationAutomations_TenantId_TemplateId" ON public."CommunicationAutomations" USING btree ("TenantId", "TemplateId");


--
-- Name: IX_CommunicationBroadcasts_Status_SendAt; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_CommunicationBroadcasts_Status_SendAt" ON public."CommunicationBroadcasts" USING btree ("Status", "SendAt");


--
-- Name: IX_CommunicationBroadcasts_TenantId_AccountId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_CommunicationBroadcasts_TenantId_AccountId" ON public."CommunicationBroadcasts" USING btree ("TenantId", "AccountId");


--
-- Name: IX_CommunicationBroadcasts_TenantId_ApprovalRequestId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_CommunicationBroadcasts_TenantId_ApprovalRequestId" ON public."CommunicationBroadcasts" USING btree ("TenantId", "ApprovalRequestId");


--
-- Name: IX_CommunicationBroadcasts_TenantId_EventId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_CommunicationBroadcasts_TenantId_EventId" ON public."CommunicationBroadcasts" USING btree ("TenantId", "EventId");


--
-- Name: IX_CommunicationBroadcasts_TenantId_OrgId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_CommunicationBroadcasts_TenantId_OrgId" ON public."CommunicationBroadcasts" USING btree ("TenantId", "OrgId");


--
-- Name: IX_CommunicationBroadcasts_TenantId_TemplateId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_CommunicationBroadcasts_TenantId_TemplateId" ON public."CommunicationBroadcasts" USING btree ("TenantId", "TemplateId");


--
-- Name: IX_CommunicationExecutions_AutomationId_SourceKey; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX "IX_CommunicationExecutions_AutomationId_SourceKey" ON public."CommunicationExecutions" USING btree ("AutomationId", "SourceKey");


--
-- Name: IX_CommunicationExecutions_TenantId_AutomationId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_CommunicationExecutions_TenantId_AutomationId" ON public."CommunicationExecutions" USING btree ("TenantId", "AutomationId");


--
-- Name: IX_CommunicationMessages_BroadcastId_At; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_CommunicationMessages_BroadcastId_At" ON public."CommunicationMessages" USING btree ("BroadcastId", "At");


--
-- Name: IX_CommunicationMessages_ConversationId_At_Id; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_CommunicationMessages_ConversationId_At_Id" ON public."CommunicationMessages" USING btree ("ConversationId", "At", "Id");


--
-- Name: IX_CommunicationMessages_Provider; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_CommunicationMessages_Provider" ON public."CommunicationMessages" USING btree ("TenantId", "ProviderId") WHERE ("ProviderId" <> ''::text);


--
-- Name: IX_CommunicationMessages_TenantId_BroadcastId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_CommunicationMessages_TenantId_BroadcastId" ON public."CommunicationMessages" USING btree ("TenantId", "BroadcastId");


--
-- Name: IX_CommunicationMessages_TenantId_ConversationId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_CommunicationMessages_TenantId_ConversationId" ON public."CommunicationMessages" USING btree ("TenantId", "ConversationId");


--
-- Name: IX_CommunicationMessages_TenantId_OrgId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_CommunicationMessages_TenantId_OrgId" ON public."CommunicationMessages" USING btree ("TenantId", "OrgId");


--
-- Name: IX_CommunicationMessages_TenantId_SenderId_At; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_CommunicationMessages_TenantId_SenderId_At" ON public."CommunicationMessages" USING btree ("TenantId", "SenderId", "At") WHERE ("ConversationId" IS NOT NULL);


--
-- Name: IX_CommunicationMessages_TenantId_SenderId_ClientKey; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX "IX_CommunicationMessages_TenantId_SenderId_ClientKey" ON public."CommunicationMessages" USING btree ("TenantId", "SenderId", "ClientKey");


--
-- Name: IX_CommunicationPolicies_OrgId; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX "IX_CommunicationPolicies_OrgId" ON public."CommunicationPolicies" USING btree ("OrgId");


--
-- Name: IX_CommunicationPolicies_TenantId_OrgId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_CommunicationPolicies_TenantId_OrgId" ON public."CommunicationPolicies" USING btree ("TenantId", "OrgId");


--
-- Name: IX_CommunicationPreferences_UserId_Channel; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX "IX_CommunicationPreferences_UserId_Channel" ON public."CommunicationPreferences" USING btree ("UserId", "Channel");


--
-- Name: IX_CommunicationProviderEvents_AccountId_EventKey; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX "IX_CommunicationProviderEvents_AccountId_EventKey" ON public."CommunicationProviderEvents" USING btree ("AccountId", "EventKey");


--
-- Name: IX_CommunicationProviderEvents_TenantId_AccountId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_CommunicationProviderEvents_TenantId_AccountId" ON public."CommunicationProviderEvents" USING btree ("TenantId", "AccountId");


--
-- Name: IX_CommunicationRecipients_BroadcastId_PersonId; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX "IX_CommunicationRecipients_BroadcastId_PersonId" ON public."CommunicationRecipients" USING btree ("BroadcastId", "PersonId");


--
-- Name: IX_CommunicationRecipients_BroadcastId_QueuedCycle_Id; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_CommunicationRecipients_BroadcastId_QueuedCycle_Id" ON public."CommunicationRecipients" USING btree ("BroadcastId", "QueuedCycle", "Id");


--
-- Name: IX_CommunicationRecipients_PersonId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_CommunicationRecipients_PersonId" ON public."CommunicationRecipients" USING btree ("PersonId");


--
-- Name: IX_CommunicationRecipients_TenantId_BroadcastId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_CommunicationRecipients_TenantId_BroadcastId" ON public."CommunicationRecipients" USING btree ("TenantId", "BroadcastId");


--
-- Name: IX_CommunicationRecipients_UserId_BroadcastId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_CommunicationRecipients_UserId_BroadcastId" ON public."CommunicationRecipients" USING btree ("UserId", "BroadcastId");


--
-- Name: IX_CommunicationRevisions_ResourceId_Version; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX "IX_CommunicationRevisions_ResourceId_Version" ON public."CommunicationRevisions" USING btree ("ResourceId", "Version");


--
-- Name: IX_CommunicationSuppressions_TenantId_Channel_AddressHash; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX "IX_CommunicationSuppressions_TenantId_Channel_AddressHash" ON public."CommunicationSuppressions" USING btree ("TenantId", "Channel", "AddressHash");


--
-- Name: IX_CommunicationTemplates_TenantId_AccountId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_CommunicationTemplates_TenantId_AccountId" ON public."CommunicationTemplates" USING btree ("TenantId", "AccountId");


--
-- Name: IX_CommunicationTemplates_TenantId_OrgId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_CommunicationTemplates_TenantId_OrgId" ON public."CommunicationTemplates" USING btree ("TenantId", "OrgId");


--
-- Name: IX_ConversationParticipants_ConversationId_UserId; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX "IX_ConversationParticipants_ConversationId_UserId" ON public."ConversationParticipants" USING btree ("ConversationId", "UserId");


--
-- Name: IX_ConversationParticipants_TenantId_ConversationId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_ConversationParticipants_TenantId_ConversationId" ON public."ConversationParticipants" USING btree ("TenantId", "ConversationId");


--
-- Name: IX_ConversationParticipants_UserId_Active_ConversationId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_ConversationParticipants_UserId_Active_ConversationId" ON public."ConversationParticipants" USING btree ("UserId", "Active", "ConversationId");


--
-- Name: IX_Conversations_AccountId_ExternalThread; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX "IX_Conversations_AccountId_ExternalThread" ON public."Conversations" USING btree ("AccountId", "ExternalThread") WHERE ("ExternalThread" <> ''::text);


--
-- Name: IX_Conversations_TenantId_AccountId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_Conversations_TenantId_AccountId" ON public."Conversations" USING btree ("TenantId", "AccountId");


--
-- Name: IX_Conversations_TenantId_CommitteeId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_Conversations_TenantId_CommitteeId" ON public."Conversations" USING btree ("TenantId", "CommitteeId");


--
-- Name: IX_Conversations_TenantId_EventId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_Conversations_TenantId_EventId" ON public."Conversations" USING btree ("TenantId", "EventId");


--
-- Name: IX_Conversations_TenantId_OrgId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_Conversations_TenantId_OrgId" ON public."Conversations" USING btree ("TenantId", "OrgId");


--
-- Name: IX_Conversations_TenantId_UpdatedAt_Id; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_Conversations_TenantId_UpdatedAt_Id" ON public."Conversations" USING btree ("TenantId", "UpdatedAt", "Id");


--
-- Name: IX_DigitalCardVerifications_At; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_DigitalCardVerifications_At" ON public."DigitalCardVerifications" USING btree ("At");


--
-- Name: IX_DigitalCardVerifications_CardId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_DigitalCardVerifications_CardId" ON public."DigitalCardVerifications" USING btree ("CardId");


--
-- Name: IX_DigitalCards_MembershipId; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX "IX_DigitalCards_MembershipId" ON public."DigitalCards" USING btree ("MembershipId") WHERE ("RevokedAt" IS NULL);


--
-- Name: IX_DigitalCards_TenantId_MembershipId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_DigitalCards_TenantId_MembershipId" ON public."DigitalCards" USING btree ("TenantId", "MembershipId");


--
-- Name: IX_DigitalCards_TokenHash; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX "IX_DigitalCards_TokenHash" ON public."DigitalCards" USING btree ("TokenHash");


--
-- Name: IX_DocumentVersions_DocumentId_Number; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX "IX_DocumentVersions_DocumentId_Number" ON public."DocumentVersions" USING btree ("DocumentId", "Number");


--
-- Name: IX_DocumentVersions_TenantId_DocumentId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_DocumentVersions_TenantId_DocumentId" ON public."DocumentVersions" USING btree ("TenantId", "DocumentId");


--
-- Name: IX_DocumentVersions_TenantId_OrgId_UpdatedAt; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_DocumentVersions_TenantId_OrgId_UpdatedAt" ON public."DocumentVersions" USING btree ("TenantId", "OrgId", "UpdatedAt");


--
-- Name: IX_EventAudiences_PersonId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_EventAudiences_PersonId" ON public."EventAudiences" USING btree ("PersonId");


--
-- Name: IX_EventAudiences_TenantId_CategoryId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_EventAudiences_TenantId_CategoryId" ON public."EventAudiences" USING btree ("TenantId", "CategoryId");


--
-- Name: IX_EventAudiences_TenantId_CommitteeId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_EventAudiences_TenantId_CommitteeId" ON public."EventAudiences" USING btree ("TenantId", "CommitteeId");


--
-- Name: IX_EventAudiences_TenantId_EventId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_EventAudiences_TenantId_EventId" ON public."EventAudiences" USING btree ("TenantId", "EventId");


--
-- Name: IX_EventAudiences_TenantId_OrgId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_EventAudiences_TenantId_OrgId" ON public."EventAudiences" USING btree ("TenantId", "OrgId");


--
-- Name: IX_EventAudiences_TenantId_RoleId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_EventAudiences_TenantId_RoleId" ON public."EventAudiences" USING btree ("TenantId", "RoleId");


--
-- Name: IX_EventCheckins_EventId_SessionId_Voided; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_EventCheckins_EventId_SessionId_Voided" ON public."EventCheckins" USING btree ("EventId", "SessionId", "Voided");


--
-- Name: IX_EventCheckins_OperatorId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_EventCheckins_OperatorId" ON public."EventCheckins" USING btree ("OperatorId");


--
-- Name: IX_EventCheckins_RegistrationId_SessionId; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX "IX_EventCheckins_RegistrationId_SessionId" ON public."EventCheckins" USING btree ("RegistrationId", "SessionId") NULLS NOT DISTINCT;


--
-- Name: IX_EventCheckins_TenantId_EventId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_EventCheckins_TenantId_EventId" ON public."EventCheckins" USING btree ("TenantId", "EventId");


--
-- Name: IX_EventCheckins_TenantId_RegistrationId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_EventCheckins_TenantId_RegistrationId" ON public."EventCheckins" USING btree ("TenantId", "RegistrationId");


--
-- Name: IX_EventCheckins_TenantId_SessionId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_EventCheckins_TenantId_SessionId" ON public."EventCheckins" USING btree ("TenantId", "SessionId");


--
-- Name: IX_EventCohosts_TenantId_EventId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_EventCohosts_TenantId_EventId" ON public."EventCohosts" USING btree ("TenantId", "EventId");


--
-- Name: IX_EventCohosts_TenantId_OrgId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_EventCohosts_TenantId_OrgId" ON public."EventCohosts" USING btree ("TenantId", "OrgId");


--
-- Name: IX_EventFeedbackForms_EventId; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX "IX_EventFeedbackForms_EventId" ON public."EventFeedbackForms" USING btree ("EventId");


--
-- Name: IX_EventFeedbackForms_TenantId_EventId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_EventFeedbackForms_TenantId_EventId" ON public."EventFeedbackForms" USING btree ("TenantId", "EventId");


--
-- Name: IX_EventFeedbackResponses_EventId_RegistrationId; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX "IX_EventFeedbackResponses_EventId_RegistrationId" ON public."EventFeedbackResponses" USING btree ("EventId", "RegistrationId");


--
-- Name: IX_EventFeedbackResponses_TenantId_EventId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_EventFeedbackResponses_TenantId_EventId" ON public."EventFeedbackResponses" USING btree ("TenantId", "EventId");


--
-- Name: IX_EventFeedbackResponses_TenantId_RegistrationId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_EventFeedbackResponses_TenantId_RegistrationId" ON public."EventFeedbackResponses" USING btree ("TenantId", "RegistrationId");


--
-- Name: IX_EventInvitations_EventId_PersonId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_EventInvitations_EventId_PersonId" ON public."EventInvitations" USING btree ("EventId", "PersonId");


--
-- Name: IX_EventInvitations_EventId_RecipientKey; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX "IX_EventInvitations_EventId_RecipientKey" ON public."EventInvitations" USING btree ("EventId", "RecipientKey");


--
-- Name: IX_EventInvitations_InviterId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_EventInvitations_InviterId" ON public."EventInvitations" USING btree ("InviterId");


--
-- Name: IX_EventInvitations_PersonId_SendAt; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_EventInvitations_PersonId_SendAt" ON public."EventInvitations" USING btree ("PersonId", "SendAt");


--
-- Name: IX_EventInvitations_Status_SendAt; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_EventInvitations_Status_SendAt" ON public."EventInvitations" USING btree ("Status", "SendAt");


--
-- Name: IX_EventInvitations_TenantId_EventId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_EventInvitations_TenantId_EventId" ON public."EventInvitations" USING btree ("TenantId", "EventId");


--
-- Name: IX_EventInvitations_TokenHash; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX "IX_EventInvitations_TokenHash" ON public."EventInvitations" USING btree ("TokenHash");


--
-- Name: IX_EventNotices_Completed_EventId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_EventNotices_Completed_EventId" ON public."EventNotices" USING btree ("Completed", "EventId");


--
-- Name: IX_EventNotices_TenantId_EventId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_EventNotices_TenantId_EventId" ON public."EventNotices" USING btree ("TenantId", "EventId");


--
-- Name: IX_EventRegistrations_EventId_MembershipId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_EventRegistrations_EventId_MembershipId" ON public."EventRegistrations" USING btree ("EventId", "MembershipId");


--
-- Name: IX_EventRegistrations_EventId_ParticipantKey; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX "IX_EventRegistrations_EventId_ParticipantKey" ON public."EventRegistrations" USING btree ("EventId", "ParticipantKey");


--
-- Name: IX_EventRegistrations_EventId_Status_CreatedAt_Id; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_EventRegistrations_EventId_Status_CreatedAt_Id" ON public."EventRegistrations" USING btree ("EventId", "Status", "CreatedAt", "Id");


--
-- Name: IX_EventRegistrations_PersonId_EventId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_EventRegistrations_PersonId_EventId" ON public."EventRegistrations" USING btree ("PersonId", "EventId");


--
-- Name: IX_EventRegistrations_TenantId_ApprovalRequestId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_EventRegistrations_TenantId_ApprovalRequestId" ON public."EventRegistrations" USING btree ("TenantId", "ApprovalRequestId");


--
-- Name: IX_EventRegistrations_TenantId_EventId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_EventRegistrations_TenantId_EventId" ON public."EventRegistrations" USING btree ("TenantId", "EventId");


--
-- Name: IX_EventRegistrations_TenantId_InvitationId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_EventRegistrations_TenantId_InvitationId" ON public."EventRegistrations" USING btree ("TenantId", "InvitationId");


--
-- Name: IX_EventRegistrations_TenantId_MembershipId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_EventRegistrations_TenantId_MembershipId" ON public."EventRegistrations" USING btree ("TenantId", "MembershipId");


--
-- Name: IX_EventRsvpHistory_TenantId_InvitationId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_EventRsvpHistory_TenantId_InvitationId" ON public."EventRsvpHistory" USING btree ("TenantId", "InvitationId");


--
-- Name: IX_EventSavedFilters_UserId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_EventSavedFilters_UserId" ON public."EventSavedFilters" USING btree ("UserId");


--
-- Name: IX_EventSessionBookings_RegistrationId_SessionId; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX "IX_EventSessionBookings_RegistrationId_SessionId" ON public."EventSessionBookings" USING btree ("RegistrationId", "SessionId");


--
-- Name: IX_EventSessionBookings_TenantId_EventId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_EventSessionBookings_TenantId_EventId" ON public."EventSessionBookings" USING btree ("TenantId", "EventId");


--
-- Name: IX_EventSessionBookings_TenantId_RegistrationId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_EventSessionBookings_TenantId_RegistrationId" ON public."EventSessionBookings" USING btree ("TenantId", "RegistrationId");


--
-- Name: IX_EventSessionBookings_TenantId_SessionId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_EventSessionBookings_TenantId_SessionId" ON public."EventSessionBookings" USING btree ("TenantId", "SessionId");


--
-- Name: IX_EventSessions_TenantId_EventId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_EventSessions_TenantId_EventId" ON public."EventSessions" USING btree ("TenantId", "EventId");


--
-- Name: IX_EventStaff_EventId_PersonId; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX "IX_EventStaff_EventId_PersonId" ON public."EventStaff" USING btree ("EventId", "PersonId");


--
-- Name: IX_EventStaff_PersonId_Active; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_EventStaff_PersonId_Active" ON public."EventStaff" USING btree ("PersonId", "Active");


--
-- Name: IX_EventStaff_TenantId_EventId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_EventStaff_TenantId_EventId" ON public."EventStaff" USING btree ("TenantId", "EventId");


--
-- Name: IX_EventTickets_RegistrationId_Revoked; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_EventTickets_RegistrationId_Revoked" ON public."EventTickets" USING btree ("RegistrationId", "Revoked");


--
-- Name: IX_EventTickets_TenantId_EventId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_EventTickets_TenantId_EventId" ON public."EventTickets" USING btree ("TenantId", "EventId");


--
-- Name: IX_EventTickets_TenantId_RegistrationId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_EventTickets_TenantId_RegistrationId" ON public."EventTickets" USING btree ("TenantId", "RegistrationId");


--
-- Name: IX_EventTickets_TokenHash; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX "IX_EventTickets_TokenHash" ON public."EventTickets" USING btree ("TokenHash");


--
-- Name: IX_EventTypes_TenantId_Name; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX "IX_EventTypes_TenantId_Name" ON public."EventTypes" USING btree ("TenantId", "Name");


--
-- Name: IX_Events_CreatorId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_Events_CreatorId" ON public."Events" USING btree ("CreatorId");


--
-- Name: IX_Events_EndsAt_Id; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_Events_EndsAt_Id" ON public."Events" USING btree ("EndsAt", "Id");


--
-- Name: IX_Events_Publication_StartsAt_Id; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_Events_Publication_StartsAt_Id" ON public."Events" USING btree ("Publication", "StartsAt", "Id");


--
-- Name: IX_Events_TenantId_ApprovalRequestId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_Events_TenantId_ApprovalRequestId" ON public."Events" USING btree ("TenantId", "ApprovalRequestId");


--
-- Name: IX_Events_TenantId_CommitteeId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_Events_TenantId_CommitteeId" ON public."Events" USING btree ("TenantId", "CommitteeId");


--
-- Name: IX_Events_TenantId_OrgId_StartsAt_Id; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_Events_TenantId_OrgId_StartsAt_Id" ON public."Events" USING btree ("TenantId", "OrgId", "StartsAt", "Id");


--
-- Name: IX_Events_TenantId_TypeId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_Events_TenantId_TypeId" ON public."Events" USING btree ("TenantId", "TypeId");


--
-- Name: IX_GovernanceDelegations_TenantId_OrgId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_GovernanceDelegations_TenantId_OrgId" ON public."GovernanceDelegations" USING btree ("TenantId", "OrgId");


--
-- Name: IX_GovernanceDelegations_TenantId_RoleId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_GovernanceDelegations_TenantId_RoleId" ON public."GovernanceDelegations" USING btree ("TenantId", "RoleId");


--
-- Name: IX_GovernanceDocuments_TenantId_CommitteeId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_GovernanceDocuments_TenantId_CommitteeId" ON public."GovernanceDocuments" USING btree ("TenantId", "CommitteeId");


--
-- Name: IX_GovernanceDocuments_TenantId_EventId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_GovernanceDocuments_TenantId_EventId" ON public."GovernanceDocuments" USING btree ("TenantId", "EventId");


--
-- Name: IX_GovernanceDocuments_TenantId_OrgId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_GovernanceDocuments_TenantId_OrgId" ON public."GovernanceDocuments" USING btree ("TenantId", "OrgId");


--
-- Name: IX_GovernanceDocuments_TenantId_OrgId_Id; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_GovernanceDocuments_TenantId_OrgId_Id" ON public."GovernanceDocuments" USING btree ("TenantId", "OrgId", "Id");


--
-- Name: IX_GovernanceRequests_TenantId_AppointmentId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_GovernanceRequests_TenantId_AppointmentId" ON public."GovernanceRequests" USING btree ("TenantId", "AppointmentId");


--
-- Name: IX_GovernanceRequests_TenantId_CommitteeId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_GovernanceRequests_TenantId_CommitteeId" ON public."GovernanceRequests" USING btree ("TenantId", "CommitteeId");


--
-- Name: IX_GovernanceRequests_TenantId_DocumentId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_GovernanceRequests_TenantId_DocumentId" ON public."GovernanceRequests" USING btree ("TenantId", "DocumentId");


--
-- Name: IX_GovernanceRequests_TenantId_HandoverId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_GovernanceRequests_TenantId_HandoverId" ON public."GovernanceRequests" USING btree ("TenantId", "HandoverId");


--
-- Name: IX_GovernanceRequests_TenantId_OrgId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_GovernanceRequests_TenantId_OrgId" ON public."GovernanceRequests" USING btree ("TenantId", "OrgId");


--
-- Name: IX_GovernanceRequests_TenantId_OrgId_Status_CreatedAt; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_GovernanceRequests_TenantId_OrgId_Status_CreatedAt" ON public."GovernanceRequests" USING btree ("TenantId", "OrgId", "Status", "CreatedAt");


--
-- Name: IX_GovernanceRequests_TenantId_ResolutionId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_GovernanceRequests_TenantId_ResolutionId" ON public."GovernanceRequests" USING btree ("TenantId", "ResolutionId");


--
-- Name: IX_GovernanceRequests_TenantId_WorkflowId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_GovernanceRequests_TenantId_WorkflowId" ON public."GovernanceRequests" USING btree ("TenantId", "WorkflowId");


--
-- Name: IX_GovernanceResolutions_TenantId_OrgId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_GovernanceResolutions_TenantId_OrgId" ON public."GovernanceResolutions" USING btree ("TenantId", "OrgId");


--
-- Name: IX_GovernanceResolutions_TenantId_OrgId_Number; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX "IX_GovernanceResolutions_TenantId_OrgId_Number" ON public."GovernanceResolutions" USING btree ("TenantId", "OrgId", "Number");


--
-- Name: IX_HandoverChecklistItems_TenantId_DocumentId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_HandoverChecklistItems_TenantId_DocumentId" ON public."HandoverChecklistItems" USING btree ("TenantId", "DocumentId");


--
-- Name: IX_HandoverChecklistItems_TenantId_HandoverId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_HandoverChecklistItems_TenantId_HandoverId" ON public."HandoverChecklistItems" USING btree ("TenantId", "HandoverId");


--
-- Name: IX_ImpactMetrics_TenantId_OrgId_UpdatedAt; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_ImpactMetrics_TenantId_OrgId_UpdatedAt" ON public."ImpactMetrics" USING btree ("TenantId", "OrgId", "UpdatedAt");


--
-- Name: IX_IssuedCertificates_PersonId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_IssuedCertificates_PersonId" ON public."IssuedCertificates" USING btree ("PersonId");


--
-- Name: IX_IssuedCertificates_TenantId_Number; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX "IX_IssuedCertificates_TenantId_Number" ON public."IssuedCertificates" USING btree ("TenantId", "Number");


--
-- Name: IX_IssuedCertificates_TenantId_OrgId_UpdatedAt; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_IssuedCertificates_TenantId_OrgId_UpdatedAt" ON public."IssuedCertificates" USING btree ("TenantId", "OrgId", "UpdatedAt");


--
-- Name: IX_IssuedCertificates_TenantId_TemplateId_PersonId_SourceId_So~; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_IssuedCertificates_TenantId_TemplateId_PersonId_SourceId_So~" ON public."IssuedCertificates" USING btree ("TenantId", "TemplateId", "PersonId", "SourceId", "SourceType", "TemplateVersion");


--
-- Name: IX_IssuedCertificates_TokenHash; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX "IX_IssuedCertificates_TokenHash" ON public."IssuedCertificates" USING btree ("TokenHash");


--
-- Name: IX_LeadershipHandovers_TenantId_IncomingAppointmentId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_LeadershipHandovers_TenantId_IncomingAppointmentId" ON public."LeadershipHandovers" USING btree ("TenantId", "IncomingAppointmentId");


--
-- Name: IX_LeadershipHandovers_TenantId_OrgId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_LeadershipHandovers_TenantId_OrgId" ON public."LeadershipHandovers" USING btree ("TenantId", "OrgId");


--
-- Name: IX_LeadershipHandovers_TenantId_OutgoingAppointmentId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_LeadershipHandovers_TenantId_OutgoingAppointmentId" ON public."LeadershipHandovers" USING btree ("TenantId", "OutgoingAppointmentId");


--
-- Name: IX_LeadershipTerms_TenantId_OrgId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_LeadershipTerms_TenantId_OrgId" ON public."LeadershipTerms" USING btree ("TenantId", "OrgId");


--
-- Name: IX_Levels_TenantId_Name; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX "IX_Levels_TenantId_Name" ON public."Levels" USING btree ("TenantId", "Name");


--
-- Name: IX_Levels_TenantId_ParentLevelId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_Levels_TenantId_ParentLevelId" ON public."Levels" USING btree ("TenantId", "ParentLevelId");


--
-- Name: IX_MailDeliveryAttempts_MailId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_MailDeliveryAttempts_MailId" ON public."MailDeliveryAttempts" USING btree ("MailId");


--
-- Name: IX_Mails_CommunicationDispatch; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_Mails_CommunicationDispatch" ON public."Mails" USING btree ("Channel", "NextAttemptAt", "Id") WHERE (("SentAt" IS NULL) AND (NOT "Suppressed") AND (NOT "DeadLetter"));


--
-- Name: IX_Mails_CommunicationId; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX "IX_Mails_CommunicationId" ON public."Mails" USING btree ("CommunicationId");


--
-- Name: IX_Mails_NextAttemptAt; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_Mails_NextAttemptAt" ON public."Mails" USING btree ("NextAttemptAt") WHERE (("SentAt" IS NULL) AND (NOT "Used"));


--
-- Name: IX_Mails_TenantId_AccountId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_Mails_TenantId_AccountId" ON public."Mails" USING btree ("TenantId", "AccountId");


--
-- Name: IX_Mails_TenantId_CommunicationId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_Mails_TenantId_CommunicationId" ON public."Mails" USING btree ("TenantId", "CommunicationId");


--
-- Name: IX_Mails_TenantId_CreatedAt_Channel_DeliveryStatus; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_Mails_TenantId_CreatedAt_Channel_DeliveryStatus" ON public."Mails" USING btree ("TenantId", "CreatedAt", "Channel", "DeliveryStatus");


--
-- Name: IX_Mails_TenantId_EventId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_Mails_TenantId_EventId" ON public."Mails" USING btree ("TenantId", "EventId");


--
-- Name: IX_Mails_TokenHash; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX "IX_Mails_TokenHash" ON public."Mails" USING btree ("TokenHash");


--
-- Name: IX_Mails_UserId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_Mails_UserId" ON public."Mails" USING btree ("UserId");


--
-- Name: IX_MeetingAttendances_MeetingId_PersonId; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX "IX_MeetingAttendances_MeetingId_PersonId" ON public."MeetingAttendances" USING btree ("MeetingId", "PersonId");


--
-- Name: IX_MeetingAttendances_PersonId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_MeetingAttendances_PersonId" ON public."MeetingAttendances" USING btree ("PersonId");


--
-- Name: IX_MeetingAttendances_TenantId_MeetingId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_MeetingAttendances_TenantId_MeetingId" ON public."MeetingAttendances" USING btree ("TenantId", "MeetingId");


--
-- Name: IX_MeetingAttendances_TenantId_OrgId_UpdatedAt; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_MeetingAttendances_TenantId_OrgId_UpdatedAt" ON public."MeetingAttendances" USING btree ("TenantId", "OrgId", "UpdatedAt");


--
-- Name: IX_MeetingMinutes_MeetingId_Revision; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX "IX_MeetingMinutes_MeetingId_Revision" ON public."MeetingMinutes" USING btree ("MeetingId", "Revision");


--
-- Name: IX_MeetingMinutes_TenantId_MeetingId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_MeetingMinutes_TenantId_MeetingId" ON public."MeetingMinutes" USING btree ("TenantId", "MeetingId");


--
-- Name: IX_MeetingMinutes_TenantId_OrgId_UpdatedAt; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_MeetingMinutes_TenantId_OrgId_UpdatedAt" ON public."MeetingMinutes" USING btree ("TenantId", "OrgId", "UpdatedAt");


--
-- Name: IX_MemberActivities_PersonId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_MemberActivities_PersonId" ON public."MemberActivities" USING btree ("PersonId");


--
-- Name: IX_MemberActivities_TenantId_ClubId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_MemberActivities_TenantId_ClubId" ON public."MemberActivities" USING btree ("TenantId", "ClubId");


--
-- Name: IX_MemberActivities_TenantId_PersonId_At; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_MemberActivities_TenantId_PersonId_At" ON public."MemberActivities" USING btree ("TenantId", "PersonId", "At");


--
-- Name: IX_MemberContactRequests_TenantId_MembershipId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_MemberContactRequests_TenantId_MembershipId" ON public."MemberContactRequests" USING btree ("TenantId", "MembershipId");


--
-- Name: IX_MemberImportJobs_Status_LeaseUntil; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_MemberImportJobs_Status_LeaseUntil" ON public."MemberImportJobs" USING btree ("Status", "LeaseUntil");


--
-- Name: IX_MemberImportJobs_TenantId_CategoryId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_MemberImportJobs_TenantId_CategoryId" ON public."MemberImportJobs" USING btree ("TenantId", "CategoryId");


--
-- Name: IX_MemberImportJobs_TenantId_ClubId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_MemberImportJobs_TenantId_ClubId" ON public."MemberImportJobs" USING btree ("TenantId", "ClubId");


--
-- Name: IX_MemberImportJobs_TenantId_CreatorId_RequestKey; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX "IX_MemberImportJobs_TenantId_CreatorId_RequestKey" ON public."MemberImportJobs" USING btree ("TenantId", "CreatorId", "RequestKey");


--
-- Name: IX_MemberImportRows_JobId_RowNumber; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX "IX_MemberImportRows_JobId_RowNumber" ON public."MemberImportRows" USING btree ("JobId", "RowNumber");


--
-- Name: IX_MemberImportRows_TenantId_JobId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_MemberImportRows_TenantId_JobId" ON public."MemberImportRows" USING btree ("TenantId", "JobId");


--
-- Name: IX_MemberRelationships_PersonId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_MemberRelationships_PersonId" ON public."MemberRelationships" USING btree ("PersonId");


--
-- Name: IX_MemberRelationships_RelatedPersonId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_MemberRelationships_RelatedPersonId" ON public."MemberRelationships" USING btree ("RelatedPersonId");


--
-- Name: IX_MemberSavedViews_TenantId_UserId_Name; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX "IX_MemberSavedViews_TenantId_UserId_Name" ON public."MemberSavedViews" USING btree ("TenantId", "UserId", "Name");


--
-- Name: IX_MemberSavedViews_UserId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_MemberSavedViews_UserId" ON public."MemberSavedViews" USING btree ("UserId");


--
-- Name: IX_MembershipApplications_ApplicantUserId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_MembershipApplications_ApplicantUserId" ON public."MembershipApplications" USING btree ("ApplicantUserId");


--
-- Name: IX_MembershipApplications_InvitationId_ApplicantUserId; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX "IX_MembershipApplications_InvitationId_ApplicantUserId" ON public."MembershipApplications" USING btree ("InvitationId", "ApplicantUserId") WHERE ("InvitationId" IS NOT NULL);


--
-- Name: IX_MembershipApplications_PersonId_ClubId_CategoryId; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX "IX_MembershipApplications_PersonId_ClubId_CategoryId" ON public."MembershipApplications" USING btree ("PersonId", "ClubId", "CategoryId") WHERE ("Status" = ANY (ARRAY['Draft'::text, 'Submitted'::text, 'Under Review'::text, 'Changes Requested'::text]));


--
-- Name: IX_MembershipApplications_TenantId_CategoryId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_MembershipApplications_TenantId_CategoryId" ON public."MembershipApplications" USING btree ("TenantId", "CategoryId");


--
-- Name: IX_MembershipApplications_TenantId_ClubId_Status_CurrentSequen~; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_MembershipApplications_TenantId_ClubId_Status_CurrentSequen~" ON public."MembershipApplications" USING btree ("TenantId", "ClubId", "Status", "CurrentSequence");


--
-- Name: IX_MembershipApplications_TenantId_InvitationId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_MembershipApplications_TenantId_InvitationId" ON public."MembershipApplications" USING btree ("TenantId", "InvitationId");


--
-- Name: IX_MembershipApplications_TenantId_WorkflowId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_MembershipApplications_TenantId_WorkflowId" ON public."MembershipApplications" USING btree ("TenantId", "WorkflowId");


--
-- Name: IX_MembershipApprovalStages_TenantId_WorkflowId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_MembershipApprovalStages_TenantId_WorkflowId" ON public."MembershipApprovalStages" USING btree ("TenantId", "WorkflowId");


--
-- Name: IX_MembershipApprovalStages_WorkflowId_Sequence; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX "IX_MembershipApprovalStages_WorkflowId_Sequence" ON public."MembershipApprovalStages" USING btree ("WorkflowId", "Sequence");


--
-- Name: IX_MembershipApprovals_ApplicationId_Revision_StageId_ActorId; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX "IX_MembershipApprovals_ApplicationId_Revision_StageId_ActorId" ON public."MembershipApprovals" USING btree ("ApplicationId", "Revision", "StageId", "ActorId");


--
-- Name: IX_MembershipApprovals_GovernanceRequestId_Revision_StageId_Ac~; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX "IX_MembershipApprovals_GovernanceRequestId_Revision_StageId_Ac~" ON public."MembershipApprovals" USING btree ("GovernanceRequestId", "Revision", "StageId", "ActorId");


--
-- Name: IX_MembershipApprovals_GovernanceRequestId_Revision_StageId_Pr~; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX "IX_MembershipApprovals_GovernanceRequestId_Revision_StageId_Pr~" ON public."MembershipApprovals" USING btree ("GovernanceRequestId", "Revision", "StageId", "PrincipalUserId");


--
-- Name: IX_MembershipApprovals_TenantId_ApplicationId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_MembershipApprovals_TenantId_ApplicationId" ON public."MembershipApprovals" USING btree ("TenantId", "ApplicationId");


--
-- Name: IX_MembershipApprovals_TenantId_GovernanceRequestId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_MembershipApprovals_TenantId_GovernanceRequestId" ON public."MembershipApprovals" USING btree ("TenantId", "GovernanceRequestId");


--
-- Name: IX_MembershipApprovals_TenantId_StageId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_MembershipApprovals_TenantId_StageId" ON public."MembershipApprovals" USING btree ("TenantId", "StageId");


--
-- Name: IX_MembershipCategories_TenantId_Code; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX "IX_MembershipCategories_TenantId_Code" ON public."MembershipCategories" USING btree ("TenantId", "Code");


--
-- Name: IX_MembershipDocuments_TenantId_MembershipId_At; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_MembershipDocuments_TenantId_MembershipId_At" ON public."MembershipDocuments" USING btree ("TenantId", "MembershipId", "At");


--
-- Name: IX_MembershipHistory_TenantId_MembershipId_EffectiveAt; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_MembershipHistory_TenantId_MembershipId_EffectiveAt" ON public."MembershipHistory" USING btree ("TenantId", "MembershipId", "EffectiveAt");


--
-- Name: IX_MembershipInvitations_TenantId_CategoryId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_MembershipInvitations_TenantId_CategoryId" ON public."MembershipInvitations" USING btree ("TenantId", "CategoryId");


--
-- Name: IX_MembershipInvitations_TenantId_ClubId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_MembershipInvitations_TenantId_ClubId" ON public."MembershipInvitations" USING btree ("TenantId", "ClubId");


--
-- Name: IX_MembershipInvitations_TokenHash; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX "IX_MembershipInvitations_TokenHash" ON public."MembershipInvitations" USING btree ("TokenHash");


--
-- Name: IX_MembershipRenewals_MembershipId_RequestKey; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX "IX_MembershipRenewals_MembershipId_RequestKey" ON public."MembershipRenewals" USING btree ("MembershipId", "RequestKey");


--
-- Name: IX_MembershipRenewals_TenantId_MembershipId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_MembershipRenewals_TenantId_MembershipId" ON public."MembershipRenewals" USING btree ("TenantId", "MembershipId");


--
-- Name: IX_MembershipStageRoles_StageId_RoleId; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX "IX_MembershipStageRoles_StageId_RoleId" ON public."MembershipStageRoles" USING btree ("StageId", "RoleId");


--
-- Name: IX_MembershipStageRoles_TenantId_RoleId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_MembershipStageRoles_TenantId_RoleId" ON public."MembershipStageRoles" USING btree ("TenantId", "RoleId");


--
-- Name: IX_MembershipStageRoles_TenantId_StageId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_MembershipStageRoles_TenantId_StageId" ON public."MembershipStageRoles" USING btree ("TenantId", "StageId");


--
-- Name: IX_MembershipTransfers_MembershipId; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX "IX_MembershipTransfers_MembershipId" ON public."MembershipTransfers" USING btree ("MembershipId") WHERE ("Status" = ANY (ARRAY['PendingSource'::text, 'PendingDestination'::text, 'PendingAssociation'::text, 'Scheduled'::text]));


--
-- Name: IX_MembershipTransfers_TenantId_CategoryId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_MembershipTransfers_TenantId_CategoryId" ON public."MembershipTransfers" USING btree ("TenantId", "CategoryId");


--
-- Name: IX_MembershipTransfers_TenantId_DestinationClubId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_MembershipTransfers_TenantId_DestinationClubId" ON public."MembershipTransfers" USING btree ("TenantId", "DestinationClubId");


--
-- Name: IX_MembershipTransfers_TenantId_DestinationMembershipId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_MembershipTransfers_TenantId_DestinationMembershipId" ON public."MembershipTransfers" USING btree ("TenantId", "DestinationMembershipId");


--
-- Name: IX_MembershipTransfers_TenantId_MembershipId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_MembershipTransfers_TenantId_MembershipId" ON public."MembershipTransfers" USING btree ("TenantId", "MembershipId");


--
-- Name: IX_MembershipWorkflows_TenantId_Kind; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX "IX_MembershipWorkflows_TenantId_Kind" ON public."MembershipWorkflows" USING btree ("TenantId", "Kind") WHERE "Active";


--
-- Name: IX_Membership_Directory_Cover; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_Membership_Directory_Cover" ON public."Memberships" USING btree ("TenantId", "ClubId") INCLUDE ("PersonId", "Status", "EndsAt");


--
-- Name: IX_Membership_Expiry_Due; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_Membership_Expiry_Due" ON public."Memberships" USING btree ("EndsAt", "Id") WHERE (("Status" = ANY (ARRAY['Active'::text, 'Inactive'::text])) AND ("EndsAt" IS NOT NULL));


--
-- Name: IX_Memberships_ImportRowId; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX "IX_Memberships_ImportRowId" ON public."Memberships" USING btree ("ImportRowId") WHERE ("ImportRowId" IS NOT NULL);


--
-- Name: IX_Memberships_Number; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX "IX_Memberships_Number" ON public."Memberships" USING btree ("Number");


--
-- Name: IX_Memberships_Number_Search; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_Memberships_Number_Search" ON public."Memberships" USING gin ("Number" public.gin_trgm_ops);


--
-- Name: IX_Memberships_PersonId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_Memberships_PersonId" ON public."Memberships" USING btree ("PersonId");


--
-- Name: IX_Memberships_SourceApplicationId; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX "IX_Memberships_SourceApplicationId" ON public."Memberships" USING btree ("SourceApplicationId") WHERE ("SourceApplicationId" IS NOT NULL);


--
-- Name: IX_Memberships_TenantId_CategoryId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_Memberships_TenantId_CategoryId" ON public."Memberships" USING btree ("TenantId", "CategoryId");


--
-- Name: IX_Memberships_TenantId_ClubId_Status_Id; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_Memberships_TenantId_ClubId_Status_Id" ON public."Memberships" USING btree ("TenantId", "ClubId", "Status", "Id");


--
-- Name: IX_Memberships_TenantId_ImportRowId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_Memberships_TenantId_ImportRowId" ON public."Memberships" USING btree ("TenantId", "ImportRowId");


--
-- Name: IX_Memberships_TenantId_JoinedAt; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_Memberships_TenantId_JoinedAt" ON public."Memberships" USING btree ("TenantId", "JoinedAt");


--
-- Name: IX_Memberships_TenantId_PersonId_ClubId_CategoryId; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX "IX_Memberships_TenantId_PersonId_ClubId_CategoryId" ON public."Memberships" USING btree ("TenantId", "PersonId", "ClubId", "CategoryId") WHERE ("Status" = ANY (ARRAY['Active'::text, 'Inactive'::text, 'Suspended'::text, 'Expired'::text]));


--
-- Name: IX_Memberships_TenantId_SourceApplicationId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_Memberships_TenantId_SourceApplicationId" ON public."Memberships" USING btree ("TenantId", "SourceApplicationId");


--
-- Name: IX_Memberships_TenantId_Status_EndsAt; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_Memberships_TenantId_Status_EndsAt" ON public."Memberships" USING btree ("TenantId", "Status", "EndsAt");


--
-- Name: IX_OfficerAppointmentHistory_TenantId_AppointmentId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_OfficerAppointmentHistory_TenantId_AppointmentId" ON public."OfficerAppointmentHistory" USING btree ("TenantId", "AppointmentId");


--
-- Name: IX_OfficerAppointments_PersonId_StartsAt_EndsAt; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_OfficerAppointments_PersonId_StartsAt_EndsAt" ON public."OfficerAppointments" USING btree ("PersonId", "StartsAt", "EndsAt");


--
-- Name: IX_OfficerAppointments_TenantId_DocumentId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_OfficerAppointments_TenantId_DocumentId" ON public."OfficerAppointments" USING btree ("TenantId", "DocumentId");


--
-- Name: IX_OfficerAppointments_TenantId_GrantRoleId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_OfficerAppointments_TenantId_GrantRoleId" ON public."OfficerAppointments" USING btree ("TenantId", "GrantRoleId");


--
-- Name: IX_OfficerAppointments_TenantId_MembershipId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_OfficerAppointments_TenantId_MembershipId" ON public."OfficerAppointments" USING btree ("TenantId", "MembershipId");


--
-- Name: IX_OfficerAppointments_TenantId_OrgId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_OfficerAppointments_TenantId_OrgId" ON public."OfficerAppointments" USING btree ("TenantId", "OrgId");


--
-- Name: IX_OfficerAppointments_TenantId_OrgId_PositionId_StartsAt_Ends~; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_OfficerAppointments_TenantId_OrgId_PositionId_StartsAt_Ends~" ON public."OfficerAppointments" USING btree ("TenantId", "OrgId", "PositionId", "StartsAt", "EndsAt");


--
-- Name: IX_OfficerAppointments_TenantId_OrgId_Status_EndsAt; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_OfficerAppointments_TenantId_OrgId_Status_EndsAt" ON public."OfficerAppointments" USING btree ("TenantId", "OrgId", "Status", "EndsAt");


--
-- Name: IX_OfficerAppointments_TenantId_PositionId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_OfficerAppointments_TenantId_PositionId" ON public."OfficerAppointments" USING btree ("TenantId", "PositionId");


--
-- Name: IX_OfficerAppointments_TenantId_TermId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_OfficerAppointments_TenantId_TermId" ON public."OfficerAppointments" USING btree ("TenantId", "TermId");


--
-- Name: IX_OfficerPositions_TenantId_Code; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX "IX_OfficerPositions_TenantId_Code" ON public."OfficerPositions" USING btree ("TenantId", "Code");


--
-- Name: IX_OfficerPositions_TenantId_GrantRoleId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_OfficerPositions_TenantId_GrantRoleId" ON public."OfficerPositions" USING btree ("TenantId", "GrantRoleId");


--
-- Name: IX_OfficerPositions_TenantId_LevelId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_OfficerPositions_TenantId_LevelId" ON public."OfficerPositions" USING btree ("TenantId", "LevelId");


--
-- Name: IX_OfficerPositions_TenantId_ReportsToId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_OfficerPositions_TenantId_ReportsToId" ON public."OfficerPositions" USING btree ("TenantId", "ReportsToId");


--
-- Name: IX_OperationComments_TenantId_OrgId_UpdatedAt; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_OperationComments_TenantId_OrgId_UpdatedAt" ON public."OperationComments" USING btree ("TenantId", "OrgId", "UpdatedAt");


--
-- Name: IX_OperationComments_TenantId_TaskId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_OperationComments_TenantId_TaskId" ON public."OperationComments" USING btree ("TenantId", "TaskId");


--
-- Name: IX_OperationRevisions_TenantId_OrgId_UpdatedAt; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_OperationRevisions_TenantId_OrgId_UpdatedAt" ON public."OperationRevisions" USING btree ("TenantId", "OrgId", "UpdatedAt");


--
-- Name: IX_OperationRevisions_TenantId_RecordId_UpdatedAt; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_OperationRevisions_TenantId_RecordId_UpdatedAt" ON public."OperationRevisions" USING btree ("TenantId", "RecordId", "UpdatedAt");


--
-- Name: IX_OperationTasks_TenantId_OrgId_Status_DueAt; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_OperationTasks_TenantId_OrgId_Status_DueAt" ON public."OperationTasks" USING btree ("TenantId", "OrgId", "Status", "DueAt");


--
-- Name: IX_OperationTasks_TenantId_OrgId_UpdatedAt; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_OperationTasks_TenantId_OrgId_UpdatedAt" ON public."OperationTasks" USING btree ("TenantId", "OrgId", "UpdatedAt");


--
-- Name: IX_OperationTasks_TenantId_PersonId_Status_DueAt; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_OperationTasks_TenantId_PersonId_Status_DueAt" ON public."OperationTasks" USING btree ("TenantId", "PersonId", "Status", "DueAt");


--
-- Name: IX_OrganizationHistory_TenantId_OrgId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_OrganizationHistory_TenantId_OrgId" ON public."OrganizationHistory" USING btree ("TenantId", "OrgId");


--
-- Name: IX_OrganizationSettings_TenantId_OrgId_Key; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX "IX_OrganizationSettings_TenantId_OrgId_Key" ON public."OrganizationSettings" USING btree ("TenantId", "OrgId", "Key");


--
-- Name: IX_Orgs_Code_Search; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_Orgs_Code_Search" ON public."Orgs" USING gin ("Code" public.gin_trgm_ops);


--
-- Name: IX_Orgs_Name_Search; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_Orgs_Name_Search" ON public."Orgs" USING gin ("Name" public.gin_trgm_ops);


--
-- Name: IX_Orgs_TenantId_Code; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX "IX_Orgs_TenantId_Code" ON public."Orgs" USING btree ("TenantId", "Code");


--
-- Name: IX_Orgs_TenantId_LevelId_Status; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_Orgs_TenantId_LevelId_Status" ON public."Orgs" USING btree ("TenantId", "LevelId", "Status");


--
-- Name: IX_Orgs_TenantId_ParentId_Name; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_Orgs_TenantId_ParentId_Name" ON public."Orgs" USING btree ("TenantId", "ParentId", "Name");


--
-- Name: IX_PersonAddresses_City_PersonId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_PersonAddresses_City_PersonId" ON public."PersonAddresses" USING btree ("City", "PersonId");


--
-- Name: IX_PersonAddresses_PersonId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_PersonAddresses_PersonId" ON public."PersonAddresses" USING btree ("PersonId");


--
-- Name: IX_PersonContacts_NormalizedValue; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_PersonContacts_NormalizedValue" ON public."PersonContacts" USING btree ("NormalizedValue");


--
-- Name: IX_PersonContacts_PersonId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_PersonContacts_PersonId" ON public."PersonContacts" USING btree ("PersonId");


--
-- Name: IX_PersonLinkRequests_InvitationId_UserId; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX "IX_PersonLinkRequests_InvitationId_UserId" ON public."PersonLinkRequests" USING btree ("InvitationId", "UserId");


--
-- Name: IX_PersonLinkRequests_PersonId_UserId; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX "IX_PersonLinkRequests_PersonId_UserId" ON public."PersonLinkRequests" USING btree ("PersonId", "UserId") WHERE ("Status" = 'Pending'::text);


--
-- Name: IX_PersonLinkRequests_TenantId_ClubId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_PersonLinkRequests_TenantId_ClubId" ON public."PersonLinkRequests" USING btree ("TenantId", "ClubId");


--
-- Name: IX_PersonLinkRequests_TenantId_InvitationId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_PersonLinkRequests_TenantId_InvitationId" ON public."PersonLinkRequests" USING btree ("TenantId", "InvitationId");


--
-- Name: IX_PersonLinkRequests_UserId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_PersonLinkRequests_UserId" ON public."PersonLinkRequests" USING btree ("UserId");


--
-- Name: IX_PersonTags_PersonId_Kind_Value; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX "IX_PersonTags_PersonId_Kind_Value" ON public."PersonTags" USING btree ("PersonId", "Kind", "Value");


--
-- Name: IX_Persons_DisplayName_Id; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_Persons_DisplayName_Id" ON public."Persons" USING btree ("DisplayName", "Id");


--
-- Name: IX_Persons_LegalName_Normalized; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_Persons_LegalName_Normalized" ON public."Persons" USING btree (lower("LegalName"));


--
-- Name: IX_Persons_SearchName_Trigram; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_Persons_SearchName_Trigram" ON public."Persons" USING gin ("SearchName" public.gin_trgm_ops);


--
-- Name: IX_Persons_UserId; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX "IX_Persons_UserId" ON public."Persons" USING btree ("UserId") WHERE ("UserId" IS NOT NULL);


--
-- Name: IX_ProjectActivities_TenantId_OrgId_UpdatedAt; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_ProjectActivities_TenantId_OrgId_UpdatedAt" ON public."ProjectActivities" USING btree ("TenantId", "OrgId", "UpdatedAt");


--
-- Name: IX_ProjectActivities_TenantId_ProjectId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_ProjectActivities_TenantId_ProjectId" ON public."ProjectActivities" USING btree ("TenantId", "ProjectId");


--
-- Name: IX_ProjectImpacts_ProjectId_MetricId_SourceKey; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX "IX_ProjectImpacts_ProjectId_MetricId_SourceKey" ON public."ProjectImpacts" USING btree ("ProjectId", "MetricId", "SourceKey");


--
-- Name: IX_ProjectImpacts_TenantId_MetricId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_ProjectImpacts_TenantId_MetricId" ON public."ProjectImpacts" USING btree ("TenantId", "MetricId");


--
-- Name: IX_ProjectImpacts_TenantId_OrgId_UpdatedAt; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_ProjectImpacts_TenantId_OrgId_UpdatedAt" ON public."ProjectImpacts" USING btree ("TenantId", "OrgId", "UpdatedAt");


--
-- Name: IX_ProjectImpacts_TenantId_ProjectId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_ProjectImpacts_TenantId_ProjectId" ON public."ProjectImpacts" USING btree ("TenantId", "ProjectId");


--
-- Name: IX_ProjectOrganizations_ProjectId_OrgId; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX "IX_ProjectOrganizations_ProjectId_OrgId" ON public."ProjectOrganizations" USING btree ("ProjectId", "OrgId");


--
-- Name: IX_ProjectOrganizations_TenantId_OrgId_UpdatedAt; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_ProjectOrganizations_TenantId_OrgId_UpdatedAt" ON public."ProjectOrganizations" USING btree ("TenantId", "OrgId", "UpdatedAt");


--
-- Name: IX_ProjectOrganizations_TenantId_ProjectId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_ProjectOrganizations_TenantId_ProjectId" ON public."ProjectOrganizations" USING btree ("TenantId", "ProjectId");


--
-- Name: IX_RoleAssignmentHistory_TenantId_OrgId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_RoleAssignmentHistory_TenantId_OrgId" ON public."RoleAssignmentHistory" USING btree ("TenantId", "OrgId");


--
-- Name: IX_RoleAssignmentHistory_TenantId_RoleId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_RoleAssignmentHistory_TenantId_RoleId" ON public."RoleAssignmentHistory" USING btree ("TenantId", "RoleId");


--
-- Name: IX_RoleAssignmentHistory_UserId_TenantId_At; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_RoleAssignmentHistory_UserId_TenantId_At" ON public."RoleAssignmentHistory" USING btree ("UserId", "TenantId", "At");


--
-- Name: IX_RolePermissions_PermissionId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_RolePermissions_PermissionId" ON public."RolePermissions" USING btree ("PermissionId");


--
-- Name: IX_RolePermissions_RoleId_PermissionId; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX "IX_RolePermissions_RoleId_PermissionId" ON public."RolePermissions" USING btree ("RoleId", "PermissionId");


--
-- Name: IX_Roles_TenantId_Name; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX "IX_Roles_TenantId_Name" ON public."Roles" USING btree ("TenantId", "Name");


--
-- Name: IX_ServiceProjects_TenantId_OrgId_Status_StartsAt; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_ServiceProjects_TenantId_OrgId_Status_StartsAt" ON public."ServiceProjects" USING btree ("TenantId", "OrgId", "Status", "StartsAt");


--
-- Name: IX_ServiceProjects_TenantId_OrgId_UpdatedAt; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_ServiceProjects_TenantId_OrgId_UpdatedAt" ON public."ServiceProjects" USING btree ("TenantId", "OrgId", "UpdatedAt");


--
-- Name: IX_Sessions_UserId_ExpiresAt; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_Sessions_UserId_ExpiresAt" ON public."Sessions" USING btree ("UserId", "ExpiresAt");


--
-- Name: IX_TenantSettings_TenantId_Key; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX "IX_TenantSettings_TenantId_Key" ON public."TenantSettings" USING btree ("TenantId", "Key");


--
-- Name: IX_TransferDecisions_TenantId_TransferId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_TransferDecisions_TenantId_TransferId" ON public."TransferDecisions" USING btree ("TenantId", "TransferId");


--
-- Name: IX_TransferDecisions_TransferId_Stage; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX "IX_TransferDecisions_TransferId_Stage" ON public."TransferDecisions" USING btree ("TransferId", "Stage");


--
-- Name: IX_Users_Email; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX "IX_Users_Email" ON public."Users" USING btree ("Email");


--
-- Name: IX_VolunteerAssignments_PersonId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_VolunteerAssignments_PersonId" ON public."VolunteerAssignments" USING btree ("PersonId");


--
-- Name: IX_VolunteerAssignments_ShiftId_PersonId; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX "IX_VolunteerAssignments_ShiftId_PersonId" ON public."VolunteerAssignments" USING btree ("ShiftId", "PersonId");


--
-- Name: IX_VolunteerAssignments_TenantId_OrgId_UpdatedAt; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_VolunteerAssignments_TenantId_OrgId_UpdatedAt" ON public."VolunteerAssignments" USING btree ("TenantId", "OrgId", "UpdatedAt");


--
-- Name: IX_VolunteerAssignments_TenantId_ShiftId_Status; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_VolunteerAssignments_TenantId_ShiftId_Status" ON public."VolunteerAssignments" USING btree ("TenantId", "ShiftId", "Status");


--
-- Name: IX_VolunteerHours_PersonId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_VolunteerHours_PersonId" ON public."VolunteerHours" USING btree ("PersonId");


--
-- Name: IX_VolunteerHours_TenantId_AssignmentId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_VolunteerHours_TenantId_AssignmentId" ON public."VolunteerHours" USING btree ("TenantId", "AssignmentId");


--
-- Name: IX_VolunteerHours_TenantId_OrgId_Status_StartsAt; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_VolunteerHours_TenantId_OrgId_Status_StartsAt" ON public."VolunteerHours" USING btree ("TenantId", "OrgId", "Status", "StartsAt");


--
-- Name: IX_VolunteerHours_TenantId_OrgId_UpdatedAt; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_VolunteerHours_TenantId_OrgId_UpdatedAt" ON public."VolunteerHours" USING btree ("TenantId", "OrgId", "UpdatedAt");


--
-- Name: IX_VolunteerHours_TenantId_PersonId_StartsAt_EndsAt; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_VolunteerHours_TenantId_PersonId_StartsAt_EndsAt" ON public."VolunteerHours" USING btree ("TenantId", "PersonId", "StartsAt", "EndsAt");


--
-- Name: IX_VolunteerHours_TenantId_ProjectId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_VolunteerHours_TenantId_ProjectId" ON public."VolunteerHours" USING btree ("TenantId", "ProjectId");


--
-- Name: IX_VolunteerProfiles_PersonId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_VolunteerProfiles_PersonId" ON public."VolunteerProfiles" USING btree ("PersonId");


--
-- Name: IX_VolunteerProfiles_TenantId_OrgId_UpdatedAt; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_VolunteerProfiles_TenantId_OrgId_UpdatedAt" ON public."VolunteerProfiles" USING btree ("TenantId", "OrgId", "UpdatedAt");


--
-- Name: IX_VolunteerProfiles_TenantId_PersonId; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX "IX_VolunteerProfiles_TenantId_PersonId" ON public."VolunteerProfiles" USING btree ("TenantId", "PersonId");


--
-- Name: IX_VolunteerShifts_TenantId_ActivityId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_VolunteerShifts_TenantId_ActivityId" ON public."VolunteerShifts" USING btree ("TenantId", "ActivityId");


--
-- Name: IX_VolunteerShifts_TenantId_OrgId_UpdatedAt; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_VolunteerShifts_TenantId_OrgId_UpdatedAt" ON public."VolunteerShifts" USING btree ("TenantId", "OrgId", "UpdatedAt");


--
-- Name: IX_VolunteerShifts_TenantId_ProjectId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_VolunteerShifts_TenantId_ProjectId" ON public."VolunteerShifts" USING btree ("TenantId", "ProjectId");


--
-- Name: OfficerAppointmentHistory appointment_history_immutable; Type: TRIGGER; Schema: public; Owner: -
--

CREATE TRIGGER appointment_history_immutable BEFORE DELETE OR UPDATE ON public."OfficerAppointmentHistory" FOR EACH ROW EXECUTE FUNCTION public.clubs_membership_history_guard();


--
-- Name: OfficerAppointments appointment_integrity; Type: TRIGGER; Schema: public; Owner: -
--

CREATE TRIGGER appointment_integrity BEFORE INSERT OR UPDATE ON public."OfficerAppointments" FOR EACH ROW EXECUTE FUNCTION public.clubs_appointment_guard();


--
-- Name: OfficerAppointments appointment_snapshot_integrity; Type: TRIGGER; Schema: public; Owner: -
--

CREATE TRIGGER appointment_snapshot_integrity BEFORE UPDATE ON public."OfficerAppointments" FOR EACH ROW EXECUTE FUNCTION public.clubs_office_snapshot_guard();


--
-- Name: IssuedCertificates certificate_content_immutable; Type: TRIGGER; Schema: public; Owner: -
--

CREATE TRIGGER certificate_content_immutable BEFORE UPDATE ON public."IssuedCertificates" FOR EACH ROW EXECUTE FUNCTION public.guard_issued_certificate();


--
-- Name: CommitteeActivities committee_activity_immutable; Type: TRIGGER; Schema: public; Owner: -
--

CREATE TRIGGER committee_activity_immutable BEFORE DELETE OR UPDATE ON public."CommitteeActivities" FOR EACH ROW EXECUTE FUNCTION public.clubs_membership_history_guard();


--
-- Name: CommunicationBroadcasts communication_broadcast_immutable; Type: TRIGGER; Schema: public; Owner: -
--

CREATE TRIGGER communication_broadcast_immutable BEFORE UPDATE ON public."CommunicationBroadcasts" FOR EACH ROW EXECUTE FUNCTION public.clubs_communication_broadcast_guard();


--
-- Name: CommunicationRecipients communication_recipient_immutable; Type: TRIGGER; Schema: public; Owner: -
--

CREATE TRIGGER communication_recipient_immutable BEFORE UPDATE ON public."CommunicationRecipients" FOR EACH ROW EXECUTE FUNCTION public.clubs_communication_recipient_guard();


--
-- Name: CommunicationRevisions communication_revision_immutable; Type: TRIGGER; Schema: public; Owner: -
--

CREATE TRIGGER communication_revision_immutable BEFORE DELETE OR UPDATE ON public."CommunicationRevisions" FOR EACH ROW EXECUTE FUNCTION public.clubs_communication_revision_guard();


--
-- Name: EventSessionBookings event_booking_session_link; Type: TRIGGER; Schema: public; Owner: -
--

CREATE TRIGGER event_booking_session_link BEFORE INSERT OR UPDATE ON public."EventSessionBookings" FOR EACH ROW EXECUTE FUNCTION public.clubs_event_session_guard();


--
-- Name: EventCheckins event_checkin_link; Type: TRIGGER; Schema: public; Owner: -
--

CREATE TRIGGER event_checkin_link BEFORE INSERT OR UPDATE ON public."EventCheckins" FOR EACH ROW EXECUTE FUNCTION public.clubs_event_link_guard();


--
-- Name: EventCheckins event_checkin_session_link; Type: TRIGGER; Schema: public; Owner: -
--

CREATE TRIGGER event_checkin_session_link BEFORE INSERT OR UPDATE ON public."EventCheckins" FOR EACH ROW EXECUTE FUNCTION public.clubs_event_session_guard();


--
-- Name: EventRegistrations event_registration_audit; Type: TRIGGER; Schema: public; Owner: -
--

CREATE TRIGGER event_registration_audit AFTER INSERT OR UPDATE ON public."EventRegistrations" FOR EACH ROW EXECUTE FUNCTION public.clubs_event_registration_audit();


--
-- Name: EventRegistrations event_registration_capacity; Type: TRIGGER; Schema: public; Owner: -
--

CREATE TRIGGER event_registration_capacity BEFORE INSERT OR UPDATE ON public."EventRegistrations" FOR EACH ROW EXECUTE FUNCTION public.clubs_event_registration_guard();


--
-- Name: EventRsvpHistory event_rsvp_history_immutable; Type: TRIGGER; Schema: public; Owner: -
--

CREATE TRIGGER event_rsvp_history_immutable BEFORE DELETE OR UPDATE ON public."EventRsvpHistory" FOR EACH ROW EXECUTE FUNCTION public.clubs_membership_history_guard();


--
-- Name: EventSessionBookings event_session_booking_link; Type: TRIGGER; Schema: public; Owner: -
--

CREATE TRIGGER event_session_booking_link BEFORE INSERT OR UPDATE ON public."EventSessionBookings" FOR EACH ROW EXECUTE FUNCTION public.clubs_event_link_guard();


--
-- Name: Events event_state_audit; Type: TRIGGER; Schema: public; Owner: -
--

CREATE TRIGGER event_state_audit AFTER UPDATE ON public."Events" FOR EACH ROW EXECUTE FUNCTION public.clubs_event_state_audit();


--
-- Name: EventTickets event_ticket_link; Type: TRIGGER; Schema: public; Owner: -
--

CREATE TRIGGER event_ticket_link BEFORE INSERT OR UPDATE ON public."EventTickets" FOR EACH ROW EXECUTE FUNCTION public.clubs_event_link_guard();


--
-- Name: Levels level_hierarchy_guard; Type: TRIGGER; Schema: public; Owner: -
--

CREATE TRIGGER level_hierarchy_guard BEFORE INSERT OR UPDATE ON public."Levels" FOR EACH ROW EXECUTE FUNCTION public.clubs_level_guard();


--
-- Name: MembershipApprovals membership_approval_immutable; Type: TRIGGER; Schema: public; Owner: -
--

CREATE TRIGGER membership_approval_immutable BEFORE DELETE OR UPDATE ON public."MembershipApprovals" FOR EACH ROW EXECUTE FUNCTION public.clubs_membership_history_guard();


--
-- Name: MembershipHistory membership_history_immutable; Type: TRIGGER; Schema: public; Owner: -
--

CREATE TRIGGER membership_history_immutable BEFORE DELETE OR UPDATE ON public."MembershipHistory" FOR EACH ROW EXECUTE FUNCTION public.clubs_membership_history_guard();


--
-- Name: Memberships membership_integrity_guard; Type: TRIGGER; Schema: public; Owner: -
--

CREATE TRIGGER membership_integrity_guard BEFORE INSERT OR UPDATE ON public."Memberships" FOR EACH ROW EXECUTE FUNCTION public.clubs_membership_guard();


--
-- Name: MeetingMinutes minutes_body_immutable; Type: TRIGGER; Schema: public; Owner: -
--

CREATE TRIGGER minutes_body_immutable BEFORE UPDATE ON public."MeetingMinutes" FOR EACH ROW EXECUTE FUNCTION public.guard_minutes_body();


--
-- Name: Assignments office_assignment_integrity; Type: TRIGGER; Schema: public; Owner: -
--

CREATE TRIGGER office_assignment_integrity BEFORE INSERT OR UPDATE ON public."Assignments" FOR EACH ROW EXECUTE FUNCTION public.clubs_office_assignment_guard();


--
-- Name: OperationRevisions operation_history_immutable; Type: TRIGGER; Schema: public; Owner: -
--

CREATE TRIGGER operation_history_immutable BEFORE DELETE OR UPDATE ON public."OperationRevisions" FOR EACH ROW EXECUTE FUNCTION public.guard_operation_history();


--
-- Name: Orgs org_change_history; Type: TRIGGER; Schema: public; Owner: -
--

CREATE TRIGGER org_change_history AFTER INSERT OR UPDATE ON public."Orgs" FOR EACH ROW EXECUTE FUNCTION public.clubs_org_history();


--
-- Name: OrganizationHistory org_history_immutable; Type: TRIGGER; Schema: public; Owner: -
--

CREATE TRIGGER org_history_immutable BEFORE DELETE OR UPDATE ON public."OrganizationHistory" FOR EACH ROW EXECUTE FUNCTION public.clubs_membership_history_guard();


--
-- Name: Orgs organization_hierarchy_guard; Type: TRIGGER; Schema: public; Owner: -
--

CREATE TRIGGER organization_hierarchy_guard BEFORE INSERT OR UPDATE ON public."Orgs" FOR EACH ROW EXECUTE FUNCTION public.clubs_hierarchy_guard();


--
-- Name: Assignments role_assignment_history; Type: TRIGGER; Schema: public; Owner: -
--

CREATE TRIGGER role_assignment_history AFTER INSERT OR DELETE ON public."Assignments" FOR EACH ROW EXECUTE FUNCTION public.clubs_role_assignment_history();


--
-- Name: RoleAssignmentHistory role_assignment_history_immutable; Type: TRIGGER; Schema: public; Owner: -
--

CREATE TRIGGER role_assignment_history_immutable BEFORE DELETE OR UPDATE ON public."RoleAssignmentHistory" FOR EACH ROW EXECUTE FUNCTION public.clubs_membership_history_guard();


--
-- Name: TransferDecisions transfer_decision_immutable; Type: TRIGGER; Schema: public; Owner: -
--

CREATE TRIGGER transfer_decision_immutable BEFORE DELETE OR UPDATE ON public."TransferDecisions" FOR EACH ROW EXECUTE FUNCTION public.clubs_membership_history_guard();


--
-- Name: Assignments FK_Assignments_Memberships_TenantId_MembershipId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."Assignments"
    ADD CONSTRAINT "FK_Assignments_Memberships_TenantId_MembershipId" FOREIGN KEY ("TenantId", "MembershipId") REFERENCES public."Memberships"("TenantId", "Id") ON DELETE RESTRICT;


--
-- Name: Assignments FK_Assignments_OfficerAppointments_TenantId_AppointmentId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."Assignments"
    ADD CONSTRAINT "FK_Assignments_OfficerAppointments_TenantId_AppointmentId" FOREIGN KEY ("TenantId", "AppointmentId") REFERENCES public."OfficerAppointments"("TenantId", "Id") ON DELETE RESTRICT;


--
-- Name: Assignments FK_Assignments_Orgs_TenantId_OrgId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."Assignments"
    ADD CONSTRAINT "FK_Assignments_Orgs_TenantId_OrgId" FOREIGN KEY ("TenantId", "OrgId") REFERENCES public."Orgs"("TenantId", "Id") ON DELETE RESTRICT;


--
-- Name: Assignments FK_Assignments_Roles_TenantId_RoleId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."Assignments"
    ADD CONSTRAINT "FK_Assignments_Roles_TenantId_RoleId" FOREIGN KEY ("TenantId", "RoleId") REFERENCES public."Roles"("TenantId", "Id") ON DELETE RESTRICT;


--
-- Name: Assignments FK_Assignments_UserProfiles_UserId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."Assignments"
    ADD CONSTRAINT "FK_Assignments_UserProfiles_UserId" FOREIGN KEY ("UserId") REFERENCES public."UserProfiles"("Id") ON DELETE RESTRICT;


--
-- Name: Associations FK_Associations_Tenants_Id; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."Associations"
    ADD CONSTRAINT "FK_Associations_Tenants_Id" FOREIGN KEY ("Id") REFERENCES public."Tenants"("Id") ON DELETE CASCADE;


--
-- Name: CertificateTemplates FK_CertificateTemplates_Associations_TenantId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."CertificateTemplates"
    ADD CONSTRAINT "FK_CertificateTemplates_Associations_TenantId" FOREIGN KEY ("TenantId") REFERENCES public."Associations"("Id") ON DELETE RESTRICT;


--
-- Name: CertificateTemplates FK_CertificateTemplates_Orgs_TenantId_OrgId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."CertificateTemplates"
    ADD CONSTRAINT "FK_CertificateTemplates_Orgs_TenantId_OrgId" FOREIGN KEY ("TenantId", "OrgId") REFERENCES public."Orgs"("TenantId", "Id") ON DELETE RESTRICT;


--
-- Name: ClubHealthConfigurations FK_ClubHealthConfigurations_Associations_TenantId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."ClubHealthConfigurations"
    ADD CONSTRAINT "FK_ClubHealthConfigurations_Associations_TenantId" FOREIGN KEY ("TenantId") REFERENCES public."Associations"("Id") ON DELETE RESTRICT;


--
-- Name: ClubMeetings FK_ClubMeetings_Associations_TenantId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."ClubMeetings"
    ADD CONSTRAINT "FK_ClubMeetings_Associations_TenantId" FOREIGN KEY ("TenantId") REFERENCES public."Associations"("Id") ON DELETE RESTRICT;


--
-- Name: ClubMeetings FK_ClubMeetings_Committees_TenantId_CommitteeId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."ClubMeetings"
    ADD CONSTRAINT "FK_ClubMeetings_Committees_TenantId_CommitteeId" FOREIGN KEY ("TenantId", "CommitteeId") REFERENCES public."Committees"("TenantId", "Id") ON DELETE RESTRICT;


--
-- Name: ClubMeetings FK_ClubMeetings_EventSessions_TenantId_SessionId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."ClubMeetings"
    ADD CONSTRAINT "FK_ClubMeetings_EventSessions_TenantId_SessionId" FOREIGN KEY ("TenantId", "SessionId") REFERENCES public."EventSessions"("TenantId", "Id") ON DELETE RESTRICT;


--
-- Name: ClubMeetings FK_ClubMeetings_Events_TenantId_EventId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."ClubMeetings"
    ADD CONSTRAINT "FK_ClubMeetings_Events_TenantId_EventId" FOREIGN KEY ("TenantId", "EventId") REFERENCES public."Events"("TenantId", "Id") ON DELETE RESTRICT;


--
-- Name: ClubMeetings FK_ClubMeetings_Orgs_TenantId_OrgId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."ClubMeetings"
    ADD CONSTRAINT "FK_ClubMeetings_Orgs_TenantId_OrgId" FOREIGN KEY ("TenantId", "OrgId") REFERENCES public."Orgs"("TenantId", "Id") ON DELETE RESTRICT;


--
-- Name: CommitteeActivities FK_CommitteeActivities_Associations_TenantId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."CommitteeActivities"
    ADD CONSTRAINT "FK_CommitteeActivities_Associations_TenantId" FOREIGN KEY ("TenantId") REFERENCES public."Associations"("Id") ON DELETE RESTRICT;


--
-- Name: CommitteeActivities FK_CommitteeActivities_Committees_TenantId_CommitteeId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."CommitteeActivities"
    ADD CONSTRAINT "FK_CommitteeActivities_Committees_TenantId_CommitteeId" FOREIGN KEY ("TenantId", "CommitteeId") REFERENCES public."Committees"("TenantId", "Id") ON DELETE RESTRICT;


--
-- Name: CommitteeMemberships FK_CommitteeMemberships_Associations_TenantId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."CommitteeMemberships"
    ADD CONSTRAINT "FK_CommitteeMemberships_Associations_TenantId" FOREIGN KEY ("TenantId") REFERENCES public."Associations"("Id") ON DELETE RESTRICT;


--
-- Name: CommitteeMemberships FK_CommitteeMemberships_CommitteeRoles_TenantId_RoleId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."CommitteeMemberships"
    ADD CONSTRAINT "FK_CommitteeMemberships_CommitteeRoles_TenantId_RoleId" FOREIGN KEY ("TenantId", "RoleId") REFERENCES public."CommitteeRoles"("TenantId", "Id") ON DELETE RESTRICT;


--
-- Name: CommitteeMemberships FK_CommitteeMemberships_Committees_TenantId_CommitteeId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."CommitteeMemberships"
    ADD CONSTRAINT "FK_CommitteeMemberships_Committees_TenantId_CommitteeId" FOREIGN KEY ("TenantId", "CommitteeId") REFERENCES public."Committees"("TenantId", "Id") ON DELETE RESTRICT;


--
-- Name: CommitteeMemberships FK_CommitteeMemberships_Persons_PersonId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."CommitteeMemberships"
    ADD CONSTRAINT "FK_CommitteeMemberships_Persons_PersonId" FOREIGN KEY ("PersonId") REFERENCES public."Persons"("Id") ON DELETE RESTRICT;


--
-- Name: CommitteeRoles FK_CommitteeRoles_Associations_TenantId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."CommitteeRoles"
    ADD CONSTRAINT "FK_CommitteeRoles_Associations_TenantId" FOREIGN KEY ("TenantId") REFERENCES public."Associations"("Id") ON DELETE RESTRICT;


--
-- Name: Committees FK_Committees_Associations_TenantId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."Committees"
    ADD CONSTRAINT "FK_Committees_Associations_TenantId" FOREIGN KEY ("TenantId") REFERENCES public."Associations"("Id") ON DELETE RESTRICT;


--
-- Name: Committees FK_Committees_Orgs_TenantId_OrgId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."Committees"
    ADD CONSTRAINT "FK_Committees_Orgs_TenantId_OrgId" FOREIGN KEY ("TenantId", "OrgId") REFERENCES public."Orgs"("TenantId", "Id") ON DELETE RESTRICT;


--
-- Name: CommunicationAccounts FK_CommunicationAccounts_Associations_TenantId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."CommunicationAccounts"
    ADD CONSTRAINT "FK_CommunicationAccounts_Associations_TenantId" FOREIGN KEY ("TenantId") REFERENCES public."Associations"("Id") ON DELETE RESTRICT;


--
-- Name: CommunicationAccounts FK_CommunicationAccounts_Orgs_TenantId_OrgId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."CommunicationAccounts"
    ADD CONSTRAINT "FK_CommunicationAccounts_Orgs_TenantId_OrgId" FOREIGN KEY ("TenantId", "OrgId") REFERENCES public."Orgs"("TenantId", "Id") ON DELETE RESTRICT;


--
-- Name: CommunicationAttachments FK_CommunicationAttachments_Associations_TenantId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."CommunicationAttachments"
    ADD CONSTRAINT "FK_CommunicationAttachments_Associations_TenantId" FOREIGN KEY ("TenantId") REFERENCES public."Associations"("Id") ON DELETE RESTRICT;


--
-- Name: CommunicationAttachments FK_CommunicationAttachments_CommunicationAccounts_TenantId_Pro~; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."CommunicationAttachments"
    ADD CONSTRAINT "FK_CommunicationAttachments_CommunicationAccounts_TenantId_Pro~" FOREIGN KEY ("TenantId", "ProviderAccountId") REFERENCES public."CommunicationAccounts"("TenantId", "Id") ON DELETE RESTRICT;


--
-- Name: CommunicationAttachments FK_CommunicationAttachments_CommunicationMessages_TenantId_Mes~; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."CommunicationAttachments"
    ADD CONSTRAINT "FK_CommunicationAttachments_CommunicationMessages_TenantId_Mes~" FOREIGN KEY ("TenantId", "MessageId") REFERENCES public."CommunicationMessages"("TenantId", "Id") ON DELETE RESTRICT;


--
-- Name: CommunicationAudiences FK_CommunicationAudiences_Associations_TenantId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."CommunicationAudiences"
    ADD CONSTRAINT "FK_CommunicationAudiences_Associations_TenantId" FOREIGN KEY ("TenantId") REFERENCES public."Associations"("Id") ON DELETE RESTRICT;


--
-- Name: CommunicationAudiences FK_CommunicationAudiences_Orgs_TenantId_OrgId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."CommunicationAudiences"
    ADD CONSTRAINT "FK_CommunicationAudiences_Orgs_TenantId_OrgId" FOREIGN KEY ("TenantId", "OrgId") REFERENCES public."Orgs"("TenantId", "Id") ON DELETE RESTRICT;


--
-- Name: CommunicationAutomations FK_CommunicationAutomations_Associations_TenantId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."CommunicationAutomations"
    ADD CONSTRAINT "FK_CommunicationAutomations_Associations_TenantId" FOREIGN KEY ("TenantId") REFERENCES public."Associations"("Id") ON DELETE RESTRICT;


--
-- Name: CommunicationAutomations FK_CommunicationAutomations_CommunicationAccounts_TenantId_Acc~; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."CommunicationAutomations"
    ADD CONSTRAINT "FK_CommunicationAutomations_CommunicationAccounts_TenantId_Acc~" FOREIGN KEY ("TenantId", "AccountId") REFERENCES public."CommunicationAccounts"("TenantId", "Id") ON DELETE RESTRICT;


--
-- Name: CommunicationAutomations FK_CommunicationAutomations_CommunicationTemplates_TenantId_Te~; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."CommunicationAutomations"
    ADD CONSTRAINT "FK_CommunicationAutomations_CommunicationTemplates_TenantId_Te~" FOREIGN KEY ("TenantId", "TemplateId") REFERENCES public."CommunicationTemplates"("TenantId", "Id") ON DELETE RESTRICT;


--
-- Name: CommunicationAutomations FK_CommunicationAutomations_GovernanceRequests_TenantId_Approv~; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."CommunicationAutomations"
    ADD CONSTRAINT "FK_CommunicationAutomations_GovernanceRequests_TenantId_Approv~" FOREIGN KEY ("TenantId", "ApprovalRequestId") REFERENCES public."GovernanceRequests"("TenantId", "Id") ON DELETE RESTRICT;


--
-- Name: CommunicationAutomations FK_CommunicationAutomations_Orgs_TenantId_OrgId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."CommunicationAutomations"
    ADD CONSTRAINT "FK_CommunicationAutomations_Orgs_TenantId_OrgId" FOREIGN KEY ("TenantId", "OrgId") REFERENCES public."Orgs"("TenantId", "Id") ON DELETE RESTRICT;


--
-- Name: CommunicationBroadcasts FK_CommunicationBroadcasts_Associations_TenantId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."CommunicationBroadcasts"
    ADD CONSTRAINT "FK_CommunicationBroadcasts_Associations_TenantId" FOREIGN KEY ("TenantId") REFERENCES public."Associations"("Id") ON DELETE RESTRICT;


--
-- Name: CommunicationBroadcasts FK_CommunicationBroadcasts_CommunicationAccounts_TenantId_Acco~; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."CommunicationBroadcasts"
    ADD CONSTRAINT "FK_CommunicationBroadcasts_CommunicationAccounts_TenantId_Acco~" FOREIGN KEY ("TenantId", "AccountId") REFERENCES public."CommunicationAccounts"("TenantId", "Id") ON DELETE RESTRICT;


--
-- Name: CommunicationBroadcasts FK_CommunicationBroadcasts_CommunicationTemplates_TenantId_Tem~; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."CommunicationBroadcasts"
    ADD CONSTRAINT "FK_CommunicationBroadcasts_CommunicationTemplates_TenantId_Tem~" FOREIGN KEY ("TenantId", "TemplateId") REFERENCES public."CommunicationTemplates"("TenantId", "Id") ON DELETE RESTRICT;


--
-- Name: CommunicationBroadcasts FK_CommunicationBroadcasts_Events_TenantId_EventId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."CommunicationBroadcasts"
    ADD CONSTRAINT "FK_CommunicationBroadcasts_Events_TenantId_EventId" FOREIGN KEY ("TenantId", "EventId") REFERENCES public."Events"("TenantId", "Id") ON DELETE RESTRICT;


--
-- Name: CommunicationBroadcasts FK_CommunicationBroadcasts_GovernanceRequests_TenantId_Approva~; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."CommunicationBroadcasts"
    ADD CONSTRAINT "FK_CommunicationBroadcasts_GovernanceRequests_TenantId_Approva~" FOREIGN KEY ("TenantId", "ApprovalRequestId") REFERENCES public."GovernanceRequests"("TenantId", "Id") ON DELETE RESTRICT;


--
-- Name: CommunicationBroadcasts FK_CommunicationBroadcasts_Orgs_TenantId_OrgId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."CommunicationBroadcasts"
    ADD CONSTRAINT "FK_CommunicationBroadcasts_Orgs_TenantId_OrgId" FOREIGN KEY ("TenantId", "OrgId") REFERENCES public."Orgs"("TenantId", "Id") ON DELETE RESTRICT;


--
-- Name: CommunicationExecutions FK_CommunicationExecutions_Associations_TenantId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."CommunicationExecutions"
    ADD CONSTRAINT "FK_CommunicationExecutions_Associations_TenantId" FOREIGN KEY ("TenantId") REFERENCES public."Associations"("Id") ON DELETE RESTRICT;


--
-- Name: CommunicationExecutions FK_CommunicationExecutions_CommunicationAutomations_TenantId_A~; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."CommunicationExecutions"
    ADD CONSTRAINT "FK_CommunicationExecutions_CommunicationAutomations_TenantId_A~" FOREIGN KEY ("TenantId", "AutomationId") REFERENCES public."CommunicationAutomations"("TenantId", "Id") ON DELETE RESTRICT;


--
-- Name: CommunicationMessages FK_CommunicationMessages_Associations_TenantId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."CommunicationMessages"
    ADD CONSTRAINT "FK_CommunicationMessages_Associations_TenantId" FOREIGN KEY ("TenantId") REFERENCES public."Associations"("Id") ON DELETE RESTRICT;


--
-- Name: CommunicationMessages FK_CommunicationMessages_CommunicationBroadcasts_TenantId_Broa~; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."CommunicationMessages"
    ADD CONSTRAINT "FK_CommunicationMessages_CommunicationBroadcasts_TenantId_Broa~" FOREIGN KEY ("TenantId", "BroadcastId") REFERENCES public."CommunicationBroadcasts"("TenantId", "Id") ON DELETE RESTRICT;


--
-- Name: CommunicationMessages FK_CommunicationMessages_Conversations_TenantId_ConversationId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."CommunicationMessages"
    ADD CONSTRAINT "FK_CommunicationMessages_Conversations_TenantId_ConversationId" FOREIGN KEY ("TenantId", "ConversationId") REFERENCES public."Conversations"("TenantId", "Id") ON DELETE RESTRICT;


--
-- Name: CommunicationMessages FK_CommunicationMessages_Orgs_TenantId_OrgId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."CommunicationMessages"
    ADD CONSTRAINT "FK_CommunicationMessages_Orgs_TenantId_OrgId" FOREIGN KEY ("TenantId", "OrgId") REFERENCES public."Orgs"("TenantId", "Id") ON DELETE RESTRICT;


--
-- Name: CommunicationPolicies FK_CommunicationPolicies_Associations_TenantId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."CommunicationPolicies"
    ADD CONSTRAINT "FK_CommunicationPolicies_Associations_TenantId" FOREIGN KEY ("TenantId") REFERENCES public."Associations"("Id") ON DELETE RESTRICT;


--
-- Name: CommunicationPolicies FK_CommunicationPolicies_Orgs_TenantId_OrgId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."CommunicationPolicies"
    ADD CONSTRAINT "FK_CommunicationPolicies_Orgs_TenantId_OrgId" FOREIGN KEY ("TenantId", "OrgId") REFERENCES public."Orgs"("TenantId", "Id") ON DELETE RESTRICT;


--
-- Name: CommunicationPreferences FK_CommunicationPreferences_UserProfiles_UserId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."CommunicationPreferences"
    ADD CONSTRAINT "FK_CommunicationPreferences_UserProfiles_UserId" FOREIGN KEY ("UserId") REFERENCES public."UserProfiles"("Id") ON DELETE RESTRICT;


--
-- Name: CommunicationProviderEvents FK_CommunicationProviderEvents_Associations_TenantId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."CommunicationProviderEvents"
    ADD CONSTRAINT "FK_CommunicationProviderEvents_Associations_TenantId" FOREIGN KEY ("TenantId") REFERENCES public."Associations"("Id") ON DELETE RESTRICT;


--
-- Name: CommunicationProviderEvents FK_CommunicationProviderEvents_CommunicationAccounts_TenantId_~; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."CommunicationProviderEvents"
    ADD CONSTRAINT "FK_CommunicationProviderEvents_CommunicationAccounts_TenantId_~" FOREIGN KEY ("TenantId", "AccountId") REFERENCES public."CommunicationAccounts"("TenantId", "Id") ON DELETE RESTRICT;


--
-- Name: CommunicationRecipients FK_CommunicationRecipients_Associations_TenantId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."CommunicationRecipients"
    ADD CONSTRAINT "FK_CommunicationRecipients_Associations_TenantId" FOREIGN KEY ("TenantId") REFERENCES public."Associations"("Id") ON DELETE RESTRICT;


--
-- Name: CommunicationRecipients FK_CommunicationRecipients_CommunicationBroadcasts_TenantId_Br~; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."CommunicationRecipients"
    ADD CONSTRAINT "FK_CommunicationRecipients_CommunicationBroadcasts_TenantId_Br~" FOREIGN KEY ("TenantId", "BroadcastId") REFERENCES public."CommunicationBroadcasts"("TenantId", "Id") ON DELETE RESTRICT;


--
-- Name: CommunicationRecipients FK_CommunicationRecipients_Persons_PersonId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."CommunicationRecipients"
    ADD CONSTRAINT "FK_CommunicationRecipients_Persons_PersonId" FOREIGN KEY ("PersonId") REFERENCES public."Persons"("Id") ON DELETE RESTRICT;


--
-- Name: CommunicationRevisions FK_CommunicationRevisions_Associations_TenantId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."CommunicationRevisions"
    ADD CONSTRAINT "FK_CommunicationRevisions_Associations_TenantId" FOREIGN KEY ("TenantId") REFERENCES public."Associations"("Id") ON DELETE RESTRICT;


--
-- Name: CommunicationSuppressions FK_CommunicationSuppressions_Associations_TenantId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."CommunicationSuppressions"
    ADD CONSTRAINT "FK_CommunicationSuppressions_Associations_TenantId" FOREIGN KEY ("TenantId") REFERENCES public."Associations"("Id") ON DELETE RESTRICT;


--
-- Name: CommunicationTemplates FK_CommunicationTemplates_Associations_TenantId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."CommunicationTemplates"
    ADD CONSTRAINT "FK_CommunicationTemplates_Associations_TenantId" FOREIGN KEY ("TenantId") REFERENCES public."Associations"("Id") ON DELETE RESTRICT;


--
-- Name: CommunicationTemplates FK_CommunicationTemplates_CommunicationAccounts_TenantId_Accou~; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."CommunicationTemplates"
    ADD CONSTRAINT "FK_CommunicationTemplates_CommunicationAccounts_TenantId_Accou~" FOREIGN KEY ("TenantId", "AccountId") REFERENCES public."CommunicationAccounts"("TenantId", "Id") ON DELETE RESTRICT;


--
-- Name: CommunicationTemplates FK_CommunicationTemplates_Orgs_TenantId_OrgId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."CommunicationTemplates"
    ADD CONSTRAINT "FK_CommunicationTemplates_Orgs_TenantId_OrgId" FOREIGN KEY ("TenantId", "OrgId") REFERENCES public."Orgs"("TenantId", "Id") ON DELETE RESTRICT;


--
-- Name: ConversationParticipants FK_ConversationParticipants_Associations_TenantId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."ConversationParticipants"
    ADD CONSTRAINT "FK_ConversationParticipants_Associations_TenantId" FOREIGN KEY ("TenantId") REFERENCES public."Associations"("Id") ON DELETE RESTRICT;


--
-- Name: ConversationParticipants FK_ConversationParticipants_Conversations_TenantId_Conversatio~; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."ConversationParticipants"
    ADD CONSTRAINT "FK_ConversationParticipants_Conversations_TenantId_Conversatio~" FOREIGN KEY ("TenantId", "ConversationId") REFERENCES public."Conversations"("TenantId", "Id") ON DELETE RESTRICT;


--
-- Name: ConversationParticipants FK_ConversationParticipants_UserProfiles_UserId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."ConversationParticipants"
    ADD CONSTRAINT "FK_ConversationParticipants_UserProfiles_UserId" FOREIGN KEY ("UserId") REFERENCES public."UserProfiles"("Id") ON DELETE RESTRICT;


--
-- Name: Conversations FK_Conversations_Associations_TenantId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."Conversations"
    ADD CONSTRAINT "FK_Conversations_Associations_TenantId" FOREIGN KEY ("TenantId") REFERENCES public."Associations"("Id") ON DELETE RESTRICT;


--
-- Name: Conversations FK_Conversations_Committees_TenantId_CommitteeId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."Conversations"
    ADD CONSTRAINT "FK_Conversations_Committees_TenantId_CommitteeId" FOREIGN KEY ("TenantId", "CommitteeId") REFERENCES public."Committees"("TenantId", "Id") ON DELETE RESTRICT;


--
-- Name: Conversations FK_Conversations_CommunicationAccounts_TenantId_AccountId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."Conversations"
    ADD CONSTRAINT "FK_Conversations_CommunicationAccounts_TenantId_AccountId" FOREIGN KEY ("TenantId", "AccountId") REFERENCES public."CommunicationAccounts"("TenantId", "Id") ON DELETE RESTRICT;


--
-- Name: Conversations FK_Conversations_Events_TenantId_EventId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."Conversations"
    ADD CONSTRAINT "FK_Conversations_Events_TenantId_EventId" FOREIGN KEY ("TenantId", "EventId") REFERENCES public."Events"("TenantId", "Id") ON DELETE RESTRICT;


--
-- Name: Conversations FK_Conversations_Orgs_TenantId_OrgId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."Conversations"
    ADD CONSTRAINT "FK_Conversations_Orgs_TenantId_OrgId" FOREIGN KEY ("TenantId", "OrgId") REFERENCES public."Orgs"("TenantId", "Id") ON DELETE RESTRICT;


--
-- Name: DigitalCardVerifications FK_DigitalCardVerifications_DigitalCards_CardId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."DigitalCardVerifications"
    ADD CONSTRAINT "FK_DigitalCardVerifications_DigitalCards_CardId" FOREIGN KEY ("CardId") REFERENCES public."DigitalCards"("Id") ON DELETE RESTRICT;


--
-- Name: DigitalCards FK_DigitalCards_Associations_TenantId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."DigitalCards"
    ADD CONSTRAINT "FK_DigitalCards_Associations_TenantId" FOREIGN KEY ("TenantId") REFERENCES public."Associations"("Id") ON DELETE RESTRICT;


--
-- Name: DigitalCards FK_DigitalCards_Memberships_TenantId_MembershipId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."DigitalCards"
    ADD CONSTRAINT "FK_DigitalCards_Memberships_TenantId_MembershipId" FOREIGN KEY ("TenantId", "MembershipId") REFERENCES public."Memberships"("TenantId", "Id") ON DELETE RESTRICT;


--
-- Name: DocumentVersions FK_DocumentVersions_Associations_TenantId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."DocumentVersions"
    ADD CONSTRAINT "FK_DocumentVersions_Associations_TenantId" FOREIGN KEY ("TenantId") REFERENCES public."Associations"("Id") ON DELETE RESTRICT;


--
-- Name: DocumentVersions FK_DocumentVersions_GovernanceDocuments_TenantId_DocumentId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."DocumentVersions"
    ADD CONSTRAINT "FK_DocumentVersions_GovernanceDocuments_TenantId_DocumentId" FOREIGN KEY ("TenantId", "DocumentId") REFERENCES public."GovernanceDocuments"("TenantId", "Id") ON DELETE RESTRICT;


--
-- Name: DocumentVersions FK_DocumentVersions_Orgs_TenantId_OrgId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."DocumentVersions"
    ADD CONSTRAINT "FK_DocumentVersions_Orgs_TenantId_OrgId" FOREIGN KEY ("TenantId", "OrgId") REFERENCES public."Orgs"("TenantId", "Id") ON DELETE RESTRICT;


--
-- Name: EventAudiences FK_EventAudiences_Associations_TenantId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."EventAudiences"
    ADD CONSTRAINT "FK_EventAudiences_Associations_TenantId" FOREIGN KEY ("TenantId") REFERENCES public."Associations"("Id") ON DELETE RESTRICT;


--
-- Name: EventAudiences FK_EventAudiences_Committees_TenantId_CommitteeId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."EventAudiences"
    ADD CONSTRAINT "FK_EventAudiences_Committees_TenantId_CommitteeId" FOREIGN KEY ("TenantId", "CommitteeId") REFERENCES public."Committees"("TenantId", "Id") ON DELETE RESTRICT;


--
-- Name: EventAudiences FK_EventAudiences_Events_TenantId_EventId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."EventAudiences"
    ADD CONSTRAINT "FK_EventAudiences_Events_TenantId_EventId" FOREIGN KEY ("TenantId", "EventId") REFERENCES public."Events"("TenantId", "Id") ON DELETE RESTRICT;


--
-- Name: EventAudiences FK_EventAudiences_MembershipCategories_TenantId_CategoryId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."EventAudiences"
    ADD CONSTRAINT "FK_EventAudiences_MembershipCategories_TenantId_CategoryId" FOREIGN KEY ("TenantId", "CategoryId") REFERENCES public."MembershipCategories"("TenantId", "Id") ON DELETE RESTRICT;


--
-- Name: EventAudiences FK_EventAudiences_Orgs_TenantId_OrgId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."EventAudiences"
    ADD CONSTRAINT "FK_EventAudiences_Orgs_TenantId_OrgId" FOREIGN KEY ("TenantId", "OrgId") REFERENCES public."Orgs"("TenantId", "Id") ON DELETE RESTRICT;


--
-- Name: EventAudiences FK_EventAudiences_Persons_PersonId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."EventAudiences"
    ADD CONSTRAINT "FK_EventAudiences_Persons_PersonId" FOREIGN KEY ("PersonId") REFERENCES public."Persons"("Id") ON DELETE RESTRICT;


--
-- Name: EventAudiences FK_EventAudiences_Roles_TenantId_RoleId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."EventAudiences"
    ADD CONSTRAINT "FK_EventAudiences_Roles_TenantId_RoleId" FOREIGN KEY ("TenantId", "RoleId") REFERENCES public."Roles"("TenantId", "Id") ON DELETE RESTRICT;


--
-- Name: EventCheckins FK_EventCheckins_Associations_TenantId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."EventCheckins"
    ADD CONSTRAINT "FK_EventCheckins_Associations_TenantId" FOREIGN KEY ("TenantId") REFERENCES public."Associations"("Id") ON DELETE RESTRICT;


--
-- Name: EventCheckins FK_EventCheckins_EventRegistrations_TenantId_RegistrationId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."EventCheckins"
    ADD CONSTRAINT "FK_EventCheckins_EventRegistrations_TenantId_RegistrationId" FOREIGN KEY ("TenantId", "RegistrationId") REFERENCES public."EventRegistrations"("TenantId", "Id") ON DELETE RESTRICT;


--
-- Name: EventCheckins FK_EventCheckins_EventSessions_TenantId_SessionId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."EventCheckins"
    ADD CONSTRAINT "FK_EventCheckins_EventSessions_TenantId_SessionId" FOREIGN KEY ("TenantId", "SessionId") REFERENCES public."EventSessions"("TenantId", "Id") ON DELETE RESTRICT;


--
-- Name: EventCheckins FK_EventCheckins_Events_TenantId_EventId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."EventCheckins"
    ADD CONSTRAINT "FK_EventCheckins_Events_TenantId_EventId" FOREIGN KEY ("TenantId", "EventId") REFERENCES public."Events"("TenantId", "Id") ON DELETE RESTRICT;


--
-- Name: EventCheckins FK_EventCheckins_UserProfiles_OperatorId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."EventCheckins"
    ADD CONSTRAINT "FK_EventCheckins_UserProfiles_OperatorId" FOREIGN KEY ("OperatorId") REFERENCES public."UserProfiles"("Id") ON DELETE RESTRICT;


--
-- Name: EventCohosts FK_EventCohosts_Associations_TenantId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."EventCohosts"
    ADD CONSTRAINT "FK_EventCohosts_Associations_TenantId" FOREIGN KEY ("TenantId") REFERENCES public."Associations"("Id") ON DELETE RESTRICT;


--
-- Name: EventCohosts FK_EventCohosts_Events_TenantId_EventId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."EventCohosts"
    ADD CONSTRAINT "FK_EventCohosts_Events_TenantId_EventId" FOREIGN KEY ("TenantId", "EventId") REFERENCES public."Events"("TenantId", "Id") ON DELETE RESTRICT;


--
-- Name: EventCohosts FK_EventCohosts_Orgs_TenantId_OrgId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."EventCohosts"
    ADD CONSTRAINT "FK_EventCohosts_Orgs_TenantId_OrgId" FOREIGN KEY ("TenantId", "OrgId") REFERENCES public."Orgs"("TenantId", "Id") ON DELETE RESTRICT;


--
-- Name: EventFeedbackForms FK_EventFeedbackForms_Associations_TenantId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."EventFeedbackForms"
    ADD CONSTRAINT "FK_EventFeedbackForms_Associations_TenantId" FOREIGN KEY ("TenantId") REFERENCES public."Associations"("Id") ON DELETE RESTRICT;


--
-- Name: EventFeedbackForms FK_EventFeedbackForms_Events_TenantId_EventId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."EventFeedbackForms"
    ADD CONSTRAINT "FK_EventFeedbackForms_Events_TenantId_EventId" FOREIGN KEY ("TenantId", "EventId") REFERENCES public."Events"("TenantId", "Id") ON DELETE RESTRICT;


--
-- Name: EventFeedbackResponses FK_EventFeedbackResponses_Associations_TenantId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."EventFeedbackResponses"
    ADD CONSTRAINT "FK_EventFeedbackResponses_Associations_TenantId" FOREIGN KEY ("TenantId") REFERENCES public."Associations"("Id") ON DELETE RESTRICT;


--
-- Name: EventFeedbackResponses FK_EventFeedbackResponses_EventRegistrations_TenantId_Registra~; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."EventFeedbackResponses"
    ADD CONSTRAINT "FK_EventFeedbackResponses_EventRegistrations_TenantId_Registra~" FOREIGN KEY ("TenantId", "RegistrationId") REFERENCES public."EventRegistrations"("TenantId", "Id") ON DELETE RESTRICT;


--
-- Name: EventFeedbackResponses FK_EventFeedbackResponses_Events_TenantId_EventId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."EventFeedbackResponses"
    ADD CONSTRAINT "FK_EventFeedbackResponses_Events_TenantId_EventId" FOREIGN KEY ("TenantId", "EventId") REFERENCES public."Events"("TenantId", "Id") ON DELETE RESTRICT;


--
-- Name: EventInvitations FK_EventInvitations_Associations_TenantId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."EventInvitations"
    ADD CONSTRAINT "FK_EventInvitations_Associations_TenantId" FOREIGN KEY ("TenantId") REFERENCES public."Associations"("Id") ON DELETE RESTRICT;


--
-- Name: EventInvitations FK_EventInvitations_Events_TenantId_EventId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."EventInvitations"
    ADD CONSTRAINT "FK_EventInvitations_Events_TenantId_EventId" FOREIGN KEY ("TenantId", "EventId") REFERENCES public."Events"("TenantId", "Id") ON DELETE RESTRICT;


--
-- Name: EventInvitations FK_EventInvitations_Persons_PersonId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."EventInvitations"
    ADD CONSTRAINT "FK_EventInvitations_Persons_PersonId" FOREIGN KEY ("PersonId") REFERENCES public."Persons"("Id") ON DELETE RESTRICT;


--
-- Name: EventInvitations FK_EventInvitations_UserProfiles_InviterId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."EventInvitations"
    ADD CONSTRAINT "FK_EventInvitations_UserProfiles_InviterId" FOREIGN KEY ("InviterId") REFERENCES public."UserProfiles"("Id") ON DELETE RESTRICT;


--
-- Name: EventNotices FK_EventNotices_Associations_TenantId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."EventNotices"
    ADD CONSTRAINT "FK_EventNotices_Associations_TenantId" FOREIGN KEY ("TenantId") REFERENCES public."Associations"("Id") ON DELETE RESTRICT;


--
-- Name: EventNotices FK_EventNotices_Events_TenantId_EventId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."EventNotices"
    ADD CONSTRAINT "FK_EventNotices_Events_TenantId_EventId" FOREIGN KEY ("TenantId", "EventId") REFERENCES public."Events"("TenantId", "Id") ON DELETE RESTRICT;


--
-- Name: EventRegistrations FK_EventRegistrations_Associations_TenantId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."EventRegistrations"
    ADD CONSTRAINT "FK_EventRegistrations_Associations_TenantId" FOREIGN KEY ("TenantId") REFERENCES public."Associations"("Id") ON DELETE RESTRICT;


--
-- Name: EventRegistrations FK_EventRegistrations_EventInvitations_TenantId_InvitationId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."EventRegistrations"
    ADD CONSTRAINT "FK_EventRegistrations_EventInvitations_TenantId_InvitationId" FOREIGN KEY ("TenantId", "InvitationId") REFERENCES public."EventInvitations"("TenantId", "Id") ON DELETE RESTRICT;


--
-- Name: EventRegistrations FK_EventRegistrations_Events_TenantId_EventId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."EventRegistrations"
    ADD CONSTRAINT "FK_EventRegistrations_Events_TenantId_EventId" FOREIGN KEY ("TenantId", "EventId") REFERENCES public."Events"("TenantId", "Id") ON DELETE RESTRICT;


--
-- Name: EventRegistrations FK_EventRegistrations_GovernanceRequests_TenantId_ApprovalRequ~; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."EventRegistrations"
    ADD CONSTRAINT "FK_EventRegistrations_GovernanceRequests_TenantId_ApprovalRequ~" FOREIGN KEY ("TenantId", "ApprovalRequestId") REFERENCES public."GovernanceRequests"("TenantId", "Id") ON DELETE RESTRICT;


--
-- Name: EventRegistrations FK_EventRegistrations_Memberships_TenantId_MembershipId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."EventRegistrations"
    ADD CONSTRAINT "FK_EventRegistrations_Memberships_TenantId_MembershipId" FOREIGN KEY ("TenantId", "MembershipId") REFERENCES public."Memberships"("TenantId", "Id") ON DELETE RESTRICT;


--
-- Name: EventRegistrations FK_EventRegistrations_Persons_PersonId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."EventRegistrations"
    ADD CONSTRAINT "FK_EventRegistrations_Persons_PersonId" FOREIGN KEY ("PersonId") REFERENCES public."Persons"("Id") ON DELETE RESTRICT;


--
-- Name: EventRsvpHistory FK_EventRsvpHistory_Associations_TenantId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."EventRsvpHistory"
    ADD CONSTRAINT "FK_EventRsvpHistory_Associations_TenantId" FOREIGN KEY ("TenantId") REFERENCES public."Associations"("Id") ON DELETE RESTRICT;


--
-- Name: EventRsvpHistory FK_EventRsvpHistory_EventInvitations_TenantId_InvitationId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."EventRsvpHistory"
    ADD CONSTRAINT "FK_EventRsvpHistory_EventInvitations_TenantId_InvitationId" FOREIGN KEY ("TenantId", "InvitationId") REFERENCES public."EventInvitations"("TenantId", "Id") ON DELETE RESTRICT;


--
-- Name: EventSavedFilters FK_EventSavedFilters_UserProfiles_UserId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."EventSavedFilters"
    ADD CONSTRAINT "FK_EventSavedFilters_UserProfiles_UserId" FOREIGN KEY ("UserId") REFERENCES public."UserProfiles"("Id") ON DELETE CASCADE;


--
-- Name: EventSessionBookings FK_EventSessionBookings_Associations_TenantId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."EventSessionBookings"
    ADD CONSTRAINT "FK_EventSessionBookings_Associations_TenantId" FOREIGN KEY ("TenantId") REFERENCES public."Associations"("Id") ON DELETE RESTRICT;


--
-- Name: EventSessionBookings FK_EventSessionBookings_EventRegistrations_TenantId_Registrati~; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."EventSessionBookings"
    ADD CONSTRAINT "FK_EventSessionBookings_EventRegistrations_TenantId_Registrati~" FOREIGN KEY ("TenantId", "RegistrationId") REFERENCES public."EventRegistrations"("TenantId", "Id") ON DELETE RESTRICT;


--
-- Name: EventSessionBookings FK_EventSessionBookings_EventSessions_TenantId_SessionId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."EventSessionBookings"
    ADD CONSTRAINT "FK_EventSessionBookings_EventSessions_TenantId_SessionId" FOREIGN KEY ("TenantId", "SessionId") REFERENCES public."EventSessions"("TenantId", "Id") ON DELETE RESTRICT;


--
-- Name: EventSessionBookings FK_EventSessionBookings_Events_TenantId_EventId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."EventSessionBookings"
    ADD CONSTRAINT "FK_EventSessionBookings_Events_TenantId_EventId" FOREIGN KEY ("TenantId", "EventId") REFERENCES public."Events"("TenantId", "Id") ON DELETE RESTRICT;


--
-- Name: EventSessions FK_EventSessions_Associations_TenantId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."EventSessions"
    ADD CONSTRAINT "FK_EventSessions_Associations_TenantId" FOREIGN KEY ("TenantId") REFERENCES public."Associations"("Id") ON DELETE RESTRICT;


--
-- Name: EventSessions FK_EventSessions_Events_TenantId_EventId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."EventSessions"
    ADD CONSTRAINT "FK_EventSessions_Events_TenantId_EventId" FOREIGN KEY ("TenantId", "EventId") REFERENCES public."Events"("TenantId", "Id") ON DELETE RESTRICT;


--
-- Name: EventStaff FK_EventStaff_Associations_TenantId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."EventStaff"
    ADD CONSTRAINT "FK_EventStaff_Associations_TenantId" FOREIGN KEY ("TenantId") REFERENCES public."Associations"("Id") ON DELETE RESTRICT;


--
-- Name: EventStaff FK_EventStaff_Events_TenantId_EventId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."EventStaff"
    ADD CONSTRAINT "FK_EventStaff_Events_TenantId_EventId" FOREIGN KEY ("TenantId", "EventId") REFERENCES public."Events"("TenantId", "Id") ON DELETE RESTRICT;


--
-- Name: EventStaff FK_EventStaff_Persons_PersonId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."EventStaff"
    ADD CONSTRAINT "FK_EventStaff_Persons_PersonId" FOREIGN KEY ("PersonId") REFERENCES public."Persons"("Id") ON DELETE RESTRICT;


--
-- Name: EventTickets FK_EventTickets_Associations_TenantId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."EventTickets"
    ADD CONSTRAINT "FK_EventTickets_Associations_TenantId" FOREIGN KEY ("TenantId") REFERENCES public."Associations"("Id") ON DELETE RESTRICT;


--
-- Name: EventTickets FK_EventTickets_EventRegistrations_TenantId_RegistrationId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."EventTickets"
    ADD CONSTRAINT "FK_EventTickets_EventRegistrations_TenantId_RegistrationId" FOREIGN KEY ("TenantId", "RegistrationId") REFERENCES public."EventRegistrations"("TenantId", "Id") ON DELETE RESTRICT;


--
-- Name: EventTickets FK_EventTickets_Events_TenantId_EventId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."EventTickets"
    ADD CONSTRAINT "FK_EventTickets_Events_TenantId_EventId" FOREIGN KEY ("TenantId", "EventId") REFERENCES public."Events"("TenantId", "Id") ON DELETE RESTRICT;


--
-- Name: EventTypes FK_EventTypes_Associations_TenantId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."EventTypes"
    ADD CONSTRAINT "FK_EventTypes_Associations_TenantId" FOREIGN KEY ("TenantId") REFERENCES public."Associations"("Id") ON DELETE RESTRICT;


--
-- Name: Events FK_Events_Associations_TenantId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."Events"
    ADD CONSTRAINT "FK_Events_Associations_TenantId" FOREIGN KEY ("TenantId") REFERENCES public."Associations"("Id") ON DELETE RESTRICT;


--
-- Name: Events FK_Events_Committees_TenantId_CommitteeId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."Events"
    ADD CONSTRAINT "FK_Events_Committees_TenantId_CommitteeId" FOREIGN KEY ("TenantId", "CommitteeId") REFERENCES public."Committees"("TenantId", "Id") ON DELETE RESTRICT;


--
-- Name: Events FK_Events_EventTypes_TenantId_TypeId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."Events"
    ADD CONSTRAINT "FK_Events_EventTypes_TenantId_TypeId" FOREIGN KEY ("TenantId", "TypeId") REFERENCES public."EventTypes"("TenantId", "Id") ON DELETE RESTRICT;


--
-- Name: Events FK_Events_GovernanceRequests_TenantId_ApprovalRequestId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."Events"
    ADD CONSTRAINT "FK_Events_GovernanceRequests_TenantId_ApprovalRequestId" FOREIGN KEY ("TenantId", "ApprovalRequestId") REFERENCES public."GovernanceRequests"("TenantId", "Id") ON DELETE RESTRICT;


--
-- Name: Events FK_Events_Orgs_TenantId_OrgId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."Events"
    ADD CONSTRAINT "FK_Events_Orgs_TenantId_OrgId" FOREIGN KEY ("TenantId", "OrgId") REFERENCES public."Orgs"("TenantId", "Id") ON DELETE RESTRICT;


--
-- Name: Events FK_Events_UserProfiles_CreatorId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."Events"
    ADD CONSTRAINT "FK_Events_UserProfiles_CreatorId" FOREIGN KEY ("CreatorId") REFERENCES public."UserProfiles"("Id") ON DELETE RESTRICT;


--
-- Name: GovernanceDelegations FK_GovernanceDelegations_Associations_TenantId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."GovernanceDelegations"
    ADD CONSTRAINT "FK_GovernanceDelegations_Associations_TenantId" FOREIGN KEY ("TenantId") REFERENCES public."Associations"("Id") ON DELETE RESTRICT;


--
-- Name: GovernanceDelegations FK_GovernanceDelegations_Orgs_TenantId_OrgId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."GovernanceDelegations"
    ADD CONSTRAINT "FK_GovernanceDelegations_Orgs_TenantId_OrgId" FOREIGN KEY ("TenantId", "OrgId") REFERENCES public."Orgs"("TenantId", "Id") ON DELETE RESTRICT;


--
-- Name: GovernanceDelegations FK_GovernanceDelegations_Roles_TenantId_RoleId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."GovernanceDelegations"
    ADD CONSTRAINT "FK_GovernanceDelegations_Roles_TenantId_RoleId" FOREIGN KEY ("TenantId", "RoleId") REFERENCES public."Roles"("TenantId", "Id") ON DELETE RESTRICT;


--
-- Name: GovernanceDocuments FK_GovernanceDocuments_Associations_TenantId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."GovernanceDocuments"
    ADD CONSTRAINT "FK_GovernanceDocuments_Associations_TenantId" FOREIGN KEY ("TenantId") REFERENCES public."Associations"("Id") ON DELETE RESTRICT;


--
-- Name: GovernanceDocuments FK_GovernanceDocuments_Committees_TenantId_CommitteeId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."GovernanceDocuments"
    ADD CONSTRAINT "FK_GovernanceDocuments_Committees_TenantId_CommitteeId" FOREIGN KEY ("TenantId", "CommitteeId") REFERENCES public."Committees"("TenantId", "Id") ON DELETE RESTRICT;


--
-- Name: GovernanceDocuments FK_GovernanceDocuments_Events_TenantId_EventId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."GovernanceDocuments"
    ADD CONSTRAINT "FK_GovernanceDocuments_Events_TenantId_EventId" FOREIGN KEY ("TenantId", "EventId") REFERENCES public."Events"("TenantId", "Id") ON DELETE RESTRICT;


--
-- Name: GovernanceDocuments FK_GovernanceDocuments_Orgs_TenantId_OrgId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."GovernanceDocuments"
    ADD CONSTRAINT "FK_GovernanceDocuments_Orgs_TenantId_OrgId" FOREIGN KEY ("TenantId", "OrgId") REFERENCES public."Orgs"("TenantId", "Id") ON DELETE RESTRICT;


--
-- Name: GovernanceRequests FK_GovernanceRequests_Associations_TenantId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."GovernanceRequests"
    ADD CONSTRAINT "FK_GovernanceRequests_Associations_TenantId" FOREIGN KEY ("TenantId") REFERENCES public."Associations"("Id") ON DELETE RESTRICT;


--
-- Name: GovernanceRequests FK_GovernanceRequests_Committees_TenantId_CommitteeId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."GovernanceRequests"
    ADD CONSTRAINT "FK_GovernanceRequests_Committees_TenantId_CommitteeId" FOREIGN KEY ("TenantId", "CommitteeId") REFERENCES public."Committees"("TenantId", "Id") ON DELETE RESTRICT;


--
-- Name: GovernanceRequests FK_GovernanceRequests_GovernanceDocuments_TenantId_DocumentId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."GovernanceRequests"
    ADD CONSTRAINT "FK_GovernanceRequests_GovernanceDocuments_TenantId_DocumentId" FOREIGN KEY ("TenantId", "DocumentId") REFERENCES public."GovernanceDocuments"("TenantId", "Id") ON DELETE RESTRICT;


--
-- Name: GovernanceRequests FK_GovernanceRequests_GovernanceResolutions_TenantId_Resolutio~; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."GovernanceRequests"
    ADD CONSTRAINT "FK_GovernanceRequests_GovernanceResolutions_TenantId_Resolutio~" FOREIGN KEY ("TenantId", "ResolutionId") REFERENCES public."GovernanceResolutions"("TenantId", "Id") ON DELETE RESTRICT;


--
-- Name: GovernanceRequests FK_GovernanceRequests_LeadershipHandovers_TenantId_HandoverId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."GovernanceRequests"
    ADD CONSTRAINT "FK_GovernanceRequests_LeadershipHandovers_TenantId_HandoverId" FOREIGN KEY ("TenantId", "HandoverId") REFERENCES public."LeadershipHandovers"("TenantId", "Id") ON DELETE RESTRICT;


--
-- Name: GovernanceRequests FK_GovernanceRequests_MembershipWorkflows_TenantId_WorkflowId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."GovernanceRequests"
    ADD CONSTRAINT "FK_GovernanceRequests_MembershipWorkflows_TenantId_WorkflowId" FOREIGN KEY ("TenantId", "WorkflowId") REFERENCES public."MembershipWorkflows"("TenantId", "Id") ON DELETE RESTRICT;


--
-- Name: GovernanceRequests FK_GovernanceRequests_OfficerAppointments_TenantId_Appointment~; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."GovernanceRequests"
    ADD CONSTRAINT "FK_GovernanceRequests_OfficerAppointments_TenantId_Appointment~" FOREIGN KEY ("TenantId", "AppointmentId") REFERENCES public."OfficerAppointments"("TenantId", "Id") ON DELETE RESTRICT;


--
-- Name: GovernanceRequests FK_GovernanceRequests_Orgs_TenantId_OrgId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."GovernanceRequests"
    ADD CONSTRAINT "FK_GovernanceRequests_Orgs_TenantId_OrgId" FOREIGN KEY ("TenantId", "OrgId") REFERENCES public."Orgs"("TenantId", "Id") ON DELETE RESTRICT;


--
-- Name: GovernanceResolutions FK_GovernanceResolutions_Associations_TenantId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."GovernanceResolutions"
    ADD CONSTRAINT "FK_GovernanceResolutions_Associations_TenantId" FOREIGN KEY ("TenantId") REFERENCES public."Associations"("Id") ON DELETE RESTRICT;


--
-- Name: GovernanceResolutions FK_GovernanceResolutions_Orgs_TenantId_OrgId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."GovernanceResolutions"
    ADD CONSTRAINT "FK_GovernanceResolutions_Orgs_TenantId_OrgId" FOREIGN KEY ("TenantId", "OrgId") REFERENCES public."Orgs"("TenantId", "Id") ON DELETE RESTRICT;


--
-- Name: HandoverChecklistItems FK_HandoverChecklistItems_Associations_TenantId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."HandoverChecklistItems"
    ADD CONSTRAINT "FK_HandoverChecklistItems_Associations_TenantId" FOREIGN KEY ("TenantId") REFERENCES public."Associations"("Id") ON DELETE RESTRICT;


--
-- Name: HandoverChecklistItems FK_HandoverChecklistItems_GovernanceDocuments_TenantId_Documen~; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."HandoverChecklistItems"
    ADD CONSTRAINT "FK_HandoverChecklistItems_GovernanceDocuments_TenantId_Documen~" FOREIGN KEY ("TenantId", "DocumentId") REFERENCES public."GovernanceDocuments"("TenantId", "Id") ON DELETE RESTRICT;


--
-- Name: HandoverChecklistItems FK_HandoverChecklistItems_LeadershipHandovers_TenantId_Handove~; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."HandoverChecklistItems"
    ADD CONSTRAINT "FK_HandoverChecklistItems_LeadershipHandovers_TenantId_Handove~" FOREIGN KEY ("TenantId", "HandoverId") REFERENCES public."LeadershipHandovers"("TenantId", "Id") ON DELETE RESTRICT;


--
-- Name: ImpactMetrics FK_ImpactMetrics_Associations_TenantId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."ImpactMetrics"
    ADD CONSTRAINT "FK_ImpactMetrics_Associations_TenantId" FOREIGN KEY ("TenantId") REFERENCES public."Associations"("Id") ON DELETE RESTRICT;


--
-- Name: ImpactMetrics FK_ImpactMetrics_Orgs_TenantId_OrgId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."ImpactMetrics"
    ADD CONSTRAINT "FK_ImpactMetrics_Orgs_TenantId_OrgId" FOREIGN KEY ("TenantId", "OrgId") REFERENCES public."Orgs"("TenantId", "Id") ON DELETE RESTRICT;


--
-- Name: IssuedCertificates FK_IssuedCertificates_Associations_TenantId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."IssuedCertificates"
    ADD CONSTRAINT "FK_IssuedCertificates_Associations_TenantId" FOREIGN KEY ("TenantId") REFERENCES public."Associations"("Id") ON DELETE RESTRICT;


--
-- Name: IssuedCertificates FK_IssuedCertificates_CertificateTemplates_TenantId_TemplateId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."IssuedCertificates"
    ADD CONSTRAINT "FK_IssuedCertificates_CertificateTemplates_TenantId_TemplateId" FOREIGN KEY ("TenantId", "TemplateId") REFERENCES public."CertificateTemplates"("TenantId", "Id") ON DELETE RESTRICT;


--
-- Name: IssuedCertificates FK_IssuedCertificates_Orgs_TenantId_OrgId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."IssuedCertificates"
    ADD CONSTRAINT "FK_IssuedCertificates_Orgs_TenantId_OrgId" FOREIGN KEY ("TenantId", "OrgId") REFERENCES public."Orgs"("TenantId", "Id") ON DELETE RESTRICT;


--
-- Name: IssuedCertificates FK_IssuedCertificates_Persons_PersonId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."IssuedCertificates"
    ADD CONSTRAINT "FK_IssuedCertificates_Persons_PersonId" FOREIGN KEY ("PersonId") REFERENCES public."Persons"("Id") ON DELETE RESTRICT;


--
-- Name: LeadershipHandovers FK_LeadershipHandovers_Associations_TenantId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."LeadershipHandovers"
    ADD CONSTRAINT "FK_LeadershipHandovers_Associations_TenantId" FOREIGN KEY ("TenantId") REFERENCES public."Associations"("Id") ON DELETE RESTRICT;


--
-- Name: LeadershipHandovers FK_LeadershipHandovers_OfficerAppointments_TenantId_IncomingAp~; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."LeadershipHandovers"
    ADD CONSTRAINT "FK_LeadershipHandovers_OfficerAppointments_TenantId_IncomingAp~" FOREIGN KEY ("TenantId", "IncomingAppointmentId") REFERENCES public."OfficerAppointments"("TenantId", "Id") ON DELETE RESTRICT;


--
-- Name: LeadershipHandovers FK_LeadershipHandovers_OfficerAppointments_TenantId_OutgoingAp~; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."LeadershipHandovers"
    ADD CONSTRAINT "FK_LeadershipHandovers_OfficerAppointments_TenantId_OutgoingAp~" FOREIGN KEY ("TenantId", "OutgoingAppointmentId") REFERENCES public."OfficerAppointments"("TenantId", "Id") ON DELETE RESTRICT;


--
-- Name: LeadershipHandovers FK_LeadershipHandovers_Orgs_TenantId_OrgId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."LeadershipHandovers"
    ADD CONSTRAINT "FK_LeadershipHandovers_Orgs_TenantId_OrgId" FOREIGN KEY ("TenantId", "OrgId") REFERENCES public."Orgs"("TenantId", "Id") ON DELETE RESTRICT;


--
-- Name: LeadershipTerms FK_LeadershipTerms_Associations_TenantId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."LeadershipTerms"
    ADD CONSTRAINT "FK_LeadershipTerms_Associations_TenantId" FOREIGN KEY ("TenantId") REFERENCES public."Associations"("Id") ON DELETE RESTRICT;


--
-- Name: LeadershipTerms FK_LeadershipTerms_Orgs_TenantId_OrgId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."LeadershipTerms"
    ADD CONSTRAINT "FK_LeadershipTerms_Orgs_TenantId_OrgId" FOREIGN KEY ("TenantId", "OrgId") REFERENCES public."Orgs"("TenantId", "Id") ON DELETE RESTRICT;


--
-- Name: Levels FK_Levels_Associations_TenantId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."Levels"
    ADD CONSTRAINT "FK_Levels_Associations_TenantId" FOREIGN KEY ("TenantId") REFERENCES public."Associations"("Id") ON DELETE RESTRICT;


--
-- Name: Levels FK_Levels_Levels_TenantId_ParentLevelId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."Levels"
    ADD CONSTRAINT "FK_Levels_Levels_TenantId_ParentLevelId" FOREIGN KEY ("TenantId", "ParentLevelId") REFERENCES public."Levels"("TenantId", "Id") ON DELETE RESTRICT;


--
-- Name: MailDeliveryAttempts FK_MailDeliveryAttempts_Mails_MailId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."MailDeliveryAttempts"
    ADD CONSTRAINT "FK_MailDeliveryAttempts_Mails_MailId" FOREIGN KEY ("MailId") REFERENCES public."Mails"("Id") ON DELETE CASCADE;


--
-- Name: Mails FK_Mails_CommunicationAccounts_TenantId_AccountId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."Mails"
    ADD CONSTRAINT "FK_Mails_CommunicationAccounts_TenantId_AccountId" FOREIGN KEY ("TenantId", "AccountId") REFERENCES public."CommunicationAccounts"("TenantId", "Id") ON DELETE RESTRICT;


--
-- Name: Mails FK_Mails_CommunicationMessages_TenantId_CommunicationId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."Mails"
    ADD CONSTRAINT "FK_Mails_CommunicationMessages_TenantId_CommunicationId" FOREIGN KEY ("TenantId", "CommunicationId") REFERENCES public."CommunicationMessages"("TenantId", "Id") ON DELETE RESTRICT;


--
-- Name: Mails FK_Mails_Events_TenantId_EventId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."Mails"
    ADD CONSTRAINT "FK_Mails_Events_TenantId_EventId" FOREIGN KEY ("TenantId", "EventId") REFERENCES public."Events"("TenantId", "Id") ON DELETE RESTRICT;


--
-- Name: Mails FK_Mails_UserProfiles_UserId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."Mails"
    ADD CONSTRAINT "FK_Mails_UserProfiles_UserId" FOREIGN KEY ("UserId") REFERENCES public."UserProfiles"("Id") ON DELETE CASCADE;


--
-- Name: MeetingAttendances FK_MeetingAttendances_Associations_TenantId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."MeetingAttendances"
    ADD CONSTRAINT "FK_MeetingAttendances_Associations_TenantId" FOREIGN KEY ("TenantId") REFERENCES public."Associations"("Id") ON DELETE RESTRICT;


--
-- Name: MeetingAttendances FK_MeetingAttendances_ClubMeetings_TenantId_MeetingId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."MeetingAttendances"
    ADD CONSTRAINT "FK_MeetingAttendances_ClubMeetings_TenantId_MeetingId" FOREIGN KEY ("TenantId", "MeetingId") REFERENCES public."ClubMeetings"("TenantId", "Id") ON DELETE RESTRICT;


--
-- Name: MeetingAttendances FK_MeetingAttendances_Orgs_TenantId_OrgId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."MeetingAttendances"
    ADD CONSTRAINT "FK_MeetingAttendances_Orgs_TenantId_OrgId" FOREIGN KEY ("TenantId", "OrgId") REFERENCES public."Orgs"("TenantId", "Id") ON DELETE RESTRICT;


--
-- Name: MeetingAttendances FK_MeetingAttendances_Persons_PersonId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."MeetingAttendances"
    ADD CONSTRAINT "FK_MeetingAttendances_Persons_PersonId" FOREIGN KEY ("PersonId") REFERENCES public."Persons"("Id") ON DELETE RESTRICT;


--
-- Name: MeetingMinutes FK_MeetingMinutes_Associations_TenantId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."MeetingMinutes"
    ADD CONSTRAINT "FK_MeetingMinutes_Associations_TenantId" FOREIGN KEY ("TenantId") REFERENCES public."Associations"("Id") ON DELETE RESTRICT;


--
-- Name: MeetingMinutes FK_MeetingMinutes_ClubMeetings_TenantId_MeetingId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."MeetingMinutes"
    ADD CONSTRAINT "FK_MeetingMinutes_ClubMeetings_TenantId_MeetingId" FOREIGN KEY ("TenantId", "MeetingId") REFERENCES public."ClubMeetings"("TenantId", "Id") ON DELETE RESTRICT;


--
-- Name: MeetingMinutes FK_MeetingMinutes_Orgs_TenantId_OrgId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."MeetingMinutes"
    ADD CONSTRAINT "FK_MeetingMinutes_Orgs_TenantId_OrgId" FOREIGN KEY ("TenantId", "OrgId") REFERENCES public."Orgs"("TenantId", "Id") ON DELETE RESTRICT;


--
-- Name: MemberActivities FK_MemberActivities_Associations_TenantId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."MemberActivities"
    ADD CONSTRAINT "FK_MemberActivities_Associations_TenantId" FOREIGN KEY ("TenantId") REFERENCES public."Associations"("Id") ON DELETE RESTRICT;


--
-- Name: MemberActivities FK_MemberActivities_Orgs_TenantId_ClubId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."MemberActivities"
    ADD CONSTRAINT "FK_MemberActivities_Orgs_TenantId_ClubId" FOREIGN KEY ("TenantId", "ClubId") REFERENCES public."Orgs"("TenantId", "Id") ON DELETE RESTRICT;


--
-- Name: MemberActivities FK_MemberActivities_Persons_PersonId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."MemberActivities"
    ADD CONSTRAINT "FK_MemberActivities_Persons_PersonId" FOREIGN KEY ("PersonId") REFERENCES public."Persons"("Id") ON DELETE RESTRICT;


--
-- Name: MemberContactRequests FK_MemberContactRequests_Associations_TenantId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."MemberContactRequests"
    ADD CONSTRAINT "FK_MemberContactRequests_Associations_TenantId" FOREIGN KEY ("TenantId") REFERENCES public."Associations"("Id") ON DELETE RESTRICT;


--
-- Name: MemberContactRequests FK_MemberContactRequests_Memberships_TenantId_MembershipId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."MemberContactRequests"
    ADD CONSTRAINT "FK_MemberContactRequests_Memberships_TenantId_MembershipId" FOREIGN KEY ("TenantId", "MembershipId") REFERENCES public."Memberships"("TenantId", "Id") ON DELETE RESTRICT;


--
-- Name: MemberImportJobs FK_MemberImportJobs_Associations_TenantId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."MemberImportJobs"
    ADD CONSTRAINT "FK_MemberImportJobs_Associations_TenantId" FOREIGN KEY ("TenantId") REFERENCES public."Associations"("Id") ON DELETE RESTRICT;


--
-- Name: MemberImportJobs FK_MemberImportJobs_MembershipCategories_TenantId_CategoryId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."MemberImportJobs"
    ADD CONSTRAINT "FK_MemberImportJobs_MembershipCategories_TenantId_CategoryId" FOREIGN KEY ("TenantId", "CategoryId") REFERENCES public."MembershipCategories"("TenantId", "Id") ON DELETE RESTRICT;


--
-- Name: MemberImportJobs FK_MemberImportJobs_Orgs_TenantId_ClubId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."MemberImportJobs"
    ADD CONSTRAINT "FK_MemberImportJobs_Orgs_TenantId_ClubId" FOREIGN KEY ("TenantId", "ClubId") REFERENCES public."Orgs"("TenantId", "Id") ON DELETE RESTRICT;


--
-- Name: MemberImportRows FK_MemberImportRows_Associations_TenantId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."MemberImportRows"
    ADD CONSTRAINT "FK_MemberImportRows_Associations_TenantId" FOREIGN KEY ("TenantId") REFERENCES public."Associations"("Id") ON DELETE RESTRICT;


--
-- Name: MemberImportRows FK_MemberImportRows_MemberImportJobs_TenantId_JobId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."MemberImportRows"
    ADD CONSTRAINT "FK_MemberImportRows_MemberImportJobs_TenantId_JobId" FOREIGN KEY ("TenantId", "JobId") REFERENCES public."MemberImportJobs"("TenantId", "Id") ON DELETE CASCADE;


--
-- Name: MemberRelationships FK_MemberRelationships_Associations_TenantId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."MemberRelationships"
    ADD CONSTRAINT "FK_MemberRelationships_Associations_TenantId" FOREIGN KEY ("TenantId") REFERENCES public."Associations"("Id") ON DELETE RESTRICT;


--
-- Name: MemberRelationships FK_MemberRelationships_Persons_PersonId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."MemberRelationships"
    ADD CONSTRAINT "FK_MemberRelationships_Persons_PersonId" FOREIGN KEY ("PersonId") REFERENCES public."Persons"("Id") ON DELETE RESTRICT;


--
-- Name: MemberRelationships FK_MemberRelationships_Persons_RelatedPersonId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."MemberRelationships"
    ADD CONSTRAINT "FK_MemberRelationships_Persons_RelatedPersonId" FOREIGN KEY ("RelatedPersonId") REFERENCES public."Persons"("Id") ON DELETE RESTRICT;


--
-- Name: MemberSavedViews FK_MemberSavedViews_UserProfiles_UserId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."MemberSavedViews"
    ADD CONSTRAINT "FK_MemberSavedViews_UserProfiles_UserId" FOREIGN KEY ("UserId") REFERENCES public."UserProfiles"("Id") ON DELETE CASCADE;


--
-- Name: MembershipApplications FK_MembershipApplications_Associations_TenantId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."MembershipApplications"
    ADD CONSTRAINT "FK_MembershipApplications_Associations_TenantId" FOREIGN KEY ("TenantId") REFERENCES public."Associations"("Id") ON DELETE RESTRICT;


--
-- Name: MembershipApplications FK_MembershipApplications_MembershipCategories_TenantId_Catego~; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."MembershipApplications"
    ADD CONSTRAINT "FK_MembershipApplications_MembershipCategories_TenantId_Catego~" FOREIGN KEY ("TenantId", "CategoryId") REFERENCES public."MembershipCategories"("TenantId", "Id") ON DELETE RESTRICT;


--
-- Name: MembershipApplications FK_MembershipApplications_MembershipInvitations_TenantId_Invit~; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."MembershipApplications"
    ADD CONSTRAINT "FK_MembershipApplications_MembershipInvitations_TenantId_Invit~" FOREIGN KEY ("TenantId", "InvitationId") REFERENCES public."MembershipInvitations"("TenantId", "Id") ON DELETE RESTRICT;


--
-- Name: MembershipApplications FK_MembershipApplications_MembershipWorkflows_TenantId_Workflo~; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."MembershipApplications"
    ADD CONSTRAINT "FK_MembershipApplications_MembershipWorkflows_TenantId_Workflo~" FOREIGN KEY ("TenantId", "WorkflowId") REFERENCES public."MembershipWorkflows"("TenantId", "Id") ON DELETE RESTRICT;


--
-- Name: MembershipApplications FK_MembershipApplications_Orgs_TenantId_ClubId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."MembershipApplications"
    ADD CONSTRAINT "FK_MembershipApplications_Orgs_TenantId_ClubId" FOREIGN KEY ("TenantId", "ClubId") REFERENCES public."Orgs"("TenantId", "Id") ON DELETE RESTRICT;


--
-- Name: MembershipApplications FK_MembershipApplications_Persons_PersonId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."MembershipApplications"
    ADD CONSTRAINT "FK_MembershipApplications_Persons_PersonId" FOREIGN KEY ("PersonId") REFERENCES public."Persons"("Id") ON DELETE RESTRICT;


--
-- Name: MembershipApplications FK_MembershipApplications_UserProfiles_ApplicantUserId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."MembershipApplications"
    ADD CONSTRAINT "FK_MembershipApplications_UserProfiles_ApplicantUserId" FOREIGN KEY ("ApplicantUserId") REFERENCES public."UserProfiles"("Id") ON DELETE RESTRICT;


--
-- Name: MembershipApprovalStages FK_MembershipApprovalStages_Associations_TenantId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."MembershipApprovalStages"
    ADD CONSTRAINT "FK_MembershipApprovalStages_Associations_TenantId" FOREIGN KEY ("TenantId") REFERENCES public."Associations"("Id") ON DELETE RESTRICT;


--
-- Name: MembershipApprovalStages FK_MembershipApprovalStages_MembershipWorkflows_TenantId_Workf~; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."MembershipApprovalStages"
    ADD CONSTRAINT "FK_MembershipApprovalStages_MembershipWorkflows_TenantId_Workf~" FOREIGN KEY ("TenantId", "WorkflowId") REFERENCES public."MembershipWorkflows"("TenantId", "Id") ON DELETE RESTRICT;


--
-- Name: MembershipApprovals FK_MembershipApprovals_Associations_TenantId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."MembershipApprovals"
    ADD CONSTRAINT "FK_MembershipApprovals_Associations_TenantId" FOREIGN KEY ("TenantId") REFERENCES public."Associations"("Id") ON DELETE RESTRICT;


--
-- Name: MembershipApprovals FK_MembershipApprovals_GovernanceRequests_TenantId_GovernanceR~; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."MembershipApprovals"
    ADD CONSTRAINT "FK_MembershipApprovals_GovernanceRequests_TenantId_GovernanceR~" FOREIGN KEY ("TenantId", "GovernanceRequestId") REFERENCES public."GovernanceRequests"("TenantId", "Id") ON DELETE RESTRICT;


--
-- Name: MembershipApprovals FK_MembershipApprovals_MembershipApplications_TenantId_Applica~; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."MembershipApprovals"
    ADD CONSTRAINT "FK_MembershipApprovals_MembershipApplications_TenantId_Applica~" FOREIGN KEY ("TenantId", "ApplicationId") REFERENCES public."MembershipApplications"("TenantId", "Id") ON DELETE RESTRICT;


--
-- Name: MembershipApprovals FK_MembershipApprovals_MembershipApprovalStages_TenantId_Stage~; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."MembershipApprovals"
    ADD CONSTRAINT "FK_MembershipApprovals_MembershipApprovalStages_TenantId_Stage~" FOREIGN KEY ("TenantId", "StageId") REFERENCES public."MembershipApprovalStages"("TenantId", "Id") ON DELETE RESTRICT;


--
-- Name: MembershipCategories FK_MembershipCategories_Associations_TenantId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."MembershipCategories"
    ADD CONSTRAINT "FK_MembershipCategories_Associations_TenantId" FOREIGN KEY ("TenantId") REFERENCES public."Associations"("Id") ON DELETE RESTRICT;


--
-- Name: MembershipDocuments FK_MembershipDocuments_Associations_TenantId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."MembershipDocuments"
    ADD CONSTRAINT "FK_MembershipDocuments_Associations_TenantId" FOREIGN KEY ("TenantId") REFERENCES public."Associations"("Id") ON DELETE RESTRICT;


--
-- Name: MembershipDocuments FK_MembershipDocuments_Memberships_TenantId_MembershipId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."MembershipDocuments"
    ADD CONSTRAINT "FK_MembershipDocuments_Memberships_TenantId_MembershipId" FOREIGN KEY ("TenantId", "MembershipId") REFERENCES public."Memberships"("TenantId", "Id") ON DELETE RESTRICT;


--
-- Name: MembershipHistory FK_MembershipHistory_Associations_TenantId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."MembershipHistory"
    ADD CONSTRAINT "FK_MembershipHistory_Associations_TenantId" FOREIGN KEY ("TenantId") REFERENCES public."Associations"("Id") ON DELETE RESTRICT;


--
-- Name: MembershipHistory FK_MembershipHistory_Memberships_TenantId_MembershipId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."MembershipHistory"
    ADD CONSTRAINT "FK_MembershipHistory_Memberships_TenantId_MembershipId" FOREIGN KEY ("TenantId", "MembershipId") REFERENCES public."Memberships"("TenantId", "Id") ON DELETE RESTRICT;


--
-- Name: MembershipInvitations FK_MembershipInvitations_Associations_TenantId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."MembershipInvitations"
    ADD CONSTRAINT "FK_MembershipInvitations_Associations_TenantId" FOREIGN KEY ("TenantId") REFERENCES public."Associations"("Id") ON DELETE RESTRICT;


--
-- Name: MembershipInvitations FK_MembershipInvitations_MembershipCategories_TenantId_Categor~; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."MembershipInvitations"
    ADD CONSTRAINT "FK_MembershipInvitations_MembershipCategories_TenantId_Categor~" FOREIGN KEY ("TenantId", "CategoryId") REFERENCES public."MembershipCategories"("TenantId", "Id") ON DELETE RESTRICT;


--
-- Name: MembershipInvitations FK_MembershipInvitations_Orgs_TenantId_ClubId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."MembershipInvitations"
    ADD CONSTRAINT "FK_MembershipInvitations_Orgs_TenantId_ClubId" FOREIGN KEY ("TenantId", "ClubId") REFERENCES public."Orgs"("TenantId", "Id") ON DELETE RESTRICT;


--
-- Name: MembershipPolicies FK_MembershipPolicies_Associations_TenantId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."MembershipPolicies"
    ADD CONSTRAINT "FK_MembershipPolicies_Associations_TenantId" FOREIGN KEY ("TenantId") REFERENCES public."Associations"("Id") ON DELETE RESTRICT;


--
-- Name: MembershipRenewals FK_MembershipRenewals_Associations_TenantId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."MembershipRenewals"
    ADD CONSTRAINT "FK_MembershipRenewals_Associations_TenantId" FOREIGN KEY ("TenantId") REFERENCES public."Associations"("Id") ON DELETE RESTRICT;


--
-- Name: MembershipRenewals FK_MembershipRenewals_Memberships_TenantId_MembershipId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."MembershipRenewals"
    ADD CONSTRAINT "FK_MembershipRenewals_Memberships_TenantId_MembershipId" FOREIGN KEY ("TenantId", "MembershipId") REFERENCES public."Memberships"("TenantId", "Id") ON DELETE RESTRICT;


--
-- Name: MembershipStageRoles FK_MembershipStageRoles_MembershipApprovalStages_TenantId_Stag~; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."MembershipStageRoles"
    ADD CONSTRAINT "FK_MembershipStageRoles_MembershipApprovalStages_TenantId_Stag~" FOREIGN KEY ("TenantId", "StageId") REFERENCES public."MembershipApprovalStages"("TenantId", "Id") ON DELETE RESTRICT;


--
-- Name: MembershipStageRoles FK_MembershipStageRoles_Roles_TenantId_RoleId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."MembershipStageRoles"
    ADD CONSTRAINT "FK_MembershipStageRoles_Roles_TenantId_RoleId" FOREIGN KEY ("TenantId", "RoleId") REFERENCES public."Roles"("TenantId", "Id") ON DELETE RESTRICT;


--
-- Name: MembershipTransfers FK_MembershipTransfers_Associations_TenantId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."MembershipTransfers"
    ADD CONSTRAINT "FK_MembershipTransfers_Associations_TenantId" FOREIGN KEY ("TenantId") REFERENCES public."Associations"("Id") ON DELETE RESTRICT;


--
-- Name: MembershipTransfers FK_MembershipTransfers_MembershipCategories_TenantId_CategoryId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."MembershipTransfers"
    ADD CONSTRAINT "FK_MembershipTransfers_MembershipCategories_TenantId_CategoryId" FOREIGN KEY ("TenantId", "CategoryId") REFERENCES public."MembershipCategories"("TenantId", "Id") ON DELETE RESTRICT;


--
-- Name: MembershipTransfers FK_MembershipTransfers_Memberships_TenantId_DestinationMembers~; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."MembershipTransfers"
    ADD CONSTRAINT "FK_MembershipTransfers_Memberships_TenantId_DestinationMembers~" FOREIGN KEY ("TenantId", "DestinationMembershipId") REFERENCES public."Memberships"("TenantId", "Id") ON DELETE RESTRICT;


--
-- Name: MembershipTransfers FK_MembershipTransfers_Memberships_TenantId_MembershipId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."MembershipTransfers"
    ADD CONSTRAINT "FK_MembershipTransfers_Memberships_TenantId_MembershipId" FOREIGN KEY ("TenantId", "MembershipId") REFERENCES public."Memberships"("TenantId", "Id") ON DELETE RESTRICT;


--
-- Name: MembershipTransfers FK_MembershipTransfers_Orgs_TenantId_DestinationClubId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."MembershipTransfers"
    ADD CONSTRAINT "FK_MembershipTransfers_Orgs_TenantId_DestinationClubId" FOREIGN KEY ("TenantId", "DestinationClubId") REFERENCES public."Orgs"("TenantId", "Id") ON DELETE RESTRICT;


--
-- Name: MembershipWorkflows FK_MembershipWorkflows_Associations_TenantId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."MembershipWorkflows"
    ADD CONSTRAINT "FK_MembershipWorkflows_Associations_TenantId" FOREIGN KEY ("TenantId") REFERENCES public."Associations"("Id") ON DELETE RESTRICT;


--
-- Name: Memberships FK_Memberships_Associations_TenantId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."Memberships"
    ADD CONSTRAINT "FK_Memberships_Associations_TenantId" FOREIGN KEY ("TenantId") REFERENCES public."Associations"("Id") ON DELETE RESTRICT;


--
-- Name: Memberships FK_Memberships_MemberImportRows_TenantId_ImportRowId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."Memberships"
    ADD CONSTRAINT "FK_Memberships_MemberImportRows_TenantId_ImportRowId" FOREIGN KEY ("TenantId", "ImportRowId") REFERENCES public."MemberImportRows"("TenantId", "Id") ON DELETE RESTRICT;


--
-- Name: Memberships FK_Memberships_MembershipApplications_TenantId_SourceApplicati~; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."Memberships"
    ADD CONSTRAINT "FK_Memberships_MembershipApplications_TenantId_SourceApplicati~" FOREIGN KEY ("TenantId", "SourceApplicationId") REFERENCES public."MembershipApplications"("TenantId", "Id") ON DELETE RESTRICT;


--
-- Name: Memberships FK_Memberships_MembershipCategories_TenantId_CategoryId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."Memberships"
    ADD CONSTRAINT "FK_Memberships_MembershipCategories_TenantId_CategoryId" FOREIGN KEY ("TenantId", "CategoryId") REFERENCES public."MembershipCategories"("TenantId", "Id") ON DELETE RESTRICT;


--
-- Name: Memberships FK_Memberships_Orgs_TenantId_ClubId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."Memberships"
    ADD CONSTRAINT "FK_Memberships_Orgs_TenantId_ClubId" FOREIGN KEY ("TenantId", "ClubId") REFERENCES public."Orgs"("TenantId", "Id") ON DELETE RESTRICT;


--
-- Name: Memberships FK_Memberships_Persons_PersonId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."Memberships"
    ADD CONSTRAINT "FK_Memberships_Persons_PersonId" FOREIGN KEY ("PersonId") REFERENCES public."Persons"("Id") ON DELETE RESTRICT;


--
-- Name: OfficerAppointmentHistory FK_OfficerAppointmentHistory_Associations_TenantId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."OfficerAppointmentHistory"
    ADD CONSTRAINT "FK_OfficerAppointmentHistory_Associations_TenantId" FOREIGN KEY ("TenantId") REFERENCES public."Associations"("Id") ON DELETE RESTRICT;


--
-- Name: OfficerAppointmentHistory FK_OfficerAppointmentHistory_OfficerAppointments_TenantId_Appo~; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."OfficerAppointmentHistory"
    ADD CONSTRAINT "FK_OfficerAppointmentHistory_OfficerAppointments_TenantId_Appo~" FOREIGN KEY ("TenantId", "AppointmentId") REFERENCES public."OfficerAppointments"("TenantId", "Id") ON DELETE RESTRICT;


--
-- Name: OfficerAppointments FK_OfficerAppointments_Associations_TenantId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."OfficerAppointments"
    ADD CONSTRAINT "FK_OfficerAppointments_Associations_TenantId" FOREIGN KEY ("TenantId") REFERENCES public."Associations"("Id") ON DELETE RESTRICT;


--
-- Name: OfficerAppointments FK_OfficerAppointments_GovernanceDocuments_TenantId_DocumentId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."OfficerAppointments"
    ADD CONSTRAINT "FK_OfficerAppointments_GovernanceDocuments_TenantId_DocumentId" FOREIGN KEY ("TenantId", "DocumentId") REFERENCES public."GovernanceDocuments"("TenantId", "Id") ON DELETE RESTRICT;


--
-- Name: OfficerAppointments FK_OfficerAppointments_LeadershipTerms_TenantId_TermId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."OfficerAppointments"
    ADD CONSTRAINT "FK_OfficerAppointments_LeadershipTerms_TenantId_TermId" FOREIGN KEY ("TenantId", "TermId") REFERENCES public."LeadershipTerms"("TenantId", "Id") ON DELETE RESTRICT;


--
-- Name: OfficerAppointments FK_OfficerAppointments_Memberships_TenantId_MembershipId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."OfficerAppointments"
    ADD CONSTRAINT "FK_OfficerAppointments_Memberships_TenantId_MembershipId" FOREIGN KEY ("TenantId", "MembershipId") REFERENCES public."Memberships"("TenantId", "Id") ON DELETE RESTRICT;


--
-- Name: OfficerAppointments FK_OfficerAppointments_OfficerPositions_TenantId_PositionId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."OfficerAppointments"
    ADD CONSTRAINT "FK_OfficerAppointments_OfficerPositions_TenantId_PositionId" FOREIGN KEY ("TenantId", "PositionId") REFERENCES public."OfficerPositions"("TenantId", "Id") ON DELETE RESTRICT;


--
-- Name: OfficerAppointments FK_OfficerAppointments_Orgs_TenantId_OrgId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."OfficerAppointments"
    ADD CONSTRAINT "FK_OfficerAppointments_Orgs_TenantId_OrgId" FOREIGN KEY ("TenantId", "OrgId") REFERENCES public."Orgs"("TenantId", "Id") ON DELETE RESTRICT;


--
-- Name: OfficerAppointments FK_OfficerAppointments_Persons_PersonId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."OfficerAppointments"
    ADD CONSTRAINT "FK_OfficerAppointments_Persons_PersonId" FOREIGN KEY ("PersonId") REFERENCES public."Persons"("Id") ON DELETE RESTRICT;


--
-- Name: OfficerAppointments FK_OfficerAppointments_Roles_TenantId_GrantRoleId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."OfficerAppointments"
    ADD CONSTRAINT "FK_OfficerAppointments_Roles_TenantId_GrantRoleId" FOREIGN KEY ("TenantId", "GrantRoleId") REFERENCES public."Roles"("TenantId", "Id") ON DELETE RESTRICT;


--
-- Name: OfficerPositions FK_OfficerPositions_Associations_TenantId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."OfficerPositions"
    ADD CONSTRAINT "FK_OfficerPositions_Associations_TenantId" FOREIGN KEY ("TenantId") REFERENCES public."Associations"("Id") ON DELETE RESTRICT;


--
-- Name: OfficerPositions FK_OfficerPositions_Levels_TenantId_LevelId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."OfficerPositions"
    ADD CONSTRAINT "FK_OfficerPositions_Levels_TenantId_LevelId" FOREIGN KEY ("TenantId", "LevelId") REFERENCES public."Levels"("TenantId", "Id") ON DELETE RESTRICT;


--
-- Name: OfficerPositions FK_OfficerPositions_OfficerPositions_TenantId_ReportsToId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."OfficerPositions"
    ADD CONSTRAINT "FK_OfficerPositions_OfficerPositions_TenantId_ReportsToId" FOREIGN KEY ("TenantId", "ReportsToId") REFERENCES public."OfficerPositions"("TenantId", "Id") ON DELETE RESTRICT;


--
-- Name: OfficerPositions FK_OfficerPositions_Roles_TenantId_GrantRoleId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."OfficerPositions"
    ADD CONSTRAINT "FK_OfficerPositions_Roles_TenantId_GrantRoleId" FOREIGN KEY ("TenantId", "GrantRoleId") REFERENCES public."Roles"("TenantId", "Id") ON DELETE RESTRICT;


--
-- Name: OperationComments FK_OperationComments_Associations_TenantId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."OperationComments"
    ADD CONSTRAINT "FK_OperationComments_Associations_TenantId" FOREIGN KEY ("TenantId") REFERENCES public."Associations"("Id") ON DELETE RESTRICT;


--
-- Name: OperationComments FK_OperationComments_OperationTasks_TenantId_TaskId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."OperationComments"
    ADD CONSTRAINT "FK_OperationComments_OperationTasks_TenantId_TaskId" FOREIGN KEY ("TenantId", "TaskId") REFERENCES public."OperationTasks"("TenantId", "Id") ON DELETE RESTRICT;


--
-- Name: OperationComments FK_OperationComments_Orgs_TenantId_OrgId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."OperationComments"
    ADD CONSTRAINT "FK_OperationComments_Orgs_TenantId_OrgId" FOREIGN KEY ("TenantId", "OrgId") REFERENCES public."Orgs"("TenantId", "Id") ON DELETE RESTRICT;


--
-- Name: OperationRevisions FK_OperationRevisions_Associations_TenantId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."OperationRevisions"
    ADD CONSTRAINT "FK_OperationRevisions_Associations_TenantId" FOREIGN KEY ("TenantId") REFERENCES public."Associations"("Id") ON DELETE RESTRICT;


--
-- Name: OperationRevisions FK_OperationRevisions_Orgs_TenantId_OrgId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."OperationRevisions"
    ADD CONSTRAINT "FK_OperationRevisions_Orgs_TenantId_OrgId" FOREIGN KEY ("TenantId", "OrgId") REFERENCES public."Orgs"("TenantId", "Id") ON DELETE RESTRICT;


--
-- Name: OperationTasks FK_OperationTasks_Associations_TenantId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."OperationTasks"
    ADD CONSTRAINT "FK_OperationTasks_Associations_TenantId" FOREIGN KEY ("TenantId") REFERENCES public."Associations"("Id") ON DELETE RESTRICT;


--
-- Name: OperationTasks FK_OperationTasks_Orgs_TenantId_OrgId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."OperationTasks"
    ADD CONSTRAINT "FK_OperationTasks_Orgs_TenantId_OrgId" FOREIGN KEY ("TenantId", "OrgId") REFERENCES public."Orgs"("TenantId", "Id") ON DELETE RESTRICT;


--
-- Name: OrganizationHistory FK_OrganizationHistory_Associations_TenantId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."OrganizationHistory"
    ADD CONSTRAINT "FK_OrganizationHistory_Associations_TenantId" FOREIGN KEY ("TenantId") REFERENCES public."Associations"("Id") ON DELETE RESTRICT;


--
-- Name: OrganizationHistory FK_OrganizationHistory_Orgs_TenantId_OrgId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."OrganizationHistory"
    ADD CONSTRAINT "FK_OrganizationHistory_Orgs_TenantId_OrgId" FOREIGN KEY ("TenantId", "OrgId") REFERENCES public."Orgs"("TenantId", "Id") ON DELETE RESTRICT;


--
-- Name: OrganizationSettings FK_OrganizationSettings_Orgs_TenantId_OrgId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."OrganizationSettings"
    ADD CONSTRAINT "FK_OrganizationSettings_Orgs_TenantId_OrgId" FOREIGN KEY ("TenantId", "OrgId") REFERENCES public."Orgs"("TenantId", "Id") ON DELETE RESTRICT;


--
-- Name: Orgs FK_Orgs_Associations_TenantId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."Orgs"
    ADD CONSTRAINT "FK_Orgs_Associations_TenantId" FOREIGN KEY ("TenantId") REFERENCES public."Associations"("Id") ON DELETE RESTRICT;


--
-- Name: Orgs FK_Orgs_Levels_TenantId_LevelId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."Orgs"
    ADD CONSTRAINT "FK_Orgs_Levels_TenantId_LevelId" FOREIGN KEY ("TenantId", "LevelId") REFERENCES public."Levels"("TenantId", "Id") ON DELETE RESTRICT;


--
-- Name: Orgs FK_Orgs_Orgs_TenantId_ParentId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."Orgs"
    ADD CONSTRAINT "FK_Orgs_Orgs_TenantId_ParentId" FOREIGN KEY ("TenantId", "ParentId") REFERENCES public."Orgs"("TenantId", "Id") ON DELETE RESTRICT;


--
-- Name: PersonAddresses FK_PersonAddresses_Persons_PersonId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."PersonAddresses"
    ADD CONSTRAINT "FK_PersonAddresses_Persons_PersonId" FOREIGN KEY ("PersonId") REFERENCES public."Persons"("Id") ON DELETE CASCADE;


--
-- Name: PersonContacts FK_PersonContacts_Persons_PersonId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."PersonContacts"
    ADD CONSTRAINT "FK_PersonContacts_Persons_PersonId" FOREIGN KEY ("PersonId") REFERENCES public."Persons"("Id") ON DELETE CASCADE;


--
-- Name: PersonLinkRequests FK_PersonLinkRequests_Associations_TenantId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."PersonLinkRequests"
    ADD CONSTRAINT "FK_PersonLinkRequests_Associations_TenantId" FOREIGN KEY ("TenantId") REFERENCES public."Associations"("Id") ON DELETE RESTRICT;


--
-- Name: PersonLinkRequests FK_PersonLinkRequests_MembershipInvitations_TenantId_Invitatio~; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."PersonLinkRequests"
    ADD CONSTRAINT "FK_PersonLinkRequests_MembershipInvitations_TenantId_Invitatio~" FOREIGN KEY ("TenantId", "InvitationId") REFERENCES public."MembershipInvitations"("TenantId", "Id") ON DELETE RESTRICT;


--
-- Name: PersonLinkRequests FK_PersonLinkRequests_Orgs_TenantId_ClubId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."PersonLinkRequests"
    ADD CONSTRAINT "FK_PersonLinkRequests_Orgs_TenantId_ClubId" FOREIGN KEY ("TenantId", "ClubId") REFERENCES public."Orgs"("TenantId", "Id") ON DELETE RESTRICT;


--
-- Name: PersonLinkRequests FK_PersonLinkRequests_Persons_PersonId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."PersonLinkRequests"
    ADD CONSTRAINT "FK_PersonLinkRequests_Persons_PersonId" FOREIGN KEY ("PersonId") REFERENCES public."Persons"("Id") ON DELETE RESTRICT;


--
-- Name: PersonLinkRequests FK_PersonLinkRequests_UserProfiles_UserId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."PersonLinkRequests"
    ADD CONSTRAINT "FK_PersonLinkRequests_UserProfiles_UserId" FOREIGN KEY ("UserId") REFERENCES public."UserProfiles"("Id") ON DELETE RESTRICT;


--
-- Name: PersonPrivacy FK_PersonPrivacy_Persons_PersonId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."PersonPrivacy"
    ADD CONSTRAINT "FK_PersonPrivacy_Persons_PersonId" FOREIGN KEY ("PersonId") REFERENCES public."Persons"("Id") ON DELETE CASCADE;


--
-- Name: PersonTags FK_PersonTags_Persons_PersonId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."PersonTags"
    ADD CONSTRAINT "FK_PersonTags_Persons_PersonId" FOREIGN KEY ("PersonId") REFERENCES public."Persons"("Id") ON DELETE CASCADE;


--
-- Name: Persons FK_Persons_UserProfiles_UserId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."Persons"
    ADD CONSTRAINT "FK_Persons_UserProfiles_UserId" FOREIGN KEY ("UserId") REFERENCES public."UserProfiles"("Id") ON DELETE RESTRICT;


--
-- Name: ProjectActivities FK_ProjectActivities_Associations_TenantId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."ProjectActivities"
    ADD CONSTRAINT "FK_ProjectActivities_Associations_TenantId" FOREIGN KEY ("TenantId") REFERENCES public."Associations"("Id") ON DELETE RESTRICT;


--
-- Name: ProjectActivities FK_ProjectActivities_Orgs_TenantId_OrgId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."ProjectActivities"
    ADD CONSTRAINT "FK_ProjectActivities_Orgs_TenantId_OrgId" FOREIGN KEY ("TenantId", "OrgId") REFERENCES public."Orgs"("TenantId", "Id") ON DELETE RESTRICT;


--
-- Name: ProjectActivities FK_ProjectActivities_ServiceProjects_TenantId_ProjectId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."ProjectActivities"
    ADD CONSTRAINT "FK_ProjectActivities_ServiceProjects_TenantId_ProjectId" FOREIGN KEY ("TenantId", "ProjectId") REFERENCES public."ServiceProjects"("TenantId", "Id") ON DELETE RESTRICT;


--
-- Name: ProjectImpacts FK_ProjectImpacts_Associations_TenantId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."ProjectImpacts"
    ADD CONSTRAINT "FK_ProjectImpacts_Associations_TenantId" FOREIGN KEY ("TenantId") REFERENCES public."Associations"("Id") ON DELETE RESTRICT;


--
-- Name: ProjectImpacts FK_ProjectImpacts_ImpactMetrics_TenantId_MetricId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."ProjectImpacts"
    ADD CONSTRAINT "FK_ProjectImpacts_ImpactMetrics_TenantId_MetricId" FOREIGN KEY ("TenantId", "MetricId") REFERENCES public."ImpactMetrics"("TenantId", "Id") ON DELETE RESTRICT;


--
-- Name: ProjectImpacts FK_ProjectImpacts_Orgs_TenantId_OrgId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."ProjectImpacts"
    ADD CONSTRAINT "FK_ProjectImpacts_Orgs_TenantId_OrgId" FOREIGN KEY ("TenantId", "OrgId") REFERENCES public."Orgs"("TenantId", "Id") ON DELETE RESTRICT;


--
-- Name: ProjectImpacts FK_ProjectImpacts_ServiceProjects_TenantId_ProjectId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."ProjectImpacts"
    ADD CONSTRAINT "FK_ProjectImpacts_ServiceProjects_TenantId_ProjectId" FOREIGN KEY ("TenantId", "ProjectId") REFERENCES public."ServiceProjects"("TenantId", "Id") ON DELETE RESTRICT;


--
-- Name: ProjectOrganizations FK_ProjectOrganizations_Associations_TenantId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."ProjectOrganizations"
    ADD CONSTRAINT "FK_ProjectOrganizations_Associations_TenantId" FOREIGN KEY ("TenantId") REFERENCES public."Associations"("Id") ON DELETE RESTRICT;


--
-- Name: ProjectOrganizations FK_ProjectOrganizations_Orgs_TenantId_OrgId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."ProjectOrganizations"
    ADD CONSTRAINT "FK_ProjectOrganizations_Orgs_TenantId_OrgId" FOREIGN KEY ("TenantId", "OrgId") REFERENCES public."Orgs"("TenantId", "Id") ON DELETE RESTRICT;


--
-- Name: ProjectOrganizations FK_ProjectOrganizations_ServiceProjects_TenantId_ProjectId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."ProjectOrganizations"
    ADD CONSTRAINT "FK_ProjectOrganizations_ServiceProjects_TenantId_ProjectId" FOREIGN KEY ("TenantId", "ProjectId") REFERENCES public."ServiceProjects"("TenantId", "Id") ON DELETE RESTRICT;


--
-- Name: RoleAssignmentHistory FK_RoleAssignmentHistory_Associations_TenantId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."RoleAssignmentHistory"
    ADD CONSTRAINT "FK_RoleAssignmentHistory_Associations_TenantId" FOREIGN KEY ("TenantId") REFERENCES public."Associations"("Id") ON DELETE RESTRICT;


--
-- Name: RoleAssignmentHistory FK_RoleAssignmentHistory_Orgs_TenantId_OrgId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."RoleAssignmentHistory"
    ADD CONSTRAINT "FK_RoleAssignmentHistory_Orgs_TenantId_OrgId" FOREIGN KEY ("TenantId", "OrgId") REFERENCES public."Orgs"("TenantId", "Id") ON DELETE RESTRICT;


--
-- Name: RoleAssignmentHistory FK_RoleAssignmentHistory_Roles_TenantId_RoleId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."RoleAssignmentHistory"
    ADD CONSTRAINT "FK_RoleAssignmentHistory_Roles_TenantId_RoleId" FOREIGN KEY ("TenantId", "RoleId") REFERENCES public."Roles"("TenantId", "Id") ON DELETE RESTRICT;


--
-- Name: RolePermissions FK_RolePermissions_Permissions_PermissionId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."RolePermissions"
    ADD CONSTRAINT "FK_RolePermissions_Permissions_PermissionId" FOREIGN KEY ("PermissionId") REFERENCES public."Permissions"("Id") ON DELETE RESTRICT;


--
-- Name: RolePermissions FK_RolePermissions_Roles_RoleId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."RolePermissions"
    ADD CONSTRAINT "FK_RolePermissions_Roles_RoleId" FOREIGN KEY ("RoleId") REFERENCES public."Roles"("Id") ON DELETE CASCADE;


--
-- Name: Roles FK_Roles_Associations_TenantId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."Roles"
    ADD CONSTRAINT "FK_Roles_Associations_TenantId" FOREIGN KEY ("TenantId") REFERENCES public."Associations"("Id") ON DELETE RESTRICT;


--
-- Name: ServiceProjects FK_ServiceProjects_Associations_TenantId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."ServiceProjects"
    ADD CONSTRAINT "FK_ServiceProjects_Associations_TenantId" FOREIGN KEY ("TenantId") REFERENCES public."Associations"("Id") ON DELETE RESTRICT;


--
-- Name: ServiceProjects FK_ServiceProjects_Orgs_TenantId_OrgId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."ServiceProjects"
    ADD CONSTRAINT "FK_ServiceProjects_Orgs_TenantId_OrgId" FOREIGN KEY ("TenantId", "OrgId") REFERENCES public."Orgs"("TenantId", "Id") ON DELETE RESTRICT;


--
-- Name: Sessions FK_Sessions_UserProfiles_UserId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."Sessions"
    ADD CONSTRAINT "FK_Sessions_UserProfiles_UserId" FOREIGN KEY ("UserId") REFERENCES public."UserProfiles"("Id") ON DELETE CASCADE;


--
-- Name: TenantSettings FK_TenantSettings_Associations_TenantId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."TenantSettings"
    ADD CONSTRAINT "FK_TenantSettings_Associations_TenantId" FOREIGN KEY ("TenantId") REFERENCES public."Associations"("Id") ON DELETE RESTRICT;


--
-- Name: TransferDecisions FK_TransferDecisions_Associations_TenantId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."TransferDecisions"
    ADD CONSTRAINT "FK_TransferDecisions_Associations_TenantId" FOREIGN KEY ("TenantId") REFERENCES public."Associations"("Id") ON DELETE RESTRICT;


--
-- Name: TransferDecisions FK_TransferDecisions_MembershipTransfers_TenantId_TransferId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."TransferDecisions"
    ADD CONSTRAINT "FK_TransferDecisions_MembershipTransfers_TenantId_TransferId" FOREIGN KEY ("TenantId", "TransferId") REFERENCES public."MembershipTransfers"("TenantId", "Id") ON DELETE RESTRICT;


--
-- Name: UserProfiles FK_UserProfiles_Users_Id; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."UserProfiles"
    ADD CONSTRAINT "FK_UserProfiles_Users_Id" FOREIGN KEY ("Id") REFERENCES public."Users"("Id") ON DELETE CASCADE;


--
-- Name: VolunteerAssignments FK_VolunteerAssignments_Associations_TenantId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."VolunteerAssignments"
    ADD CONSTRAINT "FK_VolunteerAssignments_Associations_TenantId" FOREIGN KEY ("TenantId") REFERENCES public."Associations"("Id") ON DELETE RESTRICT;


--
-- Name: VolunteerAssignments FK_VolunteerAssignments_Orgs_TenantId_OrgId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."VolunteerAssignments"
    ADD CONSTRAINT "FK_VolunteerAssignments_Orgs_TenantId_OrgId" FOREIGN KEY ("TenantId", "OrgId") REFERENCES public."Orgs"("TenantId", "Id") ON DELETE RESTRICT;


--
-- Name: VolunteerAssignments FK_VolunteerAssignments_Persons_PersonId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."VolunteerAssignments"
    ADD CONSTRAINT "FK_VolunteerAssignments_Persons_PersonId" FOREIGN KEY ("PersonId") REFERENCES public."Persons"("Id") ON DELETE RESTRICT;


--
-- Name: VolunteerAssignments FK_VolunteerAssignments_VolunteerShifts_TenantId_ShiftId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."VolunteerAssignments"
    ADD CONSTRAINT "FK_VolunteerAssignments_VolunteerShifts_TenantId_ShiftId" FOREIGN KEY ("TenantId", "ShiftId") REFERENCES public."VolunteerShifts"("TenantId", "Id") ON DELETE RESTRICT;


--
-- Name: VolunteerHours FK_VolunteerHours_Associations_TenantId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."VolunteerHours"
    ADD CONSTRAINT "FK_VolunteerHours_Associations_TenantId" FOREIGN KEY ("TenantId") REFERENCES public."Associations"("Id") ON DELETE RESTRICT;


--
-- Name: VolunteerHours FK_VolunteerHours_Orgs_TenantId_OrgId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."VolunteerHours"
    ADD CONSTRAINT "FK_VolunteerHours_Orgs_TenantId_OrgId" FOREIGN KEY ("TenantId", "OrgId") REFERENCES public."Orgs"("TenantId", "Id") ON DELETE RESTRICT;


--
-- Name: VolunteerHours FK_VolunteerHours_Persons_PersonId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."VolunteerHours"
    ADD CONSTRAINT "FK_VolunteerHours_Persons_PersonId" FOREIGN KEY ("PersonId") REFERENCES public."Persons"("Id") ON DELETE RESTRICT;


--
-- Name: VolunteerHours FK_VolunteerHours_ServiceProjects_TenantId_ProjectId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."VolunteerHours"
    ADD CONSTRAINT "FK_VolunteerHours_ServiceProjects_TenantId_ProjectId" FOREIGN KEY ("TenantId", "ProjectId") REFERENCES public."ServiceProjects"("TenantId", "Id") ON DELETE RESTRICT;


--
-- Name: VolunteerHours FK_VolunteerHours_VolunteerAssignments_TenantId_AssignmentId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."VolunteerHours"
    ADD CONSTRAINT "FK_VolunteerHours_VolunteerAssignments_TenantId_AssignmentId" FOREIGN KEY ("TenantId", "AssignmentId") REFERENCES public."VolunteerAssignments"("TenantId", "Id") ON DELETE RESTRICT;


--
-- Name: VolunteerProfiles FK_VolunteerProfiles_Associations_TenantId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."VolunteerProfiles"
    ADD CONSTRAINT "FK_VolunteerProfiles_Associations_TenantId" FOREIGN KEY ("TenantId") REFERENCES public."Associations"("Id") ON DELETE RESTRICT;


--
-- Name: VolunteerProfiles FK_VolunteerProfiles_Orgs_TenantId_OrgId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."VolunteerProfiles"
    ADD CONSTRAINT "FK_VolunteerProfiles_Orgs_TenantId_OrgId" FOREIGN KEY ("TenantId", "OrgId") REFERENCES public."Orgs"("TenantId", "Id") ON DELETE RESTRICT;


--
-- Name: VolunteerProfiles FK_VolunteerProfiles_Persons_PersonId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."VolunteerProfiles"
    ADD CONSTRAINT "FK_VolunteerProfiles_Persons_PersonId" FOREIGN KEY ("PersonId") REFERENCES public."Persons"("Id") ON DELETE RESTRICT;


--
-- Name: VolunteerShifts FK_VolunteerShifts_Associations_TenantId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."VolunteerShifts"
    ADD CONSTRAINT "FK_VolunteerShifts_Associations_TenantId" FOREIGN KEY ("TenantId") REFERENCES public."Associations"("Id") ON DELETE RESTRICT;


--
-- Name: VolunteerShifts FK_VolunteerShifts_Orgs_TenantId_OrgId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."VolunteerShifts"
    ADD CONSTRAINT "FK_VolunteerShifts_Orgs_TenantId_OrgId" FOREIGN KEY ("TenantId", "OrgId") REFERENCES public."Orgs"("TenantId", "Id") ON DELETE RESTRICT;


--
-- Name: VolunteerShifts FK_VolunteerShifts_ProjectActivities_TenantId_ActivityId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."VolunteerShifts"
    ADD CONSTRAINT "FK_VolunteerShifts_ProjectActivities_TenantId_ActivityId" FOREIGN KEY ("TenantId", "ActivityId") REFERENCES public."ProjectActivities"("TenantId", "Id") ON DELETE RESTRICT;


--
-- Name: VolunteerShifts FK_VolunteerShifts_ServiceProjects_TenantId_ProjectId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."VolunteerShifts"
    ADD CONSTRAINT "FK_VolunteerShifts_ServiceProjects_TenantId_ProjectId" FOREIGN KEY ("TenantId", "ProjectId") REFERENCES public."ServiceProjects"("TenantId", "Id") ON DELETE RESTRICT;


--
-- PostgreSQL database dump complete
--

\unrestrict Ixnd9FHFPWgSBcHYI0zZwsOwJzRTuaL6biXJ1Ikfp0XjF1Sao93HtX4vuCLdaFE

