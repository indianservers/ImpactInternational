import ModuleNavigation from "../ModuleNavigation";
import { useState, useEffect } from "react";
import { Link, Routes, Route, useNavigate } from "react-router-dom";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import {
  Sparkles,
  Search,
  ArrowUpRight,
  Globe2,
  ShieldCheck,
  Plus,
  ArrowDown,
  Workflow,
  BookOpen,
  X,
  Send,
  Download,
} from "lucide-react";
import { api } from "../api";
import { useWorkspace, ErrorBox, Skeleton } from "../App";
import "./Operations.css";
import "./Finance.css";
import "./Intelligence.css";
type Row = Record<string, any>;
const root = "/app/intelligence";
function useData(path: string, enabled = true) {
  const { w } = useWorkspace();
  return useQuery<any>({
    queryKey: ["intelligence", w?.id, path],
    queryFn: () => api(path),
    enabled,
    staleTime: 10000,
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
      await qc.invalidateQueries({ queryKey: ["intelligence"] });
      await qc.invalidateQueries({ queryKey: ["tenant-experience"] });
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
    <header className="op-header">
      <div>
        <p className="op-eyebrow">IMPACT INTERNATIONAL / INTELLIGENCE</p>
        <h1>{title}</h1>
        <p>{description}</p>
      </div>
      {action}
    </header>
  );
}
function Empty({
  title = "There is no authorized data in this view",
  children,
}: any) {
  return (
    <div className="op-empty">
      <Globe2 size={30} />
      <h3>{title}</h3>
      <div>{children}</div>
    </div>
  );
}
function Badge({ children }: any) {
  return <span className="op-badge">{children}</span>;
}
function Table({
  rows,
  columns,
}: {
  rows: Row[];
  columns: [string, string, ((r: Row) => any)?][];
}) {
  return rows.length ? (
    <div className="fin-table-wrap">
      <table className="fin-table">
        <thead>
          <tr>
            {columns.map(([k, label]) => (
              <th key={k}>{label}</th>
            ))}
          </tr>
        </thead>
        <tbody>
          {rows.map((r, n) => (
            <tr key={r.id ?? n}>
              {columns.map(([k, , render]) => (
                <td key={k}>{render ? render(r) : String(r[k] ?? "—")}</td>
              ))}
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  ) : (
    <Empty>No records were returned. Restricted records are omitted.</Empty>
  );
}
function Dialog({ title, close, children }: any) {
  return (
    <div className="op-scrim">
      <section
        role="dialog"
        aria-modal="true"
        aria-label={title}
        className="op-dialog"
      >
        <div className="op-dialog-head">
          <h2>{title}</h2>
          <button onClick={close} aria-label="Close">
            <X size={20} />
          </button>
        </div>
        {children}
      </section>
    </div>
  );
}
function Field({ label, children }: any) {
  return (
    <label className="op-field">
      <span>{label}</span>
      {children}
    </label>
  );
}
const date = () => new Date().toISOString().slice(0, 10);
function value(v: any) {
  return v === null || v === undefined
    ? "Unavailable"
    : typeof v === "number"
      ? new Intl.NumberFormat(undefined, { maximumFractionDigits: 2 }).format(v)
      : String(v);
}
export default function Intelligence() {
  const { w } = useWorkspace();
  return (
    <div className="operations intelligence">
      <ModuleNavigation root={root} label="Intelligence" items={[["", "Overview", "intelligence.view"], ["assistant", "Assistant", "intelligence.assistant"], ["knowledge", "Knowledge", "intelligence.knowledge"], ["workflows", "Workflows", "intelligence.automations"], ["metrics", "Metric catalog", "intelligence.view"], ["branding", "Brand & domains", "settings.manage"], ["operations", "Release operations", "audit.view"]]} groups={{"Explore": ["", "assistant", "metrics"], "Knowledge & workflows": ["knowledge", "workflows"], "Administration": ["branding", "operations"]}} permissions={w?.permissions || []} />
      <Routes>
        <Route index element={<Overview />} />
        <Route path="assistant" element={<Assistant />} />
        <Route path="knowledge" element={<Knowledge />} />
        <Route path="workflows" element={<Workflows />} />
        <Route path="metrics" element={<Metrics />} />
        <Route path="branding" element={<Branding />} />
        <Route path="operations" element={<ReleaseOperations />} />
      </Routes>
    </div>
  );
}
function Overview() {
  const { w } = useWorkspace();
  const [scope, setScope] = useState(""),
    [from, setFrom] = useState(date().slice(0, 4) + "-01-01"),
    [to, setTo] = useState(date()),
    [trail, setTrail] = useState<Row[]>([]);
  const path =
    "/intelligence/dashboard?from=" +
    from +
    "&to=" +
    to +
    (scope ? "&orgId=" + scope : "");
  const q = useData(path, !!w?.permissions.includes("intelligence.view"));
  const [selected, setSelected] = useState<Row>();
  if (!w?.permissions.includes("intelligence.view"))
    return (
      <>
        <Header
          title="My Impact club insights"
          description="Ask authorized questions about your membership, events, documents and certificates."
        />
        <Link className="op-primary" to={root + "/assistant"}>
          Open your read-only assistant <ArrowUpRight size={17} />
        </Link>
      </>
    );
  return (
    <>
      <Header
        title="Impact International intelligence"
        description="Understand your community through verified records and explicitly authorized organizational scope."
        action={
          <Link className="op-primary" to={root + "/assistant"}>
            <Sparkles size={17} /> Ask your assistant
          </Link>
        }
      />
      <section className="intel-hero">
        <div>
          <span>CONNECTED COMMUNITY INTELLIGENCE</span>
          <h2>{trail[trail.length - 1]?.name ?? w.organization}</h2>
          <p>
            One view across your authorized hierarchy. People, participation and
            service, with definitions you can inspect.
          </p>
        </div>
        <div className="intel-orbit">
          <Globe2 size={64} />
          <span>LIVE DATA</span>
        </div>
      </section>
      <section className="intel-controls">
        <div className="intel-breadcrumbs">
          <button
            onClick={() => {
              setScope("");
              setTrail([]);
            }}
          >
            {w.organization}
          </button>
          {trail.map((t, n) => (
            <button
              key={t.id}
              onClick={() => {
                setScope(t.id);
                setTrail(trail.slice(0, n + 1));
              }}
            >
              {" "}
              / {t.name}
            </button>
          ))}
        </div>
        <Field label="From">
          <input
            type="date"
            value={from}
            onChange={(e) => setFrom(e.target.value)}
          />
        </Field>
        <Field label="Through">
          <input
            type="date"
            value={to}
            onChange={(e) => setTo(e.target.value)}
          />
        </Field>
        <a
          className="intel-export"
          href={
            "/api/v1/intelligence/export?from=" +
            from +
            "&to=" +
            to +
            (scope ? "&orgId=" + scope : "")
          }
        >
          <Download size={15} /> Export metrics
        </a>
      </section>
      <ErrorBox error={q.error} />
      {q.isPending ? (
        <Skeleton />
      ) : (
        q.data && (
          <>
            <div className="intel-metrics">
              {q.data.metrics.map((m: Row) => (
                <button
                  className="intel-kpi"
                  key={m.id}
                  onClick={() => setSelected(m)}
                >
                  <span>{m.name}</span>
                  <strong>
                    {value(m.value)}
                    {m.value !== null && m.unit === "%" ? "%" : ""}
                  </strong>
                  <small>{m.unavailable ?? m.unit}</small>
                  <ArrowUpRight size={15} />
                </button>
              ))}
            </div>
            <div className="intel-two">
              <section className="op-card">
                <div className="intel-section-title">
                  <h2>Membership starts</h2>
                  <Badge>Affiliations ≠ people</Badge>
                </div>
                <p className="op-muted">
                  Recorded starts in the selected interval. This is not a
                  reconstructed historical active-member balance.
                </p>
                {q.data.trend?.length ? (
                  <div
                    className="intel-chart"
                    role="img"
                    aria-label="Membership affiliation starts by month"
                  >
                    {q.data.trend.map((t: Row) => (
                      <div
                        className="intel-chart-column"
                        key={t.year + "-" + t.month}
                      >
                        <b>{value(t.affiliations)}</b>
                        <div
                          style={{
                            height: Math.max(
                              6,
                              (130 * t.affiliations) /
                                Math.max(
                                  ...q.data.trend.map(
                                    (x: Row) => x.affiliations,
                                  ),
                                  1,
                                ),
                            ),
                          }}
                        />
                        <span>
                          {t.year}-{String(t.month).padStart(2, "0")}
                        </span>
                        <small>{t.uniquePeople} people</small>
                      </div>
                    ))}
                  </div>
                ) : (
                  <Empty>No recorded starts for this period.</Empty>
                )}
              </section>
              <section className="op-card">
                <div className="intel-section-title">
                  <h2>Explore your hierarchy</h2>
                  <Badge>Authorized drill-down</Badge>
                </div>
                <div className="intel-orgs">
                  {q.data.organizations
                    ?.filter((o: Row) => o.id !== q.data.scope)
                    .map((o: Row) => (
                      <button
                        key={o.id}
                        onClick={() => {
                          setScope(o.id);
                          setTrail([...trail, { id: o.id, name: o.name }]);
                        }}
                      >
                        <span>
                          <Globe2 size={18} />
                          {o.name}
                          <small>{o.country || "Country not recorded"}</small>
                        </span>
                        <ArrowUpRight size={16} />
                      </button>
                    ))}
                </div>
                {q.data.organizations?.length === 1 && (
                  <p className="op-muted">
                    You are at the lowest configured organizational unit.
                  </p>
                )}
              </section>
            </div>
            <div className="intel-two">
              <section className="op-card">
                <h2>Active community by club</h2>
                <Table
                  rows={q.data.clubs ?? []}
                  columns={[
                    ["name", "Club"],
                    ["affiliations", "Affiliations"],
                    ["uniquePeople", "Unique people"],
                  ]}
                />
              </section>
              <section className="op-card">
                <h2>Verified service outcomes</h2>
                <p className="op-muted">
                  Lead-owned projects count once. People observations do not
                  imply unique beneficiaries.
                </p>
                <Table
                  rows={q.data.impact ?? []}
                  columns={[
                    ["name", "Metric"],
                    ["value", "Verified value", (r) => value(r.value)],
                    ["unit", "Unit"],
                  ]}
                />
              </section>
            </div>
            <section className="op-card">
              <h2>Authorized financial entities</h2>
              <p className="op-muted">
                Separate native-currency receivables. No consolidated bank
                balance is implied.
              </p>
              <Table
                rows={q.data.finance}
                columns={[
                  ["name", "Entity"],
                  ["currency", "Currency"],
                  ["outstanding", "Outstanding", (r) => value(r.outstanding)],
                ]}
              />
            </section>
            {q.data.alerts.map((r: Row, n: number) => (
              <aside className="intel-notice" key={n}>
                <ShieldCheck size={20} />
                <div>
                  <strong>{r.title}</strong>
                  <p>{r.detail}</p>
                </div>
              </aside>
            ))}
            <p className="intel-policy">{q.data.notes}</p>
          </>
        )
      )}
      {selected && (
        <Dialog title={selected.name} close={() => setSelected(undefined)}>
          <p>
            Value:{" "}
            <strong>
              {value(selected.value)} {selected.unit}
            </strong>
          </p>
          {selected.denominator != null && (
            <p>
              Numerator {selected.numerator} / denominator{" "}
              {selected.denominator}
            </p>
          )}
          <p>{selected.unavailable}</p>
          <Link to={root + "/metrics"}>
            Read metric definition and privacy policy →
          </Link>
        </Dialog>
      )}
    </>
  );
}
function Metrics() {
  const q = useData("/intelligence/metrics");
  const [search, setSearch] = useState("");
  return (
    <>
      <Header
        title="Impact metrics catalog"
        description="Definitions, denominators, freshness and privacy boundaries. Unknown values remain unavailable."
      />
      <input
        className="fin-search"
        aria-label="Search metric definitions"
        value={search}
        onChange={(e) => setSearch(e.target.value)}
        placeholder="Find a metric…"
      />
      <ErrorBox error={q.error} />
      <div className="intel-definition-grid">
        {q.data
          ?.filter((m: Row) =>
            (m.name + " " + m.description)
              .toLowerCase()
              .includes(search.toLowerCase()),
          )
          .map((m: Row) => (
            <article className="op-card" key={m.id}>
              <Badge>{m.id}</Badge>
              <h2>{m.name}</h2>
              <p>{m.description}</p>
              <dl>
                {[
                  ["Numerator", m.numerator],
                  ["Denominator", m.denominator],
                  ["Scope", m.scope],
                  ["Filters", m.filters],
                  ["Period", m.period],
                  ["Method", m.method],
                  ["Freshness", m.freshness],
                  ["Privacy", m.privacy],
                ].map(([n, v]) => (
                  <div key={n}>
                    <dt>{n}</dt>
                    <dd>{v}</dd>
                  </div>
                ))}
              </dl>
            </article>
          ))}
      </div>
    </>
  );
}
function Facts({ data }: any) {
  if (data == null)
    return <p className="op-muted">No authorized facts are available.</p>;
  if (Array.isArray(data)) {
    if (!data.length)
      return (
        <Empty title="No matching authorized records">
          Try another supported question or review the source module.
        </Empty>
      );
    if (data[0].excerpt)
      return (
        <div>
          {data.map((d: Row) => (
            <article className="intel-source" key={d.documentId + ":" + d.page}>
              <h3>{d.title}</h3>
              <Badge>
                Version {d.version} · Page {d.page}
              </Badge>
              <blockquote>{d.excerpt}</blockquote>
              <a href={d.url} target="_blank" rel="noreferrer">
                Open authorized source →
              </a>
            </article>
          ))}
        </div>
      );
    const keys = Object.keys(data[0])
      .filter(
        (k) =>
          !["id", "url", "notes", "policy"].includes(k) &&
          !k.endsWith("Id") &&
          typeof data[0][k] !== "object",
      )
      .slice(0, 6);
    return (
      <Table
        rows={data}
        columns={keys.map((k) => [
          k,
          k.replace(/([A-Z])/g, " $1"),
          (r: Row) =>
            k === "number" && r.url ? (
              <a href={r.url}>{String(r[k])}</a>
            ) : (
              value(r[k])
            ),
        ])}
      />
    );
  }
  if (data.metrics)
    return (
      <>
        <div className="intel-metrics compact">
          {data.metrics
            .filter((m: Row) => m.value !== null)
            .map((m: Row) => (
              <section className="intel-kpi" key={m.id}>
                <span>{m.name}</span>
                <strong>
                  {value(m.value)}
                  {m.unit === "%" ? "%" : ""}
                </strong>
              </section>
            ))}
        </div>
        <p className="op-muted">{data.notes}</p>
      </>
    );
  return (
    <dl>
      {Object.entries(data)
        .filter(([, v]) => typeof v !== "object")
        .map(([k, v]) => (
          <div key={k}>
            <dt>{k}</dt>
            <dd>{value(v)}</dd>
          </div>
        ))}
    </dl>
  );
}
function Assistant() {
  const a = useAction();
  const [question, setQuestion] = useState(""),
    [answers, setAnswers] = useState<Row[]>([]),
    [remote, setRemote] = useState(false),
    [ack, setAck] = useState(false);
  const brand = useData("/intelligence/branding/current");
  const examples = [
    "How many new members joined this year?",
    "Show our upcoming events.",
    "Show my membership status.",
    "Where are my certificates?",
    "Show outstanding membership dues.",
    "Summarize our bylaws.",
  ];
  return (
    <>
      <Header
        title="Ask Impact intelligence"
        description="Read-only tools, scoped to your active role. Facts stay connected to their sources."
      />
      <div className="intel-assistant-layout">
        <aside className="op-card">
          <Sparkles size={24} />
          <h2>Start with a question</h2>
          <p className="op-muted">
            The local assistant selects a vetted read-only view. No unrestricted
            SQL, approvals, bulk messages or financial actions.
          </p>
          {examples.map((q) => (
            <button
              className="intel-prompt"
              key={q}
              onClick={() => setQuestion(q)}
            >
              {q}
              <ArrowUpRight size={14} />
            </button>
          ))}
          <div className="intel-policy">
            {brand.data?.remoteAiConfigured && brand.data?.remoteAiEnabled ? (
              <>
                <label>
                  <input
                    type="checkbox"
                    checked={remote}
                    onChange={(e) => setRemote(e.target.checked)}
                  />{" "}
                  Use configured OpenAI tool routing
                </label>
                {remote && (
                  <>
                    <p>{brand.data.aiPolicy}</p>
                    <label>
                      <input
                        type="checkbox"
                        checked={ack}
                        onChange={(e) => setAck(e.target.checked)}
                      />{" "}
                      I acknowledge this question will be sent to OpenAI.
                      Retrieved facts and document text stay local.
                    </label>
                  </>
                )}
              </>
            ) : (
              <p>
                Model provider is not enabled. Local authorized tools are
                available.
              </p>
            )}
          </div>
        </aside>
        <section className="op-card intel-conversation">
          {answers.length ? (
            answers.map((answer, n) => (
              <article key={n} className="intel-answer">
                <div className="intel-question">{answer.question}</div>
                <p className="intel-answer-meta">
                  <Sparkles size={15} />
                  {answer.provider} · {answer.tool}
                </p>
                <p>{answer.explanation}</p>
                <Facts data={answer.facts} />
                {answer.links.map((l: Row) => (
                  <Link className="intel-citation" key={l.url} to={l.url}>
                    {l.title} →
                  </Link>
                ))}
              </article>
            ))
          ) : (
            <Empty title="What would you like to understand?">
              <p>
                Ask about verified records available in your workspace. Missing
                or restricted information is not invented.
              </p>
            </Empty>
          )}
          <form
            className="intel-ask"
            onSubmit={async (e) => {
              e.preventDefault();
              try {
                const answer = await a.run("/intelligence/assistant", {
                  question,
                  useRemoteModel: remote,
                  acknowledgeProvider: ack,
                });
                setAnswers((old) => [...old, { ...answer, question }]);
                setQuestion("");
              } catch {}
            }}
          >
            <textarea
              aria-label="Ask your assistant"
              placeholder="Ask about your authorized community records…"
              value={question}
              onChange={(e) => setQuestion(e.target.value)}
              required
              maxLength={1000}
            />
            <button
              className="op-primary"
              disabled={a.busy || question.trim().length < 2}
            >
              <Send size={16} />
              {a.busy ? "Reading authorized facts…" : "Ask"}
            </button>
          </form>
          <ErrorBox error={a.error} />
        </section>
      </div>
    </>
  );
}
function Knowledge() {
  const [query, setQuery] = useState(""),
    [submitted, setSubmitted] = useState(""),
    [document, setDocument] = useState("");
  const q = useData(
    "/intelligence/knowledge?query=" + encodeURIComponent(submitted),
    submitted.length >= 2,
  );
  const a = useAction();
  const { w } = useWorkspace();
  const indexes = useData(
    "/intelligence/knowledge/indexes",
    !!w?.permissions.includes("operations.documents"),
  );
  return (
    <>
      <Header
        title="Impact knowledge library"
        description="Search authorized text-bearing PDFs. Every result is checked against the current version, permissions and retention."
      />
      <form
        className="intel-knowledge-search"
        onSubmit={(e) => {
          e.preventDefault();
          setSubmitted(query);
        }}
      >
        <BookOpen size={20} />
        <input
          aria-label="Search organizational knowledge"
          placeholder="Try bylaws, election rules or secretary responsibilities…"
          value={query}
          onChange={(e) => setQuery(e.target.value)}
          required
          minLength={2}
        />
        <button className="op-primary">Find sources</button>
      </form>
      <ErrorBox error={q.error} />
      {q.isFetching ? (
        <Skeleton />
      ) : q.data ? (
        <Facts data={q.data.items} />
      ) : (
        <Empty title="Answers start with authorized documents">
          The index contains extracted PDF text, not an external vector copy.
          Scans require OCR and are not indexed.
        </Empty>
      )}
      {w?.permissions.includes("operations.documents") && (
        <section className="op-card">
          <h2>Index an existing vault document</h2>
          <p className="op-muted">
            Use a PDF already uploaded in the Document Vault. Original sharing
            rules apply. No new file master is created.
          </p>
          <form
            className="intel-inline-form"
            onSubmit={async (e) => {
              e.preventDefault();
              try {
                await a.run("/intelligence/knowledge/" + document + "/index");
                setDocument("");
              } catch {}
            }}
          >
            <input
              aria-label="Existing vault document ID"
              value={document}
              onChange={(e) => setDocument(e.target.value)}
              placeholder="Document ID"
              required
            />
            <button disabled={a.busy}>Extract current PDF text</button>
          </form>
          <ErrorBox error={a.error} />
          <Table
            rows={indexes.data ?? []}
            columns={[
              ["documentId", "Document reference"],
              ["documentVersion", "Current indexed version"],
              ["pages", "Text pages"],
            ]}
          />
        </section>
      )}
    </>
  );
}
function Workflows() {
  const q = useData("/intelligence/workflows"),
    options = useData("/intelligence/workflows/options");
  const a = useAction();
  const [editing, setEditing] = useState<Row>();
  const [open, setOpen] = useState(false),
    [name, setName] = useState(""),
    [org, setOrg] = useState(""),
    [entity, setEntity] = useState(""),
    [trigger, setTrigger] = useState("TaskOverdue"),
    [days, setDays] = useState(7),
    [limit, setLimit] = useState(25),
    [status, setStatus] = useState(""),
    [amount, setAmount] = useState(""),
    [steps, setSteps] = useState<Row[]>([
      { action: "NotifyCreator", delayMinutes: 0, branch: "Match" },
    ]),
    [preview, setPreview] = useState<Row>(),
    [history, setHistory] = useState<Row[]>();
  function step(n: number, k: string, v: any) {
    setSteps((old) => old.map((s, i) => (i === n ? { ...s, [k]: v } : s)));
  }
  return (
    <>
      <Header
        title="Impact follow-up workflows"
        description="Versioned follow-ups in the existing communication engine, with independent review and a visible execution trail."
        action={
          <button
            className="op-primary"
            onClick={() => {
              setEditing(undefined);
              setName("");
              setOpen(true);
            }}
          >
            <Plus size={16} /> Design workflow
          </button>
        }
      />
      <aside className="intel-notice">
        <ShieldCheck size={20} />
        <p>
          Only personal notifications and generic follow-up tasks are enabled.
          Financial posting, approvals, source-status changes and bulk messaging
          are excluded.
        </p>
      </aside>
      <div className="intel-workflow-list">
        {q.data?.map((r: Row) => (
          <section className="op-card" key={r.id}>
            <div className="intel-section-title">
              <div>
                <Badge>
                  {r.status} · {r.active ? "Active" : "Inactive"}
                </Badge>
                <h2>{r.name}</h2>
                <p className="op-muted">
                  {r.trigger} · revision {r.version} · daily limit{" "}
                  {r.dailyLimit}
                </p>
              </div>
              <Workflow size={30} />
            </div>
            <div className="intel-mini-flow">
              {JSON.parse(r.definition).steps.map((s: Row, n: number) => (
                <span key={n}>
                  {n > 0 && " → "}
                  {s.action}{" "}
                  {s.delayMinutes ? `(after ${s.delayMinutes} min)` : ""}
                </span>
              ))}
            </div>
            <div className="fin-actions">
              <button
                onClick={async () => {
                  try {
                    setPreview(
                      await a.run("/intelligence/workflows/" + r.id + "/test"),
                    );
                  } catch {}
                }}
              >
                Test without actions
              </button>
              {r.status === "Draft" && (
                <button
                  onClick={() => {
                    const d = JSON.parse(r.definition);
                    setEditing(r);
                    setName(r.name);
                    setOrg(r.orgId);
                    setEntity(d.entityId || "");
                    setTrigger(d.trigger);
                    setDays(d.days);
                    setLimit(r.dailyLimit);
                    setStatus(d.condition.status || "");
                    setAmount(d.condition.minimumAmount || "");
                    setSteps(d.steps);
                    setOpen(true);
                  }}
                >
                  Edit draft
                </button>
              )}
              {r.status === "Draft" && (
                <button
                  onClick={() =>
                    a
                      .run("/intelligence/workflows/" + r.id + "/submit")
                      .catch(() => {})
                  }
                >
                  Submit independent review
                </button>
              )}
              <button
                onClick={() =>
                  a
                    .run("/intelligence/workflows/" + r.id + "/copy")
                    .catch(() => {})
                }
              >
                New reviewed revision
              </button>
              <button
                onClick={async () => {
                  try {
                    setHistory(
                      await api(
                        "/intelligence/workflows/" + r.id + "/executions",
                      ),
                    );
                  } catch {}
                }}
              >
                Execution history
              </button>
              {r.active && (
                <button
                  onClick={() =>
                    a
                      .run("/intelligence/workflows/" + r.id + "/cancel")
                      .catch(() => {})
                  }
                >
                  Cancel future execution
                </button>
              )}
              {r.approvalRequestId && (
                <Link to={"/app/governance?request=" + r.approvalRequestId}>
                  Approval record
                </Link>
              )}
            </div>
          </section>
        ))}
      </div>
      <ErrorBox error={q.error ?? a.error} />
      {preview && (
        <Dialog
          title="Workflow test · no actions executed"
          close={() => setPreview(undefined)}
        >
          <p>
            Dry run: {String(preview.dryRun)} · Actions executed:{" "}
            {preview.actionsExecuted}
          </p>
          <Table
            rows={preview.sources}
            columns={[
              ["sourceKey", "Hashed source key"],
              [
                "matches",
                "Matches conditions",
                (r) => (r.matches ? "Yes" : "Otherwise branch"),
              ],
            ]}
          />
        </Dialog>
      )}
      {history && (
        <Dialog
          title="Durable execution history"
          close={() => setHistory(undefined)}
        >
          <Table
            rows={history}
            columns={[
              ["at", "Queued"],
              ["ruleVersion", "Rule revision"],
              ["step", "Next step"],
              ["status", "State"],
              ["error", "Error"],
              [
                "action",
                "Retry",
                (r) =>
                  r.status === "Failed" ? (
                    <button
                      onClick={() =>
                        a
                          .run("/intelligence/executions/" + r.id + "/retry")
                          .catch(() => {})
                      }
                    >
                      Retry same snapshot
                    </button>
                  ) : (
                    "—"
                  ),
              ],
            ]}
          />
        </Dialog>
      )}
      {open && options.data && (
        <Dialog title="Design a reviewed workflow" close={() => setOpen(false)}>
          <form
            className="op-form"
            onSubmit={async (e) => {
              e.preventDefault();
              try {
                await a.run(
                  "/intelligence/workflows" + (editing ? "/" + editing.id : ""),
                  {
                    version: editing?.version || 1,
                    name,
                    orgId: org || options.data.organizations[0]?.id,
                    dailyLimit: limit,
                    definition: {
                      trigger,
                      days,
                      entityId: trigger === "InvoiceOverdue" ? entity : null,
                      condition: {
                        status,
                        minimumAmount: amount || null,
                        categoryId: null,
                      },
                      steps,
                    },
                  },
                  editing ? "PUT" : "POST",
                );
                setOpen(false);
              } catch {}
            }}
          >
            <Field label="Workflow name">
              <input
                value={name}
                onChange={(e) => setName(e.target.value)}
                required
                maxLength={120}
              />
            </Field>
            <section className="intel-flow-node">
              <Badge>1 · Trigger</Badge>
              <div className="op-form-grid">
                <Field label="Organization">
                  <select
                    value={org || options.data.organizations[0]?.id}
                    onChange={(e) => setOrg(e.target.value)}
                  >
                    {options.data.organizations.map((o: Row) => (
                      <option key={o.id} value={o.id}>
                        {o.name}
                      </option>
                    ))}
                  </select>
                </Field>
                <Field label="Supported source">
                  <select
                    value={trigger}
                    onChange={(e) => setTrigger(e.target.value)}
                  >
                    {options.data.triggers.map((t: string) => (
                      <option key={t}>{t}</option>
                    ))}
                  </select>
                </Field>
                <Field label="Time window (days)">
                  <input
                    type="number"
                    min={1}
                    max={90}
                    value={days}
                    onChange={(e) => setDays(Number(e.target.value))}
                  />
                </Field>
                {trigger === "InvoiceOverdue" && (
                  <Field label="Explicit financial entity">
                    <select
                      required
                      value={entity}
                      onChange={(e) => {
                        setEntity(e.target.value);
                        setOrg(
                          options.data.entities.find(
                            (x: Row) => x.id === e.target.value,
                          )?.orgId ?? "",
                        );
                      }}
                    >
                      <option value="">Choose authorized entity</option>
                      {options.data.entities.map((e: Row) => (
                        <option key={e.id} value={e.id}>
                          {e.name}
                        </option>
                      ))}
                    </select>
                  </Field>
                )}
              </div>
            </section>
            <ArrowDown className="intel-flow-arrow" size={20} />
            <section className="intel-flow-node">
              <Badge>2 · Conditions</Badge>
              <div className="op-form-grid">
                <Field label="Exact supported status (optional)">
                  <input
                    value={status}
                    onChange={(e) => setStatus(e.target.value)}
                    placeholder="Any eligible source status"
                  />
                </Field>
                {trigger === "InvoiceOverdue" && (
                  <Field label="Minimum outstanding amount">
                    <input
                      type="number"
                      step="any"
                      value={amount}
                      onChange={(e) => setAmount(e.target.value)}
                    />
                  </Field>
                )}
                <Field label="Daily source limit">
                  <input
                    type="number"
                    min={1}
                    max={50}
                    value={limit}
                    onChange={(e) => setLimit(Number(e.target.value))}
                  />
                </Field>
              </div>
              <p className="op-muted">
                Match and Otherwise branches are reevaluated against current
                authorized source state before a step runs.
              </p>
            </section>
            {steps.map((s, n) => (
              <div key={n}>
                <ArrowDown className="intel-flow-arrow" size={20} />
                <section className="intel-flow-node">
                  <Badge>{n + 3} · Reviewed action</Badge>
                  <div className="op-form-grid">
                    <Field label="Action">
                      <select
                        value={s.action}
                        onChange={(e) => step(n, "action", e.target.value)}
                      >
                        {options.data.actions.map((a: string) => (
                          <option key={a}>{a}</option>
                        ))}
                      </select>
                    </Field>
                    <Field label="Branch">
                      <select
                        value={s.branch}
                        onChange={(e) => step(n, "branch", e.target.value)}
                      >
                        <option>Match</option>
                        <option>Otherwise</option>
                      </select>
                    </Field>
                    <Field label="Delay before step (minutes)">
                      <input
                        type="number"
                        min={0}
                        max={10080}
                        value={s.delayMinutes}
                        onChange={(e) =>
                          step(n, "delayMinutes", Number(e.target.value))
                        }
                      />
                    </Field>
                  </div>
                  {steps.length > 1 && (
                    <button
                      type="button"
                      onClick={() =>
                        setSteps((old) => old.filter((_, i) => i !== n))
                      }
                    >
                      Remove step
                    </button>
                  )}
                </section>
              </div>
            ))}
            {steps.length < 8 && (
              <button
                type="button"
                onClick={() =>
                  setSteps((old) => [
                    ...old,
                    {
                      action: "NotifyCreator",
                      delayMinutes: 0,
                      branch: "Match",
                    },
                  ])
                }
              >
                + Add step
              </button>
            )}
            <ErrorBox error={a.error} />
            <button className="op-primary" disabled={a.busy}>
              Save draft · review before activation
            </button>
          </form>
        </Dialog>
      )}
    </>
  );
}
function Branding() {
  const q = useData("/intelligence/branding");
  const a = useAction();
  const [form, setForm] = useState<Row>(),
    [host, setHost] = useState(""),
    [challenge, setChallenge] = useState<Row>();
  useEffect(() => {
    if (q.data)
      setForm(
        q.data.branding ?? {
          displayName: "Impact International",
          accent: "#302b78",
          clubTerm: "Club",
          districtTerm: "District",
          logo: "/brand/impact-logo.webp",
          favicon: "/brand/impact-logo.webp",
          welcome: "Connected leadership. Meaningful impact.",
          languages: "en",
          disabledFeatures: "[]",
          remoteAiEnabled: false,
          aiPolicy: "",
          version: 1,
        },
      );
  }, [q.data]);
  function set(k: string, v: any) {
    setForm((f: Row | undefined) => ({ ...f, [k]: v }));
  }
  return (
    <>
      <Header
        title="Impact International experience"
        description="Impact terminology, feature availability, and website configuration."
      />
      <ErrorBox error={q.error ?? a.error} />
      {form && (
        <div className="intel-two">
          <form
            className="op-card op-form"
            onSubmit={async (e) => {
              e.preventDefault();
              try {
                await a.run(
                  "/intelligence/branding",
                  {
                    ...form,
                    disabledFeatures: JSON.parse(form.disabledFeatures),
                  },
                  "PUT",
                );
              } catch {}
            }}
          >
            <h2>Impact identity</h2>
            <div className="op-form-grid">
              {[
                ["displayName", "Display name"],
                ["accent", "Brand color"],
                ["clubTerm", "Club terminology"],
                ["districtTerm", "District terminology"],
                ["logo", "Logo asset URL"],
                ["favicon", "Favicon asset URL"],
                ["welcome", "Welcome message"],
                ["languages", "Declared languages"],
              ].map(([k, label]) => (
                <Field key={k} label={label}>
                  <input
                    value={form[k]}
                    readOnly={["displayName", "logo", "favicon"].includes(k)}
                    type={k === "accent" ? "color" : "text"}
                    onChange={(e) => set(k, e.target.value)}
                    required={[
                      "displayName",
                      "accent",
                      "clubTerm",
                      "districtTerm",
                    ].includes(k)}
                  />
                </Field>
              ))}
            </div>
            <p className="op-muted">
              Language declarations do not install translations. Deploy asset
              URLs and allow their hosts in the CSP.
            </p>
            <h3>Feature availability</h3>
            {q.data?.features.map((feature: string) => (
              <label className="intel-check" key={feature}>
                <input
                  type="checkbox"
                  checked={!JSON.parse(form.disabledFeatures).includes(feature)}
                  onChange={(e) => {
                    const old = JSON.parse(form.disabledFeatures);
                    set(
                      "disabledFeatures",
                      JSON.stringify(
                        e.target.checked
                          ? old.filter((x: string) => x !== feature)
                          : [...old, feature],
                      ),
                    );
                  }}
                />
                Enable {feature}
              </label>
            ))}
            <h3>Optional model routing</h3>
            <label className="intel-check">
              <input
                type="checkbox"
                checked={form.remoteAiEnabled}
                onChange={(e) => set("remoteAiEnabled", e.target.checked)}
              />
              Permit configured OpenAI question routing
            </label>
            <Field label="Impact data-processing policy">
              <textarea
                value={form.aiPolicy}
                onChange={(e) => set("aiPolicy", e.target.value)}
                required={form.remoteAiEnabled}
              />
            </Field>
            <p className="op-muted">
              The provider receives questions only after acknowledgment. Tool
              facts and retrieved document text stay local. Configure API keys
              on the server.
            </p>
            <button className="op-primary" disabled={a.busy}>
              Save Impact experience
            </button>
          </form>
          <div>
            <section
              className="op-card intel-brand-preview"
              style={{ borderTop: "5px solid " + form.accent }}
            >
              <p>IDENTITY PREVIEW</p>
              {form.logo && (
                <img src={form.logo} alt="Impact International logo preview" />
              )}
              <h2>{form.displayName || "Impact International"}</h2>
              <p>{form.welcome}</p>
              <Badge>
                {form.clubTerm} / {form.districtTerm}
              </Badge>
            </section>
            <section className="op-card">
              <h2>Custom domains</h2>
              <p className="op-muted">
                Prove DNS ownership first. Verification does not configure
                routing, TLS or ingress.
              </p>
              <form
                className="intel-inline-form"
                onSubmit={async (e) => {
                  e.preventDefault();
                  try {
                    setChallenge(
                      await a.run("/intelligence/domains", { host }),
                    );
                    setHost("");
                  } catch {}
                }}
              >
                <input
                  aria-label="Custom public hostname"
                  placeholder="members.impactinternational.in"
                  value={host}
                  onChange={(e) => setHost(e.target.value)}
                  required
                />
                <button disabled={a.busy}>Create DNS challenge</button>
              </form>
              {challenge && (
                <div className="intel-source">
                  <p>Add this public DNS record:</p>
                  <dl>
                    <dt>Record</dt>
                    <dd>{challenge.record}</dd>
                    <dt>Type</dt>
                    <dd>{challenge.type}</dd>
                    <dt>Value</dt>
                    <dd>{challenge.value}</dd>
                  </dl>
                </div>
              )}
              <Table
                rows={q.data?.domains ?? []}
                columns={[
                  ["host", "Hostname"],
                  ["status", "State"],
                  [
                    "action",
                    "Ownership",
                    (d) => (
                      <div className="fin-actions">
                        {["Pending", "Verified"].includes(d.status) && (
                          <button
                            onClick={() =>
                              a
                                .run(
                                  "/intelligence/domains/" + d.id + "/verify",
                                )
                                .catch(() => {})
                            }
                          >
                            Check DNS TXT
                          </button>
                        )}
                        {d.status === "Active" && (
                          <button
                            onClick={() =>
                              a
                                .run(
                                  "/intelligence/domains/" +
                                    d.id +
                                    "/deactivate",
                                )
                                .catch(() => {})
                            }
                          >
                            Disable routing
                          </button>
                        )}
                      </div>
                    ),
                  ],
                ]}
              />
              <p className="intel-policy">
                A platform operator activates verified hosts only after the
                deployment lists a TLS/ingress-provisioned hostname. No wildcard
                tenant mapping or session cookies are created.
              </p>
            </section>
          </div>
        </div>
      )}
    </>
  );
}
function ReleaseOperations() {
  const q = useData("/intelligence/operations");
  return (
    <>
      <Header
        title="Impact system diagnostics"
        description="Observed health and explicit release gates. A successful build is not production approval."
      />
      <aside className="intel-notice warning">
        <ShieldCheck size={22} />
        <div>
          <strong>Production readiness is not cleared</strong>
          <p>
            Review security, providers, recovery, TLS, deployment and shared
            enforcement.
          </p>
        </div>
      </aside>
      <ErrorBox error={q.error} />
      {q.data && (
        <>
          <div className="intel-metrics">
            {Object.entries(q.data.queues).map(([name, count]) => (
              <section className="intel-kpi" key={name}>
                <span>{name.replace(/([A-Z])/g, " $1")}</span>
                <strong>{value(count)}</strong>
              </section>
            ))}
          </div>
          <section className="op-card">
            <h2>Release gates</h2>
            <dl>
              {Object.entries(q.data.gates).map(([k, v]) => (
                <div key={k}>
                  <dt>{k}</dt>
                  <dd>{String(v)}</dd>
                </div>
              ))}
            </dl>
          </section>
          <section className="op-card">
            <h2>Process telemetry</h2>
            <Facts data={q.data.telemetry} />
            <p>
              {q.data.migrations.length} applied migrations ·{" "}
              {q.data.environment}
            </p>
            <a href="/health/ready" target="_blank" rel="noreferrer">
              Inspect dependency readiness
            </a>
          </section>
        </>
      )}
    </>
  );
}
export function SearchLauncher() {
  const { w } = useWorkspace();
  const [open, setOpen] = useState(false),
    [query, setQuery] = useState(""),
    [debounced, setDebounced] = useState(""),
    [kind, setKind] = useState("all");
  const navigate = useNavigate();
  useEffect(() => {
    const timer = setTimeout(() => setDebounced(query), 300);
    return () => clearTimeout(timer);
  }, [query]);
  useEffect(() => {
    function key(e: KeyboardEvent) {
      if ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === "k") {
        e.preventDefault();
        setOpen((v) => !v);
      }
      if (e.key === "Escape") setOpen(false);
    }
    window.addEventListener("keydown", key);
    return () => window.removeEventListener("keydown", key);
  }, []);
  const q = useData(
    "/intelligence/search?query=" +
      encodeURIComponent(debounced) +
      "&kind=" +
      kind,
    open && debounced.trim().length >= 2,
  );
  const recentKey = "intelligence-search:" + w?.id;
  const [recent, setRecent] = useState<string[]>([]);
  useEffect(() => {
    try {
      setRecent(JSON.parse(localStorage.getItem(recentKey) ?? "[]"));
    } catch {
      setRecent([]);
    }
  }, [recentKey]);
  function go(hit: Row) {
    const next = [query, ...recent.filter((t) => t !== query)].slice(0, 5);
    localStorage.setItem(recentKey, JSON.stringify(next));
    setRecent(next);
    setOpen(false);
    navigate(hit.url);
  }
  if (!w?.permissions.includes("intelligence.assistant")) return null;
  return (
    <>
      <button
        className="icon-button intel-search-launcher"
        aria-label="Search all authorized records (Ctrl K)"
        onClick={() => setOpen(true)}
      >
        <Search size={18} />
        <kbd>Ctrl K</kbd>
      </button>
      {open && (
        <Dialog
          title="Search your authorized workspace"
          close={() => setOpen(false)}
        >
          <div className="intel-global-search">
            <Search size={20} />
            <input
              autoFocus
              aria-label="Universal search"
              placeholder="Search people, clubs, events, documents, invoices…"
              value={query}
              onChange={(e) => setQuery(e.target.value)}
            />
          </div>
          <select
            aria-label="Resource type"
            value={kind}
            onChange={(e) => setKind(e.target.value)}
          >
            {[
              "all",
              "members",
              "organizations",
              "officers",
              "committees",
              "events",
              "meetings",
              "tasks",
              "projects",
              "documents",
              "certificates",
              "invoices",
            ].map((k) => (
              <option key={k}>{k}</option>
            ))}
          </select>
          <ErrorBox error={q.error} />
          {query.length < 2 ? (
            <div className="intel-recent">
              <p className="op-muted">
                Recent searches in this workspace · stored on this browser
              </p>
              {recent.map((term) => (
                <button key={term} onClick={() => setQuery(term)}>
                  {term}
                </button>
              ))}
            </div>
          ) : q.isFetching ? (
            <Skeleton />
          ) : (
            <div className="intel-search-results">
              {q.data?.items.map((hit: Row) => (
                <button key={hit.kind + hit.id} onClick={() => go(hit)}>
                  <span>
                    <Badge>{hit.kind}</Badge>
                    <strong>{hit.title}</strong>
                    <small>{hit.context}</small>
                  </span>
                  <ArrowUpRight size={17} />
                </button>
              ))}
              {q.data?.items.length === 0 && (
                <Empty>
                  No authorized matches. Suggestions and counts do not reveal
                  restricted records.
                </Empty>
              )}
            </div>
          )}
        </Dialog>
      )}
    </>
  );
}
