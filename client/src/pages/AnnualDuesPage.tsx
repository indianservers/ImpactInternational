import { Fragment, useId, useState } from "react";
import { ChevronDown, ChevronRight } from "lucide-react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { api } from "../api";
import { ErrorBox, Skeleton, useWorkspace } from "../App";
import "./FinanceDuesHome.css";

const base = "/finance/annual-dues";
const money = (value: number) => new Intl.NumberFormat("en-IN", { style: "currency", currency: "INR", maximumFractionDigits: 2 }).format(value);
const printDate = (value: string) => new Intl.DateTimeFormat("en-IN", { dateStyle: "medium", timeZone: "Asia/Kolkata" }).format(new Date(value));

type Settings = {
  expiryMonth: number;
  expiryDay: number;
  firstHalfFee: number;
  secondHalfFee: number;
  version: number;
  expiryDate: string;
  today: string;
  firstReportYear: number;
  secondHalfStarts: string;
  currentFee: number;
  canConfigure: boolean;
  canWrite: boolean;
  organizations: {id: string; name: string; type: string; parentId: string | null}[];
};

type Member = {
  id: string;
  number: string;
  name: string;
  club: string;
  paid: boolean;
  amount: number;
  paidOn: string | null;
  membershipStatus: string;
  endsAt: string | null;
  version: number;
};

type ReportView = "club" | "region" | "member";
type Group = { id: string | null; name: string; members: number; paid: number; unpaid: number; collected: number };

type Report = {
  cycleYear: number;
  cycleStart: string;
  cycleEnd: string;
  historical: boolean;
  pendingFee: number;
  groups?: Group[];
  total: number;
  totals: {paid: number; unpaid: number; collected: number} | null;
  items: Member[];
};

