import { useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { api } from "../api";
import { ErrorBox, PageHeader, Skeleton, useWorkspace } from "../App";

const base = "/ici/delegated-positions";
type Unit = { id: string; name: string; code: string; level: string; parentId: string | null };
type Holder = { id: string; name: string; role: string; portfolio: string };
type Candidate = { id: string; name: string; number: string; club: string; accountLinked: boolean };

export default function DelegatedPositions() {
  const { w } = useWorkspace();
  const units = useQuery<Unit[]>({
    queryKey: ["delegated-position-units", w?.id],
    queryFn: () => api(base + "/units"),
    enabled: !!w,
  });
  const [level, setLevel] = useState("Club");
  const [unitId, setUnitId] = useState("");
  const [unitSearch, setUnitSearch] = useState("");
  const options = units.data?.filter(unit => unit.level === level &&
    (unit.name + " " + unit.code).toLowerCase().includes(unitSearch.trim().toLowerCase())) ?? [];

  return <>
    <PageHeader title="Manage positions" description="Choose a club or region to see its officers and assign an active member." />
    <ErrorBox error={units.error} />
    {units.isPending && <Skeleton />}
    {units.data && <section className="panel form-stack">
      {units.data.some(unit => unit.level === "Region") && <label>Unit type
        <select value={level} onChange={e => { setLevel(e.target.value); setUnitId(""); setUnitSearch(""); }}>
          <option>Club</option><option>Region</option>
        </select>
      </label>}
      <label>Find {level.toLowerCase()}<input value={unitSearch} onChange={e => setUnitSearch(e.target.value)} placeholder="Name or code" /></label>
      <label>{level}<select value={unitId} onChange={e => setUnitId(e.target.value)}>
        <option value="">Select a {level.toLowerCase()}</option>
        {options.map(unit => <option key={unit.id} value={unit.id}>{unit.name} · {unit.code}</option>)}
        {unitId && !options.some(unit => unit.id === unitId) && <option value={unitId}>{units.data.find(unit => unit.id === unitId)?.name}</option>}
      </select></label>
    </section>}
    {unitId && <PositionEditor key={(w?.id ?? "") + unitId} orgId={unitId} />}
  </>;
}

function PositionEditor({ orgId }: { orgId: string }) {
  const { w } = useWorkspace();
  const client = useQueryClient();
  const [position, setPosition] = useState("");
  const [search, setSearch] = useState("");
  const [page, setPage] = useState(1);
  const [candidate, setCandidate] = useState("");
  const [notice, setNotice] = useState("");
  const positions = useQuery<{ roles: string[]; portfolios: string[]; holders: Holder[] }>({
    queryKey: ["delegated-positions", w?.id, orgId],
    queryFn: () => api(base + "/" + orgId),
  });
  const members = useQuery<{items: Candidate[]; total: number; size: number}>({
    queryKey: ["delegated-position-members", w?.id, orgId, search.trim(), page],
    queryFn: () => api(base + "/" + orgId + "/members?" + new URLSearchParams({ search: search.trim(), page: String(page) })),
    enabled: !!position,
  });
  const slots = positions.data?.roles.flatMap(role => (role === "Regional Director" ? positions.data!.portfolios : [""])
    .map(portfolio => ({ role, portfolio, key: role + "|" + portfolio, label: portfolio ? role + " · " + portfolio : role }))) ?? [];
  const slot = slots.find(item => item.key === position);
  const holderFor = (role: string, portfolio: string) => positions.data?.holders.filter(holder => holder.role === role && holder.portfolio === portfolio) ?? [];
  const holders = slot ? holderFor(slot.role, slot.portfolio) : [];
  const holder = holders[0];
  const update = useMutation({
    mutationFn: (membershipId: string | null) => api(base + "/" + orgId, "PUT", {
      role: slot?.role, portfolio: slot?.portfolio, membershipId, currentAssignmentId: holder?.id ?? null,
    }),
    onSuccess: async (_, membershipId) => {
      setNotice(membershipId ? "Position saved." : "Position is now vacant.");
      setCandidate("");
      await Promise.all([
        client.invalidateQueries({ queryKey: ["delegated-positions", w?.id, orgId] }),
        client.invalidateQueries({ queryKey: ["ici"] }),
        client.invalidateQueries({ queryKey: ["workspaces"] }),
      ]);
    },
  });

  return <section className="panel form-stack">
    <h2>Positions</h2>
    <ErrorBox error={positions.error || members.error || update.error} />
    {positions.isPending && <Skeleton />}
    {notice && <p role="status">{notice}</p>}
    {slots.length > 0 && <form className="form-stack" onSubmit={e => { e.preventDefault(); if (candidate && slot && holders.length < 2) update.mutate(candidate); }}>
      <label>Position<select required value={position} onChange={e => { setPosition(e.target.value); setCandidate(""); setSearch(""); setPage(1); setNotice(""); update.reset(); }}>
        <option value="">Choose a position</option>
        {slots.map(item => {
          const holders = holderFor(item.role, item.portfolio);
          return <option key={item.key} value={item.key}>{item.label} — {holders.map(person => person.name).join(", ") || "Vacant"}</option>;
        })}
      </select></label>
      {slot && holders.length > 1 && <p>Multiple holders are assigned. Resolve the duplicate assignments in administration.</p>}
      {slot && holders.length < 2 && <>
        <label>Find member<input value={search} onChange={e => { setSearch(e.target.value); setCandidate(""); setPage(1); }} placeholder="Name or membership ID" /></label>
        <label>Member<select required value={candidate} onChange={e => setCandidate(e.target.value)}>
          <option value="">{members.isPending ? "Loading members…" : "Choose a member"}</option>
          {members.data?.items.map(member => <option key={member.id} value={member.id}>{member.name} · {member.number}{slot.role.startsWith("Regional") ? " · " + member.club : ""}</option>)}
        </select></label>
        {members.data?.total === 0 && <p>No matching active members.</p>}
        {members.data?.items.some(member => !member.accountLinked) && <p>Members marked Account required need a unique email or a link to their existing account.</p>}
        {members.data && <div className="header-actions">
          <span>{members.data.total} matching members · Page {page}</span>
          <button type="button" className="secondary" disabled={page === 1} onClick={() => { setPage(page - 1); setCandidate(""); }}>Previous</button>
          <button type="button" className="secondary" disabled={page * members.data.size >= members.data.total} onClick={() => { setPage(page + 1); setCandidate(""); }}>Next</button>
        </div>}
        <div className="header-actions">
          <button type="submit" disabled={!candidate || update.isPending}>{holder ? "Replace officer" : "Assign officer"}</button>
          {holder && <button type="button" className="secondary" disabled={update.isPending} onClick={() => {
            if (window.confirm("Vacate " + slot.label + " and remove " + holder.name + "’s role access?")) update.mutate(null);
          }}>Vacate position</button>}
        </div>
      </>}
    </form>}
  </section>;
}
