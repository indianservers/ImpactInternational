import { useEffect, useState } from "react";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { Link } from "react-router-dom";
import { api } from "../api";
import { ErrorBox, Skeleton, useWorkspace } from "../App";
import { MemberDrawer } from "../MemberComponents";

import DuesManagement from "./DuesManagement";
const base = "/finance/dues-center";
const today = () => new Date().toISOString().slice(0, 10);
const money = (v: any, c: string) => new Intl.NumberFormat(undefined, { style: "currency", currency: c }).format(Number(v ?? 0));
const displayValue = (value: any) => ({ PromiseToPay: "Promise to pay", NoAnswer: "No answer", PersonalAccount: "Personal account", BankTransfer: "Bank transfer" } as Record<string, string>)[String(value)] ?? String(value ?? "—");
const labels: Record<string, string> = { group: "Group", memberId: "Member ID", accountHolder: "Account holder", pendingVerification: "Awaiting verification", dueOn: "Due date", collected: "Verified collections", pending: "Pending dues", billed: "Billed", credits: "Credits / waivers", submitted: "Recorded total", destination: "Receiving account / UPI", reviewNote: "Review note", availableToCollect: "To collect", daysOverdue: "Days overdue", followUpOutcome: "Follow-up", nextFollowUp: "Next follow-up", promisedOn: "Promised date", promisedAmount: "Promised amount", collectionRate: "Collection rate", members: "Members", club: "Club", region: "Region", district: "District", zone: "Zone" };
const numeric = new Set(["amount", "billed", "collected", "pending", "credits", "overdue", "approved", "rejected", "cancelled", "submitted", "pendingVerification", "availableToCollect", "promisedAmount"]);

