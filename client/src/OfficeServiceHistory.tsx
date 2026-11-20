import { useEffect, useRef, useState } from "react";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { api } from "./api";
import { ErrorBox, Skeleton, useWorkspace } from "./App";
import { MemberDrawer } from "./MemberComponents";
import { useLanguage } from "./i18n";
import "./OfficeServiceHistory.css";

type ServiceRecord = { id: string; name: string; officeTitle: string; isPresident: boolean; startYear: number; endYear: number; notes: string; source: string; version: number };
function InlineEditor({ title, children, onClose }: any) {
  const ref = useRef<HTMLElement>(null);
  useEffect(() => { ref.current?.scrollIntoView({ block: "nearest" }); ref.current?.querySelector<HTMLInputElement>("input,textarea")?.focus(); }, []);
  return <section ref={ref} className="service-inline-editor" aria-label={title}><header><h4>{title}</h4><button type="button" className="icon-button" aria-label="Close editor" onClick={onClose}>×</button></header>{children}</section>;
}
export default function OfficeServiceHistory({ orgId, organization, embedded = false }: { orgId: string; organization: string; embedded?: boolean }) {
  const Editor = embedded ? InlineEditor : MemberDrawer;
  const { w } = useWorkspace(), { t } = useLanguage(), qc = useQueryClient();
  const [presidents, setPresidents] = useState(true), [year, setYear] = useState(""), [page, setPage] = useState(1);
  const [form, setForm] = useState<any>(), [archiving, setArchiving] = useState<ServiceRecord>(), [reason, setReason] = useState("");
  const [error, setError] = useState<unknown>(), [busy, setBusy] = useState(false), [notice, setNotice] = useState("");
  useEffect(() => { setPage(1); setForm(undefined); setArchiving(undefined); setNotice(""); setError(undefined); }, [orgId]);
  const path = `/organizations/${orgId}/office-service-records`;
  const q = useQuery({ queryKey: ["office-service-records", w?.id, orgId, presidents, year, page], queryFn: () => api(path + "?" + new URLSearchParams({ page: String(page), presidentsOnly: String(presidents), ...(year ? { year } : {}) })) });
  function edit(record?: ServiceRecord) {
    setError(undefined);
    setForm(record ? { ...record } : { name: "", officeTitle: "President", isPresident: true, startYear: new Date().getFullYear() - 1, endYear: new Date().getFullYear() - 1, notes: "", version: 1 });
  }
  async function save(event: React.FormEvent) {
    event.preventDefault(); if (busy) return; setBusy(true); setError(undefined);
    try {
      await api(path + (form.id ? "/" + form.id : ""), form.id ? "PUT" : "POST", { ...form, startYear: Number(form.startYear), endYear: Number(form.endYear) });
      setForm(undefined); setNotice(t("Historical record saved."));
      await qc.invalidateQueries({ queryKey: ["office-service-records"] });
    } catch (error) { setError(error); }
    finally { setBusy(false); }
  }
  return <section className="office-service-history" aria-label={t("Service history")}>
    <header><div><h3>{t("Past office bearers")}</h3><p>{organization}</p></div>{q.data?.canManage && <button type="button" onClick={() => edit()}>{t("Add historical record")}</button>}</header>
    <p className="service-history-help">{t("Completed appointments appear automatically. Add older service records here; these records do not grant access or membership.")}</p>
    <div className="service-history-filters"><div role="group" aria-label={t("Service history")}><button type="button" className={presidents ? "active" : "secondary"} aria-pressed={presidents} onClick={() => { setPresidents(true); setPage(1); }}>{t("Past presidents")}</button><button type="button" className={!presidents ? "active" : "secondary"} aria-pressed={!presidents} onClick={() => { setPresidents(false); setPage(1); }}>{t("All past offices")}</button></div>
      <label>{t("Service year")}<select value={year} onChange={e => { setYear(e.target.value); setPage(1); }}><option value="">{t("All years")}</option>{Array.from({ length: new Date().getFullYear() - 1799 }, (_, i) => new Date().getFullYear() - i).map(y => <option key={y} value={y}>{y}</option>)}</select></label>
    </div>
    {!form && !archiving && <ErrorBox error={error || q.error} />}{notice && <p role="status" className="success">{notice}</p>}
    {q.isPending ? <Skeleton /> : <div className="service-record-list">{q.data?.items.map((record: ServiceRecord) => <article className="service-record" key={record.source + record.id}>
      <div className="service-years"><span>{t("Service years")}</span><strong>{record.startYear === record.endYear ? record.startYear : `${record.startYear}–${record.endYear}`}</strong></div>
      <div className="service-record-person"><h4>{record.name}</h4><p>{record.officeTitle}</p><small>{t(record.source)}</small>{record.notes && <p className="service-record-notes">{record.notes}</p>}</div>
      {q.data.canManage && record.source === "Historical record" && <div className="service-record-actions"><button type="button" className="secondary" onClick={() => edit(record)}>{t("Edit")}</button><button type="button" className="text-button" onClick={() => { setArchiving(record); setReason(""); setError(undefined); }}>{t("Archive")}</button></div>}
    </article>)}{!q.data?.items.length && !q.error && <p>{t("No past office bearers recorded for this selection.")}</p>}</div>}
    {!!q.data?.total && <footer><span>{q.data.total} · {q.data.page} / {Math.ceil(q.data.total / 25)}</span><button type="button" className="secondary" disabled={q.data.page <= 1} onClick={() => setPage(q.data.page - 1)}>{t("Previous")}</button><button type="button" className="secondary" disabled={q.data.page * 25 >= q.data.total} onClick={() => setPage(q.data.page + 1)}>{t("Next")}</button></footer>}
    {form && <Editor title={t(form.id ? "Edit historical record" : "Add historical record")} onClose={() => !busy && setForm(undefined)}><form className="form-stack" onSubmit={save}><ErrorBox error={error} /><p><strong>{organization}</strong><br />{t("The organization and service years stay attached to this historical record.")}</p>
      <label>{t("Office bearer name")}<input required minLength={2} maxLength={200} value={form.name} onChange={e => setForm({ ...form, name: e.target.value })} /></label>
      <label>{t("Office title")}<input required minLength={2} maxLength={150} list="historical-office-titles" value={form.officeTitle} onChange={e => setForm({ ...form, officeTitle: e.target.value, isPresident: /president/i.test(e.target.value) && !/vice|elect/i.test(e.target.value) })} /><datalist id="historical-office-titles">{["President", "Secretary", "Treasurer", "Vice President", "Joint Secretary", "Director"].map(title => <option key={title} value={title} />)}</datalist></label>
      <label className="checkbox"><input type="checkbox" checked={form.isPresident} onChange={e => setForm({ ...form, isPresident: e.target.checked })} />{t("Include in past presidents")}</label>
      <div className="form-grid"><label>{t("Start year")}<input type="number" required min={1800} max={new Date().getFullYear()} value={form.startYear} onChange={e => setForm({ ...form, startYear: e.target.value })} /></label><label>{t("End year")}<input type="number" required min={form.startYear || 1800} max={new Date().getFullYear()} value={form.endYear} onChange={e => setForm({ ...form, endYear: e.target.value })} /></label></div>
      <label>{t("Notes")}<textarea maxLength={2000} rows={3} value={form.notes} onChange={e => setForm({ ...form, notes: e.target.value })} /></label><div className="drawer-footer"><button type="button" className="secondary" disabled={busy} onClick={() => setForm(undefined)}>{t("Cancel")}</button><button disabled={busy}>{t(busy ? "Saving…" : "Save record")}</button></div>
    </form></Editor>}
    {archiving && <Editor title={t("Archive record")} onClose={() => !busy && setArchiving(undefined)}><form className="form-stack" onSubmit={async e => {
      e.preventDefault(); if (busy) return; setBusy(true); setError(undefined);
      try { await api(path + "/" + archiving.id + "/archive", "POST", { version: archiving.version, reason }); setArchiving(undefined); setNotice(t("Historical record archived.")); await qc.invalidateQueries({ queryKey: ["office-service-records"] }); }
      catch (error) { setError(error); } finally { setBusy(false); }
    }}><ErrorBox error={error} /><p>{archiving.name} · {archiving.officeTitle} · {archiving.startYear}–{archiving.endYear}</p><label>{t("Archive reason")}<textarea required minLength={3} maxLength={500} value={reason} onChange={e => setReason(e.target.value)} /></label><button disabled={busy}>{t("Archive record")}</button></form></Editor>}
  </section>;
}
