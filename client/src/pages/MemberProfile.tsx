import {MemberAchievements} from "./Operations";
import {MemberOffices} from "../GovernanceUI";
import { useState } from "react";
import { Link, useParams, useSearchParams } from "react-router-dom";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import {
  ArrowLeft,
  Pencil,
  Download,
  ShieldCheck,
  Upload,
  FileText,
  ExternalLink,
  Link2,
} from "lucide-react";
import { api, apiForm } from "../api";
import { PageHeader, Skeleton, ErrorBox, useWorkspace } from "../App";
import {
  MemberAvatar,
  MemberStatus,
  MemberDrawer,
  PersonEditor,
  PrivacyEditor,
  ClubCategoryFields,
} from "../MemberComponents";
export default function MemberProfile() {
  const { id } = useParams(),
    [params] = useSearchParams(),
    { w } = useWorkspace(),
    qc = useQueryClient();
  const directory = params.get("directory") === "true";
  const [tab, setTab] = useState("Overview"),
    [editing, setEditing] = useState(false),
    [action, setAction] = useState(""),
    [error, setError] = useState<unknown>(),
    [message, setMessage] = useState(""),
    [busy, setBusy] = useState(false);
  const q = useQuery({
    queryKey: ["member-profile", id, directory, w?.id],
    queryFn: () => api("/members/" + id + (directory ? "?directory=true" : "")),
  });
  function refresh() {
    qc.invalidateQueries({ queryKey: ["member-profile"] });
    qc.invalidateQueries({ queryKey: ["members"] });
    qc.invalidateQueries({ queryKey: ["member-metrics"] });
    qc.invalidateQueries({ queryKey: ["my-membership"] });
    qc.invalidateQueries({ queryKey: ["workspaces"] });
  }
  async function mutation(path: string, method: string, body: any) {
    setBusy(true);
    setError(null);
    try {
      await api(path, method, body);
      refresh();
      setMessage("Your change has been saved.");
      return true;
    } catch (e) {
      setError(e);
      return false;
    } finally {
      setBusy(false);
    }
  }
  if (q.isPending) return <Skeleton />;
  if (q.error) return <ErrorBox error={q.error} />;
  const d = q.data,
    p = d.person,
    m = d.membership,
    own = p.privacy != null;
  const tabs = [
    "Overview",
    "Personal details",
    "Professional",
    "Membership history",
    "Leadership",
    "Participation",
    "Documents",
    "Privacy & preferences",
  ];
  return (
    <>
      <Link
        className="back-link profile-back"
        to={
          directory
            ? "/app/directory"
            : d.canManage
              ? "/app/members"
              : "/app/my-membership"
        }
      >
        <ArrowLeft size={13} />
        Back to {directory ? "directory" : "members"}
      </Link>
      <PageHeader
        eyebrow="A PERSON. A STORY. A COMMUNITY."
        title="Member profile"
        description={"Person reference · " + p.reference}
      >
        <div className="header-actions">
          {d.canEditPerson && (
            <button className="secondary" onClick={() => setEditing(true)}>
              <Pencil size={14} />
              Edit profile
            </button>
          )}
          {d.hasCurrentCard && d.canCard && (
            <a
              className="button"
              href={"/api/v1/members/" + id + "/card"}
              download
            >
              <Download size={15} />
              Digital ID
            </a>
          )}
        </div>
      </PageHeader>
      <ErrorBox error={error} />
      {message && (
        <div role="status" className="success">
          {message}
        </div>
      )}
      <section className="profile-banner panel">
        <MemberAvatar
          id={id}
          name={p.name}
          photo={p.hasPhoto}
          directory={directory}
          size="xl"
        />
        <div>
          <div className="eyebrow">{m.number}</div>
          <h1>{p.name}</h1>
          <p>
            {p.professional?.designation ||
              p.professional?.profession ||
              m.category}{" "}
            · {m.club}
          </p>
          <div className="header-actions">
            <MemberStatus status={m.status} />
            <span className="profile-chip">
              Joined {new Date(m.joinedAt).toLocaleDateString()}
            </span>
          </div>
        </div>
        {own && (
          <label className="photo-upload">
            <Upload size={15} />
            Update photo
            <input
              type="file"
              accept="image/png,image/jpeg"
              onChange={async (e) => {
                const f = e.target.files?.[0];
                if (!f) return;
                const data = new FormData();
                data.append("file", f);
                try {
                  await apiForm("/persons/me/photo", data);
                  refresh();
                  setMessage("Photo saved with metadata removed.");
                } catch (e) {
                  setError(e);
                }
              }}
            />
          </label>
        )}
      </section>
      <div
        className="member-tabs profile-tabs"
        role="tablist"
        aria-label="Member profile tabs"
      >
        {tabs.map((t) => (
          <button
            role="tab"
            aria-selected={t === tab}
            key={t}
            className={t === tab ? "active" : ""}
            onClick={() => setTab(t)}
          >
            {t}
          </button>
        ))}
      </div>
      <div className="profile-layout">
        <section className="panel profile-body">
          {tab === "Overview" && (
            <>
              <h2>Connected through service</h2>
              <p>
                Your membership identity and current affiliations, together.
              </p>
              <div className="detail-grid">
                {[
                  ["Display name", p.name],
                  ["Membership number", m.number],
                  ["Primary club", m.club],
                  ["Membership category", m.category],
                  [
                    "Location",
                    p.addresses
                      ?.map((a: any) =>
                        [a.city, a.country].filter(Boolean).join(", "),
                      )
                      .join(" · ") || "Not provided",
                  ],
                  [
                    "Current validity",
                    m.endsAt
                      ? new Date(m.endsAt).toLocaleDateString()
                      : "While active",
                  ],
                  ["Renewal status", m.renewalStatus],
                  ["Person reference", p.reference],
                ].map(([k, v]) => (
                  <Detail key={k} label={k} value={v} />
                ))}
              </div>
              <h3 className="spaced">Current affiliations</h3>
              {d.memberships.map((a: any) => (
                <Link
                  className="affiliation-row"
                  key={a.id}
                  to={
                    "/app/members/" +
                    a.id +
                    (directory ? "?directory=true" : "")
                  }
                >
                  <div>
                    <b>{a.club}</b>
                    <small>
                      {a.number} · {a.category}
                    </small>
                  </div>
                  <MemberStatus status={a.status} />
                </Link>
              ))}
              {directory && p.contactAllowed && (
                <form
                  className="form-stack spaced"
                  onSubmit={async (e) => {
                    e.preventDefault();
                    const text = new FormData(e.currentTarget).get("message");
                    if (
                      await mutation(
                        "/directory/" + id + "/contact-request",
                        "POST",
                        { message: text },
                      )
                    )
                      setMessage(
                        "Your contact request is recorded for the member.",
                      );
                  }}
                >
                  <label>
                    Request contact
                    <textarea
                      name="message"
                      required
                      minLength={5}
                      maxLength={1000}
                      placeholder="Introduce yourself and explain your reason for connecting."
                    />
                  </label>
                  <button disabled={busy}>Send contact request</button>
                </form>
              )}
            </>
          )}
          {tab === "Personal details" && (
            <>
              <h2>Personal details</h2>
              {!p.legalName && (
                <div className="privacy-notice">
                  <ShieldCheck size={18} />
                  Sensitive details are restricted by the API.
                </div>
              )}
              <div className="detail-grid">
                {[
                  ["Legal name", p.legalName],
                  ["Preferred name", p.displayName],
                  ["Date of birth", p.birthDate],
                  ["Gender", p.gender],
                  ["Marital status", p.maritalStatus],
                  ["Spouse name", p.spouseName],
                  ["Marriage anniversary", p.anniversary],
                  ["Education level", p.educationLevel],
                  ["Qualification", p.qualification],
                  ["Institution", p.institution],
                  ["Employment status", p.employmentStatus],
                  ["Additional information", p.additionalInformation],
                  ["Languages", p.languages?.join(", ")],
                ].map(([k, v]) =>
                  v ? <Detail key={k} label={k} value={v} /> : null,
                )}
              </div>
              <h3 className="spaced">Contacts</h3>
              {p.contacts?.length ? (
                p.contacts.map((c: any, i: number) => (
                  <div className="context-row" key={i}>
                    <span>
                      {c.label} · {c.kind}
                    </span>
                    <b>
                      {c.value}
                      {c.verified && <ShieldCheck size={13} />}
                    </b>
                  </div>
                ))
              ) : (
                <p>No contact details are shared with this workspace.</p>
              )}
              <h3 className="spaced">Addresses</h3>
              {p.addresses?.map((a: any, i: number) => (
                <p key={i}>
                  {[a.address, a.city, a.state, a.postalCode, a.country]
                    .filter(Boolean)
                    .join(", ")}
                </p>
              ))}
            </>
          )}
          {tab === "Professional" && (
            <>
              <h2>Professional profile</h2>
              {p.professional ? (
                <>
                  <div className="detail-grid">
                    {Object.entries(p.professional)
                      .filter(([, v]) => !Array.isArray(v))
                      .map(([k, v]) => (
                        <Detail
                          key={k}
                          label={k}
                          value={String(v || "Not provided")}
                        />
                      ))}
                  </div>
                  {["skills", "expertise", "interests"].map((k) => (
                    <div className="spaced" key={k}>
                      <h3>{k[0].toUpperCase() + k.slice(1)}</h3>
                      <div className="tag-list">
                        {p.professional[k]?.map((v: string) => (
                          <span key={v}>{v}</span>
                        ))}
                      </div>
                    </div>
                  ))}
                </>
              ) : (
                <div className="empty">
                  This member has not opted into sharing professional details.
                </div>
              )}
            </>
          )}
          {tab === "Membership history" && (
            <>
              <h2>A continuous membership story</h2>
              <p>
                Transfers preserve the original affiliation and create a
                destination affiliation for the same person.
              </p>
              {d.history?.length ? (
                d.history.map((h: any) => (
                  <div className="timeline-item" key={h.id}>
                    <span className="activity-dot" />
                    <div>
                      <b>
                        {h.oldStatus ? h.oldStatus + " → " : ""}
                        {h.newStatus}
                      </b>
                      <p>{h.reason}</p>
                      <small>
                        Effective {new Date(h.effectiveAt).toLocaleString()} ·
                        Recorded {new Date(h.recordedAt).toLocaleString()}
                      </small>
                    </div>
                  </div>
                ))
              ) : (
                <p>
                  Historical details are restricted to the member and authorized
                  administrators.
                </p>
              )}
              {d.renewals?.map((r: any) => (
                <div className="context-row" key={r.id}>
                  <span>Validity renewed</span>
                  <b>{new Date(r.newEnd).toLocaleDateString()}</b>
                </div>
              ))}
              {d.transfers?.map((r: any) => (
                <div className="context-row" key={r.id}>
                  <span>
                    Transfer · {r.status}
                    <small>{r.reason}</small>
                  </span>
                  <b>{new Date(r.effectiveAt).toLocaleDateString()}</b>
                </div>
              ))}
              {d.applications?.map((a: any) => (
                <div className="context-row" key={a.id}>
                  <span>Application · {a.status}</span>
                  <span>{new Date(a.createdAt).toLocaleDateString()}</span>
                </div>
              ))}
            </>
          )}
          {tab === "Leadership" && (
            <>
              {!directory&&<><MemberAchievements id={id}/><MemberOffices id={id}/></>}<h2>Application access roles</h2>
              <p>
                These roles come from the shared organizational authorization
                model.
              </p>
              {d.roles?.length ? (
                d.roles.map((r: any, i: number) => (
                  <div className="leadership-card" key={i}>
                    <ShieldCheck size={20} />
                    <div>
                      <h3>{r.name}</h3>
                      <p>{r.organization}</p>
                    </div>
                  </div>
                ))
              ) : (
                <div className="empty">
                  No roles are visible within this scope.
                </div>
              )}
              {d.roleHistory?.map((r: any) => (
                <div className="timeline-item" key={r.id}>
                  <span className="activity-dot" />
                  <div>
                    <b>
                      {r.role} · {r.action}
                    </b>
                    <small>
                      {r.organization} · {new Date(r.at).toLocaleString()}
                    </small>
                  </div>
                </div>
              ))}
            </>
          )}
          {tab === "Participation" && (
            <>
              <h2>Membership activity</h2>
              {d.activities?.length ? (
                d.activities.map((a: any) => (
                  <div className="timeline-item" key={a.id}>
                    <span className="activity-dot" />
                    <div>
                      <b>{a.action.replaceAll(".", " · ")}</b>
                      <p>{a.detail}</p>
                      <small>{new Date(a.at).toLocaleString()}</small>
                    </div>
                  </div>
                ))
              ) : (
                <div className="empty">
                  No activity is visible in your scope.
                </div>
              )}
              <div className="upcoming-inline">
                Events, meetings and volunteering will join this timeline in
                later phases.
              </div>
            </>
          )}
          {tab === "Documents" && (
            <>
              <h2>Secure membership documents</h2>
              <p>
                Files require authenticated access. Identity documents are never
                published through public card verification.
              </p>
              {d.canDocuments ? (
                <>
                  <form
                    className="document-upload"
                    onSubmit={async (e) => {
                      e.preventDefault();
                      const form = new FormData(e.currentTarget);
                      try {
                        await apiForm("/members/" + id + "/documents", form);
                        refresh();
                        setMessage("Document uploaded securely for review.");
                      } catch (e) {
                        setError(e);
                      }
                    }}
                  >
                    <label>
                      Document
                      <input
                        name="file"
                        type="file"
                        accept=".pdf,.png,.jpg,.jpeg"
                        required
                      />
                    </label>
                    <div className="form-grid">
                      <label>
                        Document type
                        <select name="kind">
                          {[
                            "Supporting",
                            "Application",
                            "Identity",
                            "Certificate",
                          ].map((x) => (
                            <option key={x}>{x}</option>
                          ))}
                        </select>
                      </label>
                      <label>
                        Delete after (optional)
                        <input name="retainUntil" type="date" />
                      </label>
                    </div>
                    <button>
                      <Upload size={14} />
                      Upload document
                    </button>
                  </form>
                  {d.documents?.map((doc: any) => (
                    <div className="document-row" key={doc.id}>
                      <FileText size={20} />
                      <div>
                        <b>{doc.fileName}</b>
                        <small>
                          {doc.kind} · {doc.status}
                          {doc.retainUntil
                            ? " · Retained until " +
                              new Date(doc.retainUntil).toLocaleDateString()
                            : ""}
                        </small>
                      </div>
                      <a
                        className="icon-button"
                        aria-label={"Download " + doc.fileName}
                        href={"/api/v1/membership/documents/" + doc.id}
                        download
                      >
                        <Download size={16} />
                      </a>
                      {!own && doc.status === "Pending" && (
                        <button
                          className="secondary"
                          onClick={() =>
                            mutation(
                              "/membership/documents/" + doc.id + "/approve",
                              "POST",
                              {},
                            )
                          }
                        >
                          Approve
                        </button>
                      )}
                      <button
                        className="text-button"
                        onClick={async () => {
                          if (
                            window.confirm("Permanently delete this document?")
                          )
                            await mutation(
                              "/membership/documents/" + doc.id,
                              "DELETE",
                              undefined,
                            );
                        }}
                      >
                        Delete
                      </button>
                    </div>
                  ))}
                </>
              ) : (
                <div className="privacy-notice">
                  <ShieldCheck size={18} />
                  Document access is restricted in this workspace.
                </div>
              )}
            </>
          )}
          {tab === "Privacy & preferences" && (
            <>
              <h2>Your information. Your choices.</h2>
              <PrivacyEditor
                privacy={p.privacy}
                busy={busy}
                onSave={async (v: any) => {
                  if (await mutation("/persons/me/privacy", "PUT", v))
                    setMessage(
                      "Privacy preferences saved. Directory visibility is enforced by the API.",
                    );
                }}
              />
            </>
          )}
        </section>
        <aside className="profile-aside">
          <section className="panel">
            <div className="eyebrow">MEMBERSHIP CONTEXT</div>
            <h3>{m.club}</h3>
            <p>{m.category}</p>
            <div className="context-row">
              <span>Status</span>
              <MemberStatus status={m.status} />
            </div>
            <div className="context-row">
              <span>Joined</span>
              <b>{new Date(m.joinedAt).toLocaleDateString()}</b>
            </div>
            <div className="context-row">
              <span>Renewal</span>
              <b>{m.renewalStatus}</b>
            </div>
          </section>
          {d.canManage && (
            <section className="panel spaced">
              <h3>Membership actions</h3>
              <div className="action-stack">
                {["status", "renew", "category", "hold", "link"].map((a) => (
                  <button
                    className="secondary"
                    key={a}
                    onClick={() => setAction(a)}
                  >
                    {
                      (
                        {
                          status: "Change status",
                          renew: "Record renewal",
                          category: "Change category",
                          hold: "Administrative hold",
                          link: "Invite account linking",
                        } as any
                      )[a]
                    }
                  </button>
                ))}
                <button
                  className="secondary"
                  onClick={async () => {
                    await mutation(
                      "/members/" + id + "/cards/reissue",
                      "POST",
                      { version: m.version },
                    );
                  }}
                >
                  Reissue digital ID
                </button>
              </div>
            </section>
          )}
          {own && m.status === "Active" && (
            <section className="panel spaced">
              <h3>Need a new chapter?</h3>
              <p>
                Request a reviewed transfer while preserving your membership
                history.
              </p>
              <button
                className="secondary"
                onClick={() => setAction("transfer")}
              >
                Request transfer <ExternalLink size={14} />
              </button>
            </section>
          )}
        </aside>
      </div>
      {editing && (
        <MemberDrawer
          title="Edit member profile"
          onClose={() => setEditing(false)}
        >
          <ErrorBox error={error} />
          <PersonEditor
            person={p}
            busy={busy}
            onCancel={() => setEditing(false)}
            onSave={async (person: any) => {
              if (
                await mutation(
                  own ? "/persons/me" : "/members/" + id + "/person",
                  "PUT",
                  person,
                )
              )
                setEditing(false);
            }}
          />
        </MemberDrawer>
      )}
      {action && (
        <MemberDrawer
          title={
            action === "transfer"
              ? "Request membership transfer"
              : "Membership action"
          }
          onClose={() => setAction("")}
        >
          <ErrorBox error={error} />
          {action === "transfer" ? (
            <TransferForm
              membership={m}
              onDone={() => {
                setAction("");
                refresh();
              }}
            />
          ) : (
            <MembershipAction
              action={action}
              member={m}
              busy={busy}
              mutate={async (path: string, body: any) => {
                if (await mutation(path, "POST", body)) setAction("");
              }}
            />
          )}
        </MemberDrawer>
      )}
    </>
  );
}
function Detail({ label, value }: any) {
  return (
    <div className="detail-item">
      <small>{label}</small>
      <b>{value || "Not provided"}</b>
    </div>
  );
}
export function TransferForm({ membership: m, onDone }: any) {
  const [f, setF] = useState<any>({
      clubId: "",
      categoryId: m.categoryId,
      tenantId: m.tenantId,
      reason: "",
      effectiveAt: "",
    }),
    [error, setError] = useState<unknown>(),
    [busy, setBusy] = useState(false);
  return (
    <form
      className="form-stack"
      onSubmit={async (e) => {
        e.preventDefault();
        setBusy(true);
        try {
          await api("/members/" + m.id + "/transfers", "POST", {
            destinationClubId: f.clubId,
            categoryId: f.categoryId,
            reason: f.reason,
            version: m.version,
            effectiveAt: f.effectiveAt
              ? new Date(f.effectiveAt).toISOString()
              : null,
          });
          onDone();
        } catch (e) {
          setError(e);
        } finally {
          setBusy(false);
        }
      }}
    >
      <p>
        Source and destination reviews follow association policy. Independent
        officer roles remain separate from membership access.
      </p>
      <ClubCategoryFields
        {...f}
        publicCatalog
        fixedTenant
        excludeClub={m.clubId}
        onChange={(v: any) => setF({ ...f, ...v })}
      />
      <label>
        Requested effective date (blank means now)
        <input
          type="date"
          value={f.effectiveAt}
          onChange={(e) => setF({ ...f, effectiveAt: e.target.value })}
        />
      </label>
      <label>
        Reason
        <textarea
          required
          minLength={5}
          maxLength={2000}
          value={f.reason}
          onChange={(e) => setF({ ...f, reason: e.target.value })}
        />
      </label>
      <ErrorBox error={error} />
      <button disabled={busy}>
        {busy ? "Submitting…" : "Submit transfer request"}
      </button>
    </form>
  );
}
function MembershipAction({ action, member: m, mutate, busy }: any) {
  const [f, setF] = useState<any>({
    status: "Inactive",
    reason: "",
    newEnd: "",
    categoryId: m.categoryId,
    hold: true,
    email: "",
  });
  const config = useQuery({
    queryKey: ["member-action-categories", m.tenantId],
    queryFn: () => api("/membership/config"),
  });
  return (
    <form
      className="form-stack"
      onSubmit={(e) => {
        e.preventDefault();
        if (action === "status")
          mutate("/members/" + m.id + "/status", {
            status: f.status,
            reason: f.reason,
            version: m.version,
          });
        else if (action === "renew")
          mutate("/members/" + m.id + "/renewals", {
            newEnd: new Date(f.newEnd).toISOString(),
            version: m.version,
            requestKey: crypto.randomUUID(),
          });
        else if (action === "category")
          mutate("/members/" + m.id + "/category", {
            categoryId: f.categoryId,
            reason: f.reason,
            version: m.version,
          });
        else if (action === "hold")
          mutate("/members/" + m.id + "/financial-hold", {
            hold: f.hold,
            reason: f.reason,
            version: m.version,
          });
        else
          mutate("/membership/invitations", {
            clubId: m.clubId,
            categoryId: m.categoryId,
            personId: m.personId,
            email: f.email,
            expiresInDays: 7,
            usageLimit: 1,
          });
      }}
    >
      {action === "status" && (
        <label>
          New membership status
          <select
            value={f.status}
            onChange={(e) => setF({ ...f, status: e.target.value })}
          >
            {[
              "Active",
              "Inactive",
              "Suspended",
              "Expired",
              "Resigned",
              "Terminated",
              "Deceased",
            ].map((x) => (
              <option key={x}>{x}</option>
            ))}
          </select>
        </label>
      )}
      {action === "renew" && (
        <>
          <p>
            Record a validity extension. This does not record a payment or
            confirm settlement.
          </p>
          <label>
            New validity end
            <input
              type="date"
              required
              value={f.newEnd}
              onChange={(e) => setF({ ...f, newEnd: e.target.value })}
            />
          </label>
        </>
      )}
      {action === "category" && (
        <label>
          New category
          <select
            required
            value={f.categoryId}
            onChange={(e) => setF({ ...f, categoryId: e.target.value })}
          >
            {config.data?.categories.map((c: any) => (
              <option key={c.id} value={c.id}>
                {c.name}
              </option>
            ))}
          </select>
        </label>
      )}
      {action === "hold" && (
        <>
          <p>
            This is an administrative review marker; no accounting balance is
            inferred.
          </p>
          <label className="checkbox">
            <input
              type="checkbox"
              checked={f.hold}
              onChange={(e) => setF({ ...f, hold: e.target.checked })}
            />
            Membership has an administrative financial hold
          </label>
        </>
      )}
      {action === "link" && (
        <>
          <Link2 size={24} />
          <p>
            The verified recipient must accept the invitation. An authorized
            officer then records an identity-review decision before the account
            is linked. Shared email addresses are not automatically matched.
          </p>
          <label>
            Intended verified account email
            <input
              required
              type="email"
              value={f.email}
              onChange={(e) => setF({ ...f, email: e.target.value })}
            />
          </label>
        </>
      )}
      {!["renew", "link"].includes(action) && (
        <label>
          Reason
          <textarea
            required
            minLength={5}
            maxLength={2000}
            value={f.reason}
            onChange={(e) => setF({ ...f, reason: e.target.value })}
          />
        </label>
      )}
      <button disabled={busy}>
        {busy
          ? "Saving…"
          : action === "link"
            ? "Queue account-link invitation"
            : "Save membership action"}
      </button>
    </form>
  );
}