export default function AnnualDuesPage({ initialSettings = false }: { initialSettings?: boolean }) {
  const { w } = useWorkspace();
  const [showSettings, setShowSettings] = useState(initialSettings);
  const [view, setView] = useState<ReportView>("region");
  const [region, setRegion] = useState("");
  const [club, setClub] = useState("");
  const [year, setYear] = useState("");
  const [status, setStatus] = useState("");
  const [search, setSearch] = useState("");
  const [page, setPage] = useState(1);
  const settings = useQuery<Settings>({
    queryKey: ["annual-dues-settings", w?.id],
    queryFn: () => api(base + "/settings"),
    enabled: !!w,
  });
  const query = new URLSearchParams({ page: String(page), view });
  if (region) query.set("regionId", region);
  if (club) query.set("clubId", club);
  if (status) query.set("status", status);
  if (year) query.set("cycleYear", year);
  if (search.trim()) query.set("search", search.trim());
  const report = useQuery<Report>({
    queryKey: ["annual-dues-report", w?.id, query.toString()],
    queryFn: () => api(base + "/members?" + query),
    enabled: !!settings.data,
  });
  const config = settings.data;
  const totals = report.data?.totals;
  const legacyReport = report.data && report.data.pendingFee === undefined;
  const displayedView = legacyReport ? "member" : view;
  const pendingFee = report.data?.pendingFee ?? config?.currentFee ?? 0;
  const currentYear = Number(config?.expiryDate.slice(0, 4));
  const exportParams = new URLSearchParams(query);
  exportParams.delete("page");
  exportParams.set("format", "csv");
  const regions = config?.organizations.filter(o => o.type === "Region") ?? [];
  const clubs = config?.organizations.filter(o => o.type === "Club" && (!region || o.parentId === region)) ?? [];
  const collected = totals?.collected ?? 0;
  const unpaid = totals?.unpaid ?? 0;
  const paid = totals?.paid ?? 0;
  const coverage = paid + unpaid ? Math.round(paid * 100 / (paid + unpaid)) : 0;
  function switchView(next: ReportView) {
    setView(next);
    setSearch("");
    setStatus("");
    setPage(1);
  }

  return <div className="simple-finance annual-dues-report">
    <header className="sf-heading">
      <div><h1>Dues & Membership</h1>
        {config && <span>Membership expires {printDate(config.expiryDate)} · Current fee {money(config.currentFee)}</span>}
      </div>
      <button className="secondary" aria-expanded={showSettings} onClick={() => setShowSettings(!showSettings)}>{showSettings ? "Hide dues settings" : "Dues settings"}</button>
    </header>
    <ErrorBox error={settings.error} />
    {settings.isPending && <Skeleton />}
    {config && showSettings && <DuesSettings key={config.version} settings={config} />}
    {config && <>
      <nav className="sf-actions" aria-label="Dues report view">
        {([["club", "Club-wise"], ["region", "Region-wise"], ["member", "Member-wise"]] as const).map(([key, label]) =>
          <button key={key} className="secondary" aria-pressed={view === key} onClick={() => switchView(key)}>{label}</button>)}
      </nav>
      <section className="sf-scope" aria-label="Filter dues by region and club">
        <div className="sf-extra-fields">
          <label>Membership year<select value={year} onChange={e => { setYear(e.target.value); setPage(1); }}>
            <option value="">Current year · ends {currentYear}</option>
            {Array.from({length: Math.max(0, currentYear - config.firstReportYear)}, (_, i) => currentYear - i - 1)
              .map(value => <option key={value} value={value}>Year ending {value}</option>)}
          </select></label>
          <label>Region<select value={region} onChange={e => { setRegion(e.target.value); setClub(""); setPage(1); }}>
            <option value="">All authorized regions</option>
            {regions.map(o => <option key={o.id} value={o.id}>{o.name}</option>)}
          </select></label>
          <label>Club<select value={club} onChange={e => { setClub(e.target.value); setPage(1); }}>
            <option value="">All authorized clubs</option>
            {clubs.map(o => <option key={o.id} value={o.id}>{o.name}</option>)}
          </select></label>
          <label>Payment status<select value={status} onChange={e => { setStatus(e.target.value); setPage(1); }}>
            <option value="">All members</option><option value="unpaid">Unpaid</option><option value="paid">Paid</option>
          </select></label>
          <label>Find {view}<input value={search} onChange={e => { setSearch(e.target.value); setPage(1); }}
            placeholder={view === "member" ? "Name or membership ID" : view === "club" ? "Club name" : "Region name"} /></label>
        </div>
      </section>
      <ErrorBox error={report.error} />
      {report.isPending && <Skeleton />}
      {report.data && <>
        {legacyReport && <p role="status">The local API needs the updated build to show grouped dues reports.</p>}
        <section className="annual-dues-kpis" aria-label="Dues totals for selected scope">
          <div><span>Collected</span><strong>{money(collected)}</strong><small>{paid} paid</small></div>
          <div><span>Estimated pending</span><strong>{money(unpaid * pendingFee)}</strong><small>{unpaid} unpaid · {money(pendingFee)} per membership</small></div>
          <div><span>Collection progress</span><strong>{coverage}%</strong><progress value={coverage} max={100} aria-label="Share of memberships paid" /><small>{paid} of {paid + unpaid} memberships paid</small></div>
        </section>
        <DuesCharts paid={paid} unpaid={unpaid} collected={collected} pending={unpaid * pendingFee}/>
        <section className="sf-results">
          <header><div>
            <h2>{displayedView === "club" ? "Club-wise dues" : displayedView === "region" ? "Region-wise dues" : status === "unpaid" ? "Unpaid members" : status === "paid" ? "Paid members" : "Member-wise dues"}</h2>
            {report.data.cycleStart && report.data.cycleEnd && <p>{printDate(report.data.cycleStart)} to {printDate(report.data.cycleEnd)}</p>}
          </div><a className="button secondary" download href={"/api/v1" + base + "/members?" + exportParams}>Export CSV</a></header>
          <DuesGrid key={JSON.stringify([w?.id, displayedView, year, region, club, status, search.trim()])}
            report={report.data} view={displayedView} config={config} params={query}
            page={page} onPage={setPage} />
        </section>
      </>}
    </>}
  </div>;
}

