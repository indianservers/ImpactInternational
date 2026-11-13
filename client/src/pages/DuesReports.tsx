import { useState } from "react";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { api } from "../api";
import { ErrorBox, Skeleton, useWorkspace } from "../App";
import { MemberDrawer } from "../MemberComponents";

const base = "/finance/dues-center";
const today = () => new Date().toISOString().slice(0, 10);
const money = (v: any, c: string) => new Intl.NumberFormat(undefined, { style: "currency", currency: c }).format(Number(v ?? 0));
const labels: Record<string, string> = { group: "Group", memberId: "Member ID", accountHolder: "Account holder", pendingVerification: "Pending verification", dueOn: "Due date", collected: "Verified collections", pending: "Pending dues", billed: "Billed", credits: "Credits / waivers", submitted: "Recorded total", destination: "Receiving account / UPI", reviewNote: "Review note" };
const numeric = new Set(["amount", "billed", "collected", "pending", "credits", "overdue", "approved", "rejected", "submitted", "pendingVerification"]);

export default function DuesReports({ collecting = false }: { collecting?: boolean }) {
  const { w } = useWorkspace();
  const qc = useQueryClient();
  const [kind, setKind] = useState(collecting ? "verification" : "overview");
  const [org, setOrg] = useState(""), [level, setLevel] = useState(""), [currency, setCurrency] = useState("");
  const [from, setFrom] = useState(""), [to, setTo] = useState(today()), [page, setPage] = useState(1);
  const [compose, setCompose] = useState(false), [review, setReview] = useState<any>(), [message, setMessage] = useState("");
  const allowed = !!w?.permissions.includes("finance.dues.reports");
  const options = useQuery<any>({ queryKey: ["dues-options", w?.id], queryFn: () => api(base + "/options"), enabled: allowed });
  const params = new URLSearchParams({ to, page: String(page) });
  if (from) params.set("from", from);
  if (org) params.set("orgId", org);
  if (currency) params.set("currency", currency);
  const url = base + "/reports/" + kind + "?" + params;
  const q = useQuery<any>({ queryKey: ["dues-report", w?.id, url], queryFn: () => api(url), enabled: allowed && !!to });
  function refresh() { qc.invalidateQueries({ queryKey: ["dues-report"] }); qc.invalidateQueries({ queryKey: ["dues-invoices"] }); qc.invalidateQueries({ queryKey: ["finance"] }); }
  if (!allowed) return <section className="panel"><h2>Dues reports</h2><p>Your login does not have access to organizational dues reports. Use My finances to see your own invoices.</p></section>;
  const columns: string[] = Array.from(new Set<string>((q.data?.items ?? []).flatMap((r: any) => Object.keys(r)))).filter(k => !["invoiceId", "collectionId", "groupId", "collectorId", "canReview"].includes(k));
  return <section className="dues-center">
    <header className="op-header"><div><p className="op-eyebrow">IMPACT INTERNATIONAL / DUES & COLLECTIONS</p><h1>{collecting ? "Record club collections" : "Dues and collection reports"}</h1><p>21 reports for club, zone, district, region, national, and member dues. Your assigned hierarchy controls access.</p></div>{options.data?.canCollect && <button onClick={() => setCompose(true)}>Record payment</button>}</header>
    <section className="panel"><div className="form-grid">
      <label>Report type<select value={kind} onChange={e => { setKind(e.target.value); setPage(1); }}>{options.data?.reports.map((r: any) => <option key={r.id} value={r.id}>{r.name}</option>)}</select></label>
      <label>Hierarchy level<select value={level} onChange={e => { setLevel(e.target.value); setOrg(""); setPage(1); }}><option value="">All levels</option>{["National", "Region", "District", "Zone", "Club"].map(l => <option key={l}>{l}</option>)}</select></label>
      <label>Organization scope<select value={org} onChange={e => { setOrg(e.target.value); setPage(1); }}><option value="">My entire authorized scope</option>{options.data?.organizations.filter((o: any) => !level || o.type === level).map((o: any) => <option key={o.id} value={o.id}>{o.name} · {o.type}</option>)}</select></label>
      <label>From (blank includes earlier dues)<input type="date" value={from} onChange={e => { setFrom(e.target.value); setPage(1); }} /></label>
      <label>Through<input type="date" value={to} onChange={e => { setTo(e.target.value); setPage(1); }} /></label>
      <label>Currency<input placeholder="All currencies" maxLength={3} value={currency} onChange={e => { setCurrency(e.target.value.toUpperCase()); setPage(1); }} /></label>
    </div><div className="header-actions"><button className="secondary" onClick={() => q.refetch()}>Refresh report</button><a className="button secondary" href={"/api/v1" + url + "&format=csv"}>Download full report CSV</a></div></section>
    <ErrorBox error={options.error || q.error} />{message && <p className="success" role="status">{message}</p>}
    {q.isPending && <Skeleton />}
    {q.data && <><div className="fin-stats">{q.data.summary.map((s: any) => <section className="panel" key={s.currency}><b>{s.currency} · {s.invoices} invoices</b><p>Billed: {money(s.billed, s.currency)}</p><p>Verified collections: {money(s.collected, s.currency)}</p><p>Credits / waivers: {money(s.credits, s.currency)}</p><strong>Pending dues: {money(s.pending, s.currency)}</strong><p>Overdue: {money(s.overdue, s.currency)}</p></section>)}</div>
      <section className="panel"><h2>{q.data.title}</h2><p>{q.data.total} report rows · Page {page}</p><div className="dues-report-scroll"><table className="dues-report-table"><thead><tr>{columns.map(k => <th key={k}>{labels[k] || k[0].toUpperCase() + k.slice(1)}</th>)}{q.data.items.some((r: any) => r.canReview) && <th>Review</th>}</tr></thead><tbody>{q.data.items.map((r: any, index: number) => <tr key={r.invoiceId || r.collectionId || index}>{columns.map(k => <td key={k}>{numeric.has(k) ? money(r[k], r.currency) : String(r[k] ?? "—")}</td>)}{r.canReview && <td><button className="secondary" onClick={() => setReview(r)}>Review collection</button></td>}</tr>)}</tbody></table></div>{!q.data.items.length && <p>No matching records. Issue membership-linked dues invoices to populate reports; adjust the date and hierarchy filters if needed.</p>}<div className="pagination"><button className="secondary" disabled={page === 1} onClick={() => setPage(page - 1)}>Previous</button><button className="secondary" disabled={page * 50 >= q.data.total} onClick={() => setPage(page + 1)}>Next</button></div></section>
      <p className="privacy-notice">{q.data.notice}</p></>}
    {compose && <CollectionForm onClose={() => setCompose(false)} onSaved={() => { setCompose(false); setMessage("Collection recorded for independent verification. Dues will update after approval."); refresh(); }} />}
    {review && <CollectionReview row={review} onClose={() => setReview(undefined)} onSaved={() => { setReview(undefined); setMessage("Collection review saved."); refresh(); }} />}
  </section>;
}

