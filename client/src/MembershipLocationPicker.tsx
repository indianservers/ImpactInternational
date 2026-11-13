import { useEffect, useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { api } from "./api";
import { ErrorBox, useWorkspace } from "./App";

const stages = ["National", "Region", "District", "Zone", "Club"] as const;
type Stage = typeof stages[number];
type Row = { id: string; name: string; code: string; parentId: string };
type Options = { items: Row[]; total: number; page: number; size: number; selectedPath: (Row & { kind: Stage })[] };
const labelFor = (stage: Stage) => stage === "National" ? "National unit" : stage;

export default function MembershipLocationPicker({ clubId, onChange, publicCatalog, tenantId, excludeClub }: any) {
  const { w } = useWorkspace();
  const [path, setPath] = useState<Partial<Record<Stage, string>>>({});
  const queryContext = { publicCatalog: String(!!publicCatalog), ...(tenantId ? { tenantId } : {}) };
  const selected = useQuery<Options>({
    queryKey: ["member-club-path", w?.id, publicCatalog, tenantId, clubId],
    queryFn: () => api("/membership/hierarchy?" + new URLSearchParams({ ...queryContext, clubId })),
    enabled: !!clubId && !path.Zone,
  });
  useEffect(() => {
    if (selected.data?.selectedPath.length && !path.Zone) {
      const restored: Partial<Record<Stage, string>> = {};
      for (const node of selected.data.selectedPath) if (stages.includes(node.kind)) restored[node.kind] = node.id;
      setPath(restored);
    }
  }, [selected.data, path.Zone]);
  function choose(stage: Stage, id: string) {
    const next = { ...path, [stage]: id };
    for (const child of stages.slice(stages.indexOf(stage) + 1)) delete next[child];
    setPath(next);
    onChange({ clubId: stage === "Club" ? id : "" });
  }
  return <fieldset className="membership-location-picker">
    <legend>Choose the member’s community</legend>
    <p>Select National unit → Region → District → Zone → Club.</p>
    <div className="form-grid">{stages.map((stage, index) => <LocationSelect key={stage} stage={stage}
      value={stage === "Club" ? clubId || "" : path[stage] || ""}
      parentId={index ? path[stages[index - 1]] : undefined}
      parentLabel={index ? labelFor(stages[index - 1]) : "Impact International"}
      enabled={(publicCatalog ? !!tenantId : !!w) && (index === 0 || !!path[stages[index - 1]])}
      publicCatalog={publicCatalog} tenantId={tenantId} excludeClub={excludeClub}
      onChange={(id: string) => choose(stage, id)} />)}</div>
    <ErrorBox error={selected.error} />
  </fieldset>;
}

function LocationSelect({ stage, value, parentId, parentLabel, enabled, publicCatalog, tenantId, excludeClub, onChange }: any) {
  const { w } = useWorkspace();
  const [search, setSearch] = useState(""), [term, setTerm] = useState(""), [page, setPage] = useState(1);
  useEffect(() => { setSearch(""); setTerm(""); setPage(1); }, [parentId]);
  useEffect(() => { const t = setTimeout(() => { setTerm(search); setPage(1); }, 250); return () => clearTimeout(t); }, [search]);
  const q = useQuery<Options>({
    queryKey: ["membership-location-options", w?.id, publicCatalog, tenantId, stage, parentId, term, page],
    queryFn: () => api("/membership/hierarchy?" + new URLSearchParams({ kind: stage, publicCatalog: String(!!publicCatalog), search: term, page: String(page), ...(parentId ? { parentId } : {}), ...(tenantId ? { tenantId } : {}) })),
    enabled,
  });
  const rows = enabled ? q.data?.items.filter(row => stage !== "Club" || row.id !== excludeClub) || [] : [];
  const label = labelFor(stage);
  return <div className="membership-location-field">
    <label>{label}<select required disabled={!enabled || q.isPending} value={value} onChange={e => onChange(e.target.value)}>
      <option value="">{!enabled ? "Choose " + parentLabel.toLowerCase() + " first" : q.isPending ? "Loading…" : "Choose " + label.toLowerCase()}</option>
      {rows.map(row => <option key={row.id} value={row.id}>{row.name} · {row.code}</option>)}
    </select></label>
    {enabled && !q.isPending && !q.error && !rows.length && <small>No {label.toLowerCase()}s available{stage === "National" ? ". Add the structure before assigning members." : " within the selected " + parentLabel.toLowerCase() + "."}</small>}
    {enabled && (q.data && q.data.total > 100 || search) && <label className="membership-location-search">Search {label.toLowerCase()}<input value={search} onChange={e => setSearch(e.target.value)} placeholder="Search name or code…" /></label>}
    {enabled && q.data && (q.data.total > 100 || page > 1) && <div className="pager"><button type="button" className="secondary" disabled={page === 1} onClick={() => setPage(page - 1)}>Previous</button><span>Page {page}</span><button type="button" className="secondary" disabled={page * 100 >= q.data.total} onClick={() => setPage(page + 1)}>More</button></div>}
    <ErrorBox error={q.error} />
  </div>;
}