function DuesCharts({ paid, unpaid, collected, pending }: {
  paid: number; unpaid: number; collected: number; pending: number;
}) {
  const charts = [
    { title: "Payment status", parts: [
      { label: "Paid members", value: paid, display: paid.toLocaleString("en-IN") },
      { label: "Unpaid members", value: unpaid, display: unpaid.toLocaleString("en-IN") },
    ] },
    { title: "Dues amounts", parts: [
      { label: "Collected", value: collected, display: money(collected) },
      { label: "Estimated pending", value: pending, display: money(pending) },
    ] },
  ];
  return <div className="annual-dues-charts">
    {charts.map(({ title, parts }) => {
      const total = parts.reduce((sum, part) => sum + part.value, 0);
      return <figure key={title}>
        <figcaption>{title}</figcaption>
        <div className="annual-dues-chart-bar" aria-hidden="true">
          {total > 0 && parts.map(part => <span key={part.label} style={{ flexGrow: part.value / total }}/>)}
        </div>
        <ul>{parts.map(part => <li key={part.label}><span>{part.label}</span><strong>{part.display}</strong></li>)}</ul>
      </figure>;
    })}
  </div>;
}

type GridProps = {
  report: Report;
  view: ReportView;
  config: Settings;
  params: URLSearchParams;
  page: number;
  onPage: (page: number) => void;
};

