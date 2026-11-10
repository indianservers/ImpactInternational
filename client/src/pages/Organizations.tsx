import HierarchyTree from "../HierarchyTree";
import { useEffect, useState } from "react";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import {
  Plus,
  Search,
  ChevronRight,
  Network,
  Building2,
  X,
  ArrowLeft,
  Pencil,
} from "lucide-react";
import { useDialog } from "../useDialog";
import { api, type Org, type Level } from "../api";
import { PageHeader, Skeleton, ErrorBox, useWorkspace } from "../App";
export default function Organizations({ clubs = false }: { clubs?: boolean }) {
  const { w } = useWorkspace(),
    qc = useQueryClient();
  const [search, setSearch] = useState(""),
    [term, setTerm] = useState(""),
    [status, setStatus] = useState(""),
    [level, setLevel] = useState(""),
    [sort, setSort] = useState("name"),
    [page, setPage] = useState(1),
    [parents, setParents] = useState<{ id: string; name: string }[]>([]),
    [form, setForm] = useState<Partial<Org> | null>(null),
    [detail, setDetail] = useState<Org | null>(null),
    [error, setError] = useState<unknown>(),
    [busy, setBusy] = useState(false);
  useEffect(() => {
    const t = setTimeout(() => {
      setTerm(search);
      setPage(1);
    }, 250);
    return () => clearTimeout(t);
  }, [search]);
  useEffect(() => {
    setPage(1);
    setParents([]);
  }, [clubs]);
  useDialog(!!form || !!detail, () => {
    setForm(null);
    setDetail(null);
  });
  const parent = parents.at(-1);
  const params = new URLSearchParams({
    search: term,
    status,
    sort,
    page: String(page),
    clubs: String(clubs),
  });
  if (level) params.set("levelId", level);
  if (parent) params.set("parentId", parent.id);
  const q = useQuery({
    queryKey: ["organizations", w?.id, params.toString()],
    queryFn: () => api("/organizations?" + params),
    placeholderData: (p) => p,
  });
  const levels = useQuery({
    queryKey: ["levels", w?.id],
    queryFn: () => api<Level[]>("/levels"),
  });
  const parentOptions = useQuery({
    queryKey: ["parent-options", w?.id, form?.levelId],
    queryFn: () =>
      api(
        "/organizations?size=100&levelId=" +
          levels.data?.find((x) => x.id === form?.levelId)?.parentLevelId,
      ),
    enabled:
      !!form?.levelId &&
      !!levels.data?.find((x) => x.id === form.levelId)?.parentLevelId,
  });
  async function edit(id: string) {
    try {
      setForm(await api<Org>("/organizations/" + id));
      setError(null);
    } catch (e) {
      setError(e);
    }
  }
  async function save(e: React.FormEvent) {
    e.preventDefault();
    setBusy(true);
    try {
      await api(
        "/organizations" + (form?.id ? "/" + form.id : ""),
        form?.id ? "PUT" : "POST",
        {
          ...form,
          parentId: form?.parentId || null,
          charterDate: form?.charterDate || null,
        },
      );
      setForm(null);
      qc.invalidateQueries({ queryKey: ["organizations"] });
      qc.invalidateQueries({ queryKey: ["dashboard"] });
      qc.invalidateQueries({ queryKey: ["tree-children"] });
      qc.invalidateQueries({ queryKey: ["tree-root"] });
    } catch (e) {
      setError(e);
    } finally {
      setBusy(false);
    }
  }
  return (
    <>
      <PageHeader
        eyebrow="CONNECTED COMMUNITIES"
        title={clubs ? "Club directory" : "Organization explorer"}
        description={
          clubs
            ? "Discover and manage the clubs that bring your mission to life."
            : "Navigate your hierarchy, from global leadership to local impact."
        }
      >
        {w?.permissions.includes("organization.create") && (
          <button
            onClick={() => {
              setError(null);
              setForm({
                name: "",
                code: "",
                levelId: "",
                parentId: parent?.id || w.orgId,
                city: "",
                country: "India",
                status: "Active",
                version: 1,
              });
            }}
          >
            <Plus size={17} />
            Create {clubs ? "club" : "organization"}
          </button>
        )}
      </PageHeader>
      {!clubs && (
        <HierarchyTree
          onOpen={async (id: string) => {
            try {
              setDetail(await api<Org>("/organizations/" + id));
            } catch (e) {
              setError(e);
            }
          }}
          onCreate={(parentId: string) => {
            setError(null);
            setForm({
              name: "",
              code: "",
              levelId: "",
              parentId,
              city: "",
              country: "India",
              status: "Active",
              version: 1,
            });
          }}
        />
      )}
      <div className="panel directory">
        <div className="filter-bar">
          <div className="search-field">
            <Search size={18} />
            <input
              aria-label="Search organizations"
              placeholder="Search by name or code…"
              value={search}
              onChange={(e) => setSearch(e.target.value)}
            />
          </div>
          <select
            aria-label="Filter by level"
            value={level}
            onChange={(e) => {
              setLevel(e.target.value);
              setPage(1);
            }}
          >
            <option value="">All organization types</option>
            {levels.data?.map((l) => (
              <option key={l.id} value={l.id}>
                {l.name}
              </option>
            ))}
          </select>
          <select
            aria-label="Filter by status"
            value={status}
            onChange={(e) => {
              setStatus(e.target.value);
              setPage(1);
            }}
          >
            <option value="">All statuses</option>
            {["Active", "Pending", "Archived"].map((x) => (
              <option key={x}>{x}</option>
            ))}
          </select>
          <select
            aria-label="Sort organizations"
            value={sort}
            onChange={(e) => setSort(e.target.value)}
          >
            <option value="name">Sort by name</option>
            <option value="code">Sort by code</option>
          </select>
        </div>
        {!clubs && (
          <div className="tree-breadcrumb">
            <button className="text-button" onClick={() => setParents([])}>
              <Network size={15} />
              All authorized units
            </button>
            {parents.map((x, i) => (
              <span key={x.id}>
                <ChevronRight size={14} />
                <button
                  className="text-button"
                  onClick={() => setParents((p) => p.slice(0, i + 1))}
                >
                  {x.name}
                </button>
              </span>
            ))}
          </div>
        )}
        <ErrorBox error={q.error || error} />
        {q.isPending ? (
          <Skeleton />
        ) : (
          <>
            <div className="table-scroll">
              <table>
                <thead>
                  <tr>
                    <th>Organization</th>
                    <th>Type / parent</th>
                    <th>Location</th>
                    <th>Status</th>
                    <th>Chartered</th>
                    <th>
                      <span className="sr-only">Actions</span>
                    </th>
                  </tr>
                </thead>
                <tbody>
                  {q.data?.items.map((o: Org) => (
                    <tr key={o.id}>
                      <td>
                        <div className="name-cell">
                          <span className="table-icon">
                            {clubs ? (
                              <Building2 size={18} />
                            ) : (
                              <Network size={18} />
                            )}
                          </span>
                          <div>
                            <button
                              className="text-button name-button"
                              onClick={async () =>
                                setDetail(await api("/organizations/" + o.id))
                              }
                            >
                              {o.name}
                            </button>
                            <small>{o.code}</small>
                          </div>
                        </div>
                      </td>
                      <td>
                        <b className="table-type">{o.level}</b>
                        <small>{o.parent || "Association root"}</small>
                      </td>
                      <td>
                        {o.city || "—"}
                        <small>{o.country}</small>
                      </td>
                      <td>
                        <span className={"badge " + o.status.toLowerCase()}>
                          {o.status}
                        </span>
                      </td>
                      <td>{o.charterDate || "—"}</td>
                      <td>
                        <div className="row-actions">
                          {!clubs && o.hasChildren && (
                            <button
                              className="icon-button"
                              aria-label={"Expand " + o.name}
                              onClick={() => {
                                setParents((p) => [
                                  ...p,
                                  { id: o.id, name: o.name },
                                ]);
                                setPage(1);
                                setSearch("");
                              }}
                            >
                              <ChevronRight size={18} />
                            </button>
                          )}
                          {w?.permissions.includes("organization.update") && (
                            <button
                              className="icon-button"
                              aria-label={"Edit " + o.name}
                              onClick={() => edit(o.id)}
                            >
                              <Pencil size={16} />
                            </button>
                          )}
                        </div>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
              {!q.data?.items.length && (
                <div className="empty">
                  <Building2 size={30} />
                  <h3>No organizations found</h3>
                  <p>Try adjusting your search or filters.</p>
                </div>
              )}
            </div>
            <div className="pagination">
              <span>
                {q.data?.total || 0} organizations · Page {page}
              </span>
              <div>
                <button
                  className="secondary"
                  disabled={page === 1 || q.isFetching}
                  onClick={() => setPage((p) => p - 1)}
                >
                  Previous
                </button>
                <button
                  className="secondary"
                  disabled={page * 20 >= (q.data?.total || 0) || q.isFetching}
                  onClick={() => setPage((p) => p + 1)}
                >
                  Next
                </button>
              </div>
            </div>
          </>
        )}
      </div>
      {detail && (
        <div className="modal-backdrop">
          <section
            className="modal"
            role="dialog"
            aria-modal="true"
            aria-label="Organization details"
          >
            <button
              className="close icon-button"
              aria-label="Close details"
              onClick={() => setDetail(null)}
            >
              <X />
            </button>
            <div className="eyebrow">ORGANIZATION DETAILS</div>
            <h2>{detail.name}</h2>
            {Object.entries(detail)
              .filter(
                ([k, v]) =>
                  v &&
                  ![
                    "id",
                    "tenantId",
                    "levelId",
                    "parentId",
                    "version",
                  ].includes(k),
              )
              .map(([k, v]) => (
                <div className="context-row" key={k}>
                  <span>{k}</span>
                  <b>{String(v)}</b>
                </div>
              ))}
            {w?.permissions.includes("organization.update") && (
              <button
                onClick={() => {
                  setForm(detail);
                  setDetail(null);
                }}
              >
                Edit organization
              </button>
            )}
          </section>
        </div>
      )}
      {form && (
        <div className="modal-backdrop">
          <section
            className="drawer"
            role="dialog"
            aria-modal="true"
            aria-label="Organization form"
          >
            <div className="drawer-heading">
              <div>
                <div className="eyebrow">ORGANIZATION MANAGEMENT</div>
                <h2>{form.id ? "Edit organization" : "Create organization"}</h2>
              </div>
              <button
                className="icon-button"
                aria-label="Close form"
                onClick={() => setForm(null)}
              >
                <X />
              </button>
            </div>
            <form className="form-stack" onSubmit={save}>
              <h3>Identity & structure</h3>
              <label>
                Name
                <input
                  required
                  maxLength={200}
                  value={form.name || ""}
                  onChange={(e) => setForm({ ...form, name: e.target.value })}
                />
              </label>
              <div className="form-grid">
                <label>
                  Organization code
                  <input
                    required
                    maxLength={40}
                    value={form.code || ""}
                    onChange={(e) => setForm({ ...form, code: e.target.value })}
                  />
                </label>
                <label>
                  Short name
                  <input
                    value={form.shortName || ""}
                    onChange={(e) =>
                      setForm({ ...form, shortName: e.target.value } as any)
                    }
                  />
                </label>
              </div>
              <label>
                Organization type
                <select
                  required
                  disabled={!!form.id}
                  value={form.levelId || ""}
                  onChange={(e) =>
                    setForm({
                      ...form,
                      levelId: e.target.value,
                      parentId: null,
                    })
                  }
                >
                  <option value="">Select type</option>
                  {levels.data
                    ?.filter((l) => !clubs || l.isClub)
                    .map((l) => (
                      <option key={l.id} value={l.id}>
                        {l.name}
                      </option>
                    ))}
                </select>
              </label>
              <label>
                Parent organization
                <select
                  value={form.parentId || ""}
                  onChange={(e) =>
                    setForm({ ...form, parentId: e.target.value || null })
                  }
                >
                  <option value="">Select parent</option>
                  {parentOptions.data?.items.map((o: Org) => (
                    <option key={o.id} value={o.id}>
                      {o.name}
                    </option>
                  ))}
                </select>
              </label>
              <h3>Location & contact</h3>
              <div className="form-grid">
                {["country", "state", "city", "email", "phone", "website"].map(
                  (k) => (
                    <label key={k}>
                      {k[0].toUpperCase() + k.slice(1)}
                      <input
                        type={
                          k === "email"
                            ? "email"
                            : k === "website"
                              ? "url"
                              : "text"
                        }
                        value={(form as any)[k] || ""}
                        onChange={(e) =>
                          setForm({ ...form, [k]: e.target.value })
                        }
                      />
                    </label>
                  ),
                )}
              </div>
              <label>
                Address
                <textarea
                  value={form.address || ""}
                  onChange={(e) =>
                    setForm({ ...form, address: e.target.value })
                  }
                />
              </label>
              <div className="form-grid">
                <label>
                  Status
                  <select
                    value={form.status}
                    onChange={(e) =>
                      setForm({ ...form, status: e.target.value })
                    }
                  >
                    {["Active", "Pending", "Archived"].map((x) => (
                      <option key={x}>{x}</option>
                    ))}
                  </select>
                </label>
                <label>
                  Charter date
                  <input
                    type="date"
                    value={form.charterDate || ""}
                    onChange={(e) =>
                      setForm({ ...form, charterDate: e.target.value })
                    }
                  />
                </label>
              </div>
              <ErrorBox error={error} />
              <div className="drawer-footer">
                <button
                  type="button"
                  className="secondary"
                  onClick={() => setForm(null)}
                >
                  <ArrowLeft size={16} />
                  Cancel
                </button>
                <button disabled={busy}>
                  {busy ? "Saving…" : "Save organization"}
                </button>
              </div>
            </form>
          </section>
        </div>
      )}
    </>
  );
}
