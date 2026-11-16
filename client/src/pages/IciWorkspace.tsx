import { useEffect, useState } from "react";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { Link } from "react-router-dom";
import { api } from "../api";
import { ErrorBox, PageHeader, Skeleton, useWorkspace } from "../App";
import { Field, ModalForm, useAction, useDebounce } from "../GovernanceUI";
import PresidentStatistics from "./PresidentStatistics";
import { IciCareerProfile } from "./IciCareerProfile";
import "./IciWorkspace.css";

export const iciRoleNames = [
  "Executive Board",
  "Office Bearers",
  "National President",
  "National Secretary",
  "National Treasurer",
  "National Vice President",
  "National Director",
  "National Coordinator",
  "Regional President",
  "Regional Director",
  "Club President",
];
const labels: Record<string, string> = {
  TitleClearance: "New LO title clearance",
  Subscription: "Membership subscription",
  EventBid: "Event bid",
  EventReport: "Activity report",
  FinancialAid: "Project financial aid",
  ClubCreation: "Create a club",
  ClubMapping: "Map club to region",
  RegionMapping: "Map region to NVP",
  TeamHandover: "Leadership team handover",
  LegalAction: "Confidential legal record",
};
const money = (v: number) =>
  new Intl.NumberFormat("en-IN", { style: "currency", currency: "INR" }).format(
    v || 0,
  );
function useIci(path: string, enabled = true) {
  const { w } = useWorkspace();
  return useQuery<any>({
    queryKey: ["ici", w?.id, path],
    queryFn: () => api("/ici" + path),
    enabled: !!w && enabled,
  });
}
function Badge({ children }: any) {
  return <span className="ici-badge">{children}</span>;
}
export default function IciWorkspace() {
  const { w } = useWorkspace(),
    q = useIci("/context"),
    [tab, setTab] = useState("Overview");
  useEffect(() => setTab("Overview"), [w?.id]);
  if (q.isPending) return <Skeleton />;
  if (q.error) return <ErrorBox error={q.error} />;
  const c = q.data;
  const definition = c.definitions.find((d: any) => d.name === c.role);
  const tabs = [
    "Overview",
    "Requests",
    "Activities",
    ...(c.capabilities.members ? ["Members"] : []),
    ...(w?.permissions.includes("members.transfer.approve")
      ? ["Transfers"]
      : []),
    ...(w?.permissions.includes("governance.approve")
      ? ["Event approvals"]
      : []),
    ...(c.capabilities.team ? ["Team & coverage"] : []),
    "My profile",
    "Role catalog",
  ];
  return (
    <div className="ici-workspace">
      <PageHeader
        title={c.role + " workspace"}
        eyebrow="ICI LEADERSHIP"
        description={[c.slot, c.portfolio, w?.organization]
          .filter(Boolean)
          .join(" · ")}
      />
      <nav className="ici-tabs" aria-label="Leadership workspace views">
        {tabs.map((t) => (
          <button
            className={tab === t ? "active" : "secondary"}
            key={t}
            onClick={() => setTab(t)}
          >
            {t}
          </button>
        ))}
      </nav>
      {tab === "Overview" ? (
        <>
          <section className="panel">
            <h2>Your responsibilities</h2>
            <div className="ici-responsibilities">
              <div>
                <h3>Features</h3>
                <ul>
                  {(
                    definition?.features || [
                      "Configure role assignments, review workflows and record payment verification.",
                    ]
                  ).map((s: string) => (
                    <li key={s}>{s}</li>
                  ))}
                </ul>
              </div>
              <div>
                <h3>Reports</h3>
                <ul>
                  {(
                    definition?.reports || ["Authorized organizational reports"]
                  ).map((s: string) => (
                    <li key={s}>{s}</li>
                  ))}
                </ul>
              </div>
            </div>
            {[
              "National Vice President",
              "Office Bearers",
              "National Secretary",
            ].includes(c.role) && (
              <p>
                Your area consists of your assigned regions. No additional
                hierarchy level is created.
              </p>
            )}
            {[
              "Regional Director",
              "National Director",
              "National Coordinator",
            ].includes(c.role) && (
              <p>
                All activity lists and approvals are restricted to your assigned
                portfolio:{" "}
                <strong>
                  {c.portfolio || "Awaiting assignment configuration"}
                </strong>
                .
              </p>
            )}
            <div className="ici-quick">
              <button onClick={() => setTab("Requests")}>Open requests</button>
              <button
                className="secondary"
                onClick={() => setTab("Activities")}
              >
                View activities
              </button>
              <Link to="/app/profile">Profile & account security</Link>
              <Link to="/app/my-membership">Member profile & certificates</Link>
              {c.capabilities.members && (
                <Link to="/app/members">Scoped member directory</Link>
              )}
            </div>
          </section>
          {["Club President", "Regional President"].includes(c.role) && (
            <PresidentStatistics embedded />
          )}
          {c.role === "National Treasurer" && (
            <Requests context={c} initialKind="FinancialAid" />
          )}
        </>
      ) : tab === "Requests" ? (
        <Requests context={c} />
      ) : tab === "Activities" ? (
        <Activities context={c} />
      ) : tab === "Members" ? (
        <Members />
      ) : tab === "Transfers" ? (
        <Transfers />
      ) : tab === "Event approvals" ? (
        <EventApprovals />
      ) : tab === "Team & coverage" ? (
        <Team context={c} />
      ) : tab === "My profile" ? (
        <IciCareerProfile />
      ) : (
        <section className="ici-role-grid">
          {c.definitions.map((d: any) => (
            <article className="panel" key={d.name}>
              <Badge>{d.level}</Badge>
              <h2>{d.name}</h2>
              <h3>Features</h3>
              <ul>
                {d.features.map((s: string) => (
                  <li key={s}>{s}</li>
                ))}
              </ul>
              <h3>Reports</h3>
              <ul>
                {d.reports.map((s: string) => (
                  <li key={s}>{s}</li>
                ))}
              </ul>
              {d.portfolios.length > 0 && (
                <p>Portfolios: {d.portfolios.join(", ")}</p>
              )}
            </article>
          ))}
        </section>
      )}
    </div>
  );
}

