import {OwnLeadership} from "../GovernanceUI";
import { useState } from "react";
import { Link } from "react-router-dom";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import {
  CreditCard,
  ArrowUpRight,
  ShieldCheck,
  Building2,
  Download,
  Plus,
  UserRound,
  CalendarDays,
} from "lucide-react";
import { api } from "../api";
import { PageHeader, Skeleton, ErrorBox, useWorkspace } from "../App";
import {
  MemberStatus,
  MemberDrawer,
  PersonEditor,
  PrivacyEditor,
} from "../MemberComponents";
import { TransferForm } from "./MemberProfile";
export default function MemberPortal() {
  const { me, w } = useWorkspace(),
    qc = useQueryClient();
  const [drawer, setDrawer] = useState(""),
    [chosen, setChosen] = useState(""),
    [error, setError] = useState<unknown>(),
    [busy, setBusy] = useState(false),
    [message, setMessage] = useState("");
  const q = useQuery({
    queryKey: ["my-membership"],
    queryFn: () => api("/membership/me"),
  });
  function refresh() {
    qc.invalidateQueries({ queryKey: ["my-membership"] });
    qc.invalidateQueries({ queryKey: ["member-profile"] });
    qc.invalidateQueries({ queryKey: ["workspaces"] });
  }
  if (q.isPending) return <Skeleton />;
  if (q.error) return <ErrorBox error={q.error} />;
  const d = q.data,
    p = d.profile,
    m =
      d.memberships.find((x: any) => x.id === chosen) ||
      d.memberships.find((x: any) => x.status === "Active") ||
      d.memberships[0],
    contact = d.clubContacts?.find((c: any) => c.id === m?.clubId);
  const completion = p
    ? Math.round(
        ([
          !!p.legalName,
          !!p.displayName,
          !!p.contacts?.length,
          !!p.addresses?.some((a: any) => a.country),
          !!p.addresses?.some((a: any) => a.city),
        ].filter(Boolean).length /
          5) *
          100,
      )
    : 0;
  return (
    <>
      <PageHeader
        eyebrow="YOUR COMMUNITY. YOUR IMPACT."
        title={"Hello, " + me.name.split(" ")[0] + "."}
        description="A simpler home for your memberships, identity, and the people around you."
      >
        <Link className="button secondary" to="/app/apply">
          <Plus size={14} />
          Find another club
        </Link>
      </PageHeader>
      <ErrorBox error={error} />
      {message && (
        <div className="success" role="status">
          {message}
        </div>
      )}
      {d.linkRequests?.some((r: any) => r.status === "Pending") && (
        <div className="privacy-notice">
          <ShieldCheck size={20} />
          Your existing membership account link is awaiting an officer’s
          identity review. Creating another person profile now would prevent
          that link.
        </div>
      )}
      <div className="portal-layout">
        <div>
          <section className="panel portal-identity">
            <div className="section-heading">
              <div>
                <div className="eyebrow">BELONGING, MADE VISIBLE</div>
                <h2>Your digital membership</h2>
              </div>
              <CreditCard size={24} />
            </div>
            {m ? (
              <>
                <select
                  aria-label="Select my membership"
                  value={m.id}
                  onChange={(e) => setChosen(e.target.value)}
                >
                  {d.memberships.map((x: any) => (
                    <option key={x.id} value={x.id}>
                      {x.club} · {x.category} · {x.status}
                    </option>
                  ))}
                </select>
                <DigitalIdentity membership={m} />
                <div className="context-row">
                  <span>Current club</span>
                  <b>{m.club}</b>
                </div>
                <div className="context-row">
                  <span>Category</span>
                  <b>{m.category}</b>
                </div>
                <div className="context-row">
                  <span>Status</span>
                  <MemberStatus status={m.status} />
                </div>
                <div className="header-actions spaced">
                  <Link
                    className="button secondary"
                    to={"/app/members/" + m.id}
                  >
                    My complete profile <ArrowUpRight size={15} />
                  </Link>
                  {m.status === "Active" && (
                    <button
                      className="text-button"
                      onClick={() => setDrawer("transfer")}
                    >
                      Request transfer →
                    </button>
                  )}
                </div>
              </>
            ) : (
              <div className="portal-empty">
                <Building2 size={40} />
                <h2>Your next chapter starts with a community.</h2>
                <p>
                  Apply to a club and follow your approval journey. Your digital
                  identity becomes available when membership is activated.
                </p>
                <Link className="button" to="/app/apply">
                  Find a club <ArrowUpRight size={15} />
                </Link>
              </div>
            )}
          </section>
          <section className="panel spaced">
            <div className="section-heading">
              <div>
                <h2>Your membership journey</h2>
                <p>
                  Current and previous affiliations remain part of the same
                  person’s story.
                </p>
              </div>
            </div>
            {d.memberships.map((x: any) => (
              <Link
                className="affiliation-row"
                key={x.id}
                to={"/app/members/" + x.id}
              >
                <div>
                  <b>{x.club}</b>
                  <small>
                    {x.number} · Joined{" "}
                    {new Date(x.joinedAt).toLocaleDateString()}
                  </small>
                </div>
                <MemberStatus status={x.status} />
              </Link>
            ))}
            {d.applications.map((a: any) => (
              <div className="affiliation-row" key={a.id}>
                <div>
                  <b>{a.club}</b>
                  <small>Membership application</small>
                </div>
                <MemberStatus status={a.status} />
                <Link className="text-button" to="/app/apply">
                  Track application →
                </Link>
              </div>
            ))}
            {d.transfers.map((t: any) => (
              <div className="affiliation-row" key={t.id}>
                <div>
                  <b>Transfer · {t.status}</b>
                  <small>{t.reason}</small>
                </div>
                <span>{new Date(t.effectiveAt).toLocaleDateString()}</span>
                {[
                  "PendingSource",
                  "PendingDestination",
                  "PendingAssociation",
                  "Scheduled",
                  "Blocked",
                ].includes(t.status) && (
                  <button
                    className="text-button"
                    onClick={async () => {
                      try {
                        await api(
                          "/membership/transfers/" + t.id + "/cancel",
                          "POST",
                          { version: t.version },
                        );
                        refresh();
                      } catch (e) {
                        setError(e);
                      }
                    }}
                  >
                    Cancel
                  </button>
                )}
              </div>
            ))}
          </section>
          <section className="panel spaced">
            <h2>Recent notifications</h2>
            {d.notifications.length ? (
              d.notifications.map((n: any) => (
                <div className="notification-item" key={n.id}>
                  <span className="activity-dot" />
                  <div>
                    <b>{n.subject}</b>
                    <p>{n.body}</p>
                    <small>Email delivery: {n.status}</small>
                  </div>
                </div>
              ))
            ) : (
              <div className="empty">
                Your transactional membership updates will appear here.
              </div>
            )}
          </section>
        </div>
        <aside>
          <section className="panel">
            <div className="section-heading">
              <h2>Your profile</h2>
              <UserRound size={19} />
            </div>
            <div className="completion-ring">
              <strong>{completion}%</strong>
              <span>basic profile complete</span>
            </div>
            <div className="progress-track">
              <span style={{ width: completion + "%" }} />
            </div>
            <p>
              Based on name, preferred name, a contact, country and city.
              Sensitive optional details never affect this score.
            </p>
            <button
              className="secondary full-width"
              disabled={d.linkRequests?.some(
                (r: any) => r.status === "Pending",
              )}
              onClick={() => setDrawer("profile")}
            >
              {p ? "Edit personal profile" : "Create member profile"}
            </button>
            <button
              className="text-button full-width spaced"
              disabled={!p}
              onClick={() => setDrawer("privacy")}
            >
              <ShieldCheck size={14} />
              Privacy & preferences
            </button>
          </section>
          <section className="panel spaced">
            <OwnLeadership/><h2>Application access roles</h2>
            {p?.roles?.length ? (
              p.roles.map((r: any, i: number) => (
                <div className="context-row" key={i}>
                  <span>{r.organization}</span>
                  <b>{r.role}</b>
                </div>
              ))
            ) : (
              <p>
                No organizational offices are currently linked to this identity.
              </p>
            )}
          </section>
          <section className="panel spaced">
            <h2>Your club administration</h2>
            {contact?.email ? (
              <a className="panel-link" href={"mailto:" + contact.email}>
                Contact club administration <ArrowUpRight size={14} />
              </a>
            ) : (
              <p>
                No club administration email is configured for this membership
                yet.
              </p>
            )}
            {contact?.phone && (
              <a href={"tel:" + contact.phone}>{contact.phone}</a>
            )}
            {w?.permissions.includes("directory.view") && (
              <Link className="panel-link spaced" to="/app/directory">
                Browse authorized directory →
              </Link>
            )}
          </section>
          <section className="panel spaced upcoming-card">
            <CalendarDays size={21} />
            <h3>More ways to connect</h3>
            <p>
              Club events, meetings and volunteering will arrive in later
              phases.
            </p>
            <span className="profile-chip">Upcoming capability</span>
          </section>
        </aside>
      </div>
      {d.contactRequests.length > 0 && (
        <section className="panel spaced">
          <h2>Contact requests</h2>
          {d.contactRequests.map((r: any) => (
            <ContactResponse key={r.id} request={r} onDone={refresh} />
          ))}
        </section>
      )}
      {drawer && (
        <MemberDrawer
          title={
            drawer === "profile"
              ? "Your personal profile"
              : drawer === "privacy"
                ? "Your privacy choices"
                : "Request a membership transfer"
          }
          onClose={() => setDrawer("")}
        >
          <ErrorBox error={error} />
          {drawer === "profile" ? (
            <PersonEditor
              person={p || { name: me.name }}
              busy={busy}
              onCancel={() => setDrawer("")}
              onSave={async (person: any) => {
                setBusy(true);
                try {
                  if (!p) {
                    const created = await api("/persons/me", "POST", {});
                    person.version = created.version;
                  }
                  await api("/persons/me", "PUT", person);
                  refresh();
                  setDrawer("");
                  setMessage("Your member profile was saved.");
                } catch (e) {
                  setError(e);
                } finally {
                  setBusy(false);
                }
              }}
            />
          ) : drawer === "privacy" ? (
            <PrivacyEditor
              privacy={p?.privacy}
              busy={busy}
              onSave={async (v: any) => {
                setBusy(true);
                try {
                  await api("/persons/me/privacy", "PUT", v);
                  refresh();
                  setDrawer("");
                  setMessage("Privacy choices updated.");
                } catch (e) {
                  setError(e);
                } finally {
                  setBusy(false);
                }
              }}
            />
          ) : (
            <TransferForm
              membership={m}
              onDone={() => {
                setDrawer("");
                refresh();
                setMessage(
                  "Transfer request submitted for the required reviews.",
                );
              }}
            />
          )}
        </MemberDrawer>
      )}
    </>
  );
}
function DigitalIdentity({ membership: m }: any) {
  const qc = useQueryClient();
  const [error, setError] = useState<unknown>();
  const cards = useQuery({
    queryKey: ["my-cards", m.id],
    queryFn: () => api("/members/" + m.id + "/cards"),
  });
  const valid =
      m.status === "Active" && (!m.endsAt || new Date(m.endsAt) > new Date()),
    card = cards.data?.find((c: any) => !c.revokedAt);
  return (
    <div className="digital-identity">
      <ErrorBox error={error} />
      {valid && card ? (
        <>
          <img
            className="digital-card-preview"
            src={"/api/v1/members/" + m.id + "/card?v=" + card.id}
            alt={"Digital membership card for " + m.name}
          />
          <div className="header-actions">
            <a
              className="button secondary"
              href={"/api/v1/members/" + m.id + "/card"}
              download
            >
              <Download size={14} />
              Download card
            </a>
            <button
              className="text-button"
              onClick={async () => {
                if (
                  !window.confirm(
                    "Revoke this digital card? The current verification QR will stop validating.",
                  )
                )
                  return;
                try {
                  await api(
                    "/membership/cards/" + card.id + "/revoke",
                    "POST",
                    {},
                  );
                  qc.invalidateQueries({ queryKey: ["my-cards", m.id] });
                } catch (e) {
                  setError(e);
                }
              }}
            >
              Revoke QR
            </button>
          </div>
        </>
      ) : (
        <div className="empty">
          <CreditCard size={38} />
          <h3>
            {valid
              ? "No current digital card"
              : "Membership is not currently valid"}
          </h3>
          <p>
            {valid
              ? "You can reissue your membership identity with a new opaque verification code."
              : "Your identity and membership history remain available. Contact your club about the current status."}
          </p>
          {valid && (
            <button
              className="secondary"
              onClick={async () => {
                try {
                  await api("/members/" + m.id + "/cards/reissue", "POST", {
                    version: m.version,
                  });
                  qc.invalidateQueries({ queryKey: ["my-cards", m.id] });
                } catch (e) {
                  setError(e);
                }
              }}
            >
              Reissue digital identity
            </button>
          )}
        </div>
      )}
    </div>
  );
}
function ContactResponse({ request: r, onDone }: any) {
  const [reply, setReply] = useState(""),
    [error, setError] = useState<unknown>();
  return (
    <div className="contact-response">
      <div>
        <b>{r.requester}</b>
        <p>{r.message}</p>
        <MemberStatus status={r.status} />
      </div>
      {r.status === "Pending" && (
        <form
          className="form-stack"
          onSubmit={async (e) => {
            e.preventDefault();
            try {
              await api(
                "/membership/contact-requests/" + r.id + "/respond",
                "POST",
                { status: "Accepted", reply },
              );
              onDone();
            } catch (e) {
              setError(e);
            }
          }}
        >
          <label>
            Your reply (share contact information only if you choose)
            <textarea
              required
              maxLength={1000}
              value={reply}
              onChange={(e) => setReply(e.target.value)}
            />
          </label>
          <ErrorBox error={error} />
          <div className="header-actions">
            <button>Accept & send reply</button>
            <button
              type="button"
              className="secondary"
              onClick={async () => {
                try {
                  await api(
                    "/membership/contact-requests/" + r.id + "/respond",
                    "POST",
                    { status: "Declined" },
                  );
                  onDone();
                } catch (e) {
                  setError(e);
                }
              }}
            >
              Decline
            </button>
          </div>
        </form>
      )}
    </div>
  );
}