function CollectionForm({ onClose, onSaved }: any) {
  const { w } = useWorkspace();
  const [search, setSearch] = useState(""), [invoice, setInvoice] = useState<any>(), [method, setMethod] = useState("Cash");
  const [error, setError] = useState<unknown>(), [busy, setBusy] = useState(false), [requestKey] = useState(() => crypto.randomUUID());
  const q = useQuery<any>({ queryKey: ["dues-invoices", w?.id, search], queryFn: () => api(base + "/invoices?search=" + encodeURIComponent(search)) });
  return <MemberDrawer title="Record member payment" onClose={onClose}><form className="form-stack" onSubmit={async e => {
    e.preventDefault(); setBusy(true); setError(null); const fields: any = Object.fromEntries(new FormData(e.currentTarget));
    try { await api(base + "/collections", "POST", { ...fields, invoiceId: invoice.id, amount: Number(fields.amount), method, requestKey }); onSaved(); } catch (e) { setError(e); } finally { setBusy(false); }
  }}>
    <label>Find member or invoice<input value={search} onChange={e => setSearch(e.target.value)} placeholder="Search member name or invoice number" /></label>
    <label>Member dues invoice<select required value={invoice?.id || ""} onChange={e => setInvoice(q.data?.items.find((i: any) => i.id === e.target.value))}><option value="">Choose an outstanding invoice</option>{q.data?.items.map((i: any) => <option key={i.id} value={i.id}>{i.customer} · {i.number} · {i.entity} · {money(i.outstanding - i.pendingVerification, i.currency)} available</option>)}</select></label>
    {q.data?.total > 50 && <small>Showing the first 50 invoices. Search to find a specific member.</small>}
    {invoice && <p>{invoice.club} · Due {invoice.dueOn} · Outstanding {money(invoice.outstanding, invoice.currency)} · Pending verification {money(invoice.pendingVerification, invoice.currency)}</p>}
    <div className="form-grid"><label>Amount<input name="amount" type="number" min="0.01" step="0.01" max={invoice ? invoice.outstanding - invoice.pendingVerification : undefined} required /></label><label>Payment date<input name="date" type="date" defaultValue={today()} max={today()} required /></label></div>
    <label>Payment method<select value={method} onChange={e => setMethod(e.target.value)}>{[["Cash", "Cash"], ["PersonalAccount", "Paid to a personal account"], ["BankTransfer", "Official bank transfer"], ["UPI", "Official UPI"]].map(([v, t]) => <option key={v} value={v}>{t}</option>)}</select></label>
    <label>{method === "Cash" ? "Cash receipt reference" : "Transaction / UTR reference"}<input name="reference" required maxLength={150} /></label>
    <label>{method === "Cash" ? "Received by (optional)" : "Receiving account holder"}<input name="accountHolder" required={method === "PersonalAccount"} maxLength={200} /></label>
    <label>{method === "PersonalAccount" ? "Receiving UPI ID or bank name + last four digits" : "Receiving account / cash location (optional)"}<input name="destination" required={method === "PersonalAccount"} maxLength={200} /></label>
    <label>Payment evidence / receipt details<input name="evidence" maxLength={2000} placeholder="Receipt number, proof reference, or secure document link" /></label>
    <label>Notes<textarea name="notes" maxLength={2000} rows={3} /></label>
    <p className="privacy-notice">This records the payment for verification. An independent finance reviewer confirms it before dues reduce. Personal-account receipts remain awaiting settlement into the organization’s account.</p>
    <ErrorBox error={error || q.error} /><div className="drawer-footer"><button className="secondary" type="button" onClick={onClose}>Cancel</button><button disabled={busy || !invoice}>{busy ? "Recording…" : "Record for verification"}</button></div>
  </form></MemberDrawer>;
}
function CollectionReview({ row, onClose, onSaved }: any) {
  const [note, setNote] = useState(""), [busy, setBusy] = useState(false), [error, setError] = useState<unknown>();
  async function save(approve: boolean) {
    setBusy(true); setError(null);
    try { await api(base + "/collections/" + row.collectionId + "/review", "POST", { approve, note }); onSaved(); } catch (e) { setError(e); } finally { setBusy(false); }
  }
  return <MemberDrawer title="Verify club collection" onClose={onClose}><div className="form-stack"><p>{row.member} · {row.club} · {money(row.amount, row.currency)}</p><p>{row.method} · {row.reference}</p><p>{row.accountHolder} · {row.destination}</p><p>{row.evidence}</p><p>{row.notes}</p><label>Verification / rejection note<textarea value={note} onChange={e => setNote(e.target.value)} required maxLength={2000} /></label><p>Approval requires independent financial entity reconciliation authority. It records and allocates the payment; bank settlement remains separate.</p><ErrorBox error={error} /><div className="drawer-footer"><button className="secondary" disabled={busy || !note.trim()} onClick={() => save(false)}>Reject</button><button disabled={busy || !note.trim()} onClick={() => save(true)}>Verify and update dues</button></div></div></MemberDrawer>;
}