function Requests({ context: c, initialKind = "" }: any) {
  const [kind, setKind] = useState(initialKind),
    [status, setStatus] = useState(""),
    [page, setPage] = useState(1),
    [create, setCreate] = useState(false),
    [selected, setSelected] = useState("");
  const q = useIci(
    "/requests?" + new URLSearchParams({ kind, status, page: String(page) }),
  );
  return (
    <section className="panel">
      <div className="section-heading">
        <h2>
          {initialKind === "FinancialAid"
            ? "Projects requesting financial aid"
            : "Requests & decisions"}
        </h2>
        {c.submitKinds.length > 0 && (
          <button onClick={() => setCreate(true)}>New request</button>
        )}
      </div>
      <div className="ici-filters">
        <label>
          Request type
          <select
            value={kind}
            onChange={(e) => {
              setKind(e.target.value);
              setPage(1);
            }}
          >
            <option value="">All permitted requests</option>
            {Object.entries(labels).map(([value, label]) => (
              <option key={value} value={value}>
                {label}
              </option>
            ))}
          </select>
        </label>
        <label>
          Status
          <select
            value={status}
            onChange={(e) => {
              setStatus(e.target.value);
              setPage(1);
            }}
          >
            <option value="">All statuses</option>
            {[
              "Draft",
              "Submitted",
              "ChangesRequested",
              "Approved",
              "Rejected",
              "Withdrawn",
            ].map((v) => (
              <option key={v}>{v}</option>
            ))}
          </select>
        </label>
      </div>
      {q.data?.items.map((r: any) => (
        <button
          className="ici-request secondary"
          key={r.id}
          onClick={() => setSelected(r.id)}
        >
          <div>
            <Badge>{labels[r.kind]}</Badge>
            <h3>{r.title}</h3>
            <small>
              {[c.units.find((o: any) => o.id === r.orgId)?.name, r.portfolio, new Date(r.createdAt).toLocaleDateString()].filter(Boolean).join(' · ')}
            </small>
          </div>
          <div>
            <strong>{r.status}</strong>
            {r.kind === "TitleClearance" && r.resultId && <Badge>Extended LO created</Badge>}
            {r.amount > 0 && <span>{money(r.amount)}</span>}
            <small>
              {r.status === "Submitted"
                ? "Review: " + r.reviewer
                : "Open details"}
            </small>
          </div>
        </button>
      ))}
      {q.data?.items.length === 0 && (
        <p>No matching requests in your authorized scope.</p>
      )}
      <div className="pager">
        <button
          className="secondary"
          disabled={page === 1}
          onClick={() => setPage(page - 1)}
        >
          Previous
        </button>
        <span>
          {q.data?.total || 0} requests · Page {page}
        </span>
        <button
          className="secondary"
          disabled={page * 30 >= (q.data?.total || 0)}
          onClick={() => setPage(page + 1)}
        >
          Next
        </button>
      </div>
      <ErrorBox error={q.error} />
      {create && (
        <RequestForm
          context={c}
          onClose={() => setCreate(false)}
          onSaved={(r: any) => {
            setCreate(false);
            setSelected(r.id);
          }}
        />
      )}
      {selected && (
        <RequestDetails
          id={selected}
          context={c}
          onClose={() => setSelected("")}
        />
      )}
    </section>
  );
}
function RequestDetails({ id, context: c, onClose }: any) {
  const q = useIci("/requests/" + id),
    a = useAction(),
    qc = useQueryClient(),
    { me } = useWorkspace(),
    [comment, setComment] = useState(""),
    [edit, setEdit] = useState(false);
  const r = q.data?.request,
    f = q.data?.input;
  async function action(value: string) {
    try {
      await a.run("/ici/requests/" + id + "/actions", "POST", {
        action: value,
        version: r.version,
        comment,
      });
      qc.invalidateQueries({ queryKey: ["ici"] });
    } catch {}
  }
  if (!r) return <ErrorBox error={q.error} />;
  if (edit)
    return (
      <RequestForm
        initial={{ ...f, id: r.id, version: r.version }}
        context={c}
        onClose={() => setEdit(false)}
        onSaved={() => setEdit(false)}
      />
    );
  return (
    <ModalForm
      title={r.title}
      onClose={onClose}
      action="Close"
      onSubmit={(e: any) => {
        e.preventDefault();
        onClose();
      }}
      feedback={a.feedback}
    >
      <div className="ici-quick">
        <Badge>{labels[r.kind]}</Badge>
        <Badge>{r.status}</Badge>
        {r.kind === "TitleClearance" && r.resultId && <Badge>Extended LO created</Badge>}
        {r.amount > 0 && <Badge>{money(r.amount)}</Badge>}
      </div>
      <p>{f.description}</p>
      <dl className="ici-details">
        {Object.entries({
          portfolio: r.portfolio,
          reference: f.reference,
          members: r.memberCount,
          volunteers: r.volunteerCount,
          hours: r.hours,
          code: f.code,
          location: [f.city, f.country].filter(Boolean).join(", "),
          incomingEmail: f.email,
          incomingRole: f.role,
          slot: f.slot,
        })
          .filter(([, v]) => v)
          .map(([key, value]) => (
            <div key={key}>
              <dt>{key.replace(/([A-Z])/g, " $1")}</dt>
              <dd>{String(value)}</dd>
            </div>
          ))}
      </dl>
      {r.eventId && (
        <Link to={"/app/events/" + r.eventId}>View related activity</Link>
      )}
      {r.resultId && r.kind === "ClubCreation" && (
        <Link to={"/app/clubs/" + r.resultId}>View created club</Link>
      )}
      {["TitleClearance", "Subscription"].includes(r.kind) && (
        <p className="notice">
          Payment record:{" "}
          {r.paymentVerified
            ? "Administratively verified"
            : "Awaiting verification"}{" "}
          · {r.paymentReference || "No reference recorded"}. This records a
          payment reference; it does not charge a bank account.
        </p>
      )}
      <h3>Approval route</h3>
      <ol>
        {r.route.map((s: string, i: number) => (
          <li key={i}>
            {s.replaceAll("|", " or ")}{" "}
            {r.status === "Submitted" && r.stage === i ? "← Current stage" : ""}
          </li>
        ))}
      </ol>
      <label>
        Decision comment / verified payment evidence
        <textarea
          value={comment}
          onChange={(e) => setComment(e.target.value)}
          maxLength={2000}
        />
      </label>
      <div className="ici-quick">
        {r.creatorId === me?.id &&
          ["Draft", "ChangesRequested"].includes(r.status) && (
            <>
              <button
                type="button"
                className="secondary"
                onClick={() => setEdit(true)}
              >
                Edit draft
              </button>
              <button
                type="button"
                disabled={a.busy}
                onClick={() => action("Submit")}
              >
                Submit for review
              </button>
            </>
          )}
        {r.creatorId === me?.id &&
          ["Draft", "Submitted", "ChangesRequested"].includes(r.status) && (
            <button
              type="button"
              className="secondary"
              disabled={a.busy}
              onClick={() => action("Withdraw")}
            >
              Withdraw
            </button>
          )}
        {r.canVerifyPayment && !r.paymentVerified && (
          <button
            type="button"
            disabled={a.busy || comment.trim().length < 3}
            onClick={() => action("VerifyPayment")}
          >
            Verify payment record
          </button>
        )}
        {r.canReview &&
          r.creatorId !== me?.id &&
          ["Approve", "Reject", "RequestChanges"].map((v) => (
            <button
              key={v}
              type="button"
              disabled={
                a.busy || (v !== "Approve" && comment.trim().length < 3)
              }
              className={v === "Approve" ? "" : "secondary"}
              onClick={() => action(v)}
            >
              {v === "RequestChanges" ? "Request changes" : v}
            </button>
          ))}
      </div>
      <h3>Decision history</h3>
      {q.data.decisions.map((d: any) => (
        <div className="context-row" key={d.id}>
          <span>
            {d.actorRole} · {d.decision}
            <small>{d.comment}</small>
          </span>
          <small>
            {new Date(d.at).toLocaleString()} · Revision {d.revision}
          </small>
        </div>
      ))}
    </ModalForm>
  );
}
function RequestForm({ context: c, initial, onClose, onSaved }: any) {
  const { w } = useWorkspace(),
    a = useAction(),
    qc = useQueryClient();
  const [f, set] = useState<any>({
    kind: c.submitKinds[0],
    orgId: w?.orgId,
    title: "",
    description: "",
    portfolio: "",
    amount: 0,
    memberCount: 0,
    volunteerCount: 0,
    hours: 0,
    reference: "",
    code: "",
    country: "India",
    city: "",
    email: "",
    role: "",
    slot: "",
    ...initial,
    eventId: initial?.eventId || "",
    targetOrgId: initial?.targetOrgId || "",
    relatedRequestId: initial?.relatedRequestId || "",
    oldAssignmentId: initial?.oldAssignmentId || "",
    memberIds: initial?.memberIds || [],
    regionIds: initial?.regionIds || [],
  });
  const [memberSearch, setMemberSearch] = useState(""),
    [memberPage, setMemberPage] = useState(1),
    [eventSearch, setEventSearch] = useState(""),
    [eventPage, setEventPage] = useState(1);
  const memberTerm = useDebounce(memberSearch),
    eventTerm = useDebounce(eventSearch);
  const kind = f.kind;
  const reports = useIci(
    "/reports?" +
      new URLSearchParams({ search: eventTerm, page: String(eventPage) }),
    !!kind && ["EventBid", "EventReport", "FinancialAid"].includes(kind),
  );
  const clearances = useIci(
    "/requests?kind=TitleClearance&status=Approved",
    kind === "ClubCreation",
  );
  const team = useIci(
    "/team",
    ["TeamHandover", "RegionMapping"].includes(kind) && c.capabilities.team,
  );
  const members = useQuery<any>({
    queryKey: [
      "ici",
      w?.id,
      "subscription-members",
      f.orgId,
      memberTerm,
      memberPage,
    ],
    queryFn: () =>
      api(
        "/members?" +
          new URLSearchParams({
            clubId: f.orgId,
            statuses: "Active",
            size: "100",
            search: memberTerm,
            page: String(memberPage),
          }),
      ),
    enabled: kind === "Subscription" && !!f.orgId,
  });
  const definition = c.definitions.find((d: any) => d.name === f.role);
  const units = c.units.filter((o: any) =>
    ["TitleClearance", "Subscription", "ClubMapping"].includes(kind)
      ? o.level === "Club"
      : ["ClubCreation", "RegionMapping"].includes(kind)
        ? o.level === "Region"
        : kind === "TeamHandover" && definition
          ? o.level === definition.level
          : true,
  );
  const regions = [...c.units, ...c.ancestors].filter(
    (o: any, i: number, all: any[]) =>
      o.level === "Region" && all.findIndex((n) => n.id === o.id) === i,
  );
  return (
    <ModalForm
      title={initial ? "Edit request" : "New ICI request"}
      onClose={onClose}
      busy={a.busy}
      feedback={a.feedback}
      action="Save draft"
      onSubmit={async (e: any) => {
        e.preventDefault();
        try {
          const payload = {
            ...f,
            eventId: f.eventId || null,
            targetOrgId:
              kind === "ClubCreation" ? f.orgId : f.targetOrgId || null,
            relatedRequestId: f.relatedRequestId || null,
            oldAssignmentId: f.oldAssignmentId || null,
            amount: +f.amount,
            memberCount: +f.memberCount,
            volunteerCount: +f.volunteerCount,
            hours: +f.hours,
          };
          const result = await a.run(
            "/ici/requests" + (initial ? "/" + f.id : ""),
            initial ? "PUT" : "POST",
            payload,
          );
          qc.invalidateQueries({ queryKey: ["ici"] });
          onSaved(result);
        } catch {}
      }}
    >
      <label>
        Request type
        <select
          disabled={!!initial}
          value={kind}
          onChange={(e) =>
            set({
              ...f,
              kind: e.target.value,
              eventId: "",
              targetOrgId: "",
              relatedRequestId: "",
              memberIds: [],
            })
          }
        >
          {c.submitKinds.map((v: string) => (
            <option key={v} value={v}>
              {labels[v]}
            </option>
          ))}
        </select>
      </label>
      {kind === "TeamHandover" && (
        <label>
          Incoming role
          <select
            value={f.role}
            required
            onChange={(e) =>
              set({
                ...f,
                role: e.target.value,
                portfolio: "",
                slot: "",
                orgId: "",
                regionIds: [],
              })
            }
          >
            <option value="">Choose role</option>
            {c.definitions
              .filter(
                (d: any) =>
                  c.role !== "National President" ||
                  d.name !== "National President",
              )
              .map((d: any) => (
                <option key={d.name}>{d.name}</option>
              ))}
          </select>
        </label>
      )}
      <label>
        Owning organization
        <select
          required
          value={f.orgId}
          onChange={(e) => set({ ...f, orgId: e.target.value, memberIds: [] })}
        >
          <option value="">Choose organization</option>
          {units.map((o: any) => (
            <option key={o.id} value={o.id}>
              {o.level} · {o.name}
            </option>
          ))}
        </select>
      </label>
      <Field
        label={
          kind === "TitleClearance" || kind === "ClubCreation"
            ? "Proposed club title"
            : "Request title"
        }
        name="title"
        required
        form={f}
        set={set}
      />
      <Field
        label="Details / purpose"
        name="description"
        type="textarea"
        form={f}
        set={set}
      />
      {["TitleClearance", "ClubMapping"].includes(kind) && (
        <label>
          Destination region
          <select
            required
            value={f.targetOrgId}
            onChange={(e) => set({ ...f, targetOrgId: e.target.value })}
          >
            <option value="">Choose region</option>
            {regions.map((o: any) => (
              <option key={o.id} value={o.id}>
                {o.name}
              </option>
            ))}
          </select>
        </label>
      )}
      {["EventBid", "EventReport", "FinancialAid"].includes(kind) && (
        <>
          <label>
            Find related activity
            <input
              value={eventSearch}
              onChange={(e) => {
                setEventSearch(e.target.value);
                setEventPage(1);
              }}
            />
          </label>
          <label>
            Related activity
            <select
              required={kind !== "EventBid"}
              value={f.eventId}
              onChange={(e) =>
                set({
                  ...f,
                  eventId: e.target.value,
                  orgId:
                    reports.data?.items.find(
                      (x: any) => x.id === e.target.value,
                    )?.orgId || f.orgId,
                })
              }
            >
              <option value="">
                {kind === "EventBid"
                  ? "Proposal without an existing event"
                  : "Choose activity"}
              </option>
              {reports.data?.items.map((x: any) => (
                <option key={x.id} value={x.id}>
                  {x.title} · {x.organization}
                </option>
              ))}
            </select>
          </label>
          <div className="pager">
            <button
              type="button"
              className="secondary"
              disabled={eventPage === 1}
              onClick={() => setEventPage(eventPage - 1)}
            >
              Previous activities
            </button>
            <span>Page {eventPage}</span>
            <button
              type="button"
              className="secondary"
              disabled={eventPage * 30 >= (reports.data?.total || 0)}
              onClick={() => setEventPage(eventPage + 1)}
            >
              Next activities
            </button>
          </div>
        </>
      )}
      {kind === "EventReport" && (
        <div className="form-grid">
          {[
            ["memberCount", "Member participants"],
            ["volunteerCount", "Outside volunteers"],
            ["hours", "Reported hours"],
          ].map(([name, label]) => (
            <Field
              key={name}
              label={label}
              name={name}
              type="number"
              form={f}
              set={set}
            />
          ))}
        </div>
      )}
      {["EventBid", "FinancialAid", "EventReport"].includes(kind) && (
        <Field
          label={
            kind === "FinancialAid"
              ? "Financial aid requested (INR)"
              : "Budget / actual cost (INR)"
          }
          name="amount"
          type="number"
          form={f}
          set={set}
        />
      )}
      <Field
        label={
          ["Subscription", "TitleClearance"].includes(kind)
            ? "Payment reference / receipt evidence"
            : "Reference"
        }
        name="reference"
        form={f}
        set={set}
      />
      {kind === "TitleClearance" && (
        <p>
          Title-clearance fee: {money(100)}. Administration must verify the
          payment record before approval.
        </p>
      )}
      {kind === "Subscription" && (
        <>
          <p>
            {money(365)} per member. Initial registration requires at least 20
            members. Selected: {f.memberIds.length} · Total:{" "}
            {money(f.memberIds.length * 365)}.
          </p>
          <label>
            Find active member
            <input
              value={memberSearch}
              onChange={(e) => {
                setMemberSearch(e.target.value);
                setMemberPage(1);
              }}
            />
          </label>
          <button
            type="button"
            className="secondary"
            onClick={() =>
              set({
                ...f,
                memberIds: [
                  ...new Set([
                    ...f.memberIds,
                    ...(members.data?.items || []).map((m: any) => m.id),
                  ]),
                ],
              })
            }
          >
            Select this page
          </button>
          <fieldset>
            <legend>Select active members</legend>
            {members.data?.items.map((m: any) => (
              <label className="check-row" key={m.id}>
                <input
                  type="checkbox"
                  checked={f.memberIds.includes(m.id)}
                  onChange={(e) =>
                    set({
                      ...f,
                      memberIds: e.target.checked
                        ? [...f.memberIds, m.id]
                        : f.memberIds.filter((id: string) => id !== m.id),
                    })
                  }
                />
                {m.name} · {m.number}
              </label>
            ))}
          </fieldset>
          <div className="pager">
            <button
              type="button"
              className="secondary"
              disabled={memberPage === 1}
              onClick={() => setMemberPage(memberPage - 1)}
            >
              Previous members
            </button>
            <span>
              {members.data?.total || 0} matches · Page {memberPage}
            </span>
            <button
              type="button"
              className="secondary"
              disabled={memberPage * 100 >= (members.data?.total || 0)}
              onClick={() => setMemberPage(memberPage + 1)}
            >
              Next members
            </button>
          </div>
          <ErrorBox error={members.error} />
        </>
      )}
      {kind === "ClubCreation" && (
        <>
          <label>
            Approved title clearance
            <select
              required
              value={f.relatedRequestId}
              onChange={(e) => {
                const r = clearances.data?.items.find(
                  (r: any) => r.id === e.target.value,
                );
                set({
                  ...f,
                  relatedRequestId: e.target.value,
                  ...(r ? { title: r.title, orgId: r.targetOrgId } : {}),
                });
              }}
            >
              <option value="">Choose approved clearance</option>
              {clearances.data?.items.map((r: any) => (
                <option key={r.id} value={r.id}>
                  {r.title}
                </option>
              ))}
            </select>
          </label>
          {[
            ["code", "New club code"],
            ["country", "Country"],
            ["city", "City"],
          ].map(([name, label]) => (
            <Field
              key={name}
              label={label}
              name={name}
              required={name === "code"}
              form={f}
              set={set}
            />
          ))}
        </>
      )}
      {kind === "RegionMapping" && (
        <label>
          National Vice President
          <select
            required
            value={f.targetOrgId}
            onChange={(e) => set({ ...f, targetOrgId: e.target.value })}
          >
            <option value="">Choose active NVP</option>
            {team.data?.assignments
              .filter((t: any) => t.role === "National Vice President")
              .map((t: any) => (
                <option key={t.id} value={t.id}>
                  {t.slot} · {t.name} · {t.organization}
                </option>
              ))}
          </select>
        </label>
      )}
      {kind === "TeamHandover" && (
        <>
          <Field
            label="Incoming verified account email"
            name="email"
            type="email"
            required
            form={f}
            set={set}
          />
          <label>
            Outgoing assignment
            <select
              value={f.oldAssignmentId}
              onChange={(e) => set({ ...f, oldAssignmentId: e.target.value })}
            >
              <option value="">New vacant office</option>
              {team.data?.assignments
                .filter((t: any) => t.role === f.role && t.orgId === f.orgId)
                .map((t: any) => (
                  <option key={t.id} value={t.id}>
                    {t.name} · {t.role} · {t.portfolio || t.slot}
                  </option>
                ))}
            </select>
          </label>
          <ScopeFields
            definition={definition}
            form={f}
            set={set}
            regions={regions}
          />
          <p>
            Handover changes role access after independent Executive Board
            review. Existing passwords are never revealed or changed.
          </p>
        </>
      )}
      {kind === "LegalAction" && (
        <p>
          This confidential case is visible only to Executive Board and
          authorized administrators. Record internal case details and decisions
          here.
        </p>
      )}
    </ModalForm>
  );
}

