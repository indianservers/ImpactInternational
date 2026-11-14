import { useEffect, useState } from "react";
import { Link, useSearchParams } from "react-router-dom";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import {
  Plus,
  Search,
  Download,
  Upload,
  LayoutGrid,
  List,
  ArrowUpRight,
  Mail,
  Copy,
  Send,
} from "lucide-react";
import {useDialog} from "../useDialog";
import AddMemberWizard from "./AddMemberWizard";
import MembershipIntelligence from "./MembershipIntelligence";
import "./Members.css";
import { api, apiDownload } from "../api";
import { PageHeader, Skeleton, ErrorBox, useWorkspace } from "../App";
import DataGrid, { type GridColumn, type GridConfig } from "../DataGrid";
import {
  MemberDrawer,
  MemberAvatar,
  MemberStatus,
  ClubCategoryFields,
} from "../MemberComponents";
export default function Members({
  directory = false,
}: {
  directory?: boolean;
}) {
  const [dashboardFilters,setDashboardFilters] = useSearchParams();
  const { w } = useWorkspace(),
    qc = useQueryClient();
  const grants = w?.permissions || [];
  const [bulkTarget,setBulkTarget]=useState<any>({clubId:"",categoryId:""});
  const [range,setRange]=useState("6m"),[dates,setDates]=useState<any>({}),[extra,setExtra]=useState<any>(()=>Object.fromEntries(Array.from(dashboardFilters.entries()).filter(([key])=>["nationalId","membershipType","joinedFrom","joinedTo","renewalFrom","renewalTo","country","state"].includes(key)))),[draftFilters,setDraftFilters]=useState<any>({}),[bulk,setBulk]=useState(""),[bulkReason,setBulkReason]=useState(""),[bulkStatus,setBulkStatus]=useState("Inactive"),[bulkCategory,setBulkCategory]=useState(""),[viewName,setViewName]=useState("");
  const [districtId,setDistrict]=useState(()=>dashboardFilters.get("districtId")||""),[zoneId,setZone]=useState(()=>dashboardFilters.get("zoneId")||"");
  const [search, setSearch] = useState(""),
    [term, setTerm] = useState(""),
    [statuses, setStatuses] = useState<string[]>(() => { const s=dashboardFilters.get("status"); return s && ["Active","Inactive","Suspended","Transferred","Expired","Renewal Due","Resigned","Terminated","Deceased"].includes(s)?[s]:[]; }),
    [clubId, setClub] = useState(()=>dashboardFilters.get("clubId")||""),
    [regionId, setRegion] = useState(()=>dashboardFilters.get("regionId")||""),
    [categoryId, setCategory] = useState(()=>dashboardFilters.get("categoryId")||""),
    [city, setCity] = useState(""),
    [profession, setProfession] = useState(""),
    [industry, setIndustry] = useState(""),
    [expertise, setExpertise] = useState(""),
    [office, setOffice] = useState(""),
    [sort, setSort] = useState("name"),
    [page, setPage] = useState(1),
    [view, setView] = useState(()=>localStorage.getItem("impact-members-view") || "list"),
    [advanced, setAdvanced] = useState(false),
    [selected, setSelected] = useState<Set<string>>(new Set()),
    [quick, setQuick] = useState<any>(null),
    [drawer, setDrawer] = useState(""),
    [error, setError] = useState<unknown>(),
    [message, setMessage] = useState(()=>dashboardFilters.get("asOf")?"Opened from Insights as of "+dashboardFilters.get("asOf")+". This directory shows current records; open a profile to inspect historical status.":""),
    [busy, setBusy] = useState(false),
    [config, setConfig] = useState<GridConfig>({
      order: [
        "name",
        "number",
        "club",
        "region",
        "district",
        "zone",
        "category",
        "offices",
        "city",
        "joinedAt",
        "endsAt",
        "status",
        "renewalStatus",
        "updatedAt",
        "email",
        "phone",
        "profession",
        "industry",
        "actions",
      ],
      hidden: ["updatedAt", "email", "phone", "profession", "industry", "district", "zone", "renewalStatus", "endsAt"],
      widths: {
        name: 210,
        club: 160,
        region: 110,
        number: 130,
        category: 100,
        offices: 120,
        city: 100,
        status: 90,
        renewalStatus: 110,
        joinedAt: 95,
        actions: 60,
      },
    });
  useEffect(()=>{const p=new URLSearchParams(dashboardFilters);if(statuses.length===1)p.set("status",statuses[0]);else p.delete("status");setDashboardFilters(p,{replace:true});},[statuses]);
  useEffect(() => {
    const t = setTimeout(() => {
      setTerm(search);
      setPage(1);
    }, 250);
    return () => clearTimeout(t);
  }, [search]);
  useEffect(() => {
    setPage(1);
    setSelected(new Set());
  }, [
    statuses,
    clubId,
    regionId, districtId, zoneId, extra,
    categoryId,
    city,
    profession,
    industry,
    expertise,
    office,
    term,
  ]);
  const params = new URLSearchParams({
    search: term,
    statuses: statuses.join(","),
    sort,
    page: String(page),
    size: "20",
    city,
    profession,
    industry,
    expertise,
    office,
  });
  if (clubId) params.set("clubId", clubId);
  if (regionId) params.set("regionId", regionId);
  if (categoryId) params.set("categoryId", categoryId);
  if (districtId) params.set("districtId",districtId);
  if (zoneId) params.set("zoneId",zoneId);
  for(const [k,v] of Object.entries(extra))if(v)params.set(k,String(v));
  const mobileFilters=advanced&&window.matchMedia("(max-width:760px)").matches;
  useDialog(mobileFilters,()=>setAdvanced(false));
  useEffect(()=>{if(!mobileFilters)return;const old=document.body.style.overflow;document.body.style.overflow="hidden";return()=>{document.body.style.overflow=old;};},[mobileFilters]);
  useEffect(()=>{localStorage.setItem("impact-members-view",view);},[view]);
  const selectRegion=(id:string)=>{setRegion(id);setDistrict("");setZone("");setClub("");};
  const resetFilters=()=>{setSearch("");setStatuses([]);selectRegion("");setCategory("");setCity("");setProfession("");setIndustry("");setExpertise("");setOffice("");setExtra({});setDraftFilters({});};
  const districtChoices=useQuery<any>({queryKey:["member-districts",w?.id,regionId],queryFn:()=>api("/membership/hierarchy?kind=District"+(regionId?"&parentId="+regionId:"")),enabled:!!w});
  const zoneChoices=useQuery<any>({queryKey:["member-zones",w?.id,districtId],queryFn:()=>api("/membership/hierarchy?kind=Zone&parentId="+districtId),enabled:!!w&&!!districtId});
  const q = useQuery({
    queryKey: ["members", directory, w?.id, params.toString()],
    queryFn: () => api((directory ? "/directory" : "/members") + "?" + params),
    placeholderData: (p) => p,
  });
  const metrics = useQuery({
    queryKey: ["member-metrics", w?.id, range,dates],
    refetchInterval: 30000,
    queryFn: () => api("/membership/metrics?"+new URLSearchParams({range,...dates})),
    enabled: !directory,
  });
  const choices = useQuery({
    queryKey: ["member-choices", w?.id, regionId, districtId, zoneId],
    queryFn: () => api("/membership/options?"+new URLSearchParams({...regionId?{regionId}:{},...districtId?{districtId}:{},...zoneId?{zoneId}:{}})),
    enabled: !!w,
  });
  const regions = useQuery<any[]>({ queryKey: ["directory-regions", w?.id], queryFn: () => api("/directory/regions"), enabled: !!w });
  useEffect(() => { selectRegion(""); setQuick(null); setSelected(new Set()); }, [w?.id]);
  const saved = useQuery({
    queryKey: ["member-views", w?.id],
    queryFn: () => api("/membership/views"),
    enabled: !directory,
  });
  const columns: GridColumn<any>[] = [
    {
      key: "name",
      label: "Member",
      render: (r) => (
        <button className="text-button member-name" onClick={() => setQuick(r)}>
          <MemberAvatar
            name={r.name}
            id={r.id}
            photo={r.hasPhoto}
            directory={directory}
          />
          <span>
            <b>{r.name}</b>
            <small>{r.profession || "Community member"}</small>
          </span>
        </button>
      ),
    },
    ...[
      "number",
      "club",
      "region",
      "district",
      "zone",
      "category",
      "offices",
      "city",
      "joinedAt",
      "endsAt",
      "status",
      "renewalStatus",
      "updatedAt",
      "email",
      "phone",
      "profession",
      "industry",
    ].map((key) => ({
      key,
      label:
        (
          {
            number: "Member ID",
            joinedAt: "Joined",
            endsAt: "Renewal date",
            renewalStatus: "Renewal",
            updatedAt: "Updated",
            offices: "Current office",
            city: "Location",
          } as any
        )[key] || key[0].toUpperCase() + key.slice(1),
      render: (r: any) =>
        key === "status" ? (
          <MemberStatus status={r.status} />
        ) : key === "offices" ? (
          r.offices?.join(", ") || "—"
        ) : key === "joinedAt" || key === "updatedAt" || key === "endsAt" ? (
          r[key] ? new Date(r[key]).toLocaleDateString() : "No expiry"
        ) : (
          r[key] || "—"
        ),
    })),
    {
      key: "actions",
      label: "Actions",
      render: (r) => (
        <button className="text-button" onClick={() => setQuick(r)}>
          View <ArrowUpRight size={13} />
        </button>
      ),
    },
  ];
  async function exportRows(format: string) {
    setBusy(true);
    setError(null);
    try {
      await apiDownload(
        "/membership/export",
        {
          format,
          columns: [...config.order, ...columns.map(c=>c.key).filter(k=>!config.order.includes(k))]
            .filter(
              (k) =>
                !config.hidden.includes(k) &&
                !["actions"].includes(
                  k,
                ),
            ),
          selectedIds: selected.size ? [...selected] : null,
          search: term,
          statuses: statuses.join(","),
          clubId: clubId || null,
          regionId: regionId || null, districtId:districtId||null,zoneId:zoneId||null,...extra,sort,
          categoryId: categoryId || null,
          city,
          profession,
          industry,
          expertise,
          office,
        },
        "members." + format,
      );
    } catch (e) {
      setError(e);
    } finally {
      setBusy(false);
    }
  }
  function loadView(value: string) {
    const v = saved.data?.find((x: any) => x.id === value);
    if (!v) return;
    try {
      const c = JSON.parse(v.configuration);
      if(c.grid)setConfig(c.grid);
      setDistrict(c.districtId||"");setZone(c.zoneId||"");setExtra(c.extra||{});setDraftFilters(c.extra||{});setView(c.view||"list");
      setStatuses(c.statuses || []);
      setClub(c.clubId || "");
      setRegion(c.regionId || "");
      setCategory(c.categoryId || "");
      setCity(c.city || "");
      setProfession(c.profession || "");
      setIndustry(c.industry || "");
      setExpertise(c.expertise || "");
      setOffice(c.office || "");
      setSearch(c.search || "");
      setSort(c.sort || "name");
    } catch (e) {
      setError(e);
    }
  }
  return (
    <div className="members-premium">
      <PageHeader
        eyebrow={
          directory ? "YOUR IMPACT COMMUNITY" : "PEOPLE & MEMBERSHIP"
        }
        title={directory ? "Impact member directory" : "Impact Members"}
        description={
          directory
            ? "Connect with Impact members, shared expertise, and opportunities to serve."
            : "Manage member profiles, affiliations, and membership records across Impact International."
        }
      >
        <div className="header-actions">
          {!directory && grants.includes("members.invite") && (
            <button className="secondary" onClick={() => setDrawer("invite")}>
              <Mail size={16} />
              Invite member
            </button>
          )}
          {!directory&&grants.includes("members.import")&&<Link className="button secondary" to="/app/imports"><Upload size={16}/>Import members</Link>}
          {!directory && grants.includes("members.manage") && (
            <button onClick={() => setDrawer("create")}>
              <Plus size={16} />
              Add member
            </button>
          )}
        </div>
      </PageHeader>
      <div className="mm-mobile-search search-field"><Search size={17}/><input aria-label="Search members on mobile" placeholder="Search name or member ID…" value={search} onChange={e=>setSearch(e.target.value)}/><small>{q.data?.total?.toLocaleString()??"…"}</small></div>
      {!directory&&grants.includes("members.manage")&&<button className="mm-floating-add" aria-label="Add new member" onClick={()=>setDrawer("create")}><Plus size={18}/>Add member</button>}
      {!directory&&<nav className="mm-status-tabs" aria-label="Membership status">{[["All members",""],["Active","Active"],["Inactive","Inactive"],["Suspended","Suspended"],["Transferred","Transferred"],["Expired","Expired"],["Renewals due","Renewal Due"]].map(([label,status])=><button key={label} className={statuses.join(",")===status?"active":""} aria-pressed={statuses.join(",")===status} onClick={()=>setStatuses(status?[status]:[])}>{label}<span>{status===""?metrics.data?.memberships??"—":status==="Renewal Due"?metrics.data?.upcomingRenewals??"—":metrics.data?(metrics.data.intelligence?.statusCounts.find((x:any)=>x.status===status)?.count??0):"—"}</span></button>)}{grants.includes("members.approve")&&<Link to="/app/approvals?status=Pending">Pending applications <b>{metrics.data?.pendingApplications??"—"}</b></Link>}</nav>}
      {!directory&&<MembershipIntelligence {...{metrics,range,setRange,setDates}} onStatus={setStatuses} onRegion={selectRegion} canApprove={grants.includes("members.approve")}/>}
      <section className="mm-primary-filters" aria-label="Member hierarchy filters">
       <label>Region<select value={regionId} onChange={e=>selectRegion(e.target.value)}><option value="">All authorized regions</option>{regions.data?.map(r=><option key={r.id} value={r.id}>{r.name}</option>)}</select></label>
       <label>District<select value={districtId} onChange={e=>{setDistrict(e.target.value);setZone("");setClub("");}}><option value="">All authorized districts</option>{districtChoices.data?.items.map((d:any)=><option key={d.id} value={d.id}>{d.name}</option>)}</select></label>
       <label>Zone<select value={zoneId} disabled={!districtId} onChange={e=>{setZone(e.target.value);setClub("");}}><option value="">{districtId?"All zones":"Choose district first"}</option>{zoneChoices.data?.items.map((d:any)=><option key={d.id} value={d.id}>{d.name}</option>)}</select></label>
       <label>Club<select value={clubId} onChange={e=>setClub(e.target.value)}><option value="">All authorized clubs</option>{choices.data?.clubs.map((c:any)=><option key={c.id} value={c.id}>{c.name}</option>)}</select></label>
       <label>Category<select value={categoryId} onChange={e=>setCategory(e.target.value)}><option value="">All categories</option>{choices.data?.categories.map((c:any)=><option key={c.id} value={c.id}>{c.name}</option>)}</select></label>
       <label>Status<select aria-label="Membership status filter" value={statuses.length===1?statuses[0]:""} onChange={e=>setStatuses(e.target.value?[e.target.value]:[])}><option value="">All statuses</option>{["Active","Inactive","Suspended","Transferred","Expired","Renewal Due","Resigned","Terminated","Deceased"].map(s=><option key={s}>{s}</option>)}</select></label>
       <button className="text-button" onClick={resetFilters}>Reset</button>
      </section>
      <ErrorBox error={error || q.error || metrics.error} />
      {message && (
        <div className="success" role="status">
          {message}
        </div>
      )}
      <section className="panel directory">
        <div className="filter-bar"><strong className="mm-result-count">{q.data?.total?.toLocaleString()??"…"} {directory?"directory records":"memberships"}</strong>
          <div className="search-field">
            <Search size={17} />
            <input
              aria-label="Search members"
              value={search}
              placeholder="Search people or membership IDs…"
              onChange={(e) => setSearch(e.target.value)}
            />
          </div>
          <button
            className="secondary"
            aria-expanded={advanced}
            onClick={() => setAdvanced(!advanced)}
          >
            Filters {advanced ? "−" : "+"}
          </button>
          <select
            aria-label="Sort members"
            value={sort}
            onChange={(e) => setSort(e.target.value)}
          >
            <option value="name">Name A–Z</option>
            <option value="number">Member ID</option>
            <option value="joined">Recently joined</option>
            <option value="updated">Recently updated</option>
            <option value="status">Status</option>
          </select>
          <div className="view-toggle">
            <button
              className={view === "list" ? "active" : ""}
              aria-label="List view"
              onClick={() => setView("list")}
            >
              <List size={17} />
            </button>
            <button
              className={view === "cards" ? "active" : ""}
              aria-label="Card view"
              onClick={() => setView("cards")}
            >
              <LayoutGrid size={17} />
            </button>
          </div>
          {!directory&&grants.includes("members.export")&&<button className="secondary" disabled={busy} onClick={()=>exportRows("csv")}><Download size={15}/>Export</button>}
        </div>
        {advanced && (
          <div className="advanced-filters mm-advanced" role={mobileFilters?"dialog":"region"} aria-modal={mobileFilters||undefined} aria-label="Advanced member filters"><header><h2>Advanced filters</h2><button className="text-button" onClick={()=>setAdvanced(false)}>Close filters</button></header><label>Membership type<select value={draftFilters.membershipType||""} onChange={e=>setDraftFilters({...draftFilters,membershipType:e.target.value})}><option value="">All club types</option>{choices.data?.membershipTypes?.map((t:string)=><option key={t}>{t}</option>)}</select></label>{[["joinedFrom","Joined from"],["joinedTo","Joined to"],["renewalFrom","Renewal from"],["renewalTo","Renewal to"]].map(([key,label])=><label key={key}>{label}<input type="date" value={draftFilters[key]||""} onChange={e=>setDraftFilters({...draftFilters,[key]:e.target.value})}/></label>)}<button className="secondary" onClick={()=>{setExtra({...draftFilters});setAdvanced(false);}}>Apply filters</button>
            <label>
              Statuses
              <select
                multiple
                aria-label="Membership statuses"
                value={statuses}
                onChange={(e) =>
                  setStatuses([...e.target.selectedOptions].map((o) => o.value))
                }
              >
                {[
                  "Active",
                  "Inactive",
                  "Suspended",
                  "Expired",
                  "Transferred",
                  "Resigned",
                  "Terminated",
                  "Deceased",
                ].map((x) => (
                  <option key={x}>{x}</option>
                ))}
              </select>
            </label>
            {[
              ["City", city, setCity],
              ["Profession", profession, setProfession],
              ["Industry", industry, setIndustry],
              ["Expertise", expertise, setExpertise],
              ["Current office", office, setOffice],
            ].map(([label, value, set]: any) => (
              <label key={label}>
                {label}
                <input value={value} onChange={(e) => set(e.target.value)} />
              </label>
            ))}
          </div>
        )}
        {!directory && (
          <details className="compact-summary member-tools"><summary>Saved views & export</summary><div className="saved-view-bar">
            <select
              aria-label="Saved member views"
              defaultValue=""
              onChange={(e) => loadView(e.target.value)}
            >
              <option value="">Saved views</option>
              {saved.data?.map((v: any) => (
                <option key={v.id} value={v.id}>
                  {v.name}
                </option>
              ))}
            </select>
            <input aria-label="Saved view name" placeholder="Name this view" value={viewName} onChange={e=>setViewName(e.target.value)}/><button
              className="text-button"
              onClick={async () => {
                const name = viewName.trim();
                if (!name) return;
                try {
                  await api("/membership/views", "POST", {
                    name,
                    configuration: JSON.stringify({
                      grid: config,
                      statuses,
                      clubId,
                      regionId, districtId, zoneId, extra, view,
                      categoryId,
                      city,
                      profession,
                      industry,
                      expertise,
                      office,
                      search,
                      sort,
                    }),
                  });
                  qc.invalidateQueries({ queryKey: ["member-views"] });
                  setMessage("View saved to your account.");
                } catch (e) {
                  setError(e);
                }
              }}
            >
              Save current view
            </button>
            <span />
            {grants.includes("members.import") && (
              <Link className="text-button" to="/app/imports">
                <Upload size={14} />
                Import
              </Link>
            )}
            {grants.includes("members.export") && (
              <>
                <button
                  className="text-button"
                  disabled={busy}
                  onClick={() => exportRows("csv")}
                >
                  <Download size={14} />
                  CSV
                </button>
                <button
                  className="text-button"
                  disabled={busy}
                  onClick={() => exportRows("xlsx")}
                >
                  Excel
                </button>
              </>
            )}
          </div></details>
        )}
        {!directory&&selected.size>0&&<div className="mm-bulk-bar"><strong>{selected.size} selected</strong>{grants.includes("members.export")&&<><button className="secondary" onClick={()=>exportRows("csv")}>Export selected</button><button className="secondary" onClick={()=>window.print()}>Print selected on this page</button></>}{grants.includes("members.manage")&&<><button className="secondary" onClick={()=>setBulk("status")}>Change status</button><button className="secondary" onClick={()=>setBulk("category")}>Assign category</button><button className="secondary" onClick={()=>setBulk("transfer")}>Request transfer</button></>}<button className="text-button" onClick={()=>setSelected(new Set())}>Clear selection</button></div>}
        {q.isPending ? (
          <Skeleton />
        ) : (
          <>
            <div
              className={
                view === "cards" ? "hidden-grid" : "members-desktop-grid"
              }
            >
              <DataGrid
                rows={q.data?.items || []}
                columns={columns}
                config={config}
                sort={sort} onSort={setSort}
                onConfig={setConfig}
                selected={directory ? undefined : selected}
                onSelection={directory ? undefined : setSelected}
                onOpen={setQuick}
                empty={
                  directory
                    ? "No members have opted into this directory in your authorized scope."
                    : "No matching memberships. Adjust filters or add your first member."
                }
              />
            </div>
            <div
              className={
                view === "cards"
                  ? "member-card-grid"
                  : "member-card-grid mobile-only"
              }
            >
              {q.data?.items.map((m: any) => (
                <article className="directory-person-card" key={m.id}>
                  <MemberAvatar
                    name={m.name}
                    id={m.id}
                    photo={m.hasPhoto}
                    directory={directory}
                    size="large"
                  />
                  {!directory&&<label className="mm-card-select"><input type="checkbox" aria-label={"Select member "+m.number} checked={selected.has(m.id)} onChange={e=>setSelected(old=>{const next=new Set(old);e.target.checked?next.add(m.id):next.delete(m.id);return next;})}/>Select</label>}<MemberStatus status={m.status} />
                  <h3>{m.name}</h3><b className="mm-card-id">{m.number}</b>
                  <p>{m.category} · {m.profession || "Community member"}</p><small>{m.offices?.join(", ")||"No current office"} · Joined {new Date(m.joinedAt).toLocaleDateString()}</small>
                  <span>{m.club}</span>
                  <small>{[m.region, m.district, m.zone].filter(Boolean).join(" → ")}</small>
                  <small>{m.city || m.country || "Location not shared"}</small>
                  <button className="secondary" onClick={() => setQuick(m)}>
                    View member <ArrowUpRight size={14} />
                  </button>
                </article>
              ))}
            </div>
            <div className="pagination">
              <span>
                {q.data?.total || 0} memberships · Page {page}
                {q.isFetching ? " · Refreshing…" : ""}
              </span>
              <div>
                <button
                  className="secondary"
                  disabled={page === 1}
                  onClick={() => setPage(page - 1)}
                >
                  Previous
                </button>
                <button
                  className="secondary"
                  disabled={page * 20 >= (q.data?.total || 0)}
                  onClick={() => setPage(page + 1)}
                >
                  Next
                </button>
              </div>
            </div>
          </>
        )}
      </section>
      {bulk&&<MemberDrawer title={bulk==="status"?"Confirm bulk status change":bulk==="transfer"?"Confirm bulk transfer requests":"Confirm bulk category change"} onClose={()=>setBulk("")}><form className="form-stack" onSubmit={async e=>{e.preventDefault();setBusy(true);setError(null);let done=0,unchanged=0;const failed:string[]=[];for(const id of selected){try{const d=await api("/members/"+id);if((bulk==="status"&&d.membership.status===bulkStatus)||(bulk==="category"&&d.membership.categoryId===bulkCategory)||(bulk==="transfer"&&d.membership.clubId===bulkTarget.clubId)){unchanged++;continue;}await api("/members/"+id+"/"+(bulk==="status"?"status":bulk==="transfer"?"transfers":"category"),"POST",{status:bulkStatus,destinationClubId:bulkTarget.clubId,categoryId:bulk==="transfer"?bulkTarget.categoryId:bulkCategory,reason:bulkReason,version:d.membership.version});done++;}catch(err){failed.push(id+": "+String(err));}}setBusy(false);setBulk("");setMessage(`${done} memberships updated; ${unchanged} already matched. ${failed.length?failed.length+" failed; review the error details. No automatic retries were made.":""}`);if(failed.length)setError(new Error(failed.join("; ")));setSelected(new Set());qc.invalidateQueries({queryKey:["members"]});qc.invalidateQueries({queryKey:["member-metrics"]});}}><p>You are confirming a change to <strong>{selected.size} selected memberships</strong>. Each record is validated independently; failed changes will be reported. Transfer requests follow the existing review stages and do not immediately move members.</p>{bulk==="transfer"?<ClubCategoryFields {...bulkTarget} onChange={(v:any)=>setBulkTarget({...bulkTarget,...v})}/>:bulk==="status"?<label>New status<select value={bulkStatus} onChange={e=>setBulkStatus(e.target.value)}>{["Active","Inactive","Suspended","Expired","Resigned","Terminated","Deceased"].map(s=><option key={s}>{s}</option>)}</select></label>:<label>New category<select required value={bulkCategory} onChange={e=>setBulkCategory(e.target.value)}><option value="">Choose category</option>{choices.data?.categories.map((c:any)=><option key={c.id} value={c.id}>{c.name}</option>)}</select></label>}<label>Reason<textarea required minLength={5} maxLength={2000} value={bulkReason} onChange={e=>setBulkReason(e.target.value)}/></label><button disabled={busy}>{busy?"Updating…":`Confirm change for ${selected.size} memberships`}</button></form></MemberDrawer>}
      {quick && (
        <MemberDrawer title="Member quick view" onClose={() => setQuick(null)}>
          <div className="quick-member-hero">
            <MemberAvatar
              name={quick.name}
              id={quick.id}
              photo={quick.hasPhoto}
              directory={directory}
              size="xl"
            />
            <h2>{quick.name}</h2>
            <p>{quick.number}</p>
            <MemberStatus status={quick.status} />
          </div>
          {[
            ["Club", quick.club],
            ["Region", quick.region || "Not assigned"],
            ["District", quick.district || "Not assigned"],
            ["Zone", quick.zone || "Not assigned"],
            ["Category", quick.category],
            ["Current office", quick.offices?.join(", ") || "—"],
            [
              "Location",
              [quick.city, quick.country].filter(Boolean).join(", ") ||
                "Not shared",
            ],
            ["Joined", new Date(quick.joinedAt).toLocaleDateString()],
            ["Email", quick.email || "Private / not provided"],
            ["Phone", quick.phone || "Private / not provided"],
            ["Renewal", quick.renewalStatus],
          ].map(([k, v]) => (
            <div className="context-row" key={k}>
              <span>{k}</span>
              <b>{v}</b>
            </div>
          ))}
          <Link
            className="button full-width"
            to={
              "/app/members/" + quick.id + (directory ? "?directory=true" : "")
            }
          >
            Open complete profile <ArrowUpRight size={16} />
          </Link>
          {directory && quick.contactRequestsAllowed && (
            <ContactRequest id={quick.id} />
          )}
        </MemberDrawer>
      )}
      {drawer === "create" && (
          <AddMemberWizard onClose={() => setDrawer("")}
            onDone={(member: any) => {
              if (member?.number) setMessage("Member " + member.number + " created.");
              setDrawer("");
              qc.invalidateQueries({ queryKey: ["members"] });
              qc.invalidateQueries({ queryKey: ["member-metrics"] });
            }}
          />
      )}
      {drawer === "invite" && (
        <MemberDrawer
          title="Invite someone to join"
          onClose={() => setDrawer("")}
        >
          <InviteMember />
        </MemberDrawer>
      )}
    </div>
  );
}
export function InviteMember() {
  const {w}=useWorkspace();const invites=useQuery<any[]>({queryKey:["member-invitations",w?.id],queryFn:()=>api("/membership/invitations"),enabled:!!w});const [moreInvites,setMoreInvites]=useState<any[]>([]);
  const [form, setForm] = useState<any>({
      clubId: "",
      categoryId: "",
      email: "",
      message:"",
      expiresInDays: 7,
      usageLimit: 1,
    }),
    [result, setResult] = useState<any>(null),
    [error, setError] = useState<unknown>(),
    [busy, setBusy] = useState(false);
  return (
    <form
      className="form-stack"
      onSubmit={async (e) => {
        e.preventDefault();
        setBusy(true);
        try {
          const emails=String(form.email).split(/[;,\n]+/).map(e=>e.trim()).filter(Boolean);if(emails.length>20)throw new Error("Invite up to 20 recipients at a time.");const created=[];const failures=[];for(const email of emails.length?emails:[""]){try{created.push(await api("/membership/invitations", "POST", {...form,email}));}catch(err){failures.push(email+": "+String(err));}}if(created.length){setResult(created[0]);setMoreInvites(created.slice(1));}if(failures.length)setError(new Error("Some invitations failed: "+failures.join("; ")+". Successful invitations above were retained; no retry was made."));void invites.refetch();
        } catch (e) {
          setError(e);
        } finally {
          setBusy(false);
        }
      }}
    >
      <ClubCategoryFields
        {...form}
        onChange={(v: any) => setForm({ ...form, ...v })}
      />
      <label>
        Recipient emails (optional for a shareable club link)
        <textarea
          placeholder="Separate up to 20 emails with commas or new lines"
          value={form.email}
          onChange={(e) => setForm({ ...form, email: e.target.value })}
        />
      </label>
      <label>Personal invitation message (optional)<textarea maxLength={2000} value={form.message} onChange={e=>setForm({...form,message:e.target.value})}/></label>
      <div className="form-grid">
        <label>
          Expires in days
          <input
            type="number"
            min={1}
            max={30}
            value={form.expiresInDays}
            onChange={(e) =>
              setForm({ ...form, expiresInDays: Number(e.target.value) })
            }
          />
        </label>
        <label>
          Usage limit
          <input
            type="number"
            min={1}
            max={20}
            value={form.usageLimit}
            onChange={(e) =>
              setForm({ ...form, usageLimit: Number(e.target.value) })
            }
          />
        </label>
      </div>
      <p>
        Invitees must verify their account and complete the club’s required
        approvals.
      </p>
      <ErrorBox error={error} />
      {result ? (
        <div className="invitation-result">
          <h3>{moreInvites.length?moreInvites.length+1+" invitations created":"Invitation created"}</h3>{moreInvites.map((r:any)=><input key={r.id} aria-label="Additional invitation link" readOnly value={location.origin+r.link}/>)}
          <p>
            Email delivery is queued when a recipient was supplied. A share link
            does not send a WhatsApp message.
          </p>
          <input
            readOnly
            aria-label="Invitation link"
            value={location.origin + result.link}
          />
          <div className="header-actions">
            <button
              type="button"
              className="secondary"
              onClick={async () => {
                try {
                  await navigator.clipboard.writeText(
                    location.origin + result.link,
                  );
                } catch (e) {
                  setError(e);
                }
              }}
            >
              <Copy size={14} />
              Copy link
            </button>
            <a
              className="button secondary"
              href={
                "https://wa.me/?text=" +
                encodeURIComponent(
                  "Join our club: " + location.origin + result.link,
                )
              }
            >
              <Send size={14} />
              WhatsApp share
            </a>
          </div>
          <img
            alt="Invitation QR code"
            width={180}
            height={180}
            src={"/api/v1/membership/invitations/" + result.id + "/qr"}
          />
        </div>
      ) : (
        <button disabled={busy}>
          {busy ? "Creating…" : "Create secure invitation"}
        </button>
      )}
      <details className="mm-invitation-history"><summary>Invitation tracking · {invites.data?.length??0} recent invitations</summary><ErrorBox error={invites.error}/>{invites.data?.map((i:any)=><div className="context-row" key={i.id}><span>{i.email||"Shareable club link"}<small>Expires {new Date(i.expiresAt).toLocaleDateString()} · {i.uses}/{i.usageLimit} accepted</small></span><MemberStatus status={i.revoked?"Revoked":i.uses>=i.usageLimit?"Accepted":new Date(i.expiresAt)<new Date()?"Expired":"Pending acceptance"}/></div>)}</details>
    </form>
  );
}
function ContactRequest({ id }: any) {
  const [message, setMessage] = useState(""),
    [sent, setSent] = useState(false),
    [error, setError] = useState<unknown>();
  return (
    <form
      className="form-stack spaced"
      onSubmit={async (e) => {
        e.preventDefault();
        try {
          await api("/directory/" + id + "/contact-request", "POST", {
            message,
          });
          setSent(true);
        } catch (e) {
          setError(e);
        }
      }}
    >
      <label>
        Request contact
        <textarea
          required
          minLength={5}
          maxLength={1000}
          value={message}
          onChange={(e) => setMessage(e.target.value)}
          placeholder="Introduce yourself and explain why you’d like to connect."
        />
      </label>
      <ErrorBox error={error} />
      {sent ? (
        <div className="success">
          Your request is recorded for the member to review.
        </div>
      ) : (
        <button>Send contact request</button>
      )}
    </form>
  );
}