export default function DuesReports(props: {collecting?:boolean;management?:boolean}) {return props.management?<DuesManagement/>:<DuesRegister {...props}/>;}
export function DuesRegister({ collecting = false, management = false, initialKind="pending", scopeId="", startDate="", endDate="", planId="", embedded=false }: {collecting?:boolean;management?:boolean;initialKind?:string;scopeId?:string;startDate?:string;endDate?:string;planId?:string;embedded?:boolean}) {
  const { w } = useWorkspace();
  const qc = useQueryClient();
  const [kind, setKind] = useState(collecting ? "verification" : management ? "region" : initialKind);
  const [org, setOrg] = useState(scopeId), [level, setLevel] = useState(""), [currency, setCurrency] = useState("");
  const [from, setFrom] = useState(startDate), [to, setTo] = useState(endDate||today()), [page, setPage] = useState(1);
  const [compose, setCompose] = useState(false), [review, setReview] = useState<any>(), [message, setMessage] = useState("");
  const [detail, setDetail] = useState(""), [initialInvoice, setInitialInvoice] = useState<any>();
  const [search, setSearch] = useState(""), [term, setTerm] = useState(""), [age, setAge] = useState("");
  useEffect(() => { const t = setTimeout(() => { setTerm(search); setPage(1); }, 250); return () => clearTimeout(t); }, [search]);
  const [sort, setSort] = useState("");
  const [balance, setBalance] = useState("");
  const [trail, setTrail] = useState<{id: string; name: string; kind: string}[]>([]);
  function choose(next: string, order = "", status = "") { setKind(next); setSort(order); setBalance(status); setPage(1); }
  function drill(row: any) {
    const next: Record<string, string> = {national: "region", region: "district", district: "zone", zone: "club", club: "pending"};
    setTrail([...trail, {id: org, name: options.data?.organizations.find((o: any) => o.id === org)?.name || "Authorized scope", kind}]);
    setOrg(row.groupId); setLevel(""); choose(next[kind] || "pending");
  }
  const allowed = !!w?.permissions.includes("finance.dues.reports");
  const options = useQuery<any>({ queryKey: ["dues-options", w?.id], queryFn: () => api(base + "/options"), enabled: allowed });
  const params = new URLSearchParams({ to, page: String(page) });
  if (from) params.set("from", from);
  if(planId)params.set("planId",planId);
  if (org) params.set("orgId", org);
  if (currency) params.set("currency", currency);
  if (term) params.set("search", term);
  if (age) params.set("age", age);
  if (sort) params.set("sort", sort);
  if (balance) params.set("balance", balance);
  const url = base + "/reports/" + kind + "?" + params;
  const q = useQuery<any>({ queryKey: ["dues-report", w?.id, url], queryFn: () => api(url), enabled: allowed && !!to });
  function refresh() { qc.invalidateQueries({ queryKey: ["dues-report"] }); qc.invalidateQueries({ queryKey: ["dues-detail"] }); qc.invalidateQueries({ queryKey: ["dues-invoices"] }); qc.invalidateQueries({ queryKey: ["finance"] }); }
  if (!allowed) return <section className="panel"><h2>Dues reports</h2><p>Your login does not have access to organizational dues reports. Use My finances to see your own invoices.</p></section>;
  const keys = new Set<string>((q.data?.items ?? []).flatMap((r: any) => Object.keys(r)));
  const preferred = keys.has("collectionId") ? ["date", "member", "amount", "method", "reference", "status"] : keys.has("pending") && keys.has("member") ? ["invoice", "member", "currency", "billed", "collected", "pending", "pendingVerification", "availableToCollect", "daysOverdue"] : ["group", "month", "collector", "billed", "collected", "pending", "collectionRate", "members", "pendingVerification", "availableToCollect", "collected", "overdue", "currency", "invoices", "billed", "credits", "amount", "receipt", "member", "status", "approved", "rejected", "submitted"];
  if (["followup", "promises"].includes(kind)) preferred.push("nextFollowUp", "promisedOn", "promisedAmount");
  if (kind === "unsettled") { preferred.splice(0, preferred.length, "receipt", "date", "member", "club", "currency", "amount", "method", "reference", "status"); }
  if (kind === "collector") preferred.push("cancelled");
  const columns = [...new Set(preferred)].filter(k => keys.has(k));
  if (keys.has("member")) for (const key of ["club", "region", "district", "zone"]) if (keys.has(key) && !columns.includes(key)) columns.push(key);
  const rowActions = q.data?.items.some((r: any) => r.invoiceId || r.canReview || r.groupId && ["club", "zone", "district", "region", "national"].includes(kind));
  return <section className={"dues-center"+(embedded?" dm-embedded":"")}>
    {options.data?.mockData && <div className="dues-mock-banner" role="status">MOCK DATA PREVIEW · Fictional dues and payment records in an isolated test database. No real funds.</div>}
    <header className="op-header"><div><p className="op-eyebrow">IMPACT INTERNATIONAL / DUES & COLLECTIONS</p><h1>{collecting ? "Club collections & verification" : management ? "Dues & Membership" : "Pending dues & collections"}</h1><p>Follow up on unpaid dues, record collections, and verify payments. {options.data?.reports.length ?? 25} reports cover your authorized hierarchy.</p></div>{options.data?.canCollect && <button onClick={() => { setInitialInvoice(undefined); setCompose(true); }}>Record payment</button>}</header>
    {management && <section className="dues-question-panel" aria-label="Dues questions"><div><h2>What would you like to know?</h2><p>Choose a question, then select an organization below to narrow the answer.</p></div><div className="dues-question-grid">{[
      ["region", "billed", "", "Dues generated by region"],
      ["region", "collected", "", "Collections by region"],
      ["club", "pending", "unpaid", "Clubs with no payments"],
      ["pending", "pending", "", "Members with outstanding dues"],
      ["club", "collected", "", "Collections by club"],
      ["region", "overdue", "", "Regions with highest overdue"],
      ["district", "collectionRate", "", "District collection efficiency"],
    ].map(([report, order, status, title]) => <button key={title} className={kind === report && sort === order && balance === status ? "active" : "secondary"} onClick={() => choose(report, order, status)}>{title}<span aria-hidden="true">→</span></button>)}</div><Link to="/app/finance/dues-plans">Manage dues plans & generation →</Link></section>}
    <nav className="dues-quick-tabs" aria-label="Dues work queues">{[["overview", "Overview"], ["region", "Regions"], ["district", "Districts"], ["zone", "Zones"], ["club", "Clubs"], ["member", "Member dues"], ["pending", "Pending dues"], ["overdue", "Overdue"], ["verification", "Awaiting verification"], ["unsettled", "Awaiting settlement"], ["promises", "Promises due"]].map(([id, name]) => <button key={id} className={kind === id ? "active" : "secondary"} onClick={() => { choose(id); }}>{name}</button>)}</nav>
    <section className="panel"><div className="form-grid">
      <label>Report type<select value={kind} onChange={e => { choose(e.target.value); }}>{options.data?.reports.map((r: any) => <option key={r.id} value={r.id}>{r.name}</option>)}</select></label>
      <label>Hierarchy level<select value={level} onChange={e => { setLevel(e.target.value); setOrg(""); setPage(1); }}><option value="">All levels</option>{["National", "Region", "District", "Zone", "Club"].map(l => <option key={l}>{l}</option>)}</select></label>
      <label>Organization scope<select value={org} onChange={e => { setOrg(e.target.value); setTrail([]); setPage(1); }}><option value="">My entire authorized scope</option>{options.data?.organizations.filter((o: any) => !level || o.type === level).map((o: any) => <option key={o.id} value={o.id}>{o.name} · {o.type}</option>)}</select></label>
      <label>Rank by<select value={sort} onChange={e => { setSort(e.target.value); setPage(1); }}><option value="">Name / default order</option>{[["billed", "Highest dues generated"], ["collected", "Highest collections"], ["pending", "Highest outstanding"], ["overdue", "Highest overdue"], ["collectionRate", "Lowest collection efficiency"]].map(([value, title]) => <option key={value} value={value}>{title}</option>)}</select></label>
      <label>Balance filter<select value={balance} onChange={e => { setBalance(e.target.value); setPage(1); }}><option value="">All balances</option><option value="outstanding">Outstanding balance</option><option value="unpaid">No verified payments, balance due</option></select></label>
      <label>From (blank includes earlier dues)<input type="date" value={from} onChange={e => { setFrom(e.target.value); setPage(1); }} /></label>
      <label>Through<input type="date" value={to} onChange={e => { setTo(e.target.value); setPage(1); }} /></label>
      <label>Currency<input placeholder="All currencies" maxLength={3} value={currency} onChange={e => { setCurrency(e.target.value.toUpperCase()); setPage(1); }} /></label>
      <label>Search dues<input value={search} maxLength={100} onChange={e => setSearch(e.target.value)} placeholder="Member, invoice, membership ID, or club" /></label>
      <label>Overdue age<select value={age} onChange={e => { setAge(e.target.value); setPage(1); }}><option value="">All ages</option>{[["current", "Current / not yet overdue"], ["1-30", "1–30 days"], ["31-60", "31–60 days"], ["61-90", "61–90 days"], ["90+", "Over 90 days"]].map(([id, title]) => <option key={id} value={id}>{title}</option>)}</select></label>
    </div><div className="header-actions"><button className="secondary" onClick={() => q.refetch()}>Refresh report</button>{["csv","xlsx","pdf"].map(format=><a key={format} className="button secondary" href={"/api/v1"+url+"&format="+format}>Download {format.toUpperCase()}</a>)}</div></section>
    <ErrorBox error={options.error || q.error} />{message && <p className="success" role="status">{message}</p>}
    {q.isPending && <Skeleton />}
    {q.data && <><div className="dues-currency-summaries">{q.data.summary.map((s: any) => <section className="panel" key={s.currency}><div className="section-heading"><b>{s.currency} · {s.members} members · {s.invoices} invoices</b><span>{s.collectionRate}% collected</span></div><div className="dues-kpis">{[["Dues generated", s.billed], ["Pending dues", s.pending], ["To collect", s.availableToCollect], ["Awaiting verification", s.pendingVerification], ["Overdue", s.overdue], ["Verified collections", s.collected]].map(([label, value]) => <div key={String(label)}><small>{label}</small><strong>{money(value, s.currency)}</strong></div>)}</div><small>Billed {money(s.billed, s.currency)} · Credits {money(s.credits, s.currency)} · Due today {money(s.dueToday, s.currency)} · {s.overdueInvoices} overdue invoices</small></section>)}</div>
      <section className="panel"><nav className="dues-breadcrumbs" aria-label="Dues hierarchy">{trail.map((step, index) => <button key={index} className="secondary" onClick={() => { setOrg(step.id); choose(step.kind); setTrail(trail.slice(0, index)); }}>{step.name} →</button>)}<strong>{options.data?.organizations.find((o: any) => o.id === org)?.name || "Authorized scope"}</strong></nav><h2>{q.data.title}</h2><p>Collection efficiency = verified paid ÷ (billed − credits). Rankings apply across all report rows within each currency. Summaries cover the selected invoice scope before balance filters.</p><p>{q.data.total} report rows · Page {page}</p><div className="dues-report-scroll"><table className="dues-report-table"><thead><tr>{columns.map(k => <th key={k}>{labels[k] || k[0].toUpperCase() + k.slice(1)}</th>)}{rowActions && <th>Actions</th>}</tr></thead><tbody>{q.data.items.map((r: any, index: number) => <tr key={r.collectionId || r.invoiceId || index}>{columns.map(k => <td key={k}>{k === "collectionRate" ? <span className="dues-efficiency">{r[k]}%<progress max="100" value={Math.min(100, Number(r[k]))} aria-label={`${r.group} collection efficiency`} /></span> : numeric.has(k) ? money(r[k], r.currency) : k === "daysOverdue" && r[k] > 0 ? <span className="dues-overdue">{r[k]} days</span> : displayValue(r[k])}</td>)}{rowActions && <td className="dues-row-actions">{r.invoiceId && <button className="secondary" onClick={() => setDetail(r.invoiceId)}>Details & follow-up</button>}{r.canReview && <button onClick={() => setReview(r)}>Review collection</button>}{r.groupId && ["club", "zone", "district", "region", "national"].includes(kind) && r.groupId !== "00000000-0000-0000-0000-000000000000" && <button className="secondary" onClick={() => drill(r)}>{kind === "club" ? "Open outstanding members" : "Explore next level"}</button>}</td>}</tr>)}</tbody></table></div>{!q.data.items.length && <p>No matching records. Issue membership-linked dues invoices to populate reports; adjust the date and hierarchy filters if needed.</p>}<div className="pagination"><button className="secondary" disabled={page === 1} onClick={() => setPage(page - 1)}>Previous</button><button className="secondary" disabled={page * 50 >= q.data.total} onClick={() => setPage(page + 1)}>Next</button></div></section>
      <p className="privacy-notice">{q.data.notice}</p></>}
    {compose && <CollectionForm initialInvoice={initialInvoice} onClose={() => setCompose(false)} onSaved={() => { setCompose(false); setMessage("Collection recorded for independent verification. Dues will update after approval."); refresh(); if (initialInvoice) setDetail(initialInvoice.id); }} />}
    {detail && <InvoiceDetails id={detail} onClose={() => setDetail("")} onChanged={refresh} onRecord={(invoice: any) => { setDetail(""); setInitialInvoice(invoice); setCompose(true); }} onReview={(row: any) => { setDetail(""); setReview(row); }} />}
    {review && <CollectionReview row={review} onClose={() => setReview(undefined)} onSaved={() => { setReview(undefined); setMessage("Collection review saved."); refresh(); }} />}
  </section>;
}

