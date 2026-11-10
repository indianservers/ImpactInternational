import { Link } from "react-router-dom";
import { useState } from "react";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import {
  Upload,
  FileSpreadsheet,
  CheckCircle2,
  AlertTriangle,
  ArrowRight,
  RefreshCw,
  Download,
} from "lucide-react";
import { api, apiForm } from "../api";
import { PageHeader, Skeleton, ErrorBox } from "../App";
import { MemberStatus, ClubCategoryFields } from "../MemberComponents";
export default function MemberImports() {
  const qc = useQueryClient();
  const [target, setTarget] = useState<any>({ clubId: "", categoryId: "" }),
    [file, setFile] = useState<File | null>(null),
    [id, setId] = useState(""),
    [mapping, setMapping] = useState<Record<string, string>>({}),
    [error, setError] = useState<unknown>(),
    [busy, setBusy] = useState(false),
    [page, setPage] = useState(1),
    [ack, setAck] = useState(false);
  const jobs = useQuery({
    queryKey: ["import-jobs"],
    queryFn: () => api("/membership/imports"),
    refetchInterval: 5000,
  });
  const job = useQuery({
    queryKey: ["import-job", id, page],
    queryFn: () => api("/membership/imports/" + id + "?page=" + page),
    enabled: !!id,
    refetchInterval: (q) =>
      ["Validating", "Queued", "Importing"].includes(q.state.data?.status)
        ? 1500
        : false,
  });
  const d = job.data,
    headers = d ? JSON.parse(d.headers) : [];
  async function action(path: string, body: any = {}) {
    setBusy(true);
    setError(null);
    try {
      await api(path, "POST", body);
      qc.invalidateQueries({ queryKey: ["import-job", id] });
      qc.invalidateQueries({ queryKey: ["import-jobs"] });
    } catch (e) {
      setError(e);
    } finally {
      setBusy(false);
    }
  }
  return (
    <>
      <PageHeader
        eyebrow="MAKE YOUR DATA A BETTER START"
        title="Member import"
        description="Map, validate, review duplicates, and commit with a complete audit trail."
      />
      <div className="import-steps">
        {[
          "Upload",
          "Map columns",
          "Validate",
          "Review duplicates",
          "Commit & summary",
        ].map((s, i) => (
          <div key={s}>
            <span>{i + 1}</span>
            {s}
            {i < 4 && <ArrowRight size={14} />}
          </div>
        ))}
      </div>
      <ErrorBox error={error || jobs.error || job.error} />
      {!id ? (
        <div className="dashboard-grid">
          <section className="panel">
            <FileSpreadsheet size={30} />
            <h2 className="spaced">Bring your community with you</h2>
            <p>
              CSV and .xlsx · 10 MB maximum · up to 10,000 rows per job. Formula
              cells and macros are rejected.
            </p>
            <form
              className="form-stack"
              onSubmit={async (e) => {
                e.preventDefault();
                setBusy(true);
                setError(null);
                try {
                  if (!file) throw new Error("Choose an import file.");
                  const data = new FormData();
                  data.append("file", file);
                  data.append("clubId", target.clubId);
                  data.append("categoryId", target.categoryId);
                  data.append("requestKey", crypto.randomUUID());
                  const r = await apiForm("/membership/imports", data);
                  setId(r.id);
                  setMapping(
                    Object.fromEntries(
                      r.headers
                        .map((h: string) => [
                          h.toLowerCase().replaceAll(" ", ""),
                          h,
                        ])
                        .filter(([k]: any) =>
                          [
                            "name",
                            "email",
                            "phone",
                            "city",
                            "country",
                            "clubCode",
                            "categoryCode",
                            "joinedAt",
                            "profession",
                            "industry",
                          ].includes(k),
                        ),
                    ),
                  );
                  qc.invalidateQueries({ queryKey: ["import-jobs"] });
                } catch (e) {
                  setError(e);
                } finally {
                  setBusy(false);
                }
              }}
            >
              <ClubCategoryFields
                {...target}
                onChange={(v: any) => setTarget({ ...target, ...v })}
              />
              <label>
                Upload a values-only spreadsheet
                <input
                  type="file"
                  accept=".csv,.xlsx"
                  required
                  onChange={(e) => setFile(e.target.files?.[0] || null)}
                />
              </label>
              <button disabled={busy}>
                <Upload size={15} />
                {busy ? "Staging file…" : "Stage file for review"}
              </button>
            </form>
          </section>
          <section className="panel">
            <h2>Import history</h2>
            <p>
              Jobs remain tied to the officer’s authorized organizational scope.
            </p>
            {jobs.isPending ? (
              <Skeleton />
            ) : (
              jobs.data?.map((j: any) => (
                <button
                  className="import-job-row"
                  key={j.id}
                  onClick={() => {
                    setId(j.id);
                    setPage(1);
                    setAck(false);
                  }}
                >
                  <FileSpreadsheet size={19} />
                  <span>
                    <b>{j.fileName}</b>
                    <small>
                      {j.importedRows || j.processedRows} of {j.totalRows} rows
                      · {new Date(j.createdAt).toLocaleDateString()}
                    </small>
                  </span>
                  <MemberStatus status={j.status} />
                </button>
              ))
            )}
          </section>
        </div>
      ) : (
        <>
          <button
            className="text-button"
            onClick={() => {
              setId("");
              setMapping({});
              setAck(false);
            }}
          >
            ← Back to import history
          </button>
          {job.isPending ? (
            <Skeleton />
          ) : (
            d && (
              <>
                <section className="panel spaced">
                  <div className="section-heading">
                    <div>
                      <h2>{d.fileName}</h2>
                      <p>
                        {d.status === "Importing"
                          ? "Committing all reviewed rows in one transaction. The final count appears after the commit."
                          : `${d.processedRows} rows validated · ${d.importedRows} rows imported`}
                      </p>
                    </div>
                    <MemberStatus status={d.status} />
                  </div>
                  {d.totalRows > 0 && (
                    <div
                      className="progress-track"
                      role="progressbar"
                      aria-label="Import validation progress"
                      aria-valuenow={d.processedRows}
                      aria-valuemin={0}
                      aria-valuemax={d.totalRows}
                    >
                      <span
                        style={{
                          width:
                            Math.min(
                              100,
                              (d.processedRows / d.totalRows) * 100,
                            ) + "%",
                        }}
                      />
                    </div>
                  )}
                  {d.error && <div className="error">{d.error}</div>}
                  {d.status === "Uploaded" && (
                    <form
                      className="form-stack"
                      onSubmit={(e) => {
                        e.preventDefault();
                        action("/membership/imports/" + id + "/mapping", {
                          fields: mapping,
                        });
                      }}
                    >
                      <h3>Map your columns</h3>
                      <p>
                        Name is required. Unmapped club/category fields use the
                        authorized defaults chosen during upload.
                      </p>
                      <div className="advanced-filters">
                        {[
                          ["name", "Full name"],
                          ["email", "Email"],
                          ["phone", "Phone"],
                          ["city", "City"],
                          ["country", "Country"],
                          ["clubCode", "Club code"],
                          ["categoryCode", "Category code"],
                          ["joinedAt", "Joining date"],
                          ["profession", "Profession"],
                          ["industry", "Industry"],
                        ].map(([key, label]) => (
                          <label key={key}>
                            {label}
                            <select
                              required={key === "name"}
                              value={mapping[key] || ""}
                              onChange={(e) => {
                                const next = { ...mapping };
                                if (e.target.value) next[key] = e.target.value;
                                else delete next[key];
                                setMapping(next);
                              }}
                            >
                              <option value="">Not mapped</option>
                              {headers.map((h: string) => (
                                <option key={h}>{h}</option>
                              ))}
                            </select>
                          </label>
                        ))}
                      </div>
                      <button disabled={busy}>
                        Validate mapped rows <ArrowRight size={15} />
                      </button>
                    </form>
                  )}
                  {d.status === "NeedsReview" && (
                    <div className="duplicate-review">
                      <AlertTriangle size={23} />
                      <h3>Review before joining identities</h3>
                      <p>
                        {d.totalReview} potential duplicates · {d.totalErrors}{" "}
                        invalid rows. Shared family contact details can
                        legitimately belong to different people. No identities
                        are merged automatically.
                      </p>
                      {d.totalErrors > 0 && (
                        <p>
                          Cancel this job and upload a corrected file to resolve
                          validation errors.
                        </p>
                      )}
                      <label className="checkbox">
                        <input
                          type="checkbox"
                          checked={ack}
                          onChange={(e) => setAck(e.target.checked)}
                        />
                        I reviewed the potential duplicate rows on this page and
                        confirm they are separate people.
                      </label>
                      <button
                        className="secondary"
                        disabled={
                          !ack ||
                          busy ||
                          !d.rows.some((r: any) => r.status === "NeedsReview")
                        }
                        onClick={() =>
                          action("/membership/imports/" + id + "/resolve", {
                            rows: d.rows
                              .filter((r: any) => r.status === "NeedsReview")
                              .map((r: any) => ({
                                rowId: r.id,
                                createSeparate: true,
                              })),
                          })
                        }
                      >
                        Confirm separate identities on this page
                      </button>
                    </div>
                  )}
                  {d.status === "Ready" && (
                    <div className="ready-import">
                      <CheckCircle2 size={28} />
                      <div>
                        <h3>Ready for an atomic import</h3>
                        <p>
                          Every row is valid and all potential duplicates have
                          an explicit decision. Existing verified profile
                          details are preserved.
                        </p>
                      </div>
                      <button
                        disabled={busy}
                        onClick={() =>
                          action("/membership/imports/" + id + "/commit")
                        }
                      >
                        Import {d.totalRows} reviewed memberships
                      </button>
                    </div>
                  )}
                  {d.status === "Completed" && (
                    <div className="success">
                      <b>{d.importedRows} memberships imported successfully.</b>
                      <p>
                        Retrying this completed job does not import its rows
                        again. Raw input is cleared after commit; staging
                        personal data is retained for a limited review period.
                      </p>
                    </div>
                  )}
                  <div className="header-actions spaced">
                    {[
                      "Uploaded",
                      "Validating",
                      "NeedsReview",
                      "Ready",
                      "Queued",
                      "Failed",
                    ].includes(d.status) && (
                      <button
                        className="secondary"
                        disabled={busy}
                        onClick={() =>
                          action("/membership/imports/" + id + "/cancel")
                        }
                      >
                        Cancel before final commit
                      </button>
                    )}
                    {d.status === "Failed" && (
                      <button
                        className="secondary"
                        disabled={busy}
                        onClick={() =>
                          action("/membership/imports/" + id + "/retry")
                        }
                      >
                        <RefreshCw size={14} />
                        Retry validation
                      </button>
                    )}
                    {d.status === "Completed" && (
                      <Link className="button secondary" to="/app/members">
                        <Download size={14} />
                        Review imported memberships
                      </Link>
                    )}
                  </div>
                </section>
                {d.rows?.length > 0 && (
                  <section className="panel directory spaced">
                    <div
                      className="section-heading"
                      style={{ padding: 20, margin: 0 }}
                    >
                      <div>
                        <h2>Row-level review</h2>
                        <p>
                          Only an explicit existing-person selection adds an
                          affiliation to a current identity.
                        </p>
                      </div>
                    </div>
                    <div className="table-scroll">
                      <table>
                        <thead>
                          <tr>
                            <th>Row</th>
                            <th>Person</th>
                            <th>Contact</th>
                            <th>Status</th>
                            <th>Review / validation</th>
                          </tr>
                        </thead>
                        <tbody>
                          {d.rows.map((r: any) => {
                            const v = JSON.parse(r.data);
                            return (
                              <tr key={r.id}>
                                <td>{r.rowNumber}</td>
                                <td>{v.name || "Redacted after retention"}</td>
                                <td>{v.email || v.phone || "—"}</td>
                                <td>
                                  <MemberStatus status={r.status} />
                                </td>
                                <td>
                                  {r.error}
                                  {r.status === "NeedsReview" && (
                                    <ExistingResolution
                                      row={r}
                                      onResolve={(personId: string) =>
                                        action(
                                          "/membership/imports/" +
                                            id +
                                            "/resolve",
                                          {
                                            rows: [
                                              {
                                                rowId: r.id,
                                                existingPersonId: personId,
                                              },
                                            ],
                                          },
                                        )
                                      }
                                    />
                                  )}
                                </td>
                              </tr>
                            );
                          })}
                        </tbody>
                      </table>
                    </div>
                    <div className="pagination">
                      <span>Page {page}</span>
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
                          disabled={page * 50 >= d.totalRows}
                          onClick={() => setPage(page + 1)}
                        >
                          Next
                        </button>
                      </div>
                    </div>
                  </section>
                )}
              </>
            )
          )}
        </>
      )}
    </>
  );
}
function ExistingResolution({ row, onResolve }: any) {
  const [matches, setMatches] = useState<any[] | null>(null),
    [error, setError] = useState<unknown>();
  const v = JSON.parse(row.data);
  return (
    <div className="spaced">
      <button
        className="text-button"
        onClick={async () => {
          try {
            setMatches(
              await api("/members/duplicates", "POST", {
                legalName: v.name,
                displayName: v.name,
                contacts: [
                  { kind: "Email", label: "Personal", value: v.email || "" },
                  { kind: "Phone", label: "Personal", value: v.phone || "" },
                ].filter((x) => x.value),
              }),
            );
          } catch (e) {
            setError(e);
          }
        }}
      >
        Review authorized existing matches
      </button>
      <ErrorBox error={error} />
      {matches?.map((m) => (
        <div key={m.id}>
          <b>{m.name}</b>
          <small>{m.reason}</small>
          <button className="secondary" onClick={() => onResolve(m.id)}>
            Use this existing person
          </button>
        </div>
      ))}
      {matches?.length === 0 && (
        <small>
          No existing scoped match. Resolve as a separate identity after review.
        </small>
      )}
    </div>
  );
}
