import { useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { api } from "./api";
import { ErrorBox, Skeleton, useWorkspace } from "./App";
import HierarchyExplorer from "./HierarchyExplorer";
import { UnitBreadcrumbs } from "./pages/OrganizationHierarchyCharts";

export default function HierarchyTree({ onOpen, onCreate }: any) {
  const { w } = useWorkspace();
  const [selected, setSelected] = useState("");
  const q = useQuery({ queryKey: ["visual-hierarchy", w?.id, selected], queryFn: () => api("/structure/intelligence?depth=5" + (selected ? "&unitId=" + encodeURIComponent(selected) : "")), enabled: !!w });
  return <section className="panel hierarchy-panel">
    <div className="section-heading"><div><h2>Impact International hierarchy</h2><p>Discover the connections from International headquarters to every club.</p></div><button type="button" className="secondary" onClick={() => setSelected("")}>My full scope</button></div>
    <ErrorBox error={q.error}/>
    {q.isPending ? <Skeleton/> : q.data?.root && <>
      <UnitBreadcrumbs items={q.data.breadcrumbs} onSelect={setSelected}/>
      <HierarchyExplorer key={q.data.root.id} root={q.data.root} onSelect={setSelected} onOpen={onOpen} membersAllowed={q.data.capabilities.members} leadershipAllowed={q.data.capabilities.leadership}/>
      {onCreate && w?.permissions.includes("organization.create") && q.data.root.level !== "Club" && <button type="button" className="secondary" style={{ marginTop: 16 }} onClick={() => onCreate(q.data.root.id)}>Add child unit</button>}
    </>}
  </section>;
}