function DuesGrid({ report, view, config, params, page, onPage }: GridProps) {
  const { w } = useWorkspace();
  const client = useQueryClient();
  const gridId = useId();
  const [expanded, setExpanded] = useState<Record<string, "all" | "unpaid">>({});
  const [payment, setPayment] = useState<{memberId: string; date: string}>();
  const pendingFee = report.pendingFee ?? config.currentFee;
  const update = useMutation({
    mutationFn: ({ member, action, paidOn }: { member: Member; action: "paid" | "unpaid" | "activate"; paidOn?: string }) =>
      action === "activate"
        ? api(base + "/members/" + member.id + "/activate", "POST", { version: member.version })
        : api(base + "/members/" + member.id + "/payment", "PUT", { paid: action === "paid", paidOn, cycleYear: report.cycleYear }),
    onSuccess: async (_, { action }) => {
      setPayment(undefined);
      const status = params.get("status");
      if (report.items.length === 1 && ((status === "paid" && action === "unpaid") || (status === "unpaid" && action === "paid"))) {
        onPage(Math.max(1, page - 1));
      }
      await Promise.all(["annual-dues-report", "members", "member-profile", "member-metrics", "my-membership"]
        .map(key => client.invalidateQueries({ queryKey: [key] })));
    },
  });

  function toggle(id: string, mode: "all" | "unpaid") {
    if (payment?.memberId === id) {
      if (update.isPending) return;
      setPayment(undefined);
    }
    setExpanded(previous => {
      const next = { ...previous };
      if (next[id] === mode) delete next[id];
      else next[id] = mode;
      return next;
    });
  }

  const memberView = view === "member";
  const label = view === "region" ? "Region" : "Club";
  return <>
    <ErrorBox error={update.error}/>
    <div className="sf-table annual-dues-grid">
      {memberView ? <table aria-label="Member dues">
        <thead><tr><th scope="col">Member</th><th scope="col">Club</th><th scope="col">Membership</th><th scope="col">Dues</th><th scope="col">Collected</th><th scope="col">Estimated pending</th></tr></thead>
        <tbody>{report.items.map(member => {
          const open = !!expanded[member.id];
          const panel = gridId + member.id;
          const closed = !["Active", "Inactive", "Suspended", "Expired"].includes(member.membershipStatus);
          const current = member.membershipStatus === "Active" && member.endsAt && new Date(member.endsAt) > new Date();
          return <Fragment key={member.id}>
            <tr className={open ? "annual-dues-open" : undefined}>
              <td><RowToggle name={member.name} open={open} panel={panel} onClick={() => toggle(member.id, "all")}/><small className="annual-dues-member-number">{member.number}</small></td>
              <td>{member.club}</td><td>{member.membershipStatus}</td><td>{member.paid ? "Paid" : "Unpaid"}</td>
              <td>{member.paid ? money(member.amount) : "—"}</td><td>{member.paid ? "—" : money(pendingFee)}</td>
            </tr>
            {open && <tr className="annual-dues-detail"><td colSpan={6}><div id={panel}>
              <dl className="annual-dues-member-details">
                <div><dt>Membership ID</dt><dd>{member.number}</dd></div>
                <div><dt>Membership expires</dt><dd>{member.endsAt ? printDate(member.endsAt) : "—"}</dd></div>
                <div><dt>Payment date</dt><dd>{member.paidOn ? printDate(member.paidOn) : "—"}</dd></div>
              </dl>
              {config.canWrite && (!closed || member.paid) && <div className="sf-actions">
                {!payment && <button className="secondary" disabled={update.isPending} onClick={() => {
                  if (member.paid) update.mutate({ member, action: "unpaid" });
                  else setPayment({ memberId: member.id, date: report.historical ? "" : config.today });
                }}>{member.paid ? "Mark unpaid" : "Mark paid"}</button>}
                {!closed && !current && !report.historical && !payment &&
                  <button disabled={update.isPending} onClick={() => update.mutate({ member, action: "activate" })}>Activate</button>}
              </div>}
              {payment?.memberId === member.id && <form className="annual-dues-payment" onSubmit={e => {
                e.preventDefault();
                update.mutate({ member, action: "paid", paidOn: payment.date });
              }}>
                <label>Actual payment date<input type="date" required value={payment.date}
                  min={report.cycleStart} max={config.today < report.cycleEnd ? config.today : report.cycleEnd}
                  onChange={e => setPayment({ ...payment, date: e.target.value })}/></label>
                <div className="sf-actions">
                  <button disabled={update.isPending}>Save payment</button>
                  <button type="button" className="secondary" disabled={update.isPending} onClick={() => setPayment(undefined)}>Cancel</button>
                </div>
              </form>}
            </div></td></tr>}
          </Fragment>;
        })}</tbody>
      </table> : <table aria-label={label + " dues"}>
        <thead><tr><th scope="col">{label}</th><th scope="col">Members</th><th scope="col">Paid</th><th scope="col">Unpaid</th><th scope="col">Collected</th><th scope="col">Estimated pending</th><th scope="col">Paid %</th></tr></thead>
        <tbody>{report.groups?.map(group => {
          const id = group.id;
          const panel = gridId + id;
          const mode = id ? expanded[id] : undefined;
          const coverage = group.members ? Math.round(group.paid * 100 / group.members) : 0;
          return <Fragment key={id ?? "unassigned"}>
            <tr className={mode ? "annual-dues-open" : undefined}>
              <td>{id ? <RowToggle name={group.name} open={!!mode} panel={panel} onClick={() => toggle(id, mode ?? "all")}/> : <strong>{group.name}</strong>}</td>
              <td>{group.members}</td><td>{group.paid}</td>
              <td>{id && group.unpaid > 0 ? <button type="button" className="annual-dues-count" aria-label={`View ${group.unpaid} unpaid members in ${group.name}`}
                aria-expanded={mode === "unpaid"} aria-controls={mode === "unpaid" ? panel : undefined} onClick={() => toggle(id, "unpaid")}>{group.unpaid}</button> : group.unpaid}</td>
              <td>{money(group.collected)}</td><td>{money(group.unpaid * pendingFee)}</td>
              <td><div className="annual-dues-row-progress"><progress aria-label={group.name + " paid"} value={coverage} max={100}/><span>{coverage}%</span></div></td>
            </tr>
            {id && mode && <tr className="annual-dues-detail"><td colSpan={7}><div id={panel}>
              <NestedDuesGrid key={mode} id={id} name={group.name} parentView={view} mode={mode} config={config} params={params} workspaceId={w?.id}/>
            </div></td></tr>}
          </Fragment>;
        })}</tbody>
      </table>}
    </div>
    {report.total === 0 && <p className="sf-empty">No results match these filters.</p>}
    <footer className="sf-pager">
      <span>{report.total} {memberView ? "members" : view === "club" ? "clubs" : "regions"}</span>
      <button className="secondary" disabled={page <= 1} onClick={() => onPage(page - 1)}>Previous</button>
      <button className="secondary" disabled={page * 50 >= report.total} onClick={() => onPage(page + 1)}>Next</button>
    </footer>
  </>;
}

