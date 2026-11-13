import { useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { ChevronRight, ChevronDown, Network, Plus } from "lucide-react";
import { api, type Org, type Level } from "./api";
import { useWorkspace, ErrorBox } from "./App";
export default function HierarchyTree({ onOpen, onCreate }: any) {
  const { w } = useWorkspace();
  const q = useQuery({
    queryKey: ["tree-root", w?.id],
    queryFn: () => api<Org>("/organizations/" + w?.orgId),
  });
  return (
    <section className="panel hierarchy-panel">
      <div className="section-heading">
        <div>
          <h2>Impact International hierarchy</h2>
          <p>Expand a branch to load its immediate children.</p>
        </div>
        <Network size={20} />
      </div>
      <ErrorBox error={q.error} />
      {q.data && <TreeNode org={q.data} onOpen={onOpen} onCreate={onCreate} />}
    </section>
  );
}
function TreeNode({ org, onOpen, onCreate }: any) {
  const { w } = useWorkspace();
  const levels = useQuery({ queryKey: ["levels", w?.id], queryFn: () => api<Level[]>("/levels") });
  const childLevel = levels.data?.find(l => l.parentLevelId === org.levelId);
  const [expanded, setExpanded] = useState(false),
    [page, setPage] = useState(1);
  const q = useQuery({
    queryKey: ["tree-children", w?.id, org.id, page],
    queryFn: () => api("/organizations?parentId=" + org.id + "&page=" + page),
    enabled: expanded,
  });
  return (
    <div className="tree-node">
      <div className="tree-line">
        <button
          className="icon-button"
          aria-expanded={expanded}
          aria-label={(expanded ? "Collapse " : "Expand ") + org.name}
          onClick={() => setExpanded(!expanded)}
        >
          {expanded ? <ChevronDown size={17} /> : <ChevronRight size={17} />}
        </button>
        <Network size={17} />
        <button className="text-button" onClick={() => onOpen(org.id)}>
          {org.name}
        </button>
        <small>{org.code}</small>
        {w?.permissions.includes("organization.create") && childLevel && (
          <button
            className="icon-button"
            aria-label={"Create " + childLevel.name.toLowerCase() + " within " + org.name}
            onClick={() => onCreate(org.id)}
          >
            <Plus size={15} />
          </button>
        )}
      </div>
      {expanded && (
        <div className="tree-children">
          {q.isPending ? (
            <p>Loading branch…</p>
          ) : (
            <>
              <ErrorBox error={q.error} />
              {q.data?.items.map((child: Org) => (
                <TreeNode
                  key={child.id}
                  org={child}
                  onOpen={onOpen}
                  onCreate={onCreate}
                />
              ))}
              {!q.data?.items.length && (
                <small className="tree-empty">No child units.</small>
              )}
              {q.data?.total > 20 && (
                <div className="pagination">
                  <button
                    className="secondary"
                    disabled={page === 1}
                    onClick={() => setPage(page - 1)}
                  >
                    Previous
                  </button>
                  <span>Page {page}</span>
                  <button
                    className="secondary"
                    disabled={page * 20 >= q.data.total}
                    onClick={() => setPage(page + 1)}
                  >
                    Next
                  </button>
                </div>
              )}
            </>
          )}
        </div>
      )}
    </div>
  );
}
