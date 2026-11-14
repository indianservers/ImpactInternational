import PageSections from '../PageSections';
import ModuleNavigation from "../ModuleNavigation";
import { useState, useEffect, useRef } from "react";
import { Link, Route, Routes, useParams } from "react-router-dom";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import {
  CreditCard,
  Plus,
  ArrowUpRight,
  Download,
  Users,
  ShieldCheck,
  Clock,
  RefreshCw,
  Search,
} from "lucide-react";
import { api } from "../api";
import { useWorkspace, ErrorBox, Skeleton } from "../App";
import CardDesigner from "./CardDesigner";
import "./IdCards.css";
type Row = Record<string, any>;
const base = "/id-cards",
  root = "/app/id-cards";
function useData(path: string, enabled = true) {
  const { w } = useWorkspace();
  return useQuery<any>({
    queryKey: ["id-cards", w?.id, path],
    queryFn: () => api(path),
    enabled,
    refetchInterval: path.includes("bulk-jobs") ? 1500 : false,
  });
}
function useAction() {
  const qc = useQueryClient();
  const [error, setError] = useState<unknown>(),
    [busy, setBusy] = useState(false);
  async function run(path: string, data: unknown = {}, method = "POST") {
    setBusy(true);
    setError(undefined);
    try {
      const r = await api(path, method, data);
      await qc.invalidateQueries({ queryKey: ["id-cards"] });
      return r;
    } catch (e) {
      setError(e);
      throw e;
    } finally {
      setBusy(false);
    }
  }
  return { run, error, busy };
}
function Header({ title, description, action }: any) {
  return (
    <header className="card-page-heading">
      <div>
        <span className="card-eyebrow">
          IMPACT INTERNATIONAL / IDENTITY STUDIO
        </span>
        <h1>{title}</h1>
        <p>{description}</p>
      </div>
      {action}
    </header>
  );
}
function Status({ children }: any) {
  return (
    <span
      className={
        "card-badge " + String(children).toLowerCase().replaceAll(" ", "-")
      }
    >
      {children}
    </span>
  );
}
function Panel({ title, children }: any) {
  return (
    <section className="card-panel">
      <h2>{title}</h2>
      {children}
    </section>
  );
}
function Modal({ title, close, children }: any) {
  const dialog=useRef<HTMLElement>(null);
  const closeRef=useRef(close);
  useEffect(()=>{closeRef.current=close;},[close]);
  useEffect(()=>{const previous=document.activeElement as HTMLElement|null;dialog.current?.focus();return ()=>previous?.focus();},[]);
  return (
    <div className="card-modal-backdrop">
      <section
        ref={dialog}
        tabIndex={-1}
        onKeyDown={e=>{
          if(e.key==='Escape'){e.stopPropagation();closeRef.current();}
          if(e.key==='Tab'){
            const nodes=Array.from(dialog.current?.querySelectorAll<HTMLElement>('button:not(:disabled),a[href],input:not(:disabled),textarea:not(:disabled),select:not(:disabled),[tabindex="0"]')||[]).filter(n=>n.getClientRects().length);
            const first=nodes[0],last=nodes[nodes.length-1];
            if(!first){e.preventDefault();return;}
            if(e.shiftKey&&(document.activeElement===first||document.activeElement===dialog.current)){e.preventDefault();last.focus();}
            else if(!e.shiftKey&&(document.activeElement===last||document.activeElement===dialog.current)){e.preventDefault();first.focus();}
          }
        }}
        role="dialog"
        aria-modal="true"
        aria-label={title}
        className="card-modal"
      >
        <div className="card-section-title">
          <h2>{title}</h2>
          <button onClick={close}>Close</button>
        </div>
        {children}
      </section>
    </div>
  );
}
function Field({ label, children }: any) {
  return (
    <label className="card-field">
      <span>{label}</span>
      {children}
    </label>
  );
}
function Empty({ children }: any) {
  return (
    <div className="card-empty">
      <CreditCard size={30} />
      <h3>Your next chapter starts here.</h3>
      <p>{children || "No records in your authorized scope."}</p>
    </div>
  );
}
function TemplateThumb({ template }: any) {
  let d;
  try {
    d = JSON.parse(template.layout);
  } catch {}
  if (!d?.front) return null;
  return (
    <div className="card-template-thumb actual">
      <svg
        aria-label={template.name + " design thumbnail"}
        viewBox={"0 0 " + d.width + " " + d.height}
        style={{ fontFamily: "Noto Sans" }}
      >
        <rect
          width={d.width}
          height={d.height}
          rx="1"
          fill={d.front.background}
        />
        {d.front.elements.map((e: Row) => (
          <g
            key={e.id}
            transform={
              "translate(" + e.x + "," + e.y + ") rotate(" + e.rotation + ")"
            }
            opacity={e.opacity}
          >
            {e.kind === "Text" ? (
              <text
                x={
                  e.align === "Center"
                    ? e.width / 2
                    : e.align === "Right"
                      ? e.width
                      : 0
                }
                y={e.fontSize}
                textAnchor={
                  e.align === "Center"
                    ? "middle"
                    : e.align === "Right"
                      ? "end"
                      : "start"
                }
                fontSize={e.fontSize}
                fill={e.color}
                fontWeight={e.bold ? 700 : 400}
              >
                {e.binding === "member.full_name"
                  ? "Member name"
                  : e.binding === "member.member_id"
                    ? "MEMBERSHIP ID"
                    : e.binding === "member.club_name"
                      ? "Your community"
                      : e.binding === "association.name"
                        ? "Impact International"
                        : e.binding || e.text}
              </text>
            ) : e.kind === "Rect" ? (
              <rect
                width={e.width}
                height={e.height}
                rx={e.radius}
                fill={e.fill}
                stroke={e.color}
                strokeWidth={e.border}
              />
            ) : e.kind === "Ellipse" ? (
              <ellipse
                cx={e.width / 2}
                cy={e.height / 2}
                rx={e.width / 2}
                ry={e.height / 2}
                fill={e.fill}
                stroke={e.color}
                strokeWidth={e.border}
              />
            ) : e.kind === "Line" ? (
              <line
                x1="0"
                y1="0"
                x2={e.width}
                y2={e.height}
                stroke={e.color}
                strokeWidth={e.border || 0.2}
              />
            ) : e.kind === "Qr" ? (
              <image
                href="/api/v1/id-cards/qr-preview"
                width={e.width}
                height={e.height}
              />
            ) : e.asset ? (
              <image
                href={"/api/v1/documents/" + e.asset + "/download?preview=true"}
                width={e.width}
                height={e.height}
                preserveAspectRatio="xMidYMid slice"
              />
            ) : (
              <rect
                width={e.width}
                height={e.height}
                rx={e.radius}
                fill="#dbe5dc"
              />
            )}
          </g>
        ))}
      </svg>
    </div>
  );
}

