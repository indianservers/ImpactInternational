import { useState } from "react";
import { Link } from "react-router-dom";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { Send, Users } from "lucide-react";
import { api } from "../api";
import { ErrorBox, Skeleton, useWorkspace } from "../App";
import { MemberDrawer } from "../MemberComponents";
import { impactRole } from "../brand";

export function useMemberScope() {
  const { w } = useWorkspace();
  return useQuery<any>({ queryKey: ["communication-member-scope", w?.id], queryFn: () => api("/communications/member-scope"), staleTime: 10000, refetchInterval: 30000, enabled: !!w });
}

export function CommunicationScopeBanner() {
  const { w } = useWorkspace();
  const q = useMemberScope();
  return <section className="comm-scope-banner" aria-label="Your communication scope"><Users size={22} /><div><strong>{impactRole(w?.role)} · {w?.organization}</strong><p>{q.data ? `${q.data.totalMembers} active members across ${q.data.clubs} clubs in your ${q.data.type.toLowerCase()} scope.` : "Your communication audience follows your assigned hierarchy."}</p></div>{w?.permissions.includes("communications.send") && <Link className="button" to="/app/communications/Members"><Send size={15} />Send to my members</Link>}</section>;
}

export default function CommunicationMembers() {
  const q = useMemberScope();
  const { w } = useWorkspace();
  const history = useQuery<any[]>({ queryKey: ["communication-member-history", w?.id], queryFn: () => api("/communications/member-messages"), enabled: !!w?.permissions.includes("communications.send") });
  const [open, setOpen] = useState(false), [result, setResult] = useState<any>();
  if (q.isPending) return <Skeleton />;
  if (!q.data) return <ErrorBox error={q.error} />;
  const scope = q.data;
  return <section className="panel comm-member-outreach">
    <div className="section-heading"><div><div className="eyebrow">MESSAGES FOR YOUR COMMUNITY</div><h2>Send to my members</h2><p>Your message stays within {scope.name} and its child levels.</p></div><button onClick={() => setOpen(true)} disabled={!scope.canSend || scope.reachableMembers === 0}><Send size={16} />Compose message</button></div>
    <div className="comm-scope-stats"><div><strong>{scope.totalMembers}</strong><span>Active members in scope</span></div><div><strong>{scope.clubs}</strong><span>Clubs in scope</span></div><div><strong>{scope.reachableMembers}</strong><span>Can receive in-app messages</span></div></div>
    <p className="privacy-notice">National messages reach members in the national unit’s regions, districts, zones, and clubs. Regional, district, zonal, and club messages follow the same rule within their own branch.</p>
    {scope.unlinkedMembers > 0 && <p className="privacy-notice">{scope.unlinkedMembers} members do not have a linked login yet. In-app announcements can reach linked member accounts; invite the remaining members to activate their accounts. <Link to="/app/members">Manage members →</Link></p>}
    <h3>Your sending scope</h3><p><b>{scope.name}</b> · {scope.type}</p>
    <p>Recipients see messages under Communication Center → Announcements. Only members in the selected scope receive the announcement.</p>
    {scope.sample?.length > 0 && <details><summary>Preview members in this scope</summary><ul>{scope.sample.map((p: any) => <li key={p.id}>{p.name}</li>)}</ul><small>Showing up to 10 members. The recipient list is checked again when you send.</small></details>}
    <p className="muted">For email, WhatsApp, or SMS, use Campaigns with a configured sending account and the existing approval process.</p>
    {result && <div className="success" role="status">Sent to {result.delivered} members. {result.excluded > 0 ? `${result.excluded} were excluded because a login or communication consent was unavailable.` : "All recipients received the announcement."}</div>}
    <section className="comm-member-history"><h3>Recent member announcements</h3><ErrorBox error={history.error} />{history.data?.length ? history.data.map(message => <div className="context-row" key={message.id}><div><b>{message.title}</b><small>{message.organization} · {new Date(message.at).toLocaleString()}</small></div><span>{message.delivered} delivered · {message.excluded} excluded</span></div>) : <p>No member announcements have been sent in this scope yet.</p>}</section>
    {open && <MemberMessageComposer scope={scope} onClose={() => setOpen(false)} onSent={(sent: any) => { setResult(sent); setOpen(false); }} />}
  </section>;
}

function MemberMessageComposer({ scope, onClose, onSent }: any) {
  const qc = useQueryClient();
  const [title, setTitle] = useState(""), [body, setBody] = useState(""), [busy, setBusy] = useState(false), [error, setError] = useState<unknown>();
  const [requestKey] = useState(() => crypto.randomUUID());
  return <MemberDrawer title="Message your members" eyebrow="IMPACT INTERNATIONAL / SCOPED COMMUNICATION" onClose={onClose}><form className="form-stack" onSubmit={async e => {
    e.preventDefault(); setBusy(true); setError(null);
    try { const sent = await api("/communications/member-messages", "POST", { orgId: scope.id, title, body, requestKey }); qc.invalidateQueries({ queryKey: ["gov"] }); qc.invalidateQueries({ queryKey: ["communication-member-scope"] }); qc.invalidateQueries({ queryKey: ["communication-member-history"] }); onSent(sent); }
    catch (e) { setError(e); } finally { setBusy(false); }
  }}>
    <label>Send within<input readOnly value={scope.name + " · " + scope.type} /></label>
    <p className="privacy-notice">Up to {scope.reachableMembers} linked members in your branch will receive this in-app announcement. Members outside your scope are excluded.</p>
    <label>Message title<input required minLength={2} maxLength={200} value={title} onChange={e => setTitle(e.target.value)} /></label>
    <label>Message<textarea required rows={7} maxLength={12000} value={body} onChange={e => setBody(e.target.value)} placeholder="Write to the members you lead…" /></label>
    <small>Optional personalization: {"{{member_name}}"}, {"{{club_name}}"}, {"{{member_id}}"}.</small>
    <ErrorBox error={error} /><div className="drawer-footer"><button type="button" className="secondary" onClick={onClose}>Cancel</button><button disabled={busy || !title.trim() || !body.trim()}>{busy ? "Sending…" : "Send to my members"}</button></div>
  </form></MemberDrawer>;
}
