import { useEffect, useState } from "react";
import { Link, useSearchParams } from "react-router-dom";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { Plus, Pencil, Search, Network } from "lucide-react";
import { api, type Org } from "../api";
import { PageHeader, ErrorBox, Skeleton, useWorkspace } from "../App";
import { MemberDrawer } from "../MemberComponents";
import HierarchyTree from "../HierarchyTree";
import ImpactSetupGuide, { useStructure, unitKinds, unitPaths, unitPlural, unitSingular, type UnitKind } from "../ImpactSetupGuide";

type UnitForm = Partial<Org> & { timeZone?: string; currency?: string };
export default function StructureManagement({ kind, international = false }: { kind?: UnitKind; international?: boolean }) {
  const { w, me } = useWorkspace(), qc = useQueryClient();
  const structure = useStructure();
  const [searchParams, setSearchParams] = useSearchParams();
  const [search, setSearch] = useState(""), [term, setTerm] = useState(""), [status, setStatus] = useState(""), [page, setPage] = useState(1);
  const [form, setForm] = useState<UnitForm | null>(null), [detail, setDetail] = useState<Org | null>(null);
  const [error, setError] = useState<unknown>(), [busy, setBusy] = useState(false), [notice, setNotice] = useState("");
  const [parentSearch, setParentSearch] = useState("");
  const canCreate = me?.platformAdmin || w?.permissions.includes("organization.create");
  const canEdit = me?.platformAdmin || w?.permissions.includes("organization.update");
  const level = structure.data?.levels.find(l => l.name === kind);
  const formLevel = structure.data?.levels.find(l => l.id === form?.levelId);
  const formKind = formLevel?.name || kind || "Unit";
  const parentLevel = structure.data?.levels.find(l => l.id === formLevel?.parentLevelId);
  const parentLabel = formKind === "National" ? "Organization" : parentLevel?.name || "Parent";
  useEffect(() => { const t = setTimeout(() => { setTerm(search); setPage(1); }, 250); return () => clearTimeout(t); }, [search]);
  useEffect(() => { setPage(1); setSearch(""); setTerm(""); setForm(null); setDetail(null); setStatus(""); setNotice(""); }, [kind]);
  const units = useQuery({
    queryKey: ["managed-units", w?.id, kind, term, status, page, searchParams.get("scopeId")],
    queryFn: () => api("/organizations?" + new URLSearchParams({ levelId: level!.id, search: term, status, ...(searchParams.get("scopeId")?{scopeId:searchParams.get("scopeId")!}:{}), page: String(page) })),
    enabled: !!level,
  });
  const parents = useQuery({
    queryKey: ["unit-parents", w?.id, formLevel?.parentLevelId, parentSearch],
    queryFn: () => api("/organizations?" + new URLSearchParams({ levelId: formLevel!.parentLevelId!, search: parentSearch, size: "100", status: "Active" })),
    enabled: !!form && !!formLevel?.parentLevelId,
  });
  function create(selected: UnitKind, parentId?: string) {
    const target = structure.data?.levels.find(l => l.name === selected);
    if (!target) return;
    setError(null); setNotice(""); setParentSearch("");
    setForm({ name: "", code: "", levelId: target.id, parentId: parentId || (selected === "National" ? structure.data!.root.id : ""), city: "", country: "India", state: "", status: "Active", version: 1, timeZone: "Asia/Kolkata", currency: "INR" });
  }
  useEffect(() => {
    if (kind && level && searchParams.get("create") === "1" && canCreate) {
      create(kind, searchParams.get("parentId") || undefined); setSearchParams({}, { replace: true });
    }
  }, [kind, level?.id, searchParams, canCreate]);
  useEffect(() => {
    if (form && !form.id && !form.parentId && parents.data?.items.length === 1)
      setForm(current => current && !current.parentId ? { ...current, parentId: parents.data.items[0].id } : current);
  }, [parents.data, form?.id, form?.parentId]);
  async function open(id: string, edit = false) {
    try { setError(null); const value = await api<Org>("/organizations/" + id); if (edit) { setParentSearch(""); setForm(value); } else setDetail(value); } catch (e) { setError(e); }
  }
  async function save(e: React.FormEvent) {
    e.preventDefault(); if (!form) return;
    setBusy(true); setError(null);
    try {
      await api("/organizations" + (form.id ? "/" + form.id : ""), form.id ? "PUT" : "POST", { ...form, charterDate: form.charterDate || null });
      setNotice(formKind + (form.id ? " updated." : " created.")); setForm(null);
      for (const key of ["managed-units", "impact-structure", "unit-parents", "organizations", "tree-children", "tree-root", "dashboard", "gov", "member-catalog"])
        qc.invalidateQueries({ queryKey: [key] });
    } catch (e) { setError(e); } finally { setBusy(false); }
  }
  const previousKind = kind && unitKinds.indexOf(kind) > 0 ? unitKinds[unitKinds.indexOf(kind) - 1] : null;
  return <>
    <PageHeader eyebrow="ONE ORGANIZATION. A CLEAR STRUCTURE." title={kind ? "Manage " + unitPlural(kind).toLowerCase() : international ? "International headquarters" : "Impact International structure"}
      description={kind ? (kind === "National" ? "Create and manage national units reporting to Impact International headquarters." : "Create and manage " + unitPlural(kind).toLowerCase() + " within their " + unitSingular(previousKind!) + ".") : "International → National → Regions → Districts → Zones → Clubs. All belong to Impact International."}>
      {kind && <div className="header-actions">{canCreate && <button onClick={() => create(kind)} disabled={!level}><Plus size={17} />Create {unitSingular(kind)}</button>}{kind === "Club" && <Link className="button secondary" to="/app/clubs/directory">Directory & insights</Link>}</div>}
    </PageHeader>
    <ImpactSetupGuide />
    <ErrorBox error={structure.error || (!form && error) || units.error} />
    {notice && <div className="success" role="status">{notice}</div>}
    {!kind && international ? <section className="panel impact-headquarters"><img src="/brand/impact-logo.webp" alt="" width="76" height="76" /><div><h2>Impact International</h2><p>Your single organization and the International level of the hierarchy.</p><p>Manage national units below this headquarters. International officers oversee the full structure.</p><div className="header-actions"><Link className="button" to="/app/national">Manage national units →</Link><Link className="button secondary" to="/app/leadership">International leadership</Link><Link to="/app/settings">Headquarters settings</Link></div></div></section> : !kind ? <>
      <div className="impact-unit-cards">{unitKinds.map(item => <section className="panel" key={item}><Network size={22} /><h2>{unitPlural(item)}</h2><strong>{structure.data?.counts[item] ?? "—"}</strong><p>{item === "National" ? "Within Impact International" : "Within your " + unitPlural(unitKinds[unitKinds.indexOf(item) - 1]).toLowerCase()}</p><Link to={unitPaths[item]}>Manage {unitPlural(item).toLowerCase()} →</Link>{canCreate && <Link className="button secondary" to={unitPaths[item] + "?create=1"}><Plus size={14} />Create {unitSingular(item)}</Link>}</section>)}</div>
      <HierarchyTree onOpen={(id: string) => open(id)} onCreate={async (id: string) => {
        try { const parent = await api<Org>("/organizations/" + id); const child = structure.data?.levels.find(l => l.parentLevelId === parent.levelId); if (child && unitKinds.includes(child.name as UnitKind)) create(child.name as UnitKind, id); } catch (e) { setError(e); }
      }} />
    </> : <section className="panel impact-unit-list">
      <div className="filter-bar"><label className="search-box"><Search size={16} /><input aria-label={"Search " + unitPlural(kind).toLowerCase()} placeholder={"Search " + unitSingular(kind) + " names or codes…"} value={search} onChange={e => setSearch(e.target.value)} /></label><select aria-label="Status" value={status} onChange={e => { setStatus(e.target.value); setPage(1); }}><option value="">All statuses</option>{["Active", "Pending", "Archived"].map(s => <option key={s}>{s}</option>)}</select></div>
      {units.isPending ? <Skeleton /> : !units.data?.items.length ? <div className="empty"><h2>No {unitPlural(kind).toLowerCase()} yet</h2><p>{previousKind ? "Create a " + unitSingular(previousKind) + " first, then add its " + unitPlural(kind).toLowerCase() + "." : "Start by adding your first national unit under Impact International."}</p>{canCreate && <button onClick={() => create(kind)}>Create {unitSingular(kind)}</button>}{previousKind && <Link className="panel-link" to={unitPaths[previousKind]}>Manage {unitPlural(previousKind).toLowerCase()} →</Link>}</div> : <div className="table-scroll"><table><thead><tr><th>{kind}</th><th>{kind === "National" ? "International headquarters" : previousKind}</th><th>Location</th><th>Status</th><th>Actions</th></tr></thead><tbody>{units.data.items.map((unit: Org) => <tr key={unit.id}><td><button className="text-button" onClick={() => open(unit.id)}>{unit.name}</button><small>{unit.code}</small></td><td>{unit.parent}</td><td>{[unit.city, unit.country].filter(Boolean).join(", ") || "—"}</td><td><span className="badge">{unit.status}</span></td><td><div className="impact-unit-actions">{canEdit && <button className="secondary" onClick={() => open(unit.id, true)}><Pencil size={14} />Edit</button>}{kind === "Club" && <Link to={"/app/clubs/" + unit.id}>Club workspace →</Link>}</div></td></tr>)}</tbody></table></div>}
      {!!units.data?.total && <div className="pager"><span>{units.data.total} {unitPlural(kind).toLowerCase()} · Page {page}</span><button className="secondary" disabled={page <= 1} onClick={() => setPage(page - 1)}>Previous</button><button className="secondary" disabled={page * 20 >= units.data.total} onClick={() => setPage(page + 1)}>Next</button></div>}
    </section>}
    {form && <MemberDrawer title={(form.id ? "Edit " : "Create ") + unitSingular(formKind)} eyebrow="IMPACT INTERNATIONAL STRUCTURE" onClose={() => setForm(null)}><form className="form-stack impact-unit-form" onSubmit={save}>
      <p>{formKind === "National" ? "This national unit reports to Impact International headquarters." : "Choose the " + parentLabel.toLowerCase() + " this " + unitSingular(formKind) + " belongs to."}</p>
      <ErrorBox error={error || parents.error} />
      <label>{formKind} name<input autoFocus required minLength={2} maxLength={200} value={form.name || ""} onChange={e => setForm({ ...form, name: e.target.value })} /></label>
      <label>{formKind} ID<input readOnly value={form.id ? form.code || "" : "Auto-generated when saved"} /></label>
      {formKind === "National" ? <label>Organization<input readOnly value="Impact International" /></label> : <>
        <label>Find {parentLabel.toLowerCase()}<input value={parentSearch} onChange={e => setParentSearch(e.target.value)} placeholder={"Search " + parentLabel.toLowerCase() + " names…"} /></label>
        <label>{parentLabel}<select required value={form.parentId || ""} disabled={!!form.id && !me?.platformAdmin} onChange={e => setForm({ ...form, parentId: e.target.value })}><option value="">Choose {parentLabel.toLowerCase()}</option>{parents.data?.items.map((p: Org) => <option key={p.id} value={p.id}>{p.name} · {p.code}</option>)}</select></label>
        {parents.data && !parents.data.items.length && <p className="privacy-notice">No matching {parentLabel.toLowerCase()}s. <Link to={unitPaths[parentLabel as UnitKind] + "?create=1"}>Create a {parentLabel.toLowerCase()} first →</Link></p>}
      </>}
      <div className="form-grid">{([['city', 'City'], ['state', 'State / province'], ['country', 'Country'], ['phone', 'Phone']] as const).map(([key, label]) => <label key={key}>{label}<input value={form[key] || ""} onChange={e => setForm({ ...form, [key]: e.target.value })} /></label>)}</div>
      <label>Contact email<input type="email" value={form.email || ""} onChange={e => setForm({ ...form, email: e.target.value })} /></label>
      <label>Address<textarea rows={3} value={form.address || ""} onChange={e => setForm({ ...form, address: e.target.value })} /></label>
      {form.id && <label>Status<select value={form.status} onChange={e => setForm({ ...form, status: e.target.value })}>{["Active", "Pending", "Archived"].map(s => <option key={s}>{s}</option>)}</select></label>}
      <div className="drawer-footer"><button type="button" className="secondary" onClick={() => setForm(null)}>Cancel</button><button disabled={busy || parents.isFetching}>{busy ? "Saving…" : (form.id ? "Save " : "Create ") + unitSingular(formKind)}</button></div>
    </form></MemberDrawer>}
    {detail && <MemberDrawer title={detail.name} eyebrow="IMPACT INTERNATIONAL STRUCTURE" onClose={() => setDetail(null)}><div className="impact-unit-form"><dl className="impact-unit-details">{Object.entries({ Type: structure.data?.levels.find(l => l.id === detail.levelId)?.name || "Headquarters", Code: detail.code, City: detail.city, Country: detail.country, Email: detail.email, Phone: detail.phone, Status: detail.status }).map(([label, value]) => <div key={label}><dt>{label}</dt><dd>{value || "—"}</dd></div>)}</dl>{detail.parentId && canEdit && <button onClick={() => { setDetail(null); open(detail.id, true); }}>Edit details</button>}{!detail.parentId && <Link to="/app/settings">Manage Impact International settings →</Link>}{structure.data?.levels.find(l => l.id === detail.levelId)?.isClub && <Link className="panel-link" to={"/app/clubs/" + detail.id}>Open club workspace →</Link>}</div></MemberDrawer>}
  </>;
}