export default function IdCards() {
  const { w } = useWorkspace();
  return (
    <div className="id-cards">
      <ModuleNavigation root={root} label="ID cards" items={[["", "Overview", "members.cards"], ["templates", "Templates", "cards.design"], ["issuances", "Issued cards", "cards.issue"], ["bulk", "Bulk generation", "cards.bulk"], ["print", "Print studio", "cards.design"], ["settings", "Settings", "cards.settings"]]} groups={{"Cards": ["", "issuances", "bulk"], "Design & print": ["templates", "print"], "Settings": ["settings"]}} permissions={w?.permissions || []} />
      <Routes>
        <Route index element={<Dashboard />} />
        <Route path="templates" element={<Templates />} />
        <Route path="designer/:id" element={<CardDesigner />} />
        <Route path="issuances" element={<Issuances />} />
        <Route path="bulk" element={<Bulk />} />
        <Route path="print" element={<PrintStudio />} />
        <Route path="settings" element={<Settings />} />
      </Routes>
    </div>
  );
}
function Dashboard() {
  const q = useData(base + "/analytics"),
    cards = useData(base + "/issuances");
  return (
    <>
      <Header
        title="Impact ID Card Studio"
        description="Design, review and deliver membership cards across your authorized community."
        action={
          <Link className="card-primary" to={root + "/templates"}>
            <Plus size={17} /> Create a card
          </Link>
        }
      />
      <PageSections id="card-studio" perPage={1} labels={["Overview & Metrics","Workspace & Activity"]}><div><div className="card-dashboard-hero">
        <div>
          <span className="card-eyebrow">
            ONE IDENTITY. MANY POSSIBILITIES.
          </span>
          <h2>
            A card that belongs
            <br />
            to your community.
          </h2>
          <p>
            Thoughtful design. Verified membership.
            <br />
            Secure from your first draft to every print.
          </p>
          <Link to={root + "/templates"}>
            Explore the template library <ArrowUpRight size={16} />
          </Link>
        </div>
        <div className="card-hero-sample">
          <span>IMPACT INTERNATIONAL</span>
          <div className="card-hero-body">
            <div>
              <Users size={32} />
            </div>
            <section>
              <small>YOUR MEMBERSHIP</small>
              <strong>Connected by purpose</strong>
              <span>A WORLD OF SHARED IMPACT</span>
            </section>
          </div>
          <footer>
            <span>MEMBER IDENTITY</span>
            <CreditCard size={22} />
          </footer>
        </div>
      </div>
      <ErrorBox error={q.error} />
      <div className="card-metrics">
        {[
          ["eligible", "Eligible members"],
          ["issued", "Official cards issued"],
          ["pending", "Awaiting completion"],
          ["expiring", "Expiring in 30 days"],
          ["expired", "Expired cards"],
          ["revoked", "Revoked / replaced"],
          ["missingPhotos", "Missing photographs"],
          ["failedJobs", "Failed generation jobs"],
        ].map(([key, label]) => (
          <section key={key}>
            <span>{label}</span>
            <strong>
              {q.isPending ? "—" : Number(q.data?.[key] || 0).toLocaleString()}
            </strong>
          </section>
        ))}
      </div>
      </div><div className="card-two">
        <Panel title="Your identity workspace">
          {[
            [
              "templates",
              "Design a reusable template",
              "Front and back · millimetre precision",
            ],
            [
              "issuances",
              "Issue an official card",
              "Independent review · immutable snapshots",
            ],
            [
              "bulk",
              "Generate for your community",
              "Durable jobs · private downloads",
            ],
            [
              "print",
              "Prepare the perfect print",
              "Sheet layouts · duplex alignment",
            ],
          ].map(([path, title, desc]) => (
            <Link
              className="card-quick-action"
              to={root + "/" + path}
              key={path}
            >
              <span>
                <strong>{title}</strong>
                <small>{desc}</small>
              </span>
              <ArrowUpRight size={17} />
            </Link>
          ))}
        </Panel>
        <Panel title="Recent card activity">
          {cards.data?.length ? (
            <div className="card-recent-list">
              {cards.data
                .slice(-6)
                .reverse()
                .map((c: Row) => (
                  <div key={c.id}>
                    <span>
                      <strong>{c.name}</strong>
                      <small>
                        {c.serial || "Original digital membership ID"}
                      </small>
                    </span>
                    <Status>{c.state}</Status>
                  </div>
                ))}
            </div>
          ) : (
            <Empty>
              Official card activity will appear after your first reviewed
              issuance.
            </Empty>
          )}
        </Panel>
      </div>
</PageSections>    </>
  );
}
function Templates() {
  const { w } = useWorkspace(),
    q = useData(base + "/templates"),
    options = useData(base + "/options"),
    a = useAction();
  const [create, setCreate] = useState(false),
    [name, setName] = useState("Standard Member ID"),
    [org, setOrg] = useState(""),
    [search, setSearch] = useState(""),
    [parent, setParent] = useState("");
  return (
    <>
      <Header
        title="A collection with character."
        description="Reusable designs, independently published versions and a shared visual language."
        action={
          <button className="card-primary" onClick={() => setCreate(true)}>
            <Plus size={17} /> New template
          </button>
        }
      />
      <div className="card-library-toolbar">
        <Search size={18} />
        <input
          aria-label="Search templates"
          placeholder="Find a template…"
          value={search}
          onChange={(e) => setSearch(e.target.value)}
        />
        <span>CR80 · custom sizes · front & back</span>
      </div>
      <ErrorBox error={q.error ?? a.error} />
      {q.isPending ? (
        <Skeleton />
      ) : (
        <div className="card-gallery">
          {q.data
            ?.filter((t: Row) =>
              t.name.toLowerCase().includes(search.toLowerCase()),
            )
            .map((t: Row) => (
              <article key={t.id} className="card-template-tile">
                <Link to={root + "/designer/" + t.id}>
                  <TemplateThumb template={t} />
                </Link>
                <div>
                  <div className="card-section-title">
                    <h3>{t.name}</h3>
                    <Status>
                      {t.active
                        ? t.published
                          ? "Published v" + t.published.number
                          : "Draft"
                        : "Archived"}
                    </Status>
                  </div>
                  <p>
                    Draft revision {t.version} ·{" "}
                    {JSON.parse(t.layout).doubleSided
                      ? "Two sides"
                      : "Single side"}
                  </p>
                  <div className="card-actions">
                    <Link to={root + "/designer/" + t.id}>
                      Open designer <ArrowUpRight size={14} />
                    </Link>
                    <button
                      onClick={() =>
                        a
                          .run(base + "/templates/" + t.id + "/duplicate")
                          .catch(() => {})
                      }
                    >
                      Duplicate
                    </button>
                    {w?.permissions.includes("cards.publish") && t.active && (
                      <button
                        onClick={() =>
                          a
                            .run(base + "/templates/" + t.id + "/archive")
                            .catch(() => {})
                        }
                      >
                        Archive
                      </button>
                    )}
                  </div>
                </div>
              </article>
            ))}
          <button className="card-new-template" onClick={() => setCreate(true)}>
            <Plus size={28} />
            <strong>Create something yours</strong>
            <span>Start with a flexible CR80 design</span>
          </button>
        </div>
      )}
      {create && (
        <Modal title="Create a card template" close={() => setCreate(false)}>
          <form
            className="card-form"
            onSubmit={async (e) => {
              e.preventDefault();
              try {
                const t = await a.run(base + "/templates", {
                  orgId: org || w?.orgId,
                  name,
                  design: parent
                    ? {
                        ...(await api(base + "/versions/" + parent)).design,
                        parentVersionId: parent,
                      }
                    : options.data.design,
                });
                window.location.assign(root + "/designer/" + t.id);
              } catch {}
            }}
          >
            <Field label="Template name">
              <input
                value={name}
                required
                maxLength={120}
                onChange={(e) => setName(e.target.value)}
                list="card-template-ideas"
              />
              <datalist id="card-template-ideas">
                {[
                  "Standard Club Member ID",
                  "Club President ID",
                  "Secretary ID",
                  "Treasurer ID",
                  "District Officer ID",
                  "National Officer ID",
                  "International Officer ID",
                  "Volunteer ID",
                  "Event Staff ID",
                  "Custom Member ID",
                ].map((n) => (
                  <option key={n}>{n}</option>
                ))}
              </datalist>
            </Field>
            <Field label="Owning organization">
              <select
                value={org || w?.orgId}
                onChange={(e) => setOrg(e.target.value)}
              >
                {options.data?.organizations.map((o: Row) => (
                  <option value={o.id} key={o.id}>
                    {o.name}
                  </option>
                ))}
              </select>
            </Field>
            <Field label="Start from published design">
              <select
                value={parent}
                onChange={(e) => setParent(e.target.value)}
              >
                <option value="">Standard CR80 starter</option>
                {q.data
                  ?.filter((t: Row) => t.active && t.published)
                  .map((t: Row) => (
                    <option key={t.id} value={t.published.id}>
                      {t.name} · v{t.published.number}
                    </option>
                  ))}
              </select>
            </Field>
            <p className="card-muted">
              Categories, leadership assignments, locked branding and physical
              dimensions can be set in the designer.
            </p>
            <ErrorBox error={a.error} />
            <button className="card-primary" disabled={a.busy || !options.data}>
              Create & design
            </button>
          </form>
        </Modal>
      )}
    </>
  );
}
function Issuances() {
  const q = useData(base + "/issuances"),
    a = useAction();
  const [create, setCreate] = useState(false),
    [selected, setSelected] = useState(""),
    [query, setQuery] = useState("");
  return (
    <>
      <Header
        title="Membership made official."
        description="Reviewed issuance, protected downloads and a traceable history for every card."
        action={
          <button className="card-primary" onClick={() => setCreate(true)}>
            <Plus size={17} /> Issue a card
          </button>
        }
      />
      <input
        className="card-search"
        placeholder="Search this page by member or serial…"
        value={query}
        onChange={(e) => setQuery(e.target.value)}
      />
      <ErrorBox error={q.error ?? a.error} />
      <div className="card-table-wrap">
        <table>
          <thead>
            <tr>
              <th>Member</th>
              <th>Card reference</th>
              <th>Organization</th>
              <th>State</th>
              <th>Valid until</th>
              <th />
            </tr>
          </thead>
          <tbody>
            {q.data
              ?.filter((c: Row) =>
                (c.name + " " + c.serial)
                  .toLowerCase()
                  .includes(query.toLowerCase()),
              )
              .map((c: Row) => (
                <tr key={c.id}>
                  <td>{c.name}</td>
                  <td>{c.serial || "Legacy digital ID"}</td>
                  <td>{c.club}</td>
                  <td>
                    <Status>
                      {c.revokedAt
                        ? "Revoked"
                        : c.expiryAt && new Date(c.expiryAt) < new Date()
                          ? "Expired"
                          : c.state}
                    </Status>
                  </td>
                  <td>{c.expiryAt?.slice(0, 10) || "Membership validity"}</td>
                  <td>
                    <button onClick={() => setSelected(c.id)}>
                      Open <ArrowUpRight size={14} />
                    </button>
                  </td>
                </tr>
              ))}
          </tbody>
        </table>
      </div>
      <p className="card-muted">
        First 100 records, ordered by ID. Select a member to access their
        issuance history.
      </p>
      {create && (
        <IssueForm
          close={() => setCreate(false)}
          onCreated={(id) => {
            setCreate(false);
            setSelected(id);
          }}
        />
      )}
      {selected && <CardDetails id={selected} close={() => setSelected("")} />}
    </>
  );
}
function IssueForm({
  close,
  onCreated,
}: {
  close: () => void;
  onCreated: (id: string) => void;
}) {
  const q = useData(base + "/templates"),
    a = useAction();
  const [query, setQuery] = useState(""),
    [member, setMember] = useState(""),
    [template, setTemplate] = useState(""),
    [replace, setReplace] = useState(false),
    [reason, setReason] = useState(""),
    [expiry, setExpiry] = useState("");
  const people = useData(base + "/members?search=" + encodeURIComponent(query)),
    m = people.data?.items.find((x: Row) => x.id === member);
  return (
    <Modal title="Prepare a membership card" close={close}>
      <form
        className="card-form"
        onSubmit={async (e) => {
          e.preventDefault();
          try {
            const c = await a.run(base + "/issuances", {
              membershipId: member,
              templateVersionId: template,
              expiryAt: expiry
                ? new Date(expiry + "T23:59:59Z").toISOString()
                : null,
              replacesId: replace ? m?.currentCard?.id : null,
              reason,
            });
            onCreated(c.id);
          } catch {}
        }}
      >
        <Field label="Find a member">
          <input
            placeholder="Name or member ID"
            value={query}
            onChange={(e) => setQuery(e.target.value)}
          />
        </Field>
        <Field label="Authorized membership">
          <select
            required
            value={member}
            onChange={(e) => setMember(e.target.value)}
          >
            <option value="">Select membership</option>
            {people.data?.items.map((p: Row) => (
              <option key={p.id} value={p.id}>
                {p.name} · {p.number} · {p.club}
              </option>
            ))}
          </select>
        </Field>
        {m && (
          <div className="card-notice">
            {m.status} ·{" "}
            {m.hasPhoto
              ? "Photograph available"
              : "No photograph — required-photo templates will block issuance"}
            {m.currentCard && (
              <label>
                <input
                  type="checkbox"
                  checked={replace}
                  onChange={(e) => setReplace(e.target.checked)}
                />{" "}
                Explicitly replace current card {m.currentCard.state}
              </label>
            )}
          </div>
        )}
        <Field label="Published template">
          <select
            required
            value={template}
            onChange={(e) => setTemplate(e.target.value)}
          >
            <option value="">Select published version</option>
            {q.data
              ?.filter((t: Row) => t.active && t.published)
              .map((t: Row) => (
                <option key={t.id} value={t.published.id}>
                  {t.name} · v{t.published.number}
                </option>
              ))}
          </select>
        </Field>
        <Field label="Expiry date (optional)">
          <input
            type="date"
            value={expiry}
            onChange={(e) => setExpiry(e.target.value)}
          />
        </Field>
        <Field label="Reason / correction details">
          <textarea
            value={reason}
            maxLength={500}
            onChange={(e) => setReason(e.target.value)}
          />
        </Field>
        <ErrorBox error={a.error} />
        <p className="card-muted">
          This creates a frozen draft. Independent approval, generation and
          issuance follow. It does not automatically replace the current card.
        </p>
        <button
          className="card-primary"
          disabled={a.busy || !member || !template}
        >
          Prepare draft
        </button>
      </form>
    </Modal>
  );
}
function CardDetails({ id, close }: { id: string; close: () => void }) {
  const q = useData(base + "/issuances/" + id),
    a = useAction();
  const [back, setBack] = useState(false),
    [reason, setReason] = useState("");
  const c = q.data?.card;
  return (
    <Modal title="Membership ID history" close={close}>
      {c ? (
        <>
          <div className="card-section-title">
            <strong>{c.serial || "Legacy digital membership ID"}</strong>
            <Status>{q.data.status}</Status>
          </div>
          {c.templateVersionId && (
            <img
              className="card-rendered-preview"
              src={"/api/v1/id-cards/issuances/" + id + "/preview?back=" + back}
              alt={
                back ? "Back of membership card" : "Front of membership card"
              }
            />
          )}
          <div className="card-actions">
            <button onClick={() => setBack(!back)}>Flip front / back</button>
            {c.state === "Draft" && (
              <button
                className="card-primary"
                onClick={() =>
                  a.run(base + "/issuances/" + id + "/submit").catch(() => {})
                }
              >
                Request independent approval
              </button>
            )}
            {c.state === "Approved" && (
              <button
                className="card-primary"
                onClick={() =>
                  a.run(base + "/issuances/" + id + "/generate").catch(() => {})
                }
              >
                Generate official files
              </button>
            )}
            {c.state === "Generated" && (
              <button
                className="card-primary"
                onClick={() =>
                  a.run(base + "/issuances/" + id + "/issue").catch(() => {})
                }
              >
                Issue & notify member
              </button>
            )}
            {c.requestId && (
              <Link to={"/app/governance?request=" + c.requestId}>
                Open approval record
              </Link>
            )}
            {["Issued", "Replaced", "Revoked"].includes(c.state) &&
              ["pdf", "front", "back"].map((format) => (
                <a
                  key={format}
                  href={
                    "/api/v1/id-cards/issuances/" +
                    id +
                    "/download?format=" +
                    format
                  }
                >
                  <Download size={14} />
                  {format}
                </a>
              ))}
          </div>
          <ErrorBox error={a.error} />
          <Panel title="Source snapshot">
            <dl className="card-snapshot">
              {Object.entries(q.data.values || {})
                .filter(([k]) => k !== "card.qr_code")
                .map(([k, v]) => (
                  <div key={k}>
                    <dt>{k}</dt>
                    <dd>{String(v)}</dd>
                  </div>
                ))}
            </dl>
          </Panel>
          <Panel title="Audit trail">
            {q.data.history.map((h: Row, n: number) => (
              <div className="card-history-line" key={n}>
                <Clock size={14} />
                <span>
                  <strong>{h.action}</strong>
                  <small>
                    {new Date(h.at).toLocaleString()} · {h.changes}
                  </small>
                </span>
              </div>
            ))}
          </Panel>
          {["Issued", "Generated", "Approved"].includes(c.state) && (
            <div className="card-form">
              <Field label="Revocation reason">
                <textarea
                  value={reason}
                  onChange={(e) => setReason(e.target.value)}
                />
              </Field>
              <button
                disabled={reason.length < 5}
                onClick={() =>
                  a
                    .run(base + "/issuances/" + id + "/revoke", {
                      reason,
                      version: c.version,
                    })
                    .catch(() => {})
                }
              >
                Revoke card
              </button>
              <p className="card-muted">
                To reissue, prepare a new card for this member and explicitly
                select the current card to replace. Every replacement is
                reviewed.
              </p>
            </div>
          )}
        </>
      ) : (
        <Skeleton />
      )}
    </Modal>
  );
}
function Bulk() {
  const q = useData(base + "/bulk-jobs"),
    templates = useData(base + "/templates"),
    options = useData(base + "/options"),
    a = useAction(),
    { w } = useWorkspace();
  const [org, setOrg] = useState(""),
    [category, setCategory] = useState(""),
    [template, setTemplate] = useState(""),
    [format, setFormat] = useState("Zip"),
    [reissue, setReissue] = useState(false),
    [reuseIssued, setReuseIssued] = useState(false),
    [filters, setFilters] = useState({search:"",country:"",positionId:"",memberStatus:"",cardStatus:"",expiryFrom:"",expiryTo:""}),
    [memberIds, setMemberIds] = useState(""),
    [preview, setPreview] = useState<Row>(),
    [selected, setSelected] = useState(""),
    [from, setFrom] = useState(""),
    [to, setTo] = useState("");
  const presets = useData(base + "/print-presets"),
    [preset, setPreset] = useState("");
  const selection = {
    orgId: org || w?.orgId,
    categoryId: category || null,
    ...filters,
    positionId: filters.positionId || null,
    expiryFrom: filters.expiryFrom ? new Date(filters.expiryFrom).toISOString() : null,
    expiryTo: filters.expiryTo ? new Date(filters.expiryTo+"T23:59:59Z").toISOString() : null,
    membershipIds: memberIds.trim() ? memberIds.trim().split(/[\s,;]+/) : null,
    reissue: reuseIssued ? false : reissue,
    joinedFrom: from ? new Date(from).toISOString() : null,
    joinedTo: to ? new Date(to + "T23:59:59Z").toISOString() : null,
  };
  return (
    <>
      <Header
        title="A thousand identities. One workflow."
        description="Freeze an authorized selection, approve the job, and let bounded background processing handle the rest."
      />
      <div className="card-two">
        <Panel title="1 · Select your community">
          <label><input type="checkbox" checked={reuseIssued} onChange={e=>{setReuseIssued(e.target.checked);setTemplate("");setPreview(undefined);}} /> Export existing issued cards without reissuing</label>
          <div className="card-form">
            <Field label="Organization and its subtree">
              <select
                value={org || w?.orgId}
                onChange={(e) => {
                  setOrg(e.target.value);
                  setPreview(undefined);
                }}
              >
                {options.data?.organizations.map((o: Row) => (
                  <option value={o.id} key={o.id}>
                    {o.name}
                  </option>
                ))}
              </select>
            </Field>
            {([['search','Name or member number'],['country','Country'],['expiryFrom','Card expiry from'],['expiryTo','Card expiry through']] as const).map(([key,label])=><Field key={key} label={label}><input type={key.startsWith('expiry')?'date':'text'} maxLength={100} value={filters[key]} onChange={e=>{setFilters({...filters,[key]:e.target.value});setPreview(undefined);}} /></Field>)}
            <Field label="Leadership position"><select value={filters.positionId} onChange={e=>{setFilters({...filters,positionId:e.target.value});setPreview(undefined);}}><option value="">All positions</option>{options.data?.positions.map((p:Row)=><option key={p.id} value={p.id}>{p.name}</option>)}</select></Field>
            {([['memberStatus','Membership status',['Active','Suspended','Inactive','Resigned','Terminated','Expired','Deceased']],['cardStatus','Card status',['None','Issued','Expired','Revoked','Pending']]] as const).map(([key,label,values])=><Field key={key} label={label}><select value={filters[key]} onChange={e=>{setFilters({...filters,[key]:e.target.value});setPreview(undefined);}}><option value="">All statuses</option>{values.map(v=><option key={v}>{v}</option>)}</select></Field>)}
            <Field label="Specific membership IDs (optional)"><textarea value={memberIds} onChange={e=>{setMemberIds(e.target.value);setPreview(undefined);}} placeholder="Paste membership UUIDs separated by commas or lines" /></Field>
            <Field label="Membership category">
              <select
                value={category}
                onChange={(e) => {
                  setCategory(e.target.value);
                  setPreview(undefined);
                }}
              >
                <option value="">All categories</option>
                {options.data?.categories.map((c: Row) => (
                  <option key={c.id} value={c.id}>
                    {c.name}
                  </option>
                ))}
              </select>
            </Field>
            <div className="card-field-pair">
              <Field label="Joined from">
                <input
                  type="date"
                  value={from}
                  onChange={(e) => {
                    setFrom(e.target.value);
                    setPreview(undefined);
                  }}
                />
              </Field>
              <Field label="Joined through">
                <input
                  type="date"
                  value={to}
                  onChange={(e) => {
                    setTo(e.target.value);
                    setPreview(undefined);
                  }}
                />
              </Field>
            </div>
            <label>
              <input
                type="checkbox"
                checked={reissue}
                disabled={reuseIssued}
                onChange={(e) => {
                  setReissue(e.target.checked);
                  setPreview(undefined);
                }}
              />{" "}
              Explicitly replace existing valid cards
            </label>
            <button
              onClick={async () => {
                try {
                  setPreview(await a.run(base + "/bulk-preview", selection));
                } catch {}
              }}
            >
              Preview eligibility
            </button>
            {preview && (
              <div className="card-eligibility">
                {[
                  ["total", "Selected records"],
                  ["eligible", "Active, unexpired"],
                  ["missingPhotos", "Missing photos"],
                  ["missingIds", "Missing IDs"],
                  ["alreadyIssued", "Existing official / digital cards"],
                  ["expired", "Ineligible memberships"],
                ].map(([k, label]) => (
                  <div key={k}>
                    <span>{label}</span>
                    <strong>{preview[k]}</strong>
                  </div>
                ))}
              </div>
            )}
          </div>
        </Panel>
        <Panel title="2 · Review output and submit">
          <div className="card-form">
            <Field label="Immutable template version">
              <select
                value={template}
                onChange={(e) => setTemplate(e.target.value)}
              >
                <option value="">Select published template</option>
                {templates.data
                  ?.filter((t: Row) => (reuseIssued || t.active) && t.published)
                  .flatMap((t: Row) => (reuseIssued ? t.versions || [t.published] : [t.published]).map((v:Row)=>(
                    <option value={v.id} key={v.id}>{t.name} · v{v.number}</option>
                  )))}
              </select>
            </Field>
            <Field label="Output format">
              <select
                value={format}
                onChange={(e) => setFormat(e.target.value)}
              >
                {[
                  ["Zip", "Individual PDFs + front/back PNGs in ZIP"],
                  ["Pdf", "Combined card PDF (up to 1,000)"],
                  ["Print", "Print-ready sheet PDF (up to 1,000)"],
                  ["FrontImages", "Front images in ZIP"],
                  ["BackImages", "Back images in ZIP"],
                  ["Pairs", "Front/back image pairs in ZIP"],
                ].map(([k, label]) => (
                  <option key={k} value={k}>
                    {label}
                  </option>
                ))}
              </select>
            </Field>
            {format === "Print" && (
              <Field label="Saved print preset">
                <select
                  value={preset}
                  onChange={(e) => setPreset(e.target.value)}
                >
                  <option value="">A4 · 10 mm margin · long-edge duplex</option>
                  {presets.data?.map((p: Row) => (
                    <option value={p.id} key={p.id}>
                      {p.name}
                    </option>
                  ))}
                </select>
              </Field>
            )}
            <div className="card-notice">
              <ShieldCheck size={20} />
              <span>
                Official bulk issuance requires independent approval. A
                selection snapshot is preserved; each item is revalidated when
                processed. Downloads expire after seven days and require current
                export authority.
              </span>
            </div>
            <button
              className="card-primary"
              disabled={!preview || !template || a.busy}
              onClick={async () => {
                try {
                  const job = await a.run(base + "/bulk-jobs", {
                    templateVersionId: template,
                    reuseIssued,
                    selection,
                    format,
                    print: preset
                      ? JSON.parse(
                          presets.data.find((p: Row) => p.id === preset).layout,
                        )
                      : null,
                  });
                  setSelected(job.job.id);
                } catch {}
              }}
            >
              Submit generation job for review
            </button>
            <p className="card-muted">
              Up to 10,000 memberships per ZIP/image job. Work happens outside
              the HTTP request; missing required data fails that item and
              appears in its report.
            </p>
          </div>
        </Panel>
      </div>
      <ErrorBox error={a.error ?? q.error} />
      <Panel title="Generation jobs">
        <div className="card-table-wrap">
          <table>
            <thead>
              <tr>
                <th>Created</th>
                <th>Output</th>
                <th>Status</th>
                <th>Progress</th>
                <th>Failed</th>
                <th />
              </tr>
            </thead>
            <tbody>
              {q.data?.map((j: Row) => (
                <tr key={j.id}>
                  <td>{new Date(j.createdAt).toLocaleString()}</td>
                  <td>{j.format}</td>
                  <td>
                    <Status>{j.status}</Status>
                  </td>
                  <td>
                    <progress value={j.success + j.failed} max={j.total} />
                    <small>
                      {j.success + j.failed} / {j.total}
                    </small>
                  </td>
                  <td>{j.failed}</td>
                  <td>
                    <button onClick={() => setSelected(j.id)}>View job</button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </Panel>
      {selected && <JobDetails id={selected} close={() => setSelected("")} />}
    </>
  );
}
function JobDetails({ id, close }: any) {
  const q = useData(base + "/bulk-jobs/" + id),
    a = useAction();
  const j = q.data?.job;
  return (
    <Modal title="Generation progress" close={close}>
      {j ? (
        <>
          <Status>{j.status}</Status>
          <h3>
            {j.success} successful · {j.failed} failed · {j.total} selected
          </h3>
          <progress
            className="card-job-progress"
            value={j.success + j.failed}
            max={j.total}
          />
          <div className="card-actions">
            {j.requestId && (
              <Link to={"/app/governance?request=" + j.requestId}>
                Review approval request <ArrowUpRight size={14} />
              </Link>
            )}
            {["Queued", "Running", "Pending Approval"].includes(j.status) && (
              <button
                onClick={() =>
                  a.run(base + "/bulk-jobs/" + id + "/cancel").catch(() => {})
                }
              >
                Cancel pending work
              </button>
            )}
            {["Failed", "Completed with errors"].includes(j.status) && (
              <button
                onClick={() =>
                  a.run(base + "/bulk-jobs/" + id + "/retry").catch(() => {})
                }
              >
                <RefreshCw size={14} /> Retry failed items
              </button>
            )}
            {j.outputKey && (
              <a
                className="card-primary"
                href={"/api/v1/id-cards/bulk-jobs/" + id + "/download"}
              >
                <Download size={16} /> Download private output
              </a>
            )}
          </div>
          <p className="card-muted">
            Download expiry: {new Date(j.expiresAt).toLocaleString()}. Scope,
            membership and card validity are rechecked at download time.
          </p>
          {j.error && <p className="card-notice">{j.error}</p>}
          <ErrorBox error={a.error} />
          <div className="card-table-wrap">
            <table>
              <thead>
                <tr>
                  <th>Item</th>
                  <th>Status</th>
                  <th>Result</th>
                </tr>
              </thead>
              <tbody>
                {q.data.items.map((i: Row) => (
                  <tr key={i.id}>
                    <td>{i.ordinal + 1}</td>
                    <td>{i.status}</td>
                    <td>{i.error || i.cardId || "Awaiting generation"}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          <p className="card-muted">
            First 100 items. The downloaded report includes every item.
          </p>
        </>
      ) : (
        <Skeleton />
      )}
    </Modal>
  );
}
function PrintStudio() {
  const { w } = useWorkspace(),
    a = useAction(),
    presets = useData(base + "/print-presets");
  const [layout, setLayout] = useState<Row>({
      sheetWidth: 210,
      sheetHeight: 297,
      margin: 10,
      gapX: 4,
      gapY: 4,
      sides: "Duplex",
      flip: "Long",
      offsetX: 0,
      offsetY: 0,
      cropMarks: true,
      includeBleed: false,
    }),
    [preview, setPreview] = useState<Row>(),
    [name, setName] = useState("A4 membership cards"),
    [back, setBack] = useState(false);
  function field(k: string, v: any) {
    setLayout((l) => ({ ...l, [k]: v }));
    setPreview(undefined);
  }
  return (
    <>
      <Header
        title="Made for the perfect print."
        description="Physical sheet geometry, deterministic ordering and mirrored duplex registration."
      />
      <div className="card-print-layout">
        <Panel title="Sheet & calibration">
          <div className="card-form">
            <Field label="Preset">
              <select
                onChange={(e) => {
                  const p = presets.data?.find(
                    (p: Row) => p.id === e.target.value,
                  );
                  if (p) {
                    setLayout(JSON.parse(p.layout));
                    setName(p.name);
                    setPreview(undefined);
                  }
                }}
              >
                <option value="">Custom / A4 default</option>
                {presets.data?.map((p: Row) => (
                  <option key={p.id} value={p.id}>
                    {p.name}
                  </option>
                ))}
              </select>
            </Field>
            <Field label="Sheet format">
              <select
                onChange={(e) => {
                  const [x, y] = e.target.value.split(",").map(Number);
                  setLayout((l) => ({ ...l, sheetWidth: x, sheetHeight: y }));
                  setPreview(undefined);
                }}
              >
                <option value="210,297">A4 portrait</option>
                <option value="297,210">A4 landscape</option>
                <option value="297,420">A3 portrait</option>
                <option value="420,297">A3 landscape</option>
                <option value="85.6,53.98">Single CR80 card</option>
              </select>
            </Field>
            <div className="card-field-pair">
              {[
                "sheetWidth",
                "sheetHeight",
                "margin",
                "gapX",
                "gapY",
                "offsetX",
                "offsetY",
              ].map((k) => (
                <Field key={k} label={k + " (mm)"}>
                  <input
                    type="number"
                    step=".1"
                    value={layout[k]}
                    onChange={(e) => field(k, +e.target.value)}
                  />
                </Field>
              ))}
            </div>
            <Field label="Sides">
              <select
                value={layout.sides}
                onChange={(e) => field("sides", e.target.value)}
              >
                {["Front", "Back", "Both", "Duplex"].map((v) => (
                  <option key={v}>{v}</option>
                ))}
              </select>
            </Field>
            <Field label="Duplex flip">
              <select
                value={layout.flip}
                onChange={(e) => field("flip", e.target.value)}
              >
                <option value="Long">Long edge</option>
                <option value="Short">Short edge</option>
              </select>
            </Field>
            <label>
              <input
                type="checkbox"
                checked={layout.cropMarks}
                onChange={(e) => field("cropMarks", e.target.checked)}
              />{" "}
              Crop marks
            </label>
            <label>
              <input
                type="checkbox"
                checked={layout.includeBleed}
                onChange={(e) => field("includeBleed", e.target.checked)}
              />{" "}
              Include bleed in slot spacing
            </label>
            <button
              className="card-primary"
              onClick={async () => {
                try {
                  setPreview(
                    await a.run(base + "/print-preview", {
                      orgId: w?.orgId,
                      name,
                      layout,
                    }),
                  );
                } catch {}
              }}
            >
              Calculate print preview
            </button>
            <Field label="Preset name">
              <input value={name} onChange={(e) => setName(e.target.value)} />
            </Field>
            <button
              disabled={!preview}
              onClick={() =>
                a
                  .run(base + "/print-presets", {
                    orgId: w?.orgId,
                    name,
                    layout,
                  })
                  .catch(() => {})
              }
            >
              Save print preset
            </button>
            <ErrorBox error={a.error} />
          </div>
        </Panel>
        <section className="card-print-preview">
          <div className="card-section-title">
            <span>
              {layout.sheetWidth} × {layout.sheetHeight} mm
            </span>
            <button onClick={() => setBack(!back)}>
              Show {back ? "front" : "back"} registration
            </button>
          </div>
          {preview ? (
            <svg
              role="img"
              aria-label="Physical print sheet layout"
              viewBox={"0 0 " + layout.sheetWidth + " " + layout.sheetHeight}
            >
              <rect
                width={layout.sheetWidth}
                height={layout.sheetHeight}
                fill="white"
              />
              {(back ? preview.back : preview.front).map((p: Row) => (
                <g key={p.index}>
                  <rect
                    x={p.x}
                    y={p.y}
                    width={85.6 + (layout.includeBleed ? 6 : 0)}
                    height={53.98 + (layout.includeBleed ? 6 : 0)}
                    rx="1"
                    fill={back ? "#174f46" : "#eef4ef"}
                    stroke="#aac0b3"
                    strokeWidth=".2"
                  />
                  <text
                    x={p.x + 4}
                    y={p.y + 8}
                    fontSize="3"
                    fill={back ? "white" : "#174f46"}
                  >
                    {back ? "BACK" : "FRONT"} · CARD {p.index + 1}
                  </text>
                  <text
                    x={p.x + 4}
                    y={p.y + 15}
                    fontSize="2.5"
                    fill={back ? "white" : "#5c746b"}
                  >
                    {p.x.toFixed(2)}, {p.y.toFixed(2)} mm
                  </text>
                </g>
              ))}
            </svg>
          ) : (
            <Empty>Calculate a preview to validate sheet geometry.</Empty>
          )}
          <p className="card-muted">
            Geometry preview uses standard CR80 dimensions. Saved presets
            recalculate for each published template during generation. Print at
            100% / actual size. Run a physical calibration sheet before
            production duplex printing.
          </p>
          <Link to={root + "/bulk"}>Use this preset in bulk generation →</Link>
        </section>
      </div>
    </>
  );
}
function Settings() {
  const q = useData(base + "/policy"),
    templates = useData(base + "/templates"),
    a = useAction(),
    [draft, setDraft] = useState<Row>();
  const p = draft || q.data;
  return (
    <>
      <Header
        title="Identity with clear boundaries."
        description="Impact International card policies, controlled by authorized root administrators."
      />
      {p && (
        <Panel title="Issuance & member access">
          <form
            className="card-form"
            onSubmit={(e) => {
              e.preventDefault();
              a.run(base + "/policy", p, "PUT")
                .then(setDraft)
                .catch(() => {});
            }}
          >
            <label>
              <input
                type="checkbox"
                checked={p.allowMemberDownload}
                onChange={(e) =>
                  setDraft({ ...p, allowMemberDownload: e.target.checked })
                }
              />{" "}
              Members may download their issued PDFs and images
            </label>
            <label>
              <input
                type="checkbox"
                checked={p.allowClubDesign}
                onChange={(e) =>
                  setDraft({ ...p, allowClubDesign: e.target.checked })
                }
              />{" "}
              Permit locally owned designs within authorized scope
            </label>
            <Field label="Default published template">
              <select
                value={p.defaultTemplateVersionId || ""}
                onChange={(e) =>
                  setDraft({
                    ...p,
                    defaultTemplateVersionId: e.target.value || null,
                  })
                }
              >
                <option value="">Choose explicitly at issuance</option>
                {templates.data
                  ?.filter((t: Row) => t.active && t.published)
                  .map((t: Row) => (
                    <option key={t.id} value={t.published.id}>
                      {t.name} · v{t.published.number}
                    </option>
                  ))}
              </select>
            </Field>
            <Field label="Default validity (months)">
              <input
                type="number"
                min="1"
                max="60"
                value={p.validityMonths}
                onChange={(e) =>
                  setDraft({ ...p, validityMonths: +e.target.value })
                }
              />
            </Field>
            <p className="card-muted">
              Membership expiry always caps card validity. Independent review is
              mandatory for official individual and bulk issuance. Existing
              issued snapshots never change when a policy or design is edited.
            </p>
            <button className="card-primary" disabled={a.busy}>
              Save policy
            </button>
            <ErrorBox error={a.error ?? q.error} />
          </form>
        </Panel>
      )}
    </>
  );
}
export function MyIdCard() {
  const q = useData("/my/id-card"),
    a = useAction();
  const [showHistory,setShowHistory] = useState(false);
  const [back, setBack] = useState(false),
    [request, setRequest] = useState<Row>(),
    [reason, setReason] = useState("");
  const current = q.data?.find((c:Row)=>c.state==='Issued'&&!c.revokedAt&&c.templateVersionId&&c.canDownload&&(!c.expiryAt||new Date(c.expiryAt)>new Date()));
  return (
    <div className="id-cards">
      <Header
        title="My Impact membership card"
        description="Your Impact identity, ready to carry. Download your latest valid card as a print-ready PDF."
        action={current&&<a className="card-primary card-download-main" href="/api/v1/my/id-card/download-latest"><Download size={18}/>Download my card</a>}
      />
      <section className="card-member-welcome"><div><span>IMPACT INTERNATIONAL · MEMBER WALLET</span><h2>One community. A world of impact.</h2><p>Your photo, membership ID, club and secure verification in one beautifully crafted identity card.</p></div><ShieldCheck size={48}/></section>
      <ErrorBox error={q.error} />
      {q.isPending&&<Skeleton/>}
      {q.data?.length ? (
        q.data.filter((c:Row)=>showHistory||(!c.revokedAt&&c.state!=='Replaced')).map((c: Row) => (
          <article className="card-my-id" key={c.id}>
            <div className="card-section-title">
              <div><span className="card-eyebrow">MEMBER ID · {c.memberId||'See card'}</span><h2>{c.memberName||c.club}</h2><p>{c.club}</p></div>
              <Status>
                {c.state==='Replaced'?'Replaced':c.revokedAt
                  ? "Revoked"
                  : c.expiryAt && new Date(c.expiryAt) < new Date()
                    ? "Expired"
                    : c.state}
              </Status>
            </div>
            {c.templateVersionId ? (
              <img
                className="card-rendered-preview"
                src={
                  "/api/v1/id-cards/issuances/" + c.id + "/preview?back=" + back
                }
                alt={(back?"Back":"Front")+" of "+(c.memberName||"your")+" membership card"}
              />
            ) : (
              <p>
                Your original digital membership ID is available in{" "}
                <Link to="/app/my-membership">My membership</Link>.
              </p>
            )}
            <p>
              {c.serial} ·{" "}
              {c.expiryAt
                ? "Valid till " + new Intl.DateTimeFormat("en-IN", {month:"long",year:"numeric",timeZone:"UTC"}).format(new Date(c.expiryAt.slice(0,10)+"T00:00:00Z"))
                : "Membership validity applies"}
            </p>
            <div className="card-actions">
              {c.templateVersionId && (
                <button onClick={() => setBack(!back)}>
                  Flip front / back
                </button>
              )}
              {c.templateVersionId &&
                c.canDownload &&
                ["Issued", "Revoked", "Replaced"].includes(c.state) &&
                ["pdf", "front", "back"].map((f) => (
                  <a
                    href={
                      "/api/v1/my/id-card/download?id=" + c.id + "&format=" + f
                    }
                    key={f}
                  >
                    <Download size={14} />
                    {f==='pdf'?'Download PDF':f==='front'?'Front image':'Back image'}
                  </a>
                ))}
              <button
                onClick={() => {
                  setRequest(c);
                  setReason("");
                }}
              >
                Correction, reissue or lost card
              </button>
            </div>
          </article>
        ))
      ) : !q.isPending&&!q.error ? (
        <Empty>
          No official ID card is available yet. Your issuing authority can
          prepare one after validating your membership.
        </Empty>
      ) : null}
      {q.data?.some((c:Row)=>c.revokedAt||c.state==='Replaced')&&<button className="secondary" onClick={()=>setShowHistory(!showHistory)}>{showHistory?'Hide previous cards':'View previous cards'}</button>}
      {request && (
        <Modal
          title="Contact your issuing authority"
          close={() => setRequest(undefined)}
        >
          <div className="card-form">
            <p>
              Describe the correction, renewal or lost-card replacement you
              need. A new official card requires independent approval.
            </p>
            <textarea
              aria-label="Card request details"
              value={reason}
              maxLength={500}
              onChange={(e) => setReason(e.target.value)}
            />
            <button
              className="card-primary"
              disabled={reason.length < 5}
              onClick={() =>
                a
                  .run(base + "/issuances/" + request.id + "/requests", {
                    reason,
                  })
                  .then(() => setRequest(undefined))
                  .catch(() => {})
              }
            >
              Record request
            </button>
            <ErrorBox error={a.error} />
          </div>
        </Modal>
      )}
    </div>
  );
}
export function IdVerification() {
  const { token } = useParams();
  const q = useQuery<any>({
    queryKey: ["public-id-verification", token],
    queryFn: () =>
      api("/public/id-cards/verify/" + encodeURIComponent(token || "")),
    retry: false,
    refetchOnWindowFocus: false,
  });
  return (
    <main className="card-public-verification">
      <Link to="/">Impact International</Link>
      {q.isPending ? (
        <Skeleton />
      ) : q.data ? (
        <>
          <ShieldCheck size={45} />
          <Status>
            {q.data.valid
              ? "Current membership verified"
              : "This card is not valid"}
          </Status>
          <h1>{q.data.name || "Card verification"}</h1>
          <h2>{q.data.association}</h2>
          <p>{q.data.club}</p>
          <p>{q.data.serial}</p>
          <p>Status: {q.data.status}</p>
          <p>Expires: {q.data.expiresAt?.slice(0, 10) || "Unavailable"}</p>
          <small>
            This verification grants no access to private membership records.
            Never share a private ID download link.
          </small>
        </>
      ) : (
        <ErrorBox error={q.error} />
      )}
    </main>
  );
}