export function CollectionForm({ onClose, onSaved, initialInvoice }: any) {
  const { w } = useWorkspace();
  const [search, setSearch] = useState(""), [invoice, setInvoice] = useState<any>(initialInvoice), [method, setMethod] = useState("Cash");
  const [error, setError] = useState<unknown>(), [busy, setBusy] = useState(false), [requestKey] = useState(() => crypto.randomUUID());
  const q = useQuery<any>({ queryKey: ["dues-invoices", w?.id, search], queryFn: () => api(base + "/invoices?search=" + encodeURIComponent(search)) });
  const methodOptions=useQuery<any>({queryKey:["dues-options",w?.id],queryFn:()=>api(base+"/options")});
  return <MemberDrawer title="Record member payment" eyebrow="DUES / COLLECTIONS" onClose={onClose}><form className="form-stack" onSubmit={async e => {
    e.preventDefault(); setBusy(true); setError(null); const fields: any = Object.fromEntries(new FormData(e.currentTarget));
    try { await api(base + "/collections", "POST", { ...fields, invoiceId: invoice.id, amount: Number(fields.amount), method, requestKey }); onSaved(); } catch (e) { setError(e); } finally { setBusy(false); }
  }}>
    <label>Find member or invoice<input value={search} onChange={e => setSearch(e.target.value)} placeholder="Search member name or invoice number" /></label>
    <label>Member dues invoice<select required value={invoice?.id || ""} onChange={e => setInvoice(q.data?.items.find((i: any) => i.id === e.target.value) || initialInvoice)}><option value="">Choose an outstanding invoice</option>{initialInvoice && !q.data?.items.some((i: any) => i.id === initialInvoice.id) && <option value={initialInvoice.id}>{initialInvoice.customer} · {initialInvoice.number}</option>}{q.data?.items.map((i: any) => <option key={i.id} value={i.id}>{i.customer} · {i.number} · {i.entity} · {money(i.outstanding - i.pendingVerification, i.currency)} available</option>)}</select></label>
    {q.data?.total > 50 && <small>Showing the first 50 invoices. Search to find a specific member.</small>}
    {invoice && <p>{invoice.club} · Due {invoice.dueOn} · Outstanding {money(invoice.outstanding, invoice.currency)} · Pending verification {money(invoice.pendingVerification, invoice.currency)}</p>}
    <div className="form-grid"><label>Amount<input name="amount" type="number" min="0.01" step="0.01" defaultValue={initialInvoice ? initialInvoice.outstanding - initialInvoice.pendingVerification : undefined} max={invoice ? invoice.outstanding - invoice.pendingVerification : undefined} required /></label><label>Payment date<input name="date" type="date" defaultValue={today()} max={today()} required /></label></div>
    <label>Payment method<select value={method} onChange={e => setMethod(e.target.value)}>{(methodOptions.data?.collectionMethods||["Cash","BankTransfer","UPI","PersonalAccount"]).map((v:string)=><option key={v} value={v}>{v==='PersonalAccount'?'Paid to a personal account':v==='BankTransfer'?'Official bank transfer':v}</option>)}</select></label>
    <label>{method === "Cash" ? "Cash receipt reference" : "Transaction / UTR reference"}<input name="reference" required maxLength={150} /></label>
    <label>{method === "Cash" ? "Received by (optional)" : "Receiving account holder"}<input name="accountHolder" required={method === "PersonalAccount"} maxLength={200} /></label>
    <label>{method === "PersonalAccount" ? "Receiving UPI ID or bank name + last four digits" : "Receiving account / cash location (optional)"}<input name="destination" required={method === "PersonalAccount"} maxLength={200} /></label>
    <label>Payment evidence / receipt details<input name="evidence" maxLength={2000} placeholder="Receipt number, proof reference, or secure document link" /></label>
    <label>Notes<textarea name="notes" maxLength={2000} rows={3} /></label>
    <p className="privacy-notice">This records the payment for verification. An independent finance reviewer confirms it before dues reduce. Personal-account receipts remain awaiting settlement into the organization’s account.</p>
    <ErrorBox error={error || q.error} /><div className="drawer-footer"><button className="secondary" type="button" onClick={onClose}>Cancel</button><button disabled={busy || !invoice}>{busy ? "Recording…" : "Record for verification"}</button></div>
  </form></MemberDrawer>;
}
export function CollectionReview({ row, onClose, onSaved }: any) {
  const [note, setNote] = useState(""), [busy, setBusy] = useState(false), [error, setError] = useState<unknown>();
  async function save(approve: boolean) {
    setBusy(true); setError(null);
    try { await api(base + "/collections/" + row.collectionId + "/review", "POST", { approve, note }); onSaved(); } catch (e) { setError(e); } finally { setBusy(false); }
  }
  return <MemberDrawer title="Verify club collection" onClose={onClose}><div className="form-stack"><p>{row.member} · {row.club} · {money(row.amount, row.currency)}</p><p>{row.method} · {row.reference}</p><p>{row.accountHolder} · {row.destination}</p><p>{row.evidence}</p><p>{row.notes}</p><label>Verification / rejection note<textarea value={note} onChange={e => setNote(e.target.value)} required maxLength={2000} /></label><p>Approval requires independent financial entity reconciliation authority. It records and allocates the payment; bank settlement remains separate.</p><ErrorBox error={error} /><div className="drawer-footer"><button className="secondary" disabled={busy || !note.trim()} onClick={() => save(false)}>Reject</button><button disabled={busy || !note.trim()} onClick={() => save(true)}>Verify and update dues</button></div></div></MemberDrawer>;
}

