import { useState, useEffect } from "react";
import {
  Link,
  NavLink,
  Routes,
  Route,
  useParams,
  useNavigate,
} from "react-router-dom";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import {
  CalendarDays,
  Users,
  ArrowUpRight,
  Plus,
  ClipboardList,
  FolderOpen,
  Leaf,
  Clock,
  ShieldCheck,
  Download,
} from "lucide-react";
import { api, apiForm, apiDownload } from "../api";
import { ErrorBox, Skeleton, useWorkspace } from "../App";
import "./Operations.css";
type Row = Record<string, any>;
const root = "/app/operations";
function useData(path: string) {
  const { w } = useWorkspace();
  return useQuery<any>({
    queryKey: ["operations", w?.id, path],
    queryFn: () => api(path),
    refetchInterval: 20000,
  });
}
function useOps() {
  const qc = useQueryClient();
  const [error, setError] = useState<unknown>();
  const [busy, setBusy] = useState(false);
  async function run(path: string, body?: unknown, method = "POST") {
    setBusy(true);
    setError(undefined);
    try {
      const r = await api(path, method, body);
      await qc.invalidateQueries({ queryKey: ["operations"] });
      return r;
    } catch (e) {
      setError(e);
      throw e;
    } finally {
      setBusy(false);
    }
  }
  return { run, error, busy, qc };
}
function useManager() {
  return useWorkspace().w?.permissions.includes("operations.manage");
}
function date(v: any) {
  return v
    ? new Date(v).toLocaleDateString(undefined, {
        month: "short",
        day: "numeric",
        year: "numeric",
      })
    : "—";
}
function instant(v: any) {
  return v
    ? new Date(v).toLocaleString(undefined, {
        month: "short",
        day: "numeric",
        hour: "2-digit",
        minute: "2-digit",
      })
    : "—";
}
function iso(v: string) {
  return new Date(v).toISOString();
}
function Badge({ children }: any) {
  return (
    <span
      className={
        "op-badge " +
        (String(children).match(/Verified|Approved|Published|Issued|Completed/)
          ? "good"
          : "")
      }
    >
      {children}
    </span>
  );
}
function Empty({ children }: any) {
  return (
    <div className="op-empty">
      <Leaf size={30} />
      <h3>Room for your next contribution</h3>
      <p>{children}</p>
    </div>
  );
}
function Header({ title, description, action }: any) {
  return (
    <header className="op-header">
      <div>
        <p className="op-eyebrow">CLUB OPERATIONS</p>
        <h1>{title}</h1>
        <p>{description}</p>
      </div>
      {action}
    </header>
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
function Modal({ title, close, children }: any) {
  return (
    <div className="op-scrim" onClick={close}>
      <section
        className="op-dialog"
        role="dialog"
        aria-modal="true"
        aria-label={title}
        onClick={(e) => e.stopPropagation()}
      >
        <div className="op-dialog-head">
          <h2>{title}</h2>
          <button onClick={close} aria-label="Close">
            ×
          </button>
        </div>
        {children}
      </section>
    </div>
  );
}
function Options({ items, label = "displayName" }: any) {
  return (
    <>
      {items?.map((p: Row) => (
        <option key={p.id} value={p.id}>
          {p[label]}
        </option>
      ))}
    </>
  );
}
function People({ name, required = false }: any) {
  const [search, setSearch] = useState(""),
    [term, setTerm] = useState("");
  useEffect(() => {
    const t = setTimeout(() => setTerm(search), 250);
    return () => clearTimeout(t);
  }, [search]);
  const q = useData(
    "/operations/options?personSearch=" + encodeURIComponent(term),
  );
  return (
    <>
      <input
        aria-label={"Search people for " + name}
        placeholder="Search people…"
        value={search}
        onChange={(e) => setSearch(e.target.value)}
      />
      <select
        name={name}
        aria-label={
          name === "person"
            ? "Assigned person"
            : name === "chair"
              ? "Chairperson"
              : name === "secretary"
                ? "Secretary"
                : name === "manager"
                  ? "Project manager"
                  : "Supervisor"
        }
        required={required}
      >
        <option value="">Choose a person</option>
        <Options items={q.data?.people} />
      </select>
    </>
  );
}

function OrganizationPicker({ name, initial }: any) {
  const { w } = useWorkspace();
  const [value, setValue] = useState(initial ?? w?.orgId ?? ""),
    [search, setSearch] = useState(""),
    [term, setTerm] = useState("");
  useEffect(() => {
    const t = setTimeout(() => setTerm(search), 250);
    return () => clearTimeout(t);
  }, [search]);
  const q = useData(
    "/operations/options?orgSearch=" + encodeURIComponent(term),
  );
  const label =
    q.data?.organizations.find((o: Row) => o.id === value)?.name ??
    (value === w?.orgId
      ? (w?.organization ?? "Workspace")
      : "Selected organization");
  return (
    <>
      <input
        placeholder="Search organizations…"
        aria-label={"Search organizations for " + name}
        value={search}
        onChange={(e) => setSearch(e.target.value)}
      />
      <select
        name={name}
        aria-label={
          name === "orgId" ? "Owning organization" : "Lead organization"
        }
        value={value}
        onChange={(e) => setValue(e.target.value)}
        required
      >
        <option value={value}>{label}</option>
        <Options
          items={q.data?.organizations.filter((o: Row) => o.id !== value)}
          label="name"
        />
      </select>
    </>
  );
}

export default function Operations() {
  return (
    <div className="operations">
      <nav className="op-topnav" aria-label="Operations navigation">
        {[
          ["", "Overview"],
          ["meetings", "Meetings"],
          ["tasks", "Tasks"],
          ["projects", "Projects"],
          ["volunteers", "Volunteers"],
          ["impact", "Impact"],
          ["documents", "Documents"],
          ["certificates", "Certificates"],
        ].map(([p, t]) => (
          <NavLink key={p} end={!p} to={root + "/" + p}>
            {t}
          </NavLink>
        ))}
      </nav>
      <Routes>
        <Route index element={<Dashboard />} />
        <Route path="meetings" element={<Meetings />} />
        <Route path="meetings/:id" element={<MeetingWorkspace />} />
        <Route path="tasks" element={<Tasks />} />
        <Route path="projects" element={<Projects />} />
        <Route path="projects/:id" element={<ProjectWorkspace />} />
        <Route path="volunteers" element={<Volunteers />} />
        <Route path="impact" element={<Impact />} />
        <Route path="documents" element={<Documents />} />
        <Route path="certificates" element={<Certificates />} />
      </Routes>
    </div>
  );
}
function Dashboard() {
  const q = useData("/operations/analytics");
  const { w } = useWorkspace();
  if (q.isPending) return <Skeleton />;
  return (
    <>
      <Header
        title="Small actions. Lasting impact."
        description={
          "Your " +
          (w?.organization ?? "community") +
          " operations, connected in one place."
        }
      />
      <ErrorBox error={q.error} />
      <div className="op-hero">
        <div>
          <Badge>YOUR COMMUNITY AT WORK</Badge>
          <h2>
            Bring people together.
            <br />
            Move good ideas forward.
          </h2>
          <p>
            Prepare the next meeting, support your volunteers and turn verified
            service into meaningful change.
          </p>
          <Link className="op-primary" to={root + "/projects"}>
            Explore service projects <ArrowUpRight size={16} />
          </Link>
        </div>
        <Leaf className="op-hero-leaf" size={130} />
      </div>
      <div className="op-metrics">
        {[
          ["Upcoming meetings", q.data?.upcomingMeetings, CalendarDays],
          ["Active projects", q.data?.activeProjects, Leaf],
          ["Verified service hours", q.data?.verifiedHours, Clock],
          ["Volunteers engaged", q.data?.volunteersEngaged, Users],
        ].map(([label, value, Icon]: any) => (
          <div className="op-stat" key={label}>
            <Icon size={21} />
            <strong>{value ?? "—"}</strong>
            <span>{label}</span>
          </div>
        ))}
      </div>
      <div className="op-two">
        <section className="op-panel">
          <h2>Your next move</h2>
          {[
            [
              "meetings",
              "Prepare a meeting",
              "Agendas, minutes and quorum",
              CalendarDays,
            ],
            [
              "tasks",
              "Follow through",
              "Assigned actions and due dates",
              ClipboardList,
            ],
            [
              "volunteers",
              "Coordinate service",
              "Shifts and verified hours",
              Users,
            ],
            [
              "documents",
              "Keep knowledge together",
              "Protected records and versions",
              FolderOpen,
            ],
          ].map(([p, t, d, Icon]: any) => (
            <Link className="op-row" to={root + "/" + p} key={p}>
              <Icon />
              <div>
                <strong>{t}</strong>
                <small>{d}</small>
              </div>
              <ArrowUpRight size={17} />
            </Link>
          ))}
        </section>
        <section className="op-panel">
          <h2>Verified impact</h2>
          <p className="op-muted">
            Actual approved outcomes. Estimates are excluded.
          </p>
          {!q.data?.impact?.length ? (
            <Empty>Verified project outcomes will appear here.</Empty>
          ) : (
            q.data.impact.map((m: Row) => (
              <div className="op-outcome" key={m.metricId}>
                <span>
                  {m.name}
                  <small>{m.aggregation}</small>
                </span>
                <strong>
                  {m.value.toLocaleString()} <small>{m.unit}</small>
                </strong>
              </div>
            ))
          )}
        </section>
      </div>
    </>
  );
}
function Meetings() {
  const [month, setMonth] = useState(new Date()),
    [view, setView] = useState("Month"),
    [mine, setMine] = useState(false),
    [create, setCreate] = useState(false),
    [search, setSearch] = useState(""),
    [debounced, setDebounced] = useState("");
  useEffect(() => {
    const t = setTimeout(() => setDebounced(search), 250);
    return () => clearTimeout(t);
  }, [search]);
  const from =
      view === "Week"
        ? new Date(
            month.getFullYear(),
            month.getMonth(),
            month.getDate() - month.getDay(),
          )
        : new Date(month.getFullYear(), month.getMonth(), 1),
    to =
      view === "Week"
        ? new Date(from.getFullYear(), from.getMonth(), from.getDate() + 7)
        : new Date(month.getFullYear(), month.getMonth() + 1, 1);
  const q = useData(
    "/meetings?from=" +
      from.toISOString() +
      "&to=" +
      to.toISOString() +
      "&mine=" +
      mine +
      "&search=" +
      encodeURIComponent(debounced),
  );
  const manager = useManager();
  const days =
    view === "Week"
      ? Array.from({ length: 7 }, (_, i) =>
          new Date(
            from.getFullYear(),
            from.getMonth(),
            from.getDate() + i,
          ).getDate(),
        )
      : Array.from(
          {
            length: new Date(
              month.getFullYear(),
              month.getMonth() + 1,
              0,
            ).getDate(),
          },
          (_, i) => i + 1,
        );
  return (
    <>
      <Header
        title="Meet with purpose."
        description="A shared rhythm for decisions, connection and follow-through."
        action={
          manager && (
            <button className="op-primary" onClick={() => setCreate(true)}>
              <Plus size={17} />
              Schedule meeting
            </button>
          )
        }
      />
      <div className="op-toolbar">
        <div className="op-segment">
          {["Month", "Week", "Agenda"].map((v) => (
            <button
              key={v}
              aria-pressed={view === v}
              onClick={() => setView(v)}
            >
              {v}
            </button>
          ))}
        </div>
        <button
          onClick={() =>
            setMonth(
              view === "Week"
                ? new Date(
                    month.getFullYear(),
                    month.getMonth(),
                    month.getDate() - 7,
                  )
                : new Date(month.getFullYear(), month.getMonth() - 1, 1),
            )
          }
          aria-label="Previous month"
        >
          ‹
        </button>
        <strong>
          {month.toLocaleDateString(undefined, {
            month: "long",
            year: "numeric",
          })}
        </strong>
        <button
          onClick={() =>
            setMonth(
              view === "Week"
                ? new Date(
                    month.getFullYear(),
                    month.getMonth(),
                    month.getDate() + 7,
                  )
                : new Date(month.getFullYear(), month.getMonth() + 1, 1),
            )
          }
          aria-label="Next month"
        >
          ›
        </button>
        <label>
          <input
            type="checkbox"
            checked={mine}
            onChange={(e) => setMine(e.target.checked)}
          />{" "}
          My meetings
        </label>
        <input
          aria-label="Search meetings"
          placeholder="Find a meeting…"
          value={search}
          onChange={(e) => setSearch(e.target.value)}
        />
      </div>
      <ErrorBox error={q.error} />
      {q.isPending ? (
        <Skeleton />
      ) : view === "Agenda" ? (
        <section className="op-panel">
          {q.data?.length ? (
            q.data.map((m: Row) => (
              <Link
                className="op-row"
                key={m.id}
                to={root + "/meetings/" + m.id}
              >
                <CalendarDays />
                <div>
                  <strong>{m.title}</strong>
                  <small>
                    {instant(m.startsAt)} · {m.type}
                  </small>
                </div>
                <Badge>{m.status}</Badge>
              </Link>
            ))
          ) : (
            <Empty>No meetings in this date range.</Empty>
          )}
        </section>
      ) : (
        <div className={"op-calendar " + (view === "Week" ? "week" : "")}>
          <div className="op-weekdays">
            {["Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat"].map((d) => (
              <span key={d}>{d}</span>
            ))}
          </div>
          <div className="op-days">
            {Array.from(
              { length: view === "Week" ? 0 : from.getDay() },
              (_, n) => (
                <div className="op-day faded" key={"blank" + n} />
              ),
            )}
            {days.map((day) => (
              <div className="op-day" key={day}>
                <time>{day}</time>
                {q.data
                  ?.filter((m: Row) => new Date(m.startsAt).getDate() === day)
                  .map((m: Row) => (
                    <Link key={m.id} to={root + "/meetings/" + m.id}>
                      <span>
                        {new Date(m.startsAt).toLocaleTimeString([], {
                          hour: "2-digit",
                          minute: "2-digit",
                        })}
                      </span>
                      {m.title}
                    </Link>
                  ))}
              </div>
            ))}
          </div>
        </div>
      )}
      {create && <MeetingForm close={() => setCreate(false)} />}
    </>
  );
}
function MeetingForm({ close, meeting }: any) {
  const options = useData("/operations/options");
  const ops = useOps();
  const nav = useNavigate();
  return (
    <Modal
      title={meeting ? "Reschedule meeting" : "Schedule a meeting"}
      close={close}
    >
      <form
        className="op-form"
        onSubmit={async (e) => {
          e.preventDefault();
          const f = new FormData(e.currentTarget);
          const body = {
            orgId: f.get("orgId"),
            title: f.get("title"),
            localStartsAt: String(f.get("start")),
            localEndsAt: String(f.get("end")),
            startsAt: iso(String(f.get("start"))),
            endsAt: iso(String(f.get("end"))),
            type: f.get("type"),
            timeZone: f.get("zone"),
            location: f.get("location"),
            onlineUrl: f.get("online"),
            visibility: f.get("visibility"),
            chairId: f.get("chair") || null,
            secretaryId: f.get("secretary") || null,
            requiredPeople: f.getAll("required"),
            votingCategories: f.getAll("votingCategories"),
            eventId: f.get("event") || null,
            sessionId: f.get("session") || null,
            quorumMinimum: Number(f.get("quorum")),
            quorumPercent: Number(f.get("percent")),
            recurrences: Number(f.get("repeat")),
            intervalDays: Number(f.get("interval")),
            version: meeting?.version ?? 1,
          };
          try {
            const m = await ops.run(
              meeting ? "/meetings/" + meeting.id : "/meetings",
              body,
              meeting ? "PUT" : "POST",
            );
            close();
            nav(root + "/meetings/" + m.id);
          } catch {}
        }}
      >
        <Field label="Meeting title">
          <input
            name="title"
            required
            maxLength={200}
            defaultValue={meeting?.title}
          />
        </Field>
        <div className="op-form-grid">
          <Field label="Owning organization">
            <OrganizationPicker name="orgId" initial={meeting?.orgId} />
          </Field>
          <Field label="Meeting type">
            <select name="type" defaultValue={meeting?.type}>
              {options.data?.meetingTypes.map((t: string) => (
                <option key={t}>{t}</option>
              ))}
            </select>
          </Field>
          <Field label="Starts">
            <input
              type="datetime-local"
              name="start"
              required
              defaultValue={meeting?.localStartsAt}
            />
          </Field>
          <Field label="Ends">
            <input
              type="datetime-local"
              name="end"
              required
              defaultValue={meeting?.localEndsAt}
            />
          </Field>
          <Field label="Time zone">
            <input
              name="zone"
              defaultValue={
                meeting?.timeZone ??
                Intl.DateTimeFormat().resolvedOptions().timeZone
              }
            />
          </Field>
          <Field label="Visibility">
            <select name="visibility" defaultValue={meeting?.visibility}>
              <option>Members</option>
              <option>Confidential</option>
            </select>
          </Field>
          <Field label="Chairperson">
            <People name="chair" />
          </Field>
          <Field label="Secretary">
            <People name="secretary" />
          </Field>
          <Field label="Minimum voting participants">
            <input
              type="number"
              min="0"
              name="quorum"
              defaultValue={meeting?.quorumMinimum ?? 1}
            />
          </Field>
          <Field label="Percentage quorum">
            <input
              type="number"
              min="0"
              max="100"
              name="percent"
              defaultValue={meeting?.quorumPercent ?? 0}
            />
          </Field>
          <Field label="Additional occurrences">
            <input
              type="number"
              name="repeat"
              min="0"
              max="52"
              defaultValue="0"
            />
          </Field>
          <Field label="Interval in local days">
            <input
              type="number"
              name="interval"
              min="1"
              max="366"
              defaultValue="7"
            />
          </Field>
        </div>
        <Field label="Location">
          <input name="location" defaultValue={meeting?.location} />
        </Field>
        <Field label="Online meeting URL">
          <input name="online" type="url" defaultValue={meeting?.onlineUrl} />
        </Field>
        <Field label="Required attendees">
          <select
            name="required"
            multiple
            size={5}
            defaultValue={meeting ? JSON.parse(meeting.requiredPeople) : []}
          >
            <Options items={options.data?.people} />
          </select>
        </Field>
        <Field label="Eligible voting categories (empty means all active categories)">
          <select
            name="votingCategories"
            multiple
            size={3}
            defaultValue={meeting ? JSON.parse(meeting.votingCategories) : []}
          >
            <Options items={options.data?.membershipCategories} label="name" />
          </select>
        </Field>
        <details>
          <summary>Link existing event attendance</summary>
          <Field label="Event ID">
            <input name="event" defaultValue={meeting?.eventId} />
          </Field>
          <Field label="Event session ID">
            <input name="session" defaultValue={meeting?.sessionId} />
          </Field>
        </details>
        <ErrorBox error={ops.error} />
        <button className="op-primary" disabled={ops.busy}>
          Save meeting
        </button>
      </form>
    </Modal>
  );
}
function MeetingWorkspace() {
  const { id } = useParams();
  const q = useData("/meetings/" + id);
  const [tab, setTab] = useState("Overview"),
    [edit, setEdit] = useState(false);
  const manager = useManager();
  const ops = useOps();
  if (q.isPending) return <Skeleton />;
  if (!q.data) return <ErrorBox error={q.error} />;
  const m = q.data;
  const actions: Row = {
    Draft: ["Scheduled", "Cancelled"],
    Scheduled: ["In Progress", "Postponed", "Cancelled"],
    Postponed: ["Scheduled", "Cancelled"],
    "In Progress": ["Completed", "Cancelled"],
    Completed: ["Archived"],
  };
  return (
    <>
      <Link className="op-back" to={root + "/meetings"}>
        ← All meetings
      </Link>
      <Header
        title={m.title}
        description={
          instant(m.startsAt) +
          " · " +
          m.timeZone +
          " · " +
          (m.location || "Location to be confirmed")
        }
        action={<Badge>{m.status}</Badge>}
      />
      <div className="op-tabs">
        {[
          "Overview",
          "Agenda",
          "Minutes",
          "Attendance",
          "Tasks",
          "Documents",
          "History",
        ].map((t) => (
          <button key={t} aria-pressed={tab === t} onClick={() => setTab(t)}>
            {t}
          </button>
        ))}
      </div>
      <ErrorBox error={ops.error} />
      {tab === "Overview" && (
        <div className="op-two">
          <section className="op-panel">
            <h2>A purposeful gathering</h2>
            <dl className="op-facts">
              <dt>Meeting type</dt>
              <dd>{m.type}</dd>
              <dt>Privacy</dt>
              <dd>{m.visibility}</dd>
              <dt>Ends</dt>
              <dd>{instant(m.endsAt)}</dd>
              <dt>Attendance</dt>
              <dd>
                {m.eventId
                  ? "Linked event · one attendance source"
                  : "Meeting register"}
              </dd>
            </dl>
            <div className="op-actions">
              <a
                className="op-secondary"
                href={"/api/v1/meetings/" + id + "/calendar"}
              >
                Add to calendar
              </a>
              {m.onlineUrl && (
                <a
                  className="op-primary"
                  href={m.onlineUrl}
                  target="_blank"
                  rel="noreferrer"
                >
                  Join online meeting
                </a>
              )}
            </div>
          </section>
          <section className="op-panel">
            <h2>Meeting controls</h2>
            {manager ? (
              <>
                <button onClick={() => setEdit(true)}>Reschedule / edit</button>
                <div className="op-actions">
                  {actions[m.status]?.map((status: string) => (
                    <button
                      key={status}
                      onClick={() =>
                        ops
                          .run("/meetings/" + id + "/status", {
                            status,
                            version: m.version,
                            reason: "Meeting officer update",
                          })
                          .catch(() => {})
                      }
                    >
                      {status}
                    </button>
                  ))}
                </div>
              </>
            ) : (
              <div className="op-actions">
                {["Accepted", "Tentative", "Declined"].map((status) => (
                  <button
                    key={status}
                    onClick={() =>
                      ops
                        .run("/meetings/" + id + "/rsvp", { status })
                        .catch(() => {})
                    }
                  >
                    {status}
                  </button>
                ))}
              </div>
            )}
            <p className="op-muted">
              Confidential records require explicit participation. Every
              transition is audited.
            </p>
          </section>
        </div>
      )}
      {tab === "Agenda" && <Agenda meeting={m} />}{" "}
      {tab === "Minutes" && <Minutes meeting={m} />}{" "}
      {tab === "Attendance" && <Attendance meeting={m} />}{" "}
      {tab === "Tasks" && (
        <Tasks sourceType="Meeting" sourceId={id} orgId={m.orgId} />
      )}{" "}
      {tab === "Documents" && <Documents operationId={id} />}{" "}
      {tab === "History" && <History id={id} />}{" "}
      {edit && <MeetingForm meeting={m} close={() => setEdit(false)} />}
    </>
  );
}
function Agenda({ meeting: m }: any) {
  const templates = useData("/operations/agenda-templates/" + m.orgId);
  const [templateName, setTemplateName] = useState("");
  const [items, setItems] = useState<Row[]>(JSON.parse(m.agenda)),
    [drag, setDrag] = useState<number | null>(null);
  const ops = useOps();
  const manager = useManager();
  useEffect(() => setItems(JSON.parse(m.agenda)), [m.agenda]);
  function change(n: number, key: string, value: any) {
    setItems((a) => a.map((x, i) => (i === n ? { ...x, [key]: value } : x)));
  }
  return (
    <section className="op-panel">
      <div className="op-section-head">
        <h2>Make room for what matters</h2>
        <Badge>{m.agendaStatus}</Badge>
      </div>
      <p className="op-muted">
        Drag to reorder, or use move buttons. Published revisions are preserved.
      </p>
      {manager && (
        <div className="op-toolbar">
          <select
            aria-label="Load agenda template"
            defaultValue=""
            onChange={(e) => {
              const t = templates.data?.find(
                (x: Row) => x.id === e.target.value,
              );
              if (t) setItems(JSON.parse(t.body));
            }}
          >
            <option value="">Load organization template</option>
            {templates.data?.map((t: Row) => (
              <option key={t.id} value={t.id}>
                {t.name}
              </option>
            ))}
          </select>
          <input
            aria-label="Agenda template name"
            placeholder="Template name"
            value={templateName}
            onChange={(e) => setTemplateName(e.target.value)}
          />
          <button
            disabled={!templateName || !items.length}
            onClick={() =>
              ops
                .run(
                  "/operations/agenda-templates/" +
                    m.orgId +
                    "/" +
                    encodeURIComponent(templateName),
                  { items, version: 1 },
                  "PUT",
                )
                .catch(() => {})
            }
          >
            Save as template
          </button>
        </div>
      )}
      {items.map((item, n) => (
        <div
          className="op-agenda-item"
          key={item.id}
          draggable={manager}
          onDragStart={() => setDrag(n)}
          onDragOver={(e) => e.preventDefault()}
          onDrop={() => {
            if (drag === null) return;
            const a = [...items];
            const [x] = a.splice(drag, 1);
            a.splice(n, 0, x);
            setItems(a);
            setDrag(null);
          }}
        >
          <span className="op-step">{n + 1}</span>
          <div>
            <input
              aria-label={"Agenda title " + (n + 1)}
              readOnly={!manager}
              value={item.title}
              onChange={(e) => change(n, "title", e.target.value)}
            />
            <textarea
              aria-label={"Discussion " + (n + 1)}
              readOnly={!manager}
              value={item.description}
              onChange={(e) => change(n, "description", e.target.value)}
            />
            <div className="op-actions">
              <input
                aria-label={"Duration " + (n + 1)}
                type="number"
                min="0"
                value={item.duration}
                onChange={(e) => change(n, "duration", Number(e.target.value))}
              />{" "}
              min{" "}
              {manager && (
                <>
                  <button
                    disabled={!n}
                    aria-label={"Move item " + (n + 1) + " up"}
                    onClick={() => {
                      const a = [...items];
                      [a[n - 1], a[n]] = [a[n], a[n - 1]];
                      setItems(a);
                    }}
                  >
                    ↑
                  </button>
                  <button
                    onClick={() => setItems((a) => a.filter((_, i) => i !== n))}
                  >
                    Remove
                  </button>
                </>
              )}
            </div>
          </div>
        </div>
      ))}
      {manager && (
        <div className="op-actions">
          <button
            onClick={() =>
              setItems((a) => [
                ...a,
                {
                  id: crypto.randomUUID(),
                  title: "",
                  description: "",
                  duration: 10,
                },
              ])
            }
          >
            Add agenda item
          </button>
          <button
            disabled={ops.busy || m.agendaStatus === "Review"}
            className="op-primary"
            onClick={() =>
              ops
                .run(
                  "/meetings/" + m.id + "/agenda",
                  { items, version: m.version },
                  "PUT",
                )
                .catch(() => {})
            }
          >
            Save agenda
          </button>
          <button
            disabled={ops.busy || m.agendaStatus !== "Draft"}
            onClick={() =>
              ops
                .run("/meetings/" + m.id + "/agenda/submit", {})
                .catch(() => {})
            }
          >
            Submit for approval
          </button>
        </div>
      )}
      <ErrorBox error={ops.error} />
    </section>
  );
}
function Minutes({ meeting: m }: any) {
  const q = useData("/meetings/" + m.id + "/minutes");
  const [text, setText] = useState("");
  const ops = useOps();
  const manager = useManager();
  return (
    <section className="op-panel">
      <h2>A clear record. A shared understanding.</h2>
      <p className="op-muted">
        Record discussion, decisions, resolutions and action owners. Each
        correction creates a new revision.
      </p>
      {manager && (
        <>
          <textarea
            className="op-minutes"
            aria-label="Minutes text"
            value={text}
            onChange={(e) => setText(e.target.value)}
            placeholder="Proceedings, decisions and follow-up actions…"
          />
          <button
            className="op-primary"
            disabled={!text || ops.busy}
            onClick={() =>
              ops
                .run("/meetings/" + m.id + "/minutes", { body: text })
                .then(() => setText(""))
                .catch(() => {})
            }
          >
            Save new minutes revision
          </button>
        </>
      )}
      <ErrorBox error={ops.error} />
      {q.data?.map((v: Row) => (
        <article className="op-minute" key={v.id}>
          <div className="op-section-head">
            <h3>Revision {v.revision}</h3>
            <Badge>{v.status}</Badge>
          </div>
          <p className="op-pre">{v.body}</p>
          <div className="op-actions">
            {manager && v.status === "Draft" && (
              <button
                onClick={() =>
                  ops
                    .run(
                      "/meetings/" + m.id + "/minutes/" + v.id + "/submit",
                      {},
                    )
                    .catch(() => {})
                }
              >
                Submit for review
              </button>
            )}
            {v.status === "Published" && (
              <a
                href={"/api/v1/meetings/" + m.id + "/minutes/" + v.id + "/pdf"}
                className="op-secondary"
              >
                <Download size={16} />
                Approved PDF
              </a>
            )}
          </div>
        </article>
      ))}
    </section>
  );
}
function Attendance({ meeting: m }: any) {
  const q = useData("/meetings/" + m.id + "/attendance");
  const ops = useOps();
  const manager = useManager();
  return (
    <section className="op-panel">
      <h2>Attendance & governing quorum</h2>
      {q.data && (
        <div className="op-quorum">
          <ShieldCheck />
          <div>
            <strong>{q.data.met ? "Quorum met" : "Quorum not yet met"}</strong>
            <p>
              {q.data.votingPresent} eligible voters present · {q.data.required}{" "}
              required · {q.data.eligible} eligible members
            </p>
            <small>
              Required = max(minimum count, ceiling(eligible membership ×
              configured percentage)). Presence alone does not grant voting
              eligibility.
            </small>
          </div>
          <Badge>{q.data.source}</Badge>
        </div>
      )}
      <ErrorBox error={q.error} />
      {m.eventId ? (
        <Link to={"/app/events/" + m.eventId}>
          Open authoritative event attendance →
        </Link>
      ) : (
        manager && (
          <form
            className="op-inline-form"
            onSubmit={async (e) => {
              e.preventDefault();
              const f = new FormData(e.currentTarget);
              try {
                await ops.run("/meetings/" + m.id + "/attendance", {
                  personId: f.get("person"),
                  status: f.get("status"),
                  reason: f.get("reason"),
                });
              } catch {}
            }}
          >
            <Field label="Member">
              <People name="person" required />
            </Field>
            <Field label="Attendance">
              <select name="status">
                {[
                  "Expected",
                  "Present",
                  "Absent",
                  "Excused",
                  "Late",
                  "Online",
                  "Guest",
                ].map((x) => (
                  <option key={x}>{x}</option>
                ))}
              </select>
            </Field>
            <Field label="Correction reason">
              <input name="reason" placeholder="Required for corrections" />
            </Field>
            <button className="op-primary">Record</button>
          </form>
        )
      )}
      <ErrorBox error={ops.error} />
      <div className="op-table-wrap">
        <table>
          <thead>
            <tr>
              <th>Participant / registration</th>
              <th>Status / check-in</th>
              <th>Mode</th>
            </tr>
          </thead>
          <tbody>
            {q.data?.rows.map((r: Row) => (
              <tr key={r.id}>
                <td>{r.personName || "Event participant"}</td>
                <td>{r.status ?? instant(r.at)}</td>
                <td>{r.mode ?? "Event"}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </section>
  );
}
function History({ id }: any) {
  const q = useData("/operations/history/" + id);
  return (
    <section className="op-panel">
      <h2>Preserved history</h2>
      <ErrorBox error={q.error} />
      {q.data?.map((r: Row) => (
        <div className="op-history" key={r.id}>
          <Badge>{r.kind}</Badge>
          <time>{instant(r.updatedAt)}</time>
          <details>
            <summary>{r.status || "Revision"}</summary>
            <pre>{r.body}</pre>
          </details>
        </div>
      ))}
    </section>
  );
}

function Tasks({ sourceType = "Administrative", sourceId, orgId }: any) {
  const [mine, setMine] = useState(false),
    [status, setStatus] = useState(""),
    [create, setCreate] = useState(false),
    [selected, setSelected] = useState<Row | null>(null);
  const q = useData(
    "/tasks?mine=" +
      mine +
      (status ? "&status=" + encodeURIComponent(status) : "") +
      (sourceId ? "&sourceId=" + sourceId : ""),
  );
  const ops = useOps(),
    manager = useManager();
  return (
    <>
      <Header
        title="Good intentions. Clear next steps."
        description="Keep every action connected to its source and its owner."
        action={
          manager && (
            <button className="op-primary" onClick={() => setCreate(true)}>
              <Plus size={17} />
              Create task
            </button>
          )
        }
      />
      <div className="op-toolbar">
        <label>
          <input
            type="checkbox"
            checked={mine}
            onChange={(e) => setMine(e.target.checked)}
          />
          My tasks
        </label>
        <select
          aria-label="Task status"
          value={status}
          onChange={(e) => setStatus(e.target.value)}
        >
          <option value="">All statuses</option>
          {["To Do", "In Progress", "Blocked", "Completed", "Cancelled"].map(
            (t) => (
              <option key={t}>{t}</option>
            ),
          )}
        </select>
      </div>
      <ErrorBox error={q.error} />
      <div className="op-task-grid">
        {q.data?.map((t: Row) => (
          <button
            className="op-task-card"
            key={t.id}
            onClick={() => setSelected(t)}
          >
            <div className="op-section-head">
              <Badge>{t.status}</Badge>
              <span>{t.priority}</span>
            </div>
            <h3>{t.title}</h3>
            <p>{t.description}</p>
            <div className="op-task-footer">
              <span>{t.sourceType}</span>
              <span>Due {date(t.dueAt)}</span>
            </div>
          </button>
        ))}
      </div>
      {!q.data?.length && (
        <Empty>Tasks will appear here when actions are assigned.</Empty>
      )}
      {create && (
        <TaskForm
          sourceType={sourceType}
          sourceId={sourceId}
          orgId={orgId}
          close={() => setCreate(false)}
        />
      )}{" "}
      {selected && (
        <Modal title={selected.title} close={() => setSelected(null)}>
          <p>{selected.description}</p>
          <Badge>{selected.status}</Badge>
          <form
            className="op-form"
            onSubmit={async (e) => {
              e.preventDefault();
              const f = new FormData(e.currentTarget);
              try {
                await ops.run("/tasks/" + selected.id + "/status", {
                  status: f.get("status"),
                  reason: f.get("reason"),
                  version: selected.version,
                });
                setSelected(null);
              } catch {}
            }}
          >
            <Field label="Status">
              <select name="status" defaultValue={selected.status}>
                {[
                  "To Do",
                  "In Progress",
                  "Blocked",
                  "Completed",
                  "Cancelled",
                ].map((t) => (
                  <option key={t}>{t}</option>
                ))}
              </select>
            </Field>
            <Field label="Blocker / completion evidence">
              <textarea
                name="reason"
                defaultValue={selected.blocker || selected.evidence}
              />
            </Field>
            <button className="op-primary">Update task</button>
          </form>
          <ErrorBox error={ops.error} />
          <TaskComments id={selected.id} />
        </Modal>
      )}
    </>
  );
}
function TaskComments({ id }: any) {
  const q = useData("/tasks/" + id),
    ops = useOps();
  return (
    <>
      <h3>Discussion & evidence</h3>
      {q.data?.comments.map((c: Row) => (
        <p className="op-comment" key={c.id}>
          {c.body}
          <small>{instant(c.updatedAt)}</small>
        </p>
      ))}
      <form
        className="op-inline-form"
        onSubmit={async (e) => {
          e.preventDefault();
          const form = e.currentTarget;
          const f = new FormData(form);
          try {
            await ops.run("/tasks/" + id + "/comments", {
              body: f.get("body"),
            });
            form.reset();
          } catch {}
        }}
      >
        <input
          name="body"
          required
          placeholder="Add a comment…"
          aria-label="Task comment"
        />
        <button>Post</button>
      </form>
      <ErrorBox error={ops.error} />
    </>
  );
}
function TaskForm({ close, sourceType, sourceId, orgId }: any) {
  const { w } = useWorkspace();
  const ops = useOps();
  return (
    <Modal title="Create an action item" close={close}>
      <form
        className="op-form"
        onSubmit={async (e) => {
          e.preventDefault();
          const f = new FormData(e.currentTarget);
          try {
            await ops.run("/tasks", {
              orgId: orgId ?? w?.orgId,
              title: f.get("title"),
              description: f.get("description"),
              sourceType,
              sourceId: sourceId ?? null,
              personId: f.get("person") || null,
              priority: f.get("priority"),
              dueAt: f.get("due") ? iso(String(f.get("due"))) : null,
            });
            close();
          } catch {}
        }}
      >
        <Field label="Title">
          <input required name="title" />
        </Field>
        <Field label="Description">
          <textarea name="description" />
        </Field>
        <Field label="Assign to">
          <People name="person" />
        </Field>
        <div className="op-form-grid">
          <Field label="Priority">
            <select name="priority">
              {["Normal", "High", "Urgent", "Low"].map((x) => (
                <option key={x}>{x}</option>
              ))}
            </select>
          </Field>
          <Field label="Due">
            <input name="due" type="datetime-local" />
          </Field>
        </div>
        <ErrorBox error={ops.error} />
        <button className="op-primary" disabled={ops.busy}>
          Create task
        </button>
      </form>
    </Modal>
  );
}
function Projects() {
  const q = useData("/projects"),
    [create, setCreate] = useState(false);
  const manager = useManager();
  return (
    <>
      <Header
        title="Serve together. Change lives."
        description="One shared project, many helping hands, a single trusted record of impact."
        action={
          manager && (
            <button className="op-primary" onClick={() => setCreate(true)}>
              <Plus size={17} />
              Propose a project
            </button>
          )
        }
      />
      <ErrorBox error={q.error} />
      <div className="op-project-grid">
        {q.data?.map((p: Row) => (
          <Link
            className="op-project-card"
            key={p.id}
            to={root + "/projects/" + p.id}
          >
            <div className="op-project-art">
              <Leaf size={42} />
              <Badge>{p.category}</Badge>
            </div>
            <div>
              <div className="op-section-head">
                <Badge>{p.status}</Badge>
                <ArrowUpRight size={18} />
              </div>
              <h2>{p.name}</h2>
              <p>
                {date(p.startsAt)} – {date(p.endsAt)}
              </p>
              <div className="op-task-footer">
                <span>
                  <Users size={15} /> {p.plannedVolunteers} planned volunteers
                </span>
                <span>{p.targetBeneficiaries} target beneficiaries</span>
              </div>
            </div>
          </Link>
        ))}
      </div>
      {!q.data?.length && (
        <Empty>Propose your first service project to get started.</Empty>
      )}
      {create && <ProjectForm close={() => setCreate(false)} />}
    </>
  );
}
function ProjectForm({ close }: any) {
  const options = useData("/operations/options"),
    ops = useOps(),
    nav = useNavigate();
  return (
    <Modal title="Propose a service project" close={close}>
      <form
        className="op-form"
        onSubmit={async (e) => {
          e.preventDefault();
          const f = new FormData(e.currentTarget);
          try {
            const p = await ops.run("/projects", {
              orgId: f.get("org"),
              name: f.get("name"),
              managerId: f.get("manager"),
              category: f.get("category"),
              description: f.get("description"),
              objectives: f.get("objectives"),
              location: f.get("location"),
              startsAt: iso(String(f.get("start"))),
              endsAt: iso(String(f.get("end"))),
              targetBeneficiaries: Number(f.get("target")),
              plannedVolunteers: Number(f.get("volunteers")),
              plannedHours: Number(f.get("hours")),
              estimatedBudget: Number(f.get("budget")),
              currency: f.get("currency"),
              participatingClubs: f.getAll("clubs"),
            });
            close();
            nav(root + "/projects/" + p.id);
          } catch {}
        }}
      >
        <Field label="Project name">
          <input name="name" required maxLength={200} />
        </Field>
        <div className="op-form-grid">
          <Field label="Lead organization">
            <OrganizationPicker name="org" />
          </Field>
          <Field label="Category">
            <select name="category">
              {options.data?.projectCategories.map((x: string) => (
                <option key={x}>{x}</option>
              ))}
            </select>
          </Field>
          <Field label="Project manager">
            <People name="manager" required />
          </Field>
          <Field label="Location">
            <input name="location" />
          </Field>
          <Field label="Starts">
            <input name="start" type="datetime-local" required />
          </Field>
          <Field label="Ends">
            <input name="end" type="datetime-local" required />
          </Field>
        </div>
        <Field label="Description">
          <textarea name="description" />
        </Field>
        <Field label="Objectives">
          <textarea name="objectives" />
        </Field>
        <Field label="Participating organizations">
          <select name="clubs" multiple size={4}>
            <Options items={options.data?.organizations} label="name" />
          </select>
        </Field>
        <div className="op-form-grid">
          {[
            ["target", "Target beneficiaries"],
            ["volunteers", "Planned volunteers"],
            ["hours", "Planned service hours"],
            ["budget", "Estimated budget"],
          ].map(([name, label]) => (
            <Field key={name} label={label}>
              <input
                name={name}
                type="number"
                min="0"
                step="0.01"
                defaultValue="0"
              />
            </Field>
          ))}
          <Field label="Currency">
            <input name="currency" defaultValue="USD" maxLength={3} />
          </Field>
        </div>
        <ErrorBox error={ops.error} />
        <button className="op-primary" disabled={ops.busy}>
          Save project proposal
        </button>
      </form>
    </Modal>
  );
}
function ProjectWorkspace() {
  const { id } = useParams(),
    q = useData("/projects/" + id),
    [tab, setTab] = useState("Overview"),
    [shift, setShift] = useState(false);
  const ops = useOps(),
    manager = useManager();
  if (q.isPending) return <Skeleton />;
  if (!q.data) return <ErrorBox error={q.error} />;
  const p = q.data.project ?? q.data.summary;
  const transitions: Row = {
    Approved: ["Planning", "Cancelled"],
    Planning: ["In Progress", "On Hold", "Cancelled"],
    "In Progress": ["Completed", "On Hold", "Cancelled"],
    "On Hold": ["Planning", "In Progress", "Cancelled"],
    Completed: ["Impact Verified"],
    "Impact Verified": ["Archived"],
  };
  return (
    <>
      <Link className="op-back" to={root + "/projects"}>
        ← All projects
      </Link>
      <Header
        title={p.name}
        description={
          p.category + " · " + date(p.startsAt) + " – " + date(p.endsAt)
        }
        action={<Badge>{p.status}</Badge>}
      />
      <div className="op-tabs">
        {[
          "Overview",
          "Planning",
          "Tasks",
          "Volunteers",
          "Activities",
          "Budget",
          "Documents",
          "Impact",
          "Reports",
          "History",
        ].map((t) => (
          <button key={t} aria-pressed={tab === t} onClick={() => setTab(t)}>
            {t}
          </button>
        ))}
      </div>
      <ErrorBox error={ops.error} />
      {["Overview", "Planning"].includes(tab) && (
        <div className="op-two">
          <section className="op-panel">
            <h2>Our shared purpose</h2>
            <p className="op-pre">
              {p.description || "Project description has not been added."}
            </p>
            <h3>Objectives</h3>
            <p>{p.objectives}</p>
            <h3>Participating organizations</h3>
            {q.data.organizations.map((o: Row) => (
              <div className="op-comment" key={o.id}>
                {o.organizationName ?? "Participating organization"}
                <small>{o.contribution || "Joint project participant"}</small>
              </div>
            ))}
          </section>
          <section className="op-panel">
            <h2>Move the project forward</h2>
            <dl className="op-facts">
              <dt>Planned volunteers</dt>
              <dd>{p.plannedVolunteers ?? "Summary only"}</dd>
              <dt>Target beneficiaries</dt>
              <dd>{p.targetBeneficiaries ?? "Summary only"}</dd>
              <dt>Planned hours</dt>
              <dd>{p.plannedHours ?? "Summary only"}</dd>
            </dl>
            {manager && q.data.project && (
              <div className="op-actions">
                {p.status === "Draft" && (
                  <button
                    className="op-primary"
                    onClick={() =>
                      ops.run("/projects/" + id + "/submit", {}).catch(() => {})
                    }
                  >
                    Submit proposal for approval
                  </button>
                )}
                {transitions[p.status]?.map((status: string) => (
                  <button
                    key={status}
                    onClick={() =>
                      ops
                        .run("/projects/" + id + "/status", {
                          status,
                          version: p.version,
                          reason: "Project progress update",
                        })
                        .catch(() => {})
                    }
                  >
                    {status}
                  </button>
                ))}
              </div>
            )}
            <p className="op-muted">
              Project approval and impact verification are separate decisions.
            </p>
          </section>
        </div>
      )}
      {tab === "Tasks" && q.data.project && (
        <Tasks sourceType="Project" sourceId={id} orgId={p.orgId} />
      )}{" "}
      {tab === "Volunteers" && (
        <>
          <div className="op-actions">
            {manager && q.data.project && (
              <button className="op-primary" onClick={() => setShift(true)}>
                Create volunteer shift
              </button>
            )}
          </div>
          <Volunteers projectId={id} />
        </>
      )}{" "}
      {tab === "Activities" && (
        <section className="op-panel">
          <h2>Activities</h2>
          {q.data.activities.map((a: Row) => (
            <div className="op-row" key={a.id}>
              <CalendarDays />
              <div>
                <strong>{a.title}</strong>
                <small>
                  {instant(a.startsAt)} – {instant(a.endsAt)}
                </small>
              </div>
              <Badge>{a.status}</Badge>
            </div>
          ))}
          {manager && q.data.project && (
            <form
              className="op-form"
              onSubmit={async (e) => {
                e.preventDefault();
                const form = e.currentTarget,
                  f = new FormData(form);
                try {
                  await ops.run("/projects/" + id + "/activities", {
                    title: f.get("title"),
                    description: f.get("description"),
                    startsAt: iso(String(f.get("start"))),
                    endsAt: iso(String(f.get("end"))),
                  });
                  form.reset();
                } catch {}
              }}
            >
              <Field label="Activity title">
                <input name="title" required />
              </Field>
              <div className="op-form-grid">
                <Field label="Starts">
                  <input name="start" type="datetime-local" required />
                </Field>
                <Field label="Ends">
                  <input name="end" type="datetime-local" required />
                </Field>
              </div>
              <Field label="Activity description">
                <textarea name="description" />
              </Field>
              <button>Add activity</button>
            </form>
          )}
        </section>
      )}
      {tab === "Budget" && (
        <section className="op-panel">
          <h2>Planning estimate</h2>
          <strong className="op-budget">
            {p.currency} {p.estimatedBudget?.toLocaleString() ?? "Restricted"}
          </strong>
          <p>
            Estimates are planning figures. Payments, expense approval and
            ledger accounting belong to Phase 7.
          </p>
        </section>
      )}
      {tab === "Documents" && <Documents operationId={id} />}{" "}
      {["Impact", "Reports"].includes(tab) && <Impact projectId={id} />}{" "}
      {tab === "History" && <History id={id} />}{" "}
      {shift && <ShiftForm projectId={id} close={() => setShift(false)} />}
    </>
  );
}
function ShiftForm({ projectId, close }: any) {
  const ops = useOps();
  return (
    <Modal title="Create a volunteer shift" close={close}>
      <form
        className="op-form"
        onSubmit={async (e) => {
          e.preventDefault();
          const f = new FormData(e.currentTarget);
          try {
            await ops.run("/volunteers/shifts", {
              projectId,
              title: f.get("title"),
              startsAt: iso(String(f.get("start"))),
              endsAt: iso(String(f.get("end"))),
              deadline: iso(String(f.get("deadline"))),
              supervisorId: f.get("supervisor"),
              capacity: Number(f.get("capacity")),
              requiredTraining: f.get("training"),
              location: f.get("location"),
              approvalRequired: true,
            });
            close();
          } catch {}
        }}
      >
        <Field label="Shift / role">
          <input name="title" required />
        </Field>
        <div className="op-form-grid">
          {[
            ["start", "Starts"],
            ["end", "Ends"],
            ["deadline", "Registration deadline"],
          ].map(([n, t]) => (
            <Field key={n} label={t}>
              <input type="datetime-local" name={n} required />
            </Field>
          ))}
          <Field label="Capacity">
            <input name="capacity" type="number" min="1" defaultValue="10" />
          </Field>
          <Field label="Supervisor">
            <People name="supervisor" required />
          </Field>
          <Field label="Required training">
            <input name="training" />
          </Field>
          <Field label="Location">
            <input name="location" />
          </Field>
        </div>
        <ErrorBox error={ops.error} />
        <button className="op-primary">Create shift</button>
      </form>
    </Modal>
  );
}
function Volunteers({ projectId }: any) {
  const [breaks, setBreaks] = useState<Record<string, number>>({});
  const [tab, setTab] = useState("Opportunities"),
    [profile, setProfile] = useState(false);
  const opportunities = useData(
      "/volunteers/opportunities" +
        (projectId ? "?projectId=" + projectId : ""),
    ),
    assignments = useData("/volunteers/assignments"),
    hours = useData(
      "/volunteers/hours" + (projectId ? "?projectId=" + projectId : ""),
    ),
    profiles = useData("/volunteers");
  const ops = useOps(),
    manager = useManager(),
    { w } = useWorkspace();
  return (
    <>
      <Header
        title="Many hands. One community."
        description="Find a role, show up with purpose and record service with care."
        action={
          <button onClick={() => setProfile(true)}>My volunteer profile</button>
        }
      />
      <div className="op-tabs">
        {["Opportunities", "Assignments", "Hours", "Directory"].map((t) => (
          <button key={t} aria-pressed={tab === t} onClick={() => setTab(t)}>
            {t}
          </button>
        ))}
      </div>
      <ErrorBox error={ops.error} />
      {tab === "Opportunities" && (
        <div className="op-project-grid">
          {opportunities.data?.map((row: Row) => {
            const s = row.shift;
            return (
              <article className="op-panel" key={s.id}>
                <Badge>
                  {row.assigned >= s.capacity
                    ? "Waiting list"
                    : "Open opportunity"}
                </Badge>
                <h2>{s.title}</h2>
                <p>
                  {instant(s.startsAt)} – {instant(s.endsAt)}
                </p>
                <p>
                  {s.location} · {row.assigned}/{s.capacity} assigned
                </p>
                <p className="op-muted">
                  Training: {s.requiredTraining || "No special training"}
                </p>
                <button
                  className="op-primary"
                  onClick={() =>
                    ops
                      .run("/volunteers/shifts/" + s.id + "/apply", {})
                      .catch(() => {})
                  }
                >
                  Register interest
                </button>
              </article>
            );
          })}
          {!opportunities.data?.length && (
            <Empty>
              No approved project opportunities are currently available.
            </Empty>
          )}
        </div>
      )}
      {tab === "Assignments" && (
        <section className="op-panel">
          {assignments.data?.map((a: Row) => (
            <div className="op-minute" key={a.id}>
              <div className="op-section-head">
                <strong>{a.personName || "Volunteer"}</strong>
                <Badge>{a.status}</Badge>
              </div>
              <small>Shift {a.shiftId}</small>
              <div className="op-actions">
                {manager &&
                  ["Applied", "Waiting"].includes(a.status) &&
                  ["Approved", "Rejected"].map((status) => (
                    <button
                      key={status}
                      onClick={() =>
                        ops
                          .run("/volunteers/assignments/" + a.id + "/status", {
                            status,
                            version: a.version,
                          })
                          .catch(() => {})
                      }
                    >
                      {status}
                    </button>
                  ))}
                {a.status === "Approved" && (
                  <button
                    onClick={() =>
                      ops
                        .run("/volunteers/assignments/" + a.id + "/status", {
                          status: "Confirmed",
                          version: a.version,
                        })
                        .catch(() => {})
                    }
                  >
                    Confirm
                  </button>
                )}
                {manager && a.checkIn && !a.checkOut && (
                  <label>
                    Break minutes{" "}
                    <input
                      aria-label={"Break minutes " + a.id}
                      type="number"
                      min="0"
                      value={breaks[a.id] ?? 0}
                      onChange={(e) =>
                        setBreaks((b) => ({
                          ...b,
                          [a.id]: Number(e.target.value),
                        }))
                      }
                    />
                  </label>
                )}
                {manager &&
                  ["Approved", "Confirmed", "Present"].includes(a.status) && (
                    <button
                      onClick={() =>
                        ops
                          .run(
                            "/volunteers/assignments/" + a.id + "/attendance",
                            {
                              action: a.checkIn ? "CheckOut" : "CheckIn",
                              breakMinutes: breaks[a.id] ?? 0,
                            },
                          )
                          .catch(() => {})
                      }
                    >
                      {a.checkIn ? "Check out" : "Check in"}
                    </button>
                  )}
              </div>
            </div>
          ))}
        </section>
      )}
      {tab === "Hours" && (
        <section className="op-panel">
          <h2>Service hours, verified independently</h2>
          <HourClaimForm assignments={assignments.data ?? []} />
          <p className="op-muted">
            Scheduled duration is never counted as actual service. Verified
            records alone feed achievements and impact totals.
          </p>
          {hours.data?.map((h: Row) => (
            <div className="op-row" key={h.id}>
              <Clock />
              <div>
                <strong>{h.hours} hours</strong>
                <small>
                  {instant(h.startsAt)} – {instant(h.endsAt)} · {h.reason}
                </small>
              </div>
              <Badge>{h.status}</Badge>
              {w?.permissions.includes("operations.verify") &&
                h.status === "Submitted" && (
                  <button
                    onClick={() =>
                      ops
                        .run("/volunteers/hours/" + h.id + "/verify", {
                          status: "Verified",
                          version: h.version,
                          reason: "Independent review",
                        })
                        .catch(() => {})
                    }
                  >
                    Verify
                  </button>
                )}
            </div>
          ))}
        </section>
      )}
      {tab === "Directory" && (
        <section className="op-panel">
          {profiles.data?.map((p: Row) => (
            <div className="op-row" key={p.id}>
              <Users />
              <div>
                <strong>{p.personName || "Volunteer"}</strong>
                <small>
                  {p.skills} · {p.interests}
                </small>
                <small>Availability: {p.availability}</small>
              </div>
            </div>
          ))}
        </section>
      )}
      {profile && (
        <Modal title="My volunteer profile" close={() => setProfile(false)}>
          <form
            className="op-form"
            onSubmit={async (e) => {
              e.preventDefault();
              const f = new FormData(e.currentTarget);
              try {
                await ops.run("/volunteers", {
                  personId: f.get("person"),
                  skills: f.get("skills"),
                  interests: f.get("interests"),
                  availability: f.get("availability"),
                  training: f.get("training"),
                });
                setProfile(false);
              } catch {}
            }}
          >
            <Field label="Your person record">
              <People name="person" required />
            </Field>
            {["skills", "interests", "availability", "training"].map((n) => (
              <Field key={n} label={n}>
                <textarea name={n} />
              </Field>
            ))}
            <ErrorBox error={ops.error} />
            <button className="op-primary">Save profile</button>
          </form>
        </Modal>
      )}
    </>
  );
}
function Impact({ projectId }: any) {
  const q = useData("/operations/analytics"),
    records = useData(
      projectId ? "/projects/" + projectId + "/impact" : "/impact-metrics",
    ),
    metrics = useData("/impact-metrics"),
    [create, setCreate] = useState(false),
    ops = useOps(),
    { w } = useWorkspace(),
    manager = useManager();
  return (
    <>
      <Header
        title="The difference we make."
        description="Configurable measures. Evidence-backed outcomes. A single count for joint projects."
        action={
          manager && (
            <button className="op-primary" onClick={() => setCreate(true)}>
              {projectId ? "Report an outcome" : "Define an impact metric"}
            </button>
          )
        }
      />
      {projectId && (
        <p className="op-muted">
          Workspace totals cover all authorized projects. This project’s
          outcomes are listed below.
        </p>
      )}
      <div className="op-metrics">
        <div className="op-stat">
          <strong>{q.data?.completedProjects ?? "—"}</strong>
          <span>Completed projects</span>
        </div>
        <div className="op-stat">
          <strong>{q.data?.jointProjects ?? "—"}</strong>
          <span>Joint projects</span>
        </div>
        <div className="op-stat">
          <strong>{q.data?.verifiedHours ?? "—"}</strong>
          <span>Verified hours</span>
        </div>
        <div className="op-stat">
          <strong>{q.data?.pendingImpact ?? "—"}</strong>
          <span>Outcomes awaiting review</span>
        </div>
      </div>
      <section className="op-panel">
        <h2>
          {projectId ? "Project outcomes" : "Verified community outcomes"}
        </h2>
        {projectId
          ? records.data?.map((r: Row) => (
              <div className="op-row" key={r.id}>
                <Leaf />
                <div>
                  <strong>
                    {metrics.data?.find((m: Row) => m.id === r.metricId)
                      ?.name ?? "Outcome"}
                    : {r.value}
                  </strong>
                  <small>{r.evidence}</small>
                </div>
                <Badge>{r.estimate ? "Estimate" : r.status}</Badge>
              </div>
            ))
          : q.data?.impact.map((m: Row) => (
              <div className="op-outcome" key={m.metricId}>
                <strong>{m.name}</strong>
                <span>
                  {m.value.toLocaleString()} {m.unit}
                </span>
                <Badge>{m.aggregation}</Badge>
              </div>
            ))}
      </section>
      {create && (
        <Modal
          title={projectId ? "Submit project impact" : "Define a metric"}
          close={() => setCreate(false)}
        >
          <form
            className="op-form"
            onSubmit={async (e) => {
              e.preventDefault();
              const f = new FormData(e.currentTarget);
              try {
                if (projectId)
                  await ops.run("/projects/" + projectId + "/impact", {
                    metricId: f.get("metric"),
                    sourceKey: f.get("key"),
                    value: Number(f.get("value")),
                    evidence: f.get("evidence"),
                    estimate: f.get("estimate") === "on",
                  });
                else
                  await ops.run("/impact-metrics", {
                    orgId: w?.orgId,
                    name: f.get("name"),
                    unit: f.get("unit"),
                    aggregation: f.get("aggregation"),
                    evidenceRequired: true,
                  });
                setCreate(false);
              } catch {}
            }}
          >
            {projectId ? (
              <>
                <Field label="Metric">
                  <select name="metric">
                    <Options items={metrics.data} label="name" />
                  </select>
                </Field>
                <Field label="Source / activity key (deduplicates joint project)">
                  <input name="key" required />
                </Field>
                <Field label="Value">
                  <input
                    name="value"
                    type="number"
                    min="0"
                    step="0.01"
                    required
                  />
                </Field>
                <Field label="Evidence / protected document reference">
                  <textarea name="evidence" required />
                </Field>
                <label>
                  <input type="checkbox" name="estimate" />
                  This is an estimate, not verified actual impact
                </label>
              </>
            ) : (
              <>
                <Field label="Metric name">
                  <input name="name" required />
                </Field>
                <Field label="Unit">
                  <input name="unit" defaultValue="people" />
                </Field>
                <Field label="Aggregation">
                  <select name="aggregation">
                    {["Sum", "Maximum", "Average", "Count"].map((x) => (
                      <option key={x}>{x}</option>
                    ))}
                  </select>
                </Field>
              </>
            )}
            <ErrorBox error={ops.error} />
            <button className="op-primary">
              {projectId
                ? "Submit for independent verification"
                : "Save metric"}
            </button>
          </form>
        </Modal>
      )}
    </>
  );
}

function Documents({ operationId }: any) {
  const [search, setSearch] = useState(""),
    [upload, setUpload] = useState(false),
    [selected, setSelected] = useState<Row | null>(null),
    [error, setError] = useState<unknown>();
  const q = useData(
      "/documents?search=" +
        encodeURIComponent(search) +
        (operationId ? "&operationId=" + operationId : ""),
    ),
    ops = useOps(),
    { w } = useWorkspace();
  const can = w?.permissions.includes("operations.documents");
  async function uploadFile(file: File, fields: Row) {
    const f = new FormData();
    f.set("file", file);
    Object.entries(fields).forEach(([k, v]) => f.set(k, String(v)));
    return await apiForm("/documents", f);
  }
  return (
    <>
      <Header
        title="Knowledge, kept with care."
        description="Protected documents, preserved versions and deliberate sharing."
        action={
          can && (
            <button className="op-primary" onClick={() => setUpload(true)}>
              <Plus size={16} />
              Upload documents
            </button>
          )
        }
      />
      <div className="op-toolbar">
        <input
          aria-label="Search documents"
          value={search}
          onChange={(e) => setSearch(e.target.value)}
          placeholder="Search titles or tags…"
        />
        <span className="op-muted">
          PDF, PNG and JPEG · maximum 5MB per file
        </span>
      </div>
      <ErrorBox error={q.error ?? error} />
      <section
        className="op-panel"
        onDragOver={(e) => {
          if (can) e.preventDefault();
        }}
        onDrop={async (e) => {
          if (!can) return;
          e.preventDefault();
          setError(undefined);
          try {
            for (const file of Array.from(e.dataTransfer.files))
              await uploadFile(file, {
                orgId: w?.orgId,
                title: file.name,
                visibility: "Private",
                operationId: operationId ?? "",
                folder: "General",
              });
            await ops.qc.invalidateQueries({ queryKey: ["operations"] });
          } catch (err) {
            setError(err);
          }
        }}
      >
        {q.data?.length ? (
          q.data.map((d: Row) => (
            <button
              className="op-document-row"
              key={d.id}
              onClick={() => setSelected(d)}
            >
              <FolderOpen size={22} />
              <div>
                <strong>{d.title}</strong>
                <small>
                  {d.folder} · {d.fileName} · version {d.currentVersion}
                </small>
              </div>
              <Badge>{d.visibility}</Badge>
              <ArrowUpRight size={17} />
            </button>
          ))
        ) : (
          <Empty>
            Upload a document or drop files here. Dropped files start private.
          </Empty>
        )}
      </section>
      {upload && (
        <Modal
          title="Upload to the document vault"
          close={() => setUpload(false)}
        >
          <form
            className="op-form"
            onSubmit={async (e) => {
              e.preventDefault();
              const f = new FormData(e.currentTarget);
              setError(undefined);
              try {
                for (const file of f.getAll("files") as File[])
                  await uploadFile(file, {
                    orgId: w?.orgId,
                    title: f.get("title") || file.name,
                    folder: f.get("folder"),
                    visibility: f.get("visibility"),
                    kind: f.get("kind"),
                    operationId: operationId ?? "",
                    retainUntil: f.get("expiry")
                      ? iso(String(f.get("expiry")))
                      : "",
                  });
                await ops.qc.invalidateQueries({ queryKey: ["operations"] });
                setUpload(false);
              } catch (err) {
                setError(err);
              }
            }}
          >
            <Field label="Files">
              <input
                name="files"
                type="file"
                multiple
                accept="application/pdf,image/png,image/jpeg"
                required
              />
            </Field>
            <Field label="Title (leave blank to use filenames)">
              <input name="title" />
            </Field>
            <div className="op-form-grid">
              <Field label="Folder">
                <input name="folder" defaultValue="General" />
              </Field>
              <Field label="Category">
                <select name="kind">
                  {[
                    "Policy",
                    "Meeting minutes",
                    "Project document",
                    "Volunteer form",
                    "Certificate",
                    "Report",
                    "Miscellaneous",
                  ].map((x) => (
                    <option key={x}>{x}</option>
                  ))}
                </select>
              </Field>
              <Field label="Visibility">
                <select name="visibility">
                  <option>Private</option>
                  <option>Organization</option>
                </select>
              </Field>
              <Field label="Retention expiry">
                <input type="datetime-local" name="expiry" />
              </Field>
            </div>
            <ErrorBox error={error} />
            <button className="op-primary">Upload files</button>
          </form>
        </Modal>
      )}
      {selected && (
        <DocumentPreview record={selected} close={() => setSelected(null)} />
      )}
    </>
  );
}
function DocumentPreview({ record, close }: any) {
  const options = useData("/operations/options");
  const q = useData("/documents/" + record.id),
    ops = useOps(),
    [version, setVersion] = useState(record.currentVersion),
    [error, setError] = useState<unknown>();
  const { w } = useWorkspace();
  const link =
    "/api/v1/documents/" + record.id + "/download?version=" + version;
  return (
    <Modal title={record.title} close={close}>
      <div className="op-actions">
        <select
          aria-label="Document version"
          value={version}
          onChange={(e) => setVersion(Number(e.target.value))}
        >
          {q.data?.document.legacy && (
            <option value={record.currentVersion}>
              Original governance record
            </option>
          )}
          {q.data?.versions.map((v: Row) => (
            <option key={v.id} value={v.number}>
              Version {v.number} · {date(v.updatedAt)}
            </option>
          ))}
        </select>
        <a className="op-secondary" href={link}>
          <Download size={16} />
          Download
        </a>
      </div>
      {record.contentType === "application/pdf" ? (
        <iframe
          className="op-file-preview"
          title="Protected document preview"
          src={link + "&preview=true"}
        />
      ) : (
        <img className="op-image-preview" alt={record.title} src={link} />
      )}
      <ErrorBox error={error ?? ops.error} />
      {q.data?.document.legacy && (
        <p className="op-muted">
          This original retains its governance access rules.{" "}
          <Link to="/app/governance-documents">Open Governance documents</Link>{" "}
          to manage its workflow.
        </p>
      )}
      {w?.permissions.includes("operations.documents") &&
        q.data &&
        !q.data.document.legacy && (
          <>
            <form
              className="op-inline-form"
              onSubmit={async (e) => {
                e.preventDefault();
                try {
                  const f = new FormData(e.currentTarget);
                  f.set("version", String(q.data?.document.currentVersion));
                  await apiForm("/documents/" + record.id + "/versions", f);
                  await ops.qc.invalidateQueries({ queryKey: ["operations"] });
                } catch (err) {
                  setError(err);
                }
              }}
            >
              <Field label="Upload a new version">
                <input
                  name="file"
                  type="file"
                  accept="application/pdf,image/png,image/jpeg"
                  required
                />
              </Field>
              <button>Preserve new version</button>
            </form>
            <button
              disabled={ops.busy}
              onClick={() =>
                ops
                  .run("/documents/" + record.id + "/submit", {})
                  .catch(() => {})
              }
            >
              Submit current version for approval
            </button>
            {q.data?.document && (
              <form
                className="op-form"
                onSubmit={async (e) => {
                  e.preventDefault();
                  const f = new FormData(e.currentTarget);
                  try {
                    await ops.run(
                      "/documents/" + record.id,
                      {
                        title: f.get("title"),
                        folder: f.get("folder"),
                        tags: f.get("tags"),
                        visibility: f.get("visibility"),
                        sharedUsers: f.getAll("shared"),
                      },
                      "PUT",
                    );
                  } catch {}
                }}
              >
                <h3>Owner sharing & metadata</h3>
                <Field label="Document title">
                  <input name="title" defaultValue={q.data.document.title} />
                </Field>
                <Field label="Folder">
                  <input name="folder" defaultValue={q.data.document.folder} />
                </Field>
                <Field label="Tags">
                  <input name="tags" defaultValue={q.data.document.tags} />
                </Field>
                <Field label="Visibility">
                  <select
                    name="visibility"
                    defaultValue={q.data.document.visibility}
                  >
                    <option>Private</option>
                    <option>Organization</option>
                  </select>
                </Field>
                <Field label="Explicitly shared members">
                  <select
                    name="shared"
                    multiple
                    size={5}
                    defaultValue={JSON.parse(q.data.document.sharedUsers)}
                  >
                    {options.data?.people
                      .filter((p: Row) => p.userId)
                      .map((p: Row) => (
                        <option key={p.id} value={p.userId}>
                          {p.displayName}
                        </option>
                      ))}
                  </select>
                </Field>
                <button>Save sharing</button>
              </form>
            )}
          </>
        )}
    </Modal>
  );
}
const defaultElements = [
  {
    text: "CERTIFICATE OF APPRECIATION",
    x: 50,
    y: 22,
    size: 28,
    align: "Center",
    color: "#173c42",
  },
  {
    text: "Presented to",
    x: 50,
    y: 38,
    size: 16,
    align: "Center",
    color: "#647875",
  },
  {
    text: "{{member_name}}",
    x: 50,
    y: 48,
    size: 36,
    align: "Center",
    color: "#173c42",
  },
  {
    text: "With gratitude for your contribution to our community",
    x: 50,
    y: 62,
    size: 15,
    align: "Center",
    color: "#647875",
  },
  {
    text: "{{organization_name}} · {{certificate_date}}",
    x: 50,
    y: 76,
    size: 13,
    align: "Center",
    color: "#173c42",
  },
  {
    text: "{{certificate_number}}",
    x: 50,
    y: 86,
    size: 10,
    align: "Center",
    color: "#647875",
  },
];
function Certificates() {
  const [downloadError, setDownloadError] = useState<unknown>();
  const [sourceType, setSourceType] = useState("Membership"),
    [recipients, setRecipients] = useState<string[]>([]);
  const [tab, setTab] = useState("Library"),
    [studio, setStudio] = useState<Row | null>(null),
    [issue, setIssue] = useState(false),
    [revoke, setRevoke] = useState<Row | null>(null);
  const templates = useData("/certificate-templates"),
    certs = useData("/certificates"),
    ops = useOps(),
    { w } = useWorkspace();
  const can = w?.permissions.includes("operations.certificates");
  return (
    <>
      <Header
        title="Recognize a meaningful contribution."
        description="Design with care. Issue with authority. Verify with confidence."
        action={
          can && (
            <div className="op-actions">
              <button
                onClick={() =>
                  setStudio({
                    name: "Community appreciation",
                    type: "Appreciation",
                    layout: JSON.stringify({
                      orientation: "Landscape",
                      background: "#fffdf5",
                      border: "#b39755",
                      elements: defaultElements,
                    }),
                  })
                }
              >
                Open certificate studio
              </button>
              <button className="op-primary" onClick={() => setIssue(true)}>
                Prepare certificates
              </button>
            </div>
          )
        }
      />
      <div className="op-tabs">
        {["Library", "Issued & pending"].map((t) => (
          <button key={t} aria-pressed={tab === t} onClick={() => setTab(t)}>
            {t}
          </button>
        ))}
      </div>
      <ErrorBox error={ops.error ?? downloadError} />
      {tab === "Library" ? (
        <div className="op-project-grid">
          {templates.data?.map((t: Row) => (
            <button
              className="op-template-card"
              key={t.id}
              onClick={() => setStudio(t)}
            >
              <CertificateCanvas layout={JSON.parse(t.layout)} />
              <h3>{t.name}</h3>
              <p>
                {t.type} · version {t.version}
              </p>
            </button>
          ))}
          {!templates.data?.length && (
            <Empty>Create a reusable certificate design to begin.</Empty>
          )}
        </div>
      ) : (
        <section className="op-panel">
          {can && certs.data?.some((c: Row) => c.status === "Issued") && (
            <button
              onClick={() =>
                apiDownload(
                  "/certificates/bulk-pdf",
                  certs.data
                    .filter((c: Row) => c.status === "Issued")
                    .slice(0, 50)
                    .map((c: Row) => c.id),
                  "certificates.zip",
                ).catch(setDownloadError)
              }
            >
              Download issued PDFs (up to 50)
            </button>
          )}
          {certs.data?.map((c: Row) => (
            <div className="op-row" key={c.id}>
              <ShieldCheck />
              <div>
                <strong>{c.number}</strong>
                <small>
                  {c.sourceType} · {date(c.issuedAt)}
                </small>
              </div>
              <Badge>{c.status}</Badge>
              {c.status === "Issued" && (
                <a
                  className="op-secondary"
                  href={"/api/v1/certificates/" + c.id + "/pdf"}
                >
                  PDF
                </a>
              )}
              {can && c.status === "Issued" && (
                <button onClick={() => setRevoke(c)}>Revoke</button>
              )}
            </div>
          ))}
        </section>
      )}
      {studio && (
        <CertificateStudio template={studio} close={() => setStudio(null)} />
      )}{" "}
      {issue && (
        <Modal
          title="Prepare certificates for independent review"
          close={() => setIssue(false)}
        >
          <form
            className="op-form"
            onSubmit={async (e) => {
              e.preventDefault();
              const f = new FormData(e.currentTarget);
              try {
                await ops.run("/certificates", {
                  orgId: w?.orgId,
                  templateId: f.get("template"),
                  people: f.getAll("people"),
                  sourceType: f.get("source"),
                  sourceId: f.get("sourceId"),
                  publicName: f.get("public") === "on",
                  replacesId: f.get("replaces") || null,
                });
                setIssue(false);
                setTab("Issued & pending");
              } catch {}
            }}
          >
            <Field label="Template">
              <select name="template" required>
                <Options items={templates.data} label="name" />
              </select>
            </Field>
            <RecipientPicker value={recipients} onChange={setRecipients} />
            <Field label="Authoritative eligibility source">
              <select
                name="source"
                value={sourceType}
                onChange={(e) => setSourceType(e.target.value)}
              >
                {["Membership", "Event", "Project", "Leadership"].map((x) => (
                  <option key={x}>{x}</option>
                ))}
              </select>
            </Field>
            <CertificateSourcePicker
              key={sourceType + recipients[0]}
              type={sourceType}
              personId={recipients[0]}
            />
            {sourceType === "Membership" && recipients.length > 1 && (
              <p role="alert">
                Prepare membership certificates one recipient at a time. Project
                and event sources support multiple recipients.
              </p>
            )}
            <Field label="Revoked certificate to replace (optional)">
              <select name="replaces">
                <option value="">New issuance</option>
                {certs.data
                  ?.filter((c: Row) => c.status === "Revoked")
                  .map((c: Row) => (
                    <option key={c.id} value={c.id}>
                      {c.number}
                    </option>
                  ))}
              </select>
            </Field>
            <label>
              <input type="checkbox" name="public" />
              Allow recipient name on public verification
            </label>
            <p className="op-muted">
              Up to 100 recipients. Eligibility and every placeholder are
              checked. Independent review is required before PDF issuance.
            </p>
            <ErrorBox error={ops.error} />
            <button
              className="op-primary"
              disabled={
                ops.busy ||
                (sourceType === "Membership" && recipients.length > 1)
              }
            >
              Prepare & submit for review
            </button>
          </form>
        </Modal>
      )}
      {revoke && (
        <Modal title="Revoke certificate" close={() => setRevoke(null)}>
          <form
            className="op-form"
            onSubmit={async (e) => {
              e.preventDefault();
              const f = new FormData(e.currentTarget);
              try {
                await ops.run("/certificates/" + revoke.id + "/revoke", {
                  status: "Revoked",
                  version: revoke.version,
                  reason: f.get("reason"),
                });
                setRevoke(null);
              } catch {}
            }}
          >
            <Field label="Revocation reason">
              <textarea name="reason" required minLength={5} />
            </Field>
            <ErrorBox error={ops.error} />
            <button>Revoke certificate</button>
          </form>
        </Modal>
      )}
    </>
  );
}
function RecipientPicker({ value, onChange }: any) {
  const [search, setSearch] = useState(""),
    [term, setTerm] = useState(""),
    [known, setKnown] = useState<Row[]>([]);
  useEffect(() => {
    const t = setTimeout(() => setTerm(search), 250);
    return () => clearTimeout(t);
  }, [search]);
  const q = useData(
    "/operations/options?personSearch=" + encodeURIComponent(term),
  );
  useEffect(() => {
    if (q.data?.people)
      setKnown((previous) => [
        ...previous.filter(
          (p) =>
            value.includes(p.id) &&
            !q.data.people.some((x: Row) => x.id === p.id),
        ),
        ...q.data.people,
      ]);
  }, [q.data, value]);
  return (
    <Field label="Recipients">
      <input
        aria-label="Search certificate recipients"
        placeholder="Search members…"
        value={search}
        onChange={(e) => setSearch(e.target.value)}
      />
      <select
        name="people"
        multiple
        required
        size={5}
        value={value}
        onChange={(e) =>
          onChange(Array.from(e.target.selectedOptions).map((o) => o.value))
        }
      >
        <Options items={known} />
      </select>
    </Field>
  );
}
function CertificateSourcePicker({ type, personId }: any) {
  const [search, setSearch] = useState(""),
    [term, setTerm] = useState(""),
    [value, setValue] = useState("");
  useEffect(() => {
    const t = setTimeout(() => setTerm(search), 250);
    return () => clearTimeout(t);
  }, [search]);
  const q = useData(
    "/operations/certificate-sources?type=" +
      type +
      (personId ? "&personId=" + personId : "") +
      "&search=" +
      encodeURIComponent(term),
  );
  return (
    <Field label="Eligibility record">
      <input
        aria-label="Search eligibility sources"
        placeholder="Search source records…"
        value={search}
        onChange={(e) => setSearch(e.target.value)}
      />
      <select
        name="sourceId"
        value={value}
        onChange={(e) => setValue(e.target.value)}
        required
      >
        <option value="">Choose a source record</option>
        {q.data?.map((r: Row) => (
          <option key={r.id} value={r.id}>
            {r.label}
          </option>
        ))}
      </select>
      <ErrorBox error={q.error} />
    </Field>
  );
}

function CertificateCanvas({ layout, select, selected, move }: any) {
  return (
    <div
      className={
        "op-certificate " +
        (layout.orientation === "Portrait" ? "portrait" : "")
      }
      style={{ background: layout.background, borderColor: layout.border }}
      onDragOver={(e) => e.preventDefault()}
      onDrop={(e) => {
        e.preventDefault();
        if (!move) return;
        const r = e.currentTarget.getBoundingClientRect();
        move(
          Math.max(0, Math.min(100, ((e.clientX - r.left) / r.width) * 100)),
          Math.max(0, Math.min(100, ((e.clientY - r.top) / r.height) * 100)),
        );
      }}
    >
      {layout.elements?.map((e: Row, n: number) => (
        <div
          key={n}
          draggable={!!move}
          onDragStart={() => select?.(n)}
          onClick={() => select?.(n)}
          className={selected === n ? "selected" : ""}
          style={{
            position: "absolute",
            left: e.x + "%",
            top: e.y + "%",
            fontSize: e.size * 0.55 + "px",
            color: e.color,
            textAlign: e.align.toLowerCase(),
            transform:
              e.align === "Center"
                ? "translateX(-50%)"
                : e.align === "Right"
                  ? "translateX(-100%)"
                  : undefined,
            whiteSpace: "nowrap",
          }}
        >
          {e.text
            .replaceAll("{{member_name}}", "Sample Member")
            .replaceAll("{{organization_name}}", "Your organization")
            .replaceAll("{{certificate_date}}", new Date().toLocaleDateString())
            .replaceAll("{{certificate_number}}", "CS-PREVIEW")}
        </div>
      ))}
      <span className="op-preview-watermark">DESIGN PREVIEW</span>
    </div>
  );
}
function CertificateStudio({ template, close }: any) {
  const [certificateType, setCertificateType] = useState(template.type);
  const [name, setName] = useState(template.name),
    [layout, setLayout] = useState<Row>(JSON.parse(template.layout)),
    [selected, setSelected] = useState(0),
    [error, setError] = useState<unknown>();
  const { w } = useWorkspace(),
    ops = useOps();
  function change(key: string, value: any) {
    setLayout((a) => ({
      ...a,
      elements: a.elements.map((e: Row, n: number) =>
        n === selected ? { ...e, [key]: value } : e,
      ),
    }));
  }
  const element = layout.elements[selected];
  return (
    <Modal title="Certificate studio" close={close}>
      <div className="op-studio">
        <div>
          <CertificateCanvas
            layout={layout}
            select={setSelected}
            selected={selected}
            move={(x: number, y: number) =>
              setLayout((a) => ({
                ...a,
                elements: a.elements.map((e: Row, n: number) =>
                  n === selected ? { ...e, x, y } : e,
                ),
              }))
            }
          />
          <p className="op-muted">
            Select and drag a text element. Use position fields for precise
            placement.
          </p>
        </div>
        <div className="op-form">
          <Field label="Template name">
            <input value={name} onChange={(e) => setName(e.target.value)} />
          </Field>
          <Field label="Certificate type">
            <select
              value={certificateType}
              onChange={(e) => setCertificateType(e.target.value)}
            >
              {Array.from(
                new Set([
                  certificateType,
                  "Membership certificate",
                  "Volunteer participation",
                  "Service recognition",
                  "Event participation",
                  "Leadership appointment",
                  "Appreciation",
                  "Custom",
                ]),
              ).map((type: any) => (
                <option key={type}>{type}</option>
              ))}
            </select>
          </Field>
          <Field label="Orientation">
            <select
              value={layout.orientation}
              onChange={(e) =>
                setLayout((a) => ({ ...a, orientation: e.target.value }))
              }
            >
              <option>Landscape</option>
              <option>Portrait</option>
            </select>
          </Field>
          <div className="op-form-grid">
            <Field label="Background">
              <input
                type="color"
                value={layout.background}
                onChange={(e) =>
                  setLayout((a) => ({ ...a, background: e.target.value }))
                }
              />
            </Field>
            <Field label="Border">
              <input
                type="color"
                value={layout.border}
                onChange={(e) =>
                  setLayout((a) => ({ ...a, border: e.target.value }))
                }
              />
            </Field>
          </div>
          <Field label="Selected element">
            <select
              value={selected}
              onChange={(e) => setSelected(Number(e.target.value))}
            >
              {layout.elements.map((e: Row, n: number) => (
                <option key={n} value={n}>
                  {n + 1}. {e.text.slice(0, 35)}
                </option>
              ))}
            </select>
          </Field>
          {element && (
            <>
              <Field label="Text / placeholder">
                <textarea
                  value={element.text}
                  onChange={(e) => change("text", e.target.value)}
                />
              </Field>
              <div className="op-form-grid">
                {["x", "y", "size"].map((key) => (
                  <Field
                    key={key}
                    label={
                      key === "size" ? "Font size" : key.toUpperCase() + " (%)"
                    }
                  >
                    <input
                      type="number"
                      value={element[key]}
                      onChange={(e) => change(key, Number(e.target.value))}
                    />
                  </Field>
                ))}
                <Field label="Alignment">
                  <select
                    value={element.align}
                    onChange={(e) => change("align", e.target.value)}
                  >
                    <option>Left</option>
                    <option>Center</option>
                    <option>Right</option>
                  </select>
                </Field>
              </div>
            </>
          )}
          <button
            onClick={() => {
              setSelected(layout.elements.length);
              setLayout((a) => ({
                ...a,
                elements: [
                  ...a.elements,
                  {
                    text: "New text",
                    x: 50,
                    y: 50,
                    size: 18,
                    align: "Center",
                    color: "#173c42",
                  },
                ],
              }));
            }}
          >
            Add text element
          </button>
          <p className="op-muted">
            Placeholders: member_name, member_id, club_name, organization_name,
            event_name, project_name, certificate_date, service_hours,
            officer_position, leadership_term, certificate_number.
          </p>
        </div>
      </div>
      <ErrorBox error={error ?? ops.error} />
      <div className="op-actions">
        <button
          className="op-primary"
          disabled={
            ops.busy || !w?.permissions.includes("operations.certificates")
          }
          onClick={() =>
            ops
              .run(
                template.id
                  ? "/certificate-templates/" + template.id
                  : "/certificate-templates",
                {
                  orgId: template.orgId ?? w?.orgId,
                  name,
                  type: certificateType,
                  layout,
                  version: template.version ?? 1,
                },
                template.id ? "PUT" : "POST",
              )
              .then(close)
              .catch(() => {})
          }
        >
          Save template
        </button>
        <button
          onClick={() =>
            apiDownload(
              "/certificate-templates/preview",
              {
                orgId: template.orgId ?? w?.orgId,
                name,
                type: certificateType,
                layout,
              },
              "certificate-preview.pdf",
            ).catch(setError)
          }
        >
          Download PDF preview
        </button>
      </div>
    </Modal>
  );
}
export function VerifyCertificate() {
  const { token } = useParams();
  const q = useQuery<Row>({
    queryKey: ["certificate-verification", token],
    queryFn: () => api("/certificates/verify/" + token),
    retry: false,
  });
  return (
    <div className="operations op-public">
      <ShieldCheck size={44} />
      <Header
        title="Certificate verification"
        description="A trusted record of community contribution."
      />
      {q.isPending ? (
        <Skeleton />
      ) : q.error ? (
        <ErrorBox error={q.error} />
      ) : (
        <section className="op-panel">
          <Badge>
            {q.data?.valid ? "Valid certificate" : "Revoked certificate"}
          </Badge>
          <h2>{q.data?.number}</h2>
          {q.data?.recipient && <h3>{q.data.recipient}</h3>}
          <p>{q.data?.organization}</p>
          <p>
            {q.data?.type} · Issued {date(q.data?.issuedAt)}
          </p>
          <p className="op-muted">
            Verification shares only approved public details.
          </p>
        </section>
      )}
    </div>
  );
}
export function MemberAchievements({ id }: any) {
  const q = useData("/members/" + id + "/achievements");
  if (!q.data) return null;
  return (
    <section className="panel">
      <h2>Service & achievements</h2>
      <p>
        {q.data.verifiedHours} verified service hours · {q.data.projects}{" "}
        projects
      </p>
      {q.data.certificates.map((c: Row) => (
        <p key={c.id}>
          <a href={"/api/v1/certificates/" + c.id + "/pdf"}>{c.number}</a> ·{" "}
          {c.sourceType}
        </p>
      ))}
    </section>
  );
}

function HourClaimForm({ assignments }: any) {
  const [open, setOpen] = useState(false),
    ops = useOps();
  return (
    <>
      <button onClick={() => setOpen((v) => !v)}>
        Submit documented manual hours
      </button>
      {open && (
        <form
          className="op-form"
          onSubmit={async (e) => {
            e.preventDefault();
            const f = new FormData(e.currentTarget);
            try {
              await ops.run("/volunteers/hours", {
                assignmentId: f.get("assignment"),
                startsAt: iso(String(f.get("start"))),
                endsAt: iso(String(f.get("end"))),
                breakMinutes: Number(f.get("breaks")),
                reason: f.get("reason"),
              });
              setOpen(false);
            } catch {}
          }}
        >
          <Field label="Approved assignment">
            <select name="assignment">
              {assignments
                .filter((a: Row) =>
                  ["Approved", "Confirmed", "Present", "Completed"].includes(
                    a.status,
                  ),
                )
                .map((a: Row) => (
                  <option key={a.id} value={a.id}>
                    {a.personName || "Volunteer"} · {a.status}
                  </option>
                ))}
            </select>
          </Field>
          <div className="op-form-grid">
            <Field label="Actual start">
              <input name="start" type="datetime-local" required />
            </Field>
            <Field label="Actual end">
              <input name="end" type="datetime-local" required />
            </Field>
            <Field label="Break minutes">
              <input name="breaks" type="number" min="0" defaultValue="0" />
            </Field>
          </div>
          <Field label="Evidence / adjustment reason">
            <textarea name="reason" required minLength={5} />
          </Field>
          <ErrorBox error={ops.error} />
          <button className="op-primary">
            Submit hours for independent verification
          </button>
        </form>
      )}
    </>
  );
}
