import { useEffect, useRef, useState } from "react";
import { Link } from "react-router-dom";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { api, type Org, type Level } from "./api";
import { useWorkspace, ErrorBox } from "./App";

export const unitKinds = ["National", "Region", "District", "Zone", "Club"] as const;
export type UnitKind = typeof unitKinds[number];
export const unitPlural = (kind: string) => kind === "National" ? "National units" : kind + "s";
export const unitSingular = (kind: string) => kind === "National" ? "national unit" : kind.toLowerCase();
export const unitPaths: Record<UnitKind, string> = { National: "/app/national", Region: "/app/regions", District: "/app/districts", Zone: "/app/zones", Club: "/app/clubs" };
export type Structure = { root: Org; levels: Level[]; counts: Record<UnitKind, number>; setupReady: boolean; setupCompleted: boolean };
export function useStructure() {
  const { w } = useWorkspace();
  return useQuery<Structure>({ queryKey: ["impact-structure", w?.id], queryFn: () => api("/structure"), enabled: !!w });
}

export default function ImpactSetupGuide() {
  const { w, me } = useWorkspace();
  const q = useStructure(), qc = useQueryClient();
  const [error, setError] = useState<unknown>();
  const completing = useRef(false);
  const canManage = me?.platformAdmin || w?.permissions.includes("settings.manage");
  useEffect(() => {
    if (!canManage || !q.data?.setupReady || q.data.setupCompleted || completing.current) return;
    completing.current = true;
    api("/setup-guide/complete", "POST").then(() => qc.invalidateQueries({ queryKey: ["impact-structure"] }))
      .catch(e => { setError(e); completing.current = false; });
  }, [canManage, q.data, qc]);
  if (!canManage || !q.data || q.data.setupCompleted || q.data.setupReady && !error) return null;
  const completed = 1 + unitKinds.filter(kind => q.data!.counts[kind] > 0).length;
  return <section className="impact-setup-guide" aria-label="Impact International setup guide">
    <div className="section-heading"><div><div className="eyebrow">GET STARTED WITH IMPACT INTERNATIONAL</div><h2>Set up your structure</h2><p>Your organization is ready. International → National → Regions → Districts → Zones → Clubs.</p></div><span className="badge">{completed} of 6 complete</span></div>
    <progress value={completed} max={6} aria-label="Structure setup progress" />
    <ol>
      <li className="done"><span>✓</span><div><b>International headquarters</b><small>Impact International is configured.</small></div></li>
      {unitKinds.map((kind, index) => {
        const done = q.data!.counts[kind] > 0, previous = index === 0 || q.data!.counts[unitKinds[index - 1]] > 0;
        return <li key={kind} className={done ? "done" : ""}><span>{done ? "✓" : index + 2}</span><div><b>{done ? kind + " created" : "Create your first " + unitSingular(kind)}</b><small>{index === 0 ? "National units report to International headquarters." : unitPlural(kind) + " belong to a " + unitSingular(unitKinds[index - 1]) + "."}</small>{previous && !done && <Link to={unitPaths[kind] + "?create=1"}>Create {unitSingular(kind)} →</Link>}</div></li>;
      })}
    </ol>
    <p className="impact-setup-note">This guide disappears automatically once your first national unit, region, district, zone, and club are created. You can add more from the structure submenus at any time.</p>
    <ErrorBox error={error} />
  </section>;
}