export function InvoiceDetails({ id, onClose, onChanged, onRecord, onReview }: any) {
  const { w } = useWorkspace();
  const q = useQuery<any>({ queryKey: ["dues-detail", w?.id, id], queryFn: () => api(base + "/invoices/" + id) });
  const [mode, setMode] = useState(""), [cancelId, setCancelId] = useState(""), [reason, setReason] = useState("");
  const [busy, setBusy] = useState(false), [error, setError] = useState<unknown>();
  const d = q.data, i = d?.invoice;
  return <MemberDrawer title="Member dues & collection history" onClose={onClose}>
    <ErrorBox error={q.error || error} />{q.isPending && <Skeleton />}
    {i && <div className="form-stack"><div><div className="eyebrow">{i.number} · {d.member.number}</div><h2>{i.customer}</h2><p>{d.member.club} · Due {i.dueOn} · <b>{i.paymentStatus}</b></p></div>
      <div className="dues-detail-balances"><div><small>Pending dues</small><strong>{money(i.outstanding, i.currency)}</strong></div><div><small>Awaiting verification</small><strong>{money(d.pendingVerification, i.currency)}</strong></div><div><small>To collect</small><strong>{money(d.availableToCollect, i.currency)}</strong></div></div>
      <p>Billed {money(i.total, i.currency)} · Verified paid {money(i.paid, i.currency)} · Credits {money(i.credited, i.currency)}</p>
      <div className="header-actions">{d.canCollect && <button onClick={() => onRecord({ ...i, club: d.member.club, pendingVerification: d.pendingVerification })}>Collect against this invoice</button>}{d.canFollowUp && <button className="secondary" onClick={() => { setMode(mode === "followup" ? "" : "followup"); setCancelId(""); }}>Add follow-up</button>}</div>
      {mode === "followup" && <FollowupForm invoice={i} onCancel={() => setMode("")} onSaved={() => { setMode(""); onChanged(); }} />}
      {cancelId && <form className="form-stack panel" onSubmit={async e => { e.preventDefault(); setBusy(true); setError(null); try { await api(base + "/collections/" + cancelId + "/cancel", "POST", { reason }); setCancelId(""); setReason(""); onChanged(); } catch (e) { setError(e); } finally { setBusy(false); } }}><h3>Cancel an unverified collection</h3><label>Cancellation reason<textarea required maxLength={2000} value={reason} onChange={e => setReason(e.target.value)} /></label><p>The original entry stays in the audit history. Its reserved amount becomes available to collect again.</p><div className="header-actions"><button disabled={busy || !reason.trim()}>Confirm cancellation</button><button className="secondary" type="button" onClick={() => setCancelId("")}>Keep entry</button></div></form>}
      <section><h3>Recorded collections</h3>{!d.collections.length && <p>No collections recorded yet.</p>}{d.collections.map((c: any) => <article className="dues-history-item" key={c.id}><div className="section-heading"><b>{money(c.amount, c.currency)} · {c.method === "PersonalAccount" ? "Personal account" : c.method}</b><span className={c.status === "Approved" ? "dues-approved" : ""}>{c.status}</span></div><p>{c.date} · {c.reference} · Recorded by {c.recorder}</p>{c.accountHolder && <p>{c.accountHolder} · {c.destination}</p>}{c.evidence && <p>Evidence: {c.evidence}</p>}{c.notes && <p>{c.notes}</p>}{c.reviewNote && <p>Review: {c.reviewNote} · {c.reviewer}</p>}<div className="header-actions">{c.canReview && <button onClick={() => onReview({ ...c, collectionId: c.id, member: i.customer, club: d.member.club })}>Review collection</button>}{c.canCancel && <button className="secondary" onClick={() => { setCancelId(c.id); setMode(""); setReason(""); }}>Cancel mistaken entry</button>}</div></article>)}</section>
      <section><h3>Verified payments & receipts</h3>{!d.payments.length && <p>No verified payments allocated to this invoice yet.</p>}{d.payments.map((p: any, index: number) => <article className="dues-history-item" key={p.id + ":" + index}><b>{p.receiptNumber} · {money(p.amount, i.currency)}</b><p>{p.date} · {p.reference} · {p.settlementStatus}</p></article>)}</section>
      <section><h3>Follow-up history</h3>{!d.followups.length && <p>No follow-ups recorded yet.</p>}{d.followups.map((f: any) => <article className="dues-history-item" key={f.id}><b>{f.outcome === "PromiseToPay" ? "Promise to pay" : f.outcome === "NoAnswer" ? "No answer" : f.outcome}</b><p>{f.notes}</p>{f.promisedOn && <p>Promised {money(f.promisedAmount, i.currency)} on {f.promisedOn}</p>}{f.nextFollowUp && <p>Next follow-up: {f.nextFollowUp}</p>}<small>{f.actor} · {new Date(f.createdAt).toLocaleString()}</small></article>)}</section>
    </div>}
  </MemberDrawer>;
}

