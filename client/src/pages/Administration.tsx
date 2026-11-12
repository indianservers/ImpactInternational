import SectionTabs from "../SectionTabs";
import { useState } from "react";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { ShieldCheck, Plus, X } from "lucide-react";
import { useDialog } from "../useDialog";
import { api } from "../api";
import {
  PageHeader,
  Skeleton,
  ErrorBox,
  useWorkspace,
  Provision,
} from "../App";
export default function Administration() {
  const [section, setSection] = useState("Roles");
  const { w, me } = useWorkspace(),
    qc = useQueryClient();
  const [form, setForm] = useState<any>(null),
    [error, setError] = useState<unknown>(),
    [busy, setBusy] = useState(false);
  useDialog(!!form, () => setForm(null));
  const roles = useQuery({
    queryKey: ["roles", w?.id],
    queryFn: () => api("/roles"),
  });
  const perms = useQuery({
    queryKey: ["permissions", w?.id],
    queryFn: () => api<string[]>("/permissions"),
  });
  const assignments = useQuery({
    queryKey: ["assignments", w?.id],
    queryFn: () => api("/assignments"),
  });
  const requests = useQuery({
    queryKey: ["registration-requests"],
    queryFn: () => api("/registration-requests"),
    enabled: !!me.platformAdmin,
  });
  const orgs = useQuery({
    queryKey: ["scope-options", w?.id],
    queryFn: () => api("/organizations?size=100"),
  });
  async function save(e: React.FormEvent<HTMLFormElement>) {
    e.preventDefault();
    setBusy(true);
    try {
      const d = Object.fromEntries(new FormData(e.currentTarget));
      if (form.kind === "assignment") await api("/assignments", "POST", d);
      else
        await api(
          "/roles" + (form.id ? "/" + form.id : ""),
          form.id ? "PUT" : "POST",
          { name: d.name, permissions: form.permissions },
        );
      setForm(null);
      qc.invalidateQueries({ queryKey: ["roles"] });
      qc.invalidateQueries({ queryKey: ["assignments"] });
    } catch (e) {
      setError(e);
    } finally {
      setBusy(false);
    }
  }
  return (
    <>
      <PageHeader
        eyebrow="TRUST & GOVERNANCE"
        title="Roles & permissions"
        description="The right access, for the right people, in the right place."
      >
        {w?.permissions.includes("roles.manage") && (
          <button
            onClick={() => {
              setError(null);
              setForm({ kind: "role", name: "", permissions: [] });
            }}
          >
            <Plus size={16} />
            Create role
          </button>
        )}
      </PageHeader>
      <ErrorBox error={roles.error || error} />
      <SectionTabs id="access" label="Access management" tabs={me.platformAdmin ? ["Roles", "Assignments", "Registrations"] : ["Roles", "Assignments"]} value={section} onChange={setSection} />
      <section className="panel" role="tabpanel" id="access-panel-0" aria-labelledby="access-tab-0" hidden={section !== "Roles"}>
        <div className="section-heading">
          <div>
            <h2>Permission matrix</h2>
            <p>
              Permissions are enforced by the API for each organizational
              workspace.
            </p>
          </div>
          <ShieldCheck size={23} />
        </div>
        {roles.isPending ? (
          <Skeleton />
        ) : (
          <div className="table-scroll permission-matrix" tabIndex={0} role="region" aria-label="Permission matrix · scroll for more columns">
            <table>
              <thead>
                <tr>
                  <th>Permission</th>
                  {roles.data?.map((r: any) => (
                    <th key={r.id}>
                      {w?.permissions.includes("roles.manage") ? (
                        <button
                          className="text-button"
                          onClick={() => {
                            setError(null);
                            setForm({ ...r, kind: "role" });
                          }}
                        >
                          {r.name}
                        </button>
                      ) : (
                        r.name
                      )}
                    </th>
                  ))}
                </tr>
              </thead>
              <tbody>
                {perms.data?.map((p) => (
                  <tr key={p}>
                    <td>{p}</td>
                    {roles.data?.map((r: any) => (
                      <td
                        key={r.id}
                        aria-label={
                          r.permissions.includes(p) ? "Granted" : "Not granted"
                        }
                      >
                        {r.permissions.includes(p) ? (
                          <span className="grant">✓</span>
                        ) : (
                          "—"
                        )}
                      </td>
                    ))}
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </section>
      <section className="panel spaced" role="tabpanel" id="access-panel-1" aria-labelledby="access-tab-1" hidden={section !== "Assignments"}>
        <div className="section-heading">
          <div>
            <h2>People & organizational scopes</h2>
            <p>
              Assignments visible within your active scope. Showing up to 100.
            </p>
          </div>
          {w?.permissions.includes("roles.assign") && (
            <button
              className="secondary"
              onClick={() => {
                setError(null);
                setForm({ kind: "assignment" });
              }}
            >
              Assign role
            </button>
          )}
        </div>
        <div className="table-scroll" tabIndex={0} role="region" aria-label="Permissions and assignments · scroll for more columns">
          <table>
            <thead>
              <tr>
                <th>Person</th>
                <th>Role</th>
                <th>Scope</th>
                <th>Actions</th>
              </tr>
            </thead>
            <tbody>
              {assignments.data?.map((x: any) => (
                <tr key={x.id}>
                  <td>
                    {x.user}
                    <small>{x.email}</small>
                  </td>
                  <td>{x.role}</td>
                  <td>{x.organization}</td>
                  <td>
                    {w?.permissions.includes("roles.assign") &&
                      x.email !== me.email && (
                        <button
                          className="secondary"
                          onClick={async () => {
                            if (
                              !window.confirm(
                                "Revoke this organizational role assignment?",
                              )
                            )
                              return;
                            try {
                              await api("/assignments/" + x.id, "DELETE");
                              qc.invalidateQueries({
                                queryKey: ["assignments"],
                              });
                            } catch (e) {
                              setError(e);
                            }
                          }}
                        >
                          Revoke
                        </button>
                      )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </section>
      {me.platformAdmin && (
        <section className="panel spaced" role="tabpanel" id="access-panel-2" aria-labelledby="access-tab-2" hidden={section !== "Registrations"}>
          <h2>Organization registration requests</h2>
          {requests.data?.map((r: any) => (
            <div className="context-row" key={r.id}>
              <span>
                {r.name}
                <small>{r.email}</small>
              </span>
              <span>{r.description}</span>
              <span className="badge pending">{r.status}</span>
              {r.status === "Pending" && (
                <button
                  className="secondary"
                  onClick={async () => {
                    try {
                      await api(
                        "/registration-requests/" + r.id + "/provision",
                        "POST",
                        {},
                      );
                      qc.invalidateQueries({
                        queryKey: ["registration-requests"],
                      });
                      qc.invalidateQueries({ queryKey: ["workspaces"] });
                    } catch (e) {
                      setError(e);
                    }
                  }}
                >
                  Provision association
                </button>
              )}
            </div>
          ))}
          {!requests.data?.length && <p>No registration requests.</p>}
        </section>
      )}
      {me.platformAdmin && (
        <Provision
          onDone={() => qc.invalidateQueries({ queryKey: ["workspaces"] })}
        />
      )}
      {form && (
        <div className="modal-backdrop">
          <section
            className="modal"
            role="dialog"
            aria-modal="true"
            aria-label="Access management"
          >
            <button
              className="icon-button close"
              aria-label="Close"
              onClick={() => setForm(null)}
            >
              <X />
            </button>
            <h2>
              {form.kind === "assignment"
                ? "Assign scoped role"
                : form.id
                  ? "Edit unassigned role"
                  : "Create role"}
            </h2>
            <form className="form-stack" onSubmit={save}>
              {form.kind === "assignment" ? (
                <>
                  <label>
                    User email
                    <input type="email" name="email" required />
                  </label>
                  <label>
                    Role
                    <select name="roleId" required>
                      {roles.data?.map((r: any) => (
                        <option key={r.id} value={r.id}>
                          {r.name}
                        </option>
                      ))}
                    </select>
                  </label>
                  <label>
                    Organizational scope
                    <select name="orgId" required>
                      {orgs.data?.items.map((o: any) => (
                        <option key={o.id} value={o.id}>
                          {o.name}
                        </option>
                      ))}
                    </select>
                  </label>
                </>
              ) : (
                <>
                  <label>
                    Role name
                    <input name="name" defaultValue={form.name} required />
                  </label>
                  <div className="permission-options">
                    {perms.data
                      ?.filter((p) => w?.permissions.includes(p))
                      .map((p) => (
                        <label className="checkbox" key={p}>
                          <input
                            type="checkbox"
                            checked={form.permissions.includes(p)}
                            onChange={(e) =>
                              setForm({
                                ...form,
                                permissions: e.target.checked
                                  ? [...form.permissions, p]
                                  : form.permissions.filter(
                                      (x: string) => x !== p,
                                    ),
                              })
                            }
                          />
                          {p}
                        </label>
                      ))}
                  </div>
                </>
              )}
              <ErrorBox error={error} />
              <button disabled={busy}>
                {busy ? "Saving…" : "Save access configuration"}
              </button>
            </form>
          </section>
        </div>
      )}
    </>
  );
}
