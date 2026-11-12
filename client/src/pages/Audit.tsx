import { useEffect, useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { api } from "../api";
import { PageHeader, Skeleton, ErrorBox, useWorkspace } from "../App";
export default function Audit() {
  const { w } = useWorkspace();
  const [search, setSearch] = useState(""),
    [term, setTerm] = useState(""),
    [page, setPage] = useState(1);
  useEffect(() => {
    const t = setTimeout(() => {
      setTerm(search);
      setPage(1);
    }, 250);
    return () => clearTimeout(t);
  }, [search]);
  const q = useQuery({
    queryKey: ["audit", w?.id, term, page],
    queryFn: () =>
      api("/audit-logs?search=" + encodeURIComponent(term) + "&page=" + page),
  });
  return (
    <>
      <PageHeader
        eyebrow="TRANSPARENCY BY DESIGN"
        title="Audit trail"
        description="A traceable record of changes within your authorized scope."
      />
      <section className="panel">
        <div className="filter-bar">
          <input
            aria-label="Filter audit actions"
            placeholder="Filter by action…"
            value={search}
            onChange={(e) => setSearch(e.target.value)}
          />
        </div>
        <ErrorBox error={q.error} />
        {q.isPending ? (
          <Skeleton />
        ) : (
          <div className="table-scroll" tabIndex={0} role="region" aria-label="Audit records · scroll for more columns">
            <table>
              <thead>
                <tr>
                  <th>Action</th>
                  <th>Resource</th>
                  <th>Actor / scope</th>
                  <th>Timestamp</th>
                  <th>Outcome</th>
                </tr>
              </thead>
              <tbody>
                {q.data?.items.map((x: any) => (
                  <tr key={x.id}>
                    <td>{x.action}</td>
                    <td className="mono">
                      {x.resource}
                      {x.changes && (
                        <details>
                          <summary>View changes</summary>
                          <pre>{x.changes}</pre>
                        </details>
                      )}
                    </td>
                    <td>
                      <small>{x.actor || x.actorId}</small>
                      <small>{x.organization || x.orgId}</small>
                    </td>
                    <td>{new Date(x.at).toLocaleString()}</td>
                    <td>
                      <span className="badge">{x.outcome}</span>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
            {!q.data?.items.length && (
              <div className="empty">No matching activity.</div>
            )}
          </div>
        )}
        <div className="pagination">
          <span>{q.data?.total || 0} records</span>
          <div>
            <button
              className="secondary"
              disabled={page === 1}
              onClick={() => setPage((p) => p - 1)}
            >
              Previous
            </button>
            <button
              className="secondary"
              disabled={page * 20 >= (q.data?.total || 0)}
              onClick={() => setPage((p) => p + 1)}
            >
              Next
            </button>
          </div>
        </div>
      </section>
    </>
  );
}