function RowToggle({ name, open, panel, onClick }: { name: string; open: boolean; panel: string; onClick: () => void }) {
  return <button type="button" className="annual-dues-row-toggle" aria-expanded={open} aria-controls={open ? panel : undefined} onClick={onClick}>
    {open ? <ChevronDown aria-hidden="true" size={16}/> : <ChevronRight aria-hidden="true" size={16}/>}
    {name}
  </button>;
}

function NestedDuesGrid({ id, name, parentView, mode, config, params, workspaceId }: {
  id: string; name: string; parentView: "region" | "club"; mode: "all" | "unpaid"; config: Settings; params: URLSearchParams; workspaceId?: string;
}) {
  const [page, setPage] = useState(1);
  const view = parentView === "region" && mode === "all" ? "club" : "member";
  const query = new URLSearchParams(params);
  query.delete("search");
  query.set(parentView === "region" ? "regionId" : "clubId", id);
  query.set("view", view);
  query.set("page", String(page));
  if (mode === "unpaid") query.set("status", "unpaid");
  const report = useQuery<Report>({
    queryKey: ["annual-dues-report", workspaceId, query.toString()],
    queryFn: () => api(base + "/members?" + query),
  });
  return <section aria-label={name + " details"} className="annual-dues-nested">
    <h3>{mode === "unpaid" ? "Unpaid members" : view === "club" ? "Clubs" : "Members"} · {name}</h3>
    <ErrorBox error={report.error}/>
    {report.isPending && <Skeleton/>}
    {report.data && <DuesGrid report={report.data} view={view} config={config} params={query} page={page} onPage={setPage}/>}
  </section>;
}

function DuesSettings({ settings }: { settings: Settings }) {
  const client = useQueryClient();
  const save = useMutation({
    mutationFn: (values: Record<string, number>) => api(base + "/settings", "PUT", values),
    onSuccess: async () => {
      await Promise.all([
        client.invalidateQueries({ queryKey: ["annual-dues-settings"] }),
        client.invalidateQueries({ queryKey: ["annual-dues-report"] }),
        client.invalidateQueries({ queryKey: ["members"] }),
        client.invalidateQueries({ queryKey: ["member-profile"] }),
        client.invalidateQueries({ queryKey: ["my-membership"] }),
      ]);
    },
  });

  return <section className="sf-results panel">
    <h2>Dues settings</h2>
    <p>Current membership year ends {printDate(settings.expiryDate)}. The second-half fee applies from {printDate(settings.secondHalfStarts)}.</p>
    <ErrorBox error={save.error} />
    <form className="form-stack" onSubmit={e => {
      e.preventDefault();
      const form = new FormData(e.currentTarget);
      save.mutate({
        expiryMonth: Number(form.get("expiryMonth")), expiryDay: Number(form.get("expiryDay")),
        firstHalfFee: Number(form.get("firstHalfFee")), secondHalfFee: Number(form.get("secondHalfFee")),
        version: settings.version,
      });
    }}>
      <div className="sf-extra-fields">
        <label>Expiry month<select name="expiryMonth" defaultValue={settings.expiryMonth} disabled={!settings.canConfigure}>
          {Array.from({ length: 12 }, (_, i) => <option value={i + 1} key={i}>{new Intl.DateTimeFormat("en-IN", { month: "long", timeZone: "UTC" }).format(new Date(Date.UTC(2026, i, 1)))}</option>)}
        </select></label>
        <label>Expiry day<input name="expiryDay" type="number" min={1} max={31} required defaultValue={settings.expiryDay} disabled={!settings.canConfigure} /></label>
        <label>First six months (₹)<input name="firstHalfFee" type="number" min={0} step="0.01" required defaultValue={settings.firstHalfFee} disabled={!settings.canConfigure} /></label>
        <label>Last six months (₹)<input name="secondHalfFee" type="number" min={0} step="0.01" required defaultValue={settings.secondHalfFee} disabled={!settings.canConfigure} /></label>
      </div>
      {settings.canConfigure && <button disabled={save.isPending}>Save settings</button>}
    </form>
  </section>;
}
