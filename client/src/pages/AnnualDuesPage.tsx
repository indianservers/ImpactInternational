import { useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Link } from "react-router-dom";
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
  const client = useQueryClient();
  const [showSettings, setShowSettings] = useState(initialSettings);
  const [view, setView] = useState<ReportView>("club");
  const [region, setRegion] = useState("");
  const [club, setClub] = useState("");
  const [year, setYear] = useState("");
  const [payment, setPayment] = useState<{member: Member; date: string}>();
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
    enabled: !!settings.data && !showSettings,
  });
  const update = useMutation({
    mutationFn: ({ member, action, paidOn }: { member: Member; action: "paid" | "unpaid" | "activate"; paidOn?: string }) =>
      action === "activate"
        ? api(base + "/members/" + member.id + "/activate", "POST", { version: member.version })
        : api(base + "/members/" + member.id + "/payment", "PUT", { paid: action === "paid", paidOn, cycleYear: report.data?.cycleYear }),
    onSuccess: async (_, { action }) => {
      setPayment(undefined);
      await Promise.all([
        client.invalidateQueries({ queryKey: ["annual-dues-report"] }),
        client.invalidateQueries({ queryKey: ["members"] }),
        client.invalidateQueries({ queryKey: ["member-profile"] }),
        client.invalidateQueries({ queryKey: ["member-metrics"] }),
        client.invalidateQueries({ queryKey: ["my-membership"] }),
      ]);
      if ((status === "paid" && action === "unpaid") || (status === "unpaid" && action === "paid")) {
        if (report.data?.items.length === 1) setPage(previous => Math.max(1, previous - 1));
      }
    },
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
    setPayment(undefined);
  }

  function drill(group: Group, next: ReportView, unpaidOnly = false) {
    switchView(next);
    if (view === "region" && group.id) { setRegion(group.id); setClub(""); }
    if (view === "club" && group.id) setClub(group.id);
    if (unpaidOnly) setStatus("unpaid");
  }

  return <div className="simple-finance annual-dues-report">
    <header className="sf-heading">
      <div><h1>Dues & Membership</h1>
        {config && <span>Membership expires {printDate(config.expiryDate)} · Current fee {money(config.currentFee)}</span>}
      </div>
      <button className="secondary" onClick={() => setShowSettings(!showSettings)}>{showSettings ? "Back to dues" : "Dues settings"}</button>
    </header>
    <ErrorBox error={settings.error} />
    {settings.isPending && <Skeleton />}
    {config && showSettings && <DuesSettings key={config.version} settings={config} />}
    {config && !showSettings && <>
      <nav className="sf-actions" aria-label="Dues report view">
        {([["club", "Club-wise"], ["region", "Region-wise"], ["member", "Member-wise"]] as const).map(([key, label]) =>
          <button key={key} className="secondary" aria-pressed={view === key} onClick={() => switchView(key)}>{label}</button>)}
      </nav>
      <section className="sf-scope" aria-label="Filter dues by region and club">
        <div className="sf-extra-fields">
          <label>Membership year<select value={year} onChange={e => { setYear(e.target.value); setPage(1); setPayment(undefined); }}>
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
      <ErrorBox error={report.error || update.error} />
      {report.isPending && <Skeleton />}
      {report.data && <>
        {legacyReport && <p role="status">The local API needs the updated build to show grouped dues reports.</p>}
        <section className="annual-dues-kpis" aria-label="Dues totals for selected scope">
          <div><span>Collected</span><strong>{money(collected)}</strong><small>{paid} paid</small></div>
          <div><span>Estimated pending</span><strong>{money(unpaid * pendingFee)}</strong><small>{unpaid} unpaid · {money(pendingFee)} per membership</small></div>
          <div><span>Collection progress</span><strong>{coverage}%</strong><progress value={coverage} max={100} aria-label="Share of memberships paid" /><small>{paid} of {paid + unpaid} memberships paid</small></div>
        </section>
        <section className="sf-results">
          <header><div>
            <h2>{displayedView === "club" ? "Club-wise dues" : displayedView === "region" ? "Region-wise dues" : status === "unpaid" ? "Unpaid members" : status === "paid" ? "Paid members" : "Member-wise dues"}</h2>
            <p>{printDate(report.data.cycleStart)} to {printDate(report.data.cycleEnd)}</p>
          </div><a className="button secondary" download href={"/api/v1" + base + "/members?" + exportParams}>Export CSV</a></header>
          {payment && <form className="form-stack panel" onSubmit={e => {
            e.preventDefault();
            update.mutate({ member: payment.member, action: "paid", paidOn: payment.date });
          }}>
            <h3>Record payment for {payment.member.name}</h3>
            <label>Actual payment date<input type="date" required value={payment.date}
              min={report.data.cycleStart} max={config.today < report.data.cycleEnd ? config.today : report.data.cycleEnd}
              onChange={e => setPayment({ ...payment, date: e.target.value })} /></label>
            <div className="header-actions">
              <button disabled={update.isPending}>Save payment</button>
              <button type="button" className="secondary" disabled={update.isPending} onClick={() => setPayment(undefined)}>Cancel</button>
            </div>
          </form>}
          {displayedView !== "member" ? <div className="sf-table"><table>
            <thead><tr><th>{view === "club" ? "Club" : "Region"}</th><th>Members</th><th>Paid</th><th>Unpaid</th><th>Collected</th><th>Estimated pending</th><th>Paid %</th><th>Details</th></tr></thead>
            <tbody>{report.data.groups?.map(group => <tr key={group.id ?? "unassigned"}>
              <td><strong>{group.name}</strong></td><td>{group.members}</td><td>{group.paid}</td><td>{group.unpaid}</td>
              <td>{money(group.collected)}</td><td>{money(group.unpaid * pendingFee)}</td>
              <td>{Math.round(group.paid * 100 / group.members)}%</td>
              <td>{group.id && <div className="sf-actions">
                <button className="secondary" onClick={() => drill(group, view === "region" ? "club" : "member")}>{view === "region" ? "View clubs" : "View members"}</button>
                {group.unpaid > 0 && <button className="secondary" onClick={() => drill(group, "member", true)}>View unpaid</button>}
              </div>}</td>
            </tr>)}</tbody>
          </table></div> : <div className="sf-table"><table>
            <thead><tr><th>Member</th><th>Club</th><th>Membership</th><th>Dues</th><th>Collected</th><th>Estimated pending</th>{config.canWrite && <th>Actions</th>}</tr></thead>
            <tbody>{report.data.items.map(member => {
              const closed = !["Active", "Inactive", "Suspended", "Expired"].includes(member.membershipStatus);
              const current = member.membershipStatus === "Active" && member.endsAt && new Date(member.endsAt) > new Date();
              return <tr key={member.id}>
                <td>{w?.permissions.includes("members.view") ? <Link to={"/app/members/" + member.id}>{member.name}</Link> : member.name}<br /><small>{member.number}</small></td>
                <td>{member.club}</td><td>{member.membershipStatus}</td><td>{member.paid ? "Paid" : "Unpaid"}</td>
                <td>{member.paid ? money(member.amount) : "—"}{member.paidOn && <><br /><small>{printDate(member.paidOn)}</small></>}</td>
                <td>{member.paid ? "—" : money(pendingFee)}</td>
                {config.canWrite && <td>{(!closed || member.paid) && <div className="sf-actions">
                  <button className="secondary" disabled={update.isPending} onClick={() => {
                    if (member.paid) update.mutate({ member, action: "unpaid" });
                    else setPayment({ member, date: report.data!.historical ? "" : config.today });
                  }}>{member.paid ? "Mark unpaid" : "Mark paid"}</button>
                  {!closed && !current && !report.data!.historical && <button disabled={update.isPending} onClick={() => update.mutate({ member, action: "activate" })}>Activate</button>}
                </div>}</td>}
              </tr>;
            })}</tbody>
          </table></div>}
          {report.data.total === 0 && <p className="sf-empty">No results match these filters.</p>}
          <footer className="sf-pager">
            <span>{report.data.total} {displayedView === "member" ? "members" : displayedView === "club" ? "clubs" : "regions"}</span>
            <button className="secondary" disabled={page <= 1} onClick={() => setPage(page - 1)}>Previous</button>
            <button className="secondary" disabled={page * 50 >= report.data.total} onClick={() => setPage(page + 1)}>Next</button>
          </footer>
        </section>
      </>}
    </>}
  </div>;
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
