import { useState } from "react";
import { Link, useSearchParams } from "react-router-dom";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import {
  Plus,
  ClipboardCheck,
  ArrowRight,
  Send,
  FilePenLine,
} from "lucide-react";
import { api } from "../api";
import { PageHeader, Skeleton, ErrorBox, useWorkspace } from "../App";
import {
  MemberDrawer,
  MemberStatus,
  ClubCategoryFields,
} from "../MemberComponents";
export default function MembershipApplications({
  review = false,
}: {
  review?: boolean;
}) {
  const qc = useQueryClient();
  const {w}=useWorkspace();
  const [params]=useSearchParams();
  const [status, setStatus] = useState(params.get("status")||""),
    [page, setPage] = useState(1),
    [create, setCreate] = useState(false),
    [selected, setSelected] = useState<string | null>(null),
    [message, setMessage] = useState("");
  const q = useQuery({
    queryKey: ["applications", w?.id, review, status, page],
    enabled: !!w,
    refetchInterval: 30000,
    queryFn: () =>
      api(
        "/membership/applications?mine=" +
          !review +
          "&status=" +
          encodeURIComponent(status) +
          "&page=" +
          page,
      ),
  });
  return (
    <>
      <PageHeader
        eyebrow={review ? "THOUGHTFUL ADMISSIONS" : "YOUR NEXT CHAPTER"}
        title={review ? "Impact membership approvals" : "Join an Impact club"}
        description={
          review
            ? "Review Impact International applications through the configured approval stages."
            : "Find an Impact club, apply for membership, and track your Impact journey."
        }
      >
        {!review && (
          <button onClick={() => setCreate(true)}>
            <Plus size={16} />
            Start an application
          </button>
        )}
      </PageHeader>
      {!review && (
        <div className="application-journey">
          <div>
            <span>01</span>
            <b>Find your community</b>
            <small>Choose an Impact International club.</small>
          </div>
          <ArrowRight size={18} />
          <div>
            <span>02</span>
            <b>Tell your story</b>
            <small>Save a draft and complete your profile.</small>
          </div>
          <ArrowRight size={18} />
          <div>
            <span>03</span>
            <b>Grow your impact</b>
            <small>Track review and membership activation.</small>
          </div>
        </div>
      )}
      <ErrorBox error={q.error} />
      {message && (
        <div className="success" role="status">
          {message}
        </div>
      )}
      <section className="panel directory">
        <div className="filter-bar">
          <div>
            <h2>
              {review ? "Application inbox" : "My membership applications"}
            </h2>
            <p className="compact-text">
              Applications become memberships only after every required
              approval.
            </p>
          </div>
          <select
            aria-label="Filter application status"
            value={status}
            onChange={(e) => {
              setStatus(e.target.value);
              setPage(1);
            }}
          >
            <option value="">All application states</option>
            <option value="Pending">All pending applications</option>
            {[
              "Draft",
              "Submitted",
              "Under Review",
              "Changes Requested",
              "Approved",
              "Rejected",
              "Withdrawn",
              "Cancelled",
            ].map((x) => (
              <option key={x}>{x}</option>
            ))}
          </select>
        </div>
        {q.isPending ? (
          <Skeleton />
        ) : (
          <div className="table-scroll" tabIndex={0} role="region" aria-label="Membership applications · scroll for more columns">
            <table>
              <thead>
                <tr>
                  <th>Applicant</th>
                  <th>Club / category</th>
                  <th>Submitted</th>
                  <th>Current review stage</th>
                  <th>Status</th>
                  <th>Actions</th>
                </tr>
              </thead>
              <tbody>
                {q.data?.items.map((a: any) => (
                  <tr key={a.id}>
                    <td>
                      <button
                        className="text-button name-button"
                        onClick={() => setSelected(a.id)}
                      >
                        {a.name}
                      </button>
                      <small>{a.id.slice(0, 8).toUpperCase()}</small>
                    </td>
                    <td>
                      {a.club}
                      <small>{a.category}</small>
                    </td>
                    <td>
                      {a.submittedAt
                        ? new Date(a.submittedAt).toLocaleDateString()
                        : "Draft"}
                    </td>
                    <td>{a.stage || "Not submitted"}</td>
                    <td>
                      <MemberStatus status={a.status} />
                    </td>
                    <td>
                      <button
                        className="secondary"
                        onClick={() => setSelected(a.id)}
                      >
                        {review ? "Review" : "Open application"}
                      </button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
            {!q.data?.items.length && (
              <div className="empty">
                <ClipboardCheck size={30} />
                <h3>
                  {review
                    ? "No applications match your filters."
                    : "Your next community starts here."}
                </h3>
                <p>
                  {review
                    ? "New submissions appear within your authorized organizational scope."
                    : "Start an application and save your progress at any time."}
                </p>
              </div>
            )}
          </div>
        )}
        <div className="pagination">
          <span>
            {q.data?.total || 0} applications · Page {page}
          </span>
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
              disabled={page * 20 >= (q.data?.total || 0)}
              onClick={() => setPage(page + 1)}
            >
              Next
            </button>
          </div>
        </div>
      </section>
      {create && (
        <MemberDrawer
          title="Find your next community"
          onClose={() => setCreate(false)}
        >
          <StartApplication
            onDone={(id: string) => {
              setCreate(false);
              setSelected(id);
              qc.invalidateQueries({ queryKey: ["applications"] });
            }}
          />
        </MemberDrawer>
      )}
      {selected && (
        <MemberDrawer
          title={
            review
              ? "Review membership application"
              : "Your membership application"
          }
          onClose={() => setSelected(null)}
        >
          <ApplicationDetail
            id={selected}
            review={review}
            onMessage={setMessage}
          />
        </MemberDrawer>
      )}
    </>
  );
}
function StartApplication({ onDone }: any) {
  const [form, setForm] = useState<any>({
      clubId: "",
      categoryId: "",
      tenantId: "",
      statement: "",
    }),
    [error, setError] = useState<unknown>(),
    [busy, setBusy] = useState(false);
  return (
    <form
      className="form-stack"
      onSubmit={async (e) => {
        e.preventDefault();
        setBusy(true);
        try {
          const r = await api("/membership/applications", "POST", form);
          onDone(r.id);
        } catch (e) {
          setError(e);
        } finally {
          setBusy(false);
        }
      }}
    >
      <ClubCategoryFields
        {...form}
        publicCatalog
        onChange={(v: any) => setForm({ ...form, ...v })}
      />
      <label>
        A little about your interest
        <textarea
          value={form.statement}
          maxLength={4000}
          rows={5}
          onChange={(e) => setForm({ ...form, statement: e.target.value })}
          placeholder="What inspires you to join this community?"
        />
      </label>
      <p>
        This saves an editable draft. You’ll submit separately when you’re
        ready.
      </p>
      <ErrorBox error={error} />
      <button disabled={busy}>
        <FilePenLine size={15} />
        {busy ? "Saving…" : "Save application draft"}
      </button>
    </form>
  );
}
function ApplicationDetail({ id, review, onMessage }: any) {
  const qc = useQueryClient();
  const [statement, setStatement] = useState<string | null>(null),
    [decision, setDecision] = useState("Approve"),
    [comment, setComment] = useState(""),
    [error, setError] = useState<unknown>(),
    [busy, setBusy] = useState(false);
  const q = useQuery({
    queryKey: ["application-detail", id],
    queryFn: () => api("/membership/applications/" + id),
  });
  async function mutate(path: string, body: any) {
    setBusy(true);
    setError(null);
    try {
      const r = await api(path, "POST", body);
      qc.invalidateQueries({ queryKey: ["application-detail", id] });
      qc.invalidateQueries({ queryKey: ["applications"] });
      qc.invalidateQueries({ queryKey: ["my-membership"] });
      qc.invalidateQueries({ queryKey: ["workspaces"] });
      qc.invalidateQueries({ queryKey: ["members"] });
      qc.invalidateQueries({ queryKey: ["member-metrics"] });
      onMessage(
        r?.status === "Approved"
          ? "Application approved; membership and a verifiable digital ID were created."
          : "The application action has been recorded.",
      );
      return true;
    } catch (e) {
      setError(e);
      return false;
    } finally {
      setBusy(false);
    }
  }
  if (q.isPending) return <Skeleton />;
  if (q.error) return <ErrorBox error={q.error} />;
  const { application: a, person: p, stage, decisions, reviewers } = q.data,
    editable = ["Draft", "Changes Requested"].includes(a.status);
  return (
    <>
      <div className="application-detail-head">
        <div>
          <h2>{p.name}</h2>
          <small>{p.reference}</small>
        </div>
        <MemberStatus status={a.status} />
      </div>
      <ErrorBox error={error} />
      <div className="context-row">
        <span>Revision</span>
        <b>{a.revision}</b>
      </div>
      {stage && (
        <section className="stage-card">
          <div className="eyebrow">CURRENT APPROVAL STAGE</div>
          <h3>
            {stage.sequence}. {stage.name}
          </h3>
          <p>
            {
              decisions.filter(
                (d: any) =>
                  d.stageId === stage.id &&
                  d.revision === a.revision &&
                  d.decision === "Approve",
              ).length
            }{" "}
            of {stage.requiredApprovals} required approvals recorded.
          </p>
          {reviewers?.length ? (
            <small>
              Reviewers: {reviewers.map((r: any) => r.name).join(", ")}
            </small>
          ) : (
            <small>
              Review is controlled by configured roles and club scope.
            </small>
          )}
        </section>
      )}
      <h3 className="spaced">Application statement</h3>
      {!review && editable ? (
        <textarea
          aria-label="Application statement"
          rows={6}
          maxLength={4000}
          value={statement ?? a.statement}
          onChange={(e) => setStatement(e.target.value)}
        />
      ) : (
        <p className="application-statement">
          {a.statement || "No statement supplied."}
        </p>
      )}
      {!review && editable && (
        <>
          <Link className="panel-link" to="/app/my-membership">
            Complete or update your personal profile →
          </Link>
          <div className="header-actions spaced">
            <button
              className="secondary"
              disabled={busy}
              onClick={async () => {
                setBusy(true);
                try {
                  await api("/membership/applications/" + id, "PUT", {
                    clubId: a.clubId,
                    categoryId: a.categoryId,
                    statement: statement ?? a.statement,
                    version: a.version,
                  });
                  qc.invalidateQueries({
                    queryKey: ["application-detail", id],
                  });
                  onMessage("Application draft saved.");
                } catch (e) {
                  setError(e);
                } finally {
                  setBusy(false);
                }
              }}
            >
              Save draft
            </button>
            <button
              disabled={
                busy || (statement !== null && statement !== a.statement)
              }
              onClick={() =>
                mutate("/membership/applications/" + id + "/submit", {
                  version: a.version,
                })
              }
            >
              <Send size={14} />
              Submit for review
            </button>
          </div>
          {statement !== null && statement !== a.statement && (
            <small>Save your changes before submitting.</small>
          )}
        </>
      )}
      {!review &&
        ["Draft", "Submitted", "Under Review", "Changes Requested"].includes(
          a.status,
        ) && (
          <button
            className="text-button spaced"
            disabled={busy}
            onClick={() => {
              if (window.confirm("Withdraw this application?"))
                mutate("/membership/applications/" + id + "/withdraw", {
                  version: a.version,
                });
            }}
          >
            Withdraw application
          </button>
        )}
      {q.data.canDecide && review && (
        <form
          className="form-stack spaced"
          onSubmit={async (e) => {
            e.preventDefault();
            if (
              await mutate("/membership/applications/" + id + "/decisions", {
                decision,
                comment,
                version: a.version,
              })
            )
              setComment("");
          }}
        >
          <label>
            Review decision
            <select
              value={decision}
              onChange={(e) => setDecision(e.target.value)}
            >
              <option value="Approve">Approve current stage</option>
              <option value="RequestChanges">Request changes</option>
              <option value="Reject">Reject application</option>
            </select>
          </label>
          <label>
            Comments
            <textarea
              required={decision !== "Approve"}
              maxLength={2000}
              rows={4}
              value={comment}
              onChange={(e) => setComment(e.target.value)}
            />
          </label>
          <button disabled={busy}>
            {busy ? "Recording…" : "Record decision"}
          </button>
        </form>
      )}
      {review && !q.data.canDecide && (
        <div className="privacy-notice">
          {["Approved", "Rejected", "Withdrawn", "Cancelled"].includes(a.status)
            ? "This application is complete. No further review decision is required."
            : ["Draft", "Changes Requested"].includes(a.status)
              ? "The applicant must submit this application before review."
              : "Your active role cannot decide this application at its current stage."}
        </div>
      )}
      <h3 className="spaced">Decision history</h3>
      {decisions.length ? (
        decisions.map((d: any) => (
          <div className="timeline-item" key={d.id}>
            <span className="activity-dot" />
            <div>
              <b>
                {d.decision === "RequestChanges"
                  ? "Changes requested"
                  : d.decision}{" "}
                · {d.actor}
              </b>
              <p>{d.comment}</p>
              <small>
                Revision {d.revision} · {new Date(d.at).toLocaleString()}
              </small>
            </div>
          </div>
        ))
      ) : (
        <p>No decisions recorded yet.</p>
      )}
    </>
  );
}