function Activities({ context: c }: any) {
  const [org, setOrg] = useState(""),
    [search, setSearch] = useState(""),
    [page, setPage] = useState(1);
  const q = useIci(
    "/reports?" +
      new URLSearchParams({
        ...(org ? {orgId: org} : {}),
        search: useDebounce(search),
        page: String(page),
      }),
  );
  return (
    <section className="panel">
      <h2>Authorized activity reports</h2>
      <div className="ici-filters">
        <label>
          Region / club
          <select
            value={org}
            onChange={(e) => {
              setOrg(e.target.value);
              setPage(1);
            }}
          >
            <option value="">All assigned areas</option>
            {c.units.map((o: any) => (
              <option key={o.id} value={o.id}>
                {o.level} · {o.name}
              </option>
            ))}
          </select>
        </label>
        <label>
          Find activity
          <input
            value={search}
            onChange={(e) => {
              setSearch(e.target.value);
              setPage(1);
            }}
          />
        </label>
      </div>
      <p>
        {q.data?.total || 0} activities · {q.data?.participants || 0} ICI member
        participants · {q.data?.volunteers || 0} volunteers
      </p>
      {q.data?.items.map((e: any) => (
        <div className="context-row" key={e.id}>
          <Link to={"/app/events/" + e.id}>
            {e.title}
            <small>
              {e.organization} · {e.activityType}
              {e.activityProgram ? " · " + e.activityProgram : ""}
            </small>
          </Link>
          <span>
            {new Date(e.startsAt).toLocaleDateString()}
            <small>
              {e.publication} · {e.approval}
            </small>
          </span>
        </div>
      ))}
      <div className="pager">
        <button
          className="secondary"
          disabled={page === 1}
          onClick={() => setPage(page - 1)}
        >
          Previous
        </button>
        <span>Page {page}</span>
        <button
          className="secondary"
          disabled={page * 30 >= (q.data?.total || 0)}
          onClick={() => setPage(page + 1)}
        >
          Next
        </button>
      </div>
      <ErrorBox error={q.error} />
    </section>
  );
}
function Members() {
  const { w } = useWorkspace(),
    [search, setSearch] = useState(""),
    [page, setPage] = useState(1);
  const term = useDebounce(search);
  const q = useQuery<any>({
    queryKey: ["ici", w?.id, "member-report", term, page],
    queryFn: () =>
      api(
        "/members?" +
          new URLSearchParams({ search: term, page: String(page), size: "25" }),
      ),
  });
  return (
    <section className="panel">
      <h2>Members in your assigned regions and clubs</h2>
      <label>
        Find member
        <input
          value={search}
          onChange={(e) => {
            setSearch(e.target.value);
            setPage(1);
          }}
        />
      </label>
      {q.data?.items.map((m: any) => (
        <div className="context-row" key={m.id}>
          <Link to={"/app/members/" + m.id}>
            {m.name}
            <small>
              {m.number} · {m.club}
            </small>
          </Link>
          <span>{m.status}</span>
        </div>
      ))}
      <div className="pager">
        <button
          className="secondary"
          disabled={page === 1}
          onClick={() => setPage(page - 1)}
        >
          Previous
        </button>
        <span>
          {q.data?.total || 0} members · Page {page}
        </span>
        <button
          className="secondary"
          disabled={page * 25 >= (q.data?.total || 0)}
          onClick={() => setPage(page + 1)}
        >
          Next
        </button>
      </div>
      <ErrorBox error={q.error} />
    </section>
  );
}
function Transfers() {
  const { w } = useWorkspace(),
    a = useAction(),
    qc = useQueryClient(),
    [page, setPage] = useState(1),
    [comment, setComment] = useState("");
  const q = useQuery<any>({
    queryKey: ["ici", w?.id, "transfers", page],
    queryFn: () => api("/membership/transfers?page=" + page),
  });
  return (
    <section className="panel">
      <h2>Inward / outward member transfers</h2>
      <p>
        Regional Presidents review source and destination first, then the
        assigned NVPs. National President or Executive Board provides final
        approval.
      </p>
      <label>
        Review comment
        <input
          value={comment}
          onChange={(e) => setComment(e.target.value)}
          maxLength={2000}
        />
      </label>
      {q.data?.items.map((t: any) => (
        <article className="panel" key={t.id}>
          <h3>{t.person}</h3>
          <p>
            {t.source} → {t.destination}
          </p>
          <Badge>{t.status}</Badge>
          {t.reviewer && <p>Awaiting {t.reviewer}</p>}
          <p>{t.reason}</p>
          {t.canReview && (
            <div className="ici-quick">
              {["Approve", "Reject"].map((decision) => (
                <button
                  key={decision}
                  className={decision === "Approve" ? "" : "secondary"}
                  disabled={
                    a.busy || (decision === "Reject" && !comment.trim())
                  }
                  onClick={async () => {
                    try {
                      await a.run(
                        "/membership/transfers/" + t.id + "/decisions",
                        "POST",
                        { decision, comment, version: t.version },
                      );
                      qc.invalidateQueries({ queryKey: ["ici"] });
                    } catch {}
                  }}
                >
                  {decision}
                </button>
              ))}
            </div>
          )}
        </article>
      ))}
      <div className="pager">
        <button
          className="secondary"
          disabled={page === 1}
          onClick={() => setPage(page - 1)}
        >
          Previous
        </button>
        <span>Page {page}</span>
        <button
          className="secondary"
          disabled={page * 20 >= (q.data?.total || 0)}
          onClick={() => setPage(page + 1)}
        >
          Next
        </button>
      </div>
      {a.feedback}
      <ErrorBox error={q.error} />
    </section>
  );
}
function EventApprovals() {
  const q = useIci("/event-reviews"),
    qc = useQueryClient(),
    a = useAction(),
    [comment, setComment] = useState("");
  return (
    <section className="panel">
      <h2>Staged event approvals</h2>
      <p>
        Club: Regional President → matching National Director. Region: matching
        National Director → Executive Board.
      </p>
      <label>
        Decision comment
        <textarea
          value={comment}
          onChange={(e) => setComment(e.target.value)}
          maxLength={2000}
        />
      </label>
      {q.data?.map((r: any) => {
        const reviewer = r.reviewer;
        const allowed = r.canReview;
        return (
          <article className="panel" key={r.id}>
            <h3>{r.title}</h3>
            <p>
              Stage {r.currentSequence}: {reviewer}
            </p>
            <Link to={"/app/governance?request=" + r.id}>
              Review full proposal
            </Link>
            {allowed && (
              <div className="ici-quick">
                {["Approve", "Reject", "RequestChanges"].map((decision) => (
                  <button
                    disabled={
                      a.busy ||
                      (decision !== "Approve" && comment.trim().length < 3)
                    }
                    key={decision}
                    className={decision === "Approve" ? "" : "secondary"}
                    onClick={async () => {
                      try {
                        await a.run(
                          "/governance/requests/" + r.id + "/decisions",
                          "POST",
                          { decision, comment, version: r.version },
                        );
                        qc.invalidateQueries({ queryKey: ["ici"] });
                      } catch {}
                    }}
                  >
                    {decision === "RequestChanges"
                      ? "Request changes"
                      : decision}
                  </button>
                ))}
              </div>
            )}
          </article>
        );
      })}
      {q.data?.length === 0 && <p>No pending events in your review scope.</p>}
      {a.feedback}
      <ErrorBox error={q.error} />
    </section>
  );
}
function ScopeFields({ definition, form: f, set, regions }: any) {
  return (
    <>
      {definition?.portfolios.length > 0 && (
        <label>
          Assigned portfolio
          <select
            required
            value={f.portfolio}
            onChange={(e) => set({ ...f, portfolio: e.target.value })}
          >
            <option value="">Choose portfolio</option>
            {definition.portfolios.map((p: string) => (
              <option key={p}>{p}</option>
            ))}
          </select>
        </label>
      )}
      {f.role === "National Vice President" && (
        <label>
          NVP slot
          <select
            required
            value={f.slot}
            onChange={(e) => set({ ...f, slot: e.target.value })}
          >
            <option value="">Choose slot</option>
            {["NVP 1", "NVP 2", "NVP 3", "NVP 4"].map((s) => (
              <option key={s}>{s}</option>
            ))}
          </select>
        </label>
      )}
      {[
        "National Vice President",
        "Office Bearers",
        "National Secretary",
      ].includes(f.role) && (
        <fieldset>
          <legend>Assigned regions</legend>
          {regions.map((o: any) => (
            <label className="check-row" key={o.id}>
              <input
                type="checkbox"
                checked={f.regionIds.includes(o.id)}
                onChange={(e) =>
                  set({
                    ...f,
                    regionIds: e.target.checked
                      ? [...f.regionIds, o.id]
                      : f.regionIds.filter((id: string) => id !== o.id),
                  })
                }
              />
              {o.name}
            </label>
          ))}
        </fieldset>
      )}
    </>
  );
}
function Team({ context: c }: any) {
  const q = useIci("/team"),
    a = useAction(),
    qc = useQueryClient(),
    [f, set] = useState<any>();
  const definition = c.definitions.find((d: any) => d.name === f?.role);
  return (
    <section className="panel">
      <div className="section-heading">
        <h2>Leadership team & assigned coverage</h2>
        {c.capabilities.admin && (
          <button
            onClick={() =>
              set({
                email: "",
                role: "",
                roleId: "",
                orgId: "",
                portfolio: "",
                slot: "",
                regionIds: [],
              })
            }
          >
            Assign ICI role
          </button>
        )}
      </div>
      {!c.capabilities.admin && (
        <p>
          Submit team changes through Requests → Leadership team handover.
          National President cannot replace their own office; Executive Board
          review is required.
        </p>
      )}
      {q.data?.assignments.map((t: any) => (
        <article className="context-row" key={t.id}>
          <span>
            <strong>{t.name}</strong>
            <small>
              {t.role} · {t.organization} · {t.portfolio || t.slot}
            </small>
            <small>
              {q.data.coverage
                .filter((r: any) => r.assignmentId === t.id)
                .map(
                  (r: any) =>
                    c.units.find((o: any) => o.id === r.regionId)?.name,
                )
                .filter(Boolean)
                .join(", ")}
            </small>
          </span>
          {c.capabilities.admin && (
            <button
              className="secondary"
              onClick={() =>
                set({
                  ...t,
                  email: t.email,
                  regionIds: q.data.coverage
                    .filter((r: any) => r.assignmentId === t.id)
                    .map((r: any) => r.regionId),
                })
              }
            >
              Configure scope
            </button>
          )}
        </article>
      ))}
      <ErrorBox error={q.error} />
      {f && (
        <ModalForm
          title={f.id ? "Configure assigned scope" : "Assign ICI role"}
          onClose={() => set(undefined)}
          busy={a.busy}
          feedback={a.feedback}
          onSubmit={async (e: any) => {
            e.preventDefault();
            try {
              if (f.id)
                await a.run("/ici/team/" + f.id + "/scope", "PUT", {
                  portfolio: f.portfolio,
                  slot: f.slot,
                  regionIds: f.regionIds,
                });
              else
                await a.run("/ici/team", "POST", {
                  email: f.email,
                  roleId: f.roleId,
                  orgId: f.orgId,
                  portfolio: f.portfolio,
                  slot: f.slot,
                  regionIds: f.regionIds,
                });
              qc.invalidateQueries({ queryKey: ["ici"] });
              qc.invalidateQueries({ queryKey: ["workspaces"] });
              set(undefined);
            } catch {}
          }}
        >
          {!f.id && (
            <>
              <Field
                label="Verified account email"
                name="email"
                type="email"
                required
                form={f}
                set={set}
              />
              <label>
                ICI role
                <select
                  required
                  value={f.roleId}
                  onChange={(e) => {
                    const r = q.data.roles.find(
                      (r: any) => r.id === e.target.value,
                    );
                    set({
                      ...f,
                      roleId: e.target.value,
                      role: r?.name || "",
                      portfolio: "",
                      slot: "",
                      orgId: "",
                      regionIds: [],
                    });
                  }}
                >
                  <option value="">Choose role</option>
                  {q.data?.roles.map((r: any) => (
                    <option key={r.id} value={r.id}>
                      {r.name}
                    </option>
                  ))}
                </select>
              </label>
              <label>
                Organization
                <select
                  required
                  value={f.orgId}
                  onChange={(e) =>
                    set({ ...f, orgId: e.target.value, regionIds: [] })
                  }
                >
                  <option value="">Choose organization</option>
                  {c.units
                    .filter(
                      (o: any) => !definition || o.level === definition.level,
                    )
                    .map((o: any) => (
                      <option key={o.id} value={o.id}>
                        {o.level} · {o.name}
                      </option>
                    ))}
                </select>
              </label>
            </>
          )}
          <ScopeFields
            definition={definition}
            form={f}
            set={set}
            regions={c.units.filter((o: any) => o.level === "Region")}
          />
        </ModalForm>
      )}
    </section>
  );
}