function FollowupForm({ invoice, onCancel, onSaved }: any) {
  const [outcome, setOutcome] = useState("Contacted"), [busy, setBusy] = useState(false), [error, setError] = useState<unknown>();
  const [requestKey] = useState(() => crypto.randomUUID());
  return <form className="form-stack panel" onSubmit={async e => {
    e.preventDefault(); setBusy(true); setError(null); const data: any = Object.fromEntries(new FormData(e.currentTarget));
    try { await api(base + "/invoices/" + invoice.id + "/followups", "POST", { outcome, notes: data.notes, nextFollowUp: data.nextFollowUp || null, promisedOn: outcome === "PromiseToPay" ? data.promisedOn : null, promisedAmount: outcome === "PromiseToPay" ? Number(data.promisedAmount) : null, requestKey }); onSaved(); } catch (e) { setError(e); } finally { setBusy(false); }
  }}><h3>Record member follow-up</h3><label>Follow-up outcome<select value={outcome} onChange={e => setOutcome(e.target.value)}>{[["Contacted", "Contacted"], ["NoAnswer", "No answer"], ["PromiseToPay", "Promise to pay"], ["Disputed", "Disputed"], ["Resolved", "Resolved"]].map(([v, title]) => <option value={v} key={v}>{title}</option>)}</select></label><label>Follow-up notes<textarea name="notes" required maxLength={2000} rows={3} /></label><label>Next follow-up date<input name="nextFollowUp" type="date" min={today()} /></label>{outcome === "PromiseToPay" && <div className="form-grid"><label>Promised payment date<input name="promisedOn" type="date" required min={today()} defaultValue={today()} /></label><label>Promised amount<input name="promisedAmount" type="number" min="0.01" step="0.01" required max={invoice.outstanding} defaultValue={invoice.outstanding} /></label></div>}<p>Record the outcome of your contact with the member. A payment promise does not change the dues balance.</p><ErrorBox error={error} /><div className="header-actions"><button disabled={busy}>Save follow-up</button><button type="button" className="secondary" onClick={onCancel}>Cancel follow-up</button></div></form>;
}
