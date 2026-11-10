import { useState } from "react";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { Link } from "react-router-dom";
import { api, type Level } from "../api";
import { PageHeader, Skeleton, ErrorBox, useWorkspace } from "../App";
export default function Settings() {
  const { w } = useWorkspace(),
    qc = useQueryClient();
  const [error, setError] = useState<unknown>(),
    [message, setMessage] = useState(""),
    [busy, setBusy] = useState(false);
  const q = useQuery({
    queryKey: ["settings", w?.id],
    queryFn: () => api("/settings"),
  });
  const levels = useQuery({
    queryKey: ["levels", w?.id],
    queryFn: () => api<Level[]>("/levels"),
  });
  const can = w?.permissions.includes("settings.manage");
  return (
    <>
      <PageHeader
        eyebrow="MAKE IT YOUR OWN"
        title="Workspace settings"
        description="Your identity, organizational structure, and shared preferences."
      />
      <ErrorBox error={error || q.error} />
      {message && (
        <div className="success" role="status">
          {message}
        </div>
      )}
      {q.isPending ? (
        <Skeleton />
      ) : (
        <section className="panel">
          <div className="section-heading">
            <div>
              <h2>Association identity</h2>
              <p>A consistent identity across your community.</p>
            </div>
          </div>
          <form
            className="form-stack settings-form"
            onSubmit={async (e) => {
              e.preventDefault();
              setBusy(true);
              setMessage("");
              try {
                const d = Object.fromEntries(new FormData(e.currentTarget));
                await api("/settings", "PUT", { ...q.data, ...d });
                setMessage("Association settings saved.");
                qc.invalidateQueries({ queryKey: ["settings"] });
                qc.invalidateQueries({ queryKey: ["workspaces"] });
              } catch (e) {
                setError(e);
              } finally {
                setBusy(false);
              }
            }}
          >
            <fieldset disabled={!can || busy}>
              <label>
                Association name
                <input name="name" required defaultValue={q.data?.name} />
              </label>
              <div className="form-grid">
                <label>
                  Default language
                  <select name="language" defaultValue={q.data?.language}>
                    <option value="en">English</option>
                    <option value="hi">Hindi (content preference)</option>
                  </select>
                </label>
                <label>
                  Time zone
                  <input
                    name="timeZone"
                    required
                    defaultValue={q.data?.timeZone}
                  />
                </label>
                <label>
                  Currency
                  <input
                    name="currency"
                    required
                    maxLength={3}
                    minLength={3}
                    defaultValue={q.data?.currency}
                  />
                </label>
                <label>
                  Brand color
                  <input
                    name="color"
                    type="color"
                    defaultValue={q.data?.color}
                  />
                </label>
              </div>
              {can && <button>Save preferences</button>}
            </fieldset>
          </form>
        </section>
      )}
      <section className="panel spaced">
        <div className="section-heading">
          <div>
            <h2>Organizational levels</h2>
            <p>
              Configure permitted relationships. Existing levels remain stable
              to protect hierarchy integrity.
            </p>
          </div>
        </div>
        <div className="level-list">
          {levels.data?.map((l) => (
            <div key={l.id}>
              <b>{l.name}</b>
              <span>
                Parent:{" "}
                {levels.data?.find((x) => x.id === l.parentLevelId)?.name ||
                  "Association root"}
              </span>
              {l.isClub && <span className="badge">Club / chapter</span>}
            </div>
          ))}
        </div>
        {can && (
          <form
            className="form-stack settings-form"
            onSubmit={async (e) => {
              e.preventDefault();
              const f = e.currentTarget;
              const d = Object.fromEntries(new FormData(f));
              try {
                await api("/levels", "POST", {
                  name: d.name,
                  parentLevelId: d.parentLevelId || null,
                  isClub: d.isClub === "on",
                });
                qc.invalidateQueries({ queryKey: ["levels"] });
                f.reset();
                setMessage("Organizational level added.");
              } catch (e) {
                setError(e);
              }
            }}
          >
            <div className="form-grid">
              <label>
                New level name
                <input name="name" required />
              </label>
              <label>
                Permitted parent
                <select name="parentLevelId">
                  <option value="">Association root</option>
                  {levels.data?.map((l) => (
                    <option key={l.id} value={l.id}>
                      {l.name}
                    </option>
                  ))}
                </select>
              </label>
            </div>
            <label className="checkbox">
              <input type="checkbox" name="isClub" />
              Represents a club or chapter
            </label>
            <button>Add level</button>
          </form>
        )}
      </section>
      <Link className="panel-link" to="/app/administration">
        Manage roles and permissions →
      </Link>
    </>
  );
}
