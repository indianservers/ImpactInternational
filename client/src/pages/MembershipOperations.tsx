import { useState } from "react";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { api } from "../api";
import { PageHeader, Skeleton, ErrorBox, useWorkspace } from "../App";
import { MemberStatus, MemberDrawer } from "../MemberComponents";
export default function MembershipOperations() {
  const { w } = useWorkspace(),
    qc = useQueryClient();
  const grants = w?.permissions || [];
  const tabs = [
    ["transfers", "Transfers", "members.transfer.approve"],
    ["links", "Identity review", "members.link"],
    ["invitations", "Invitations", "members.invite"],
    ["notifications", "Notification delivery", "members.manage"],
  ].filter((t) => grants.includes(t[2]));
  const [tab, setTab] = useState(tabs[0]?.[0] || ""),
    [selected, setSelected] = useState<any>(null),
    [error, setError] = useState<unknown>(),
    [message, setMessage] = useState(""),
    [busy, setBusy] = useState(false),
    [page, setPage] = useState(1);
  const path = (
    {
      transfers: "/membership/transfers?page=" + page,
      links: "/membership/link-requests",
      invitations: "/membership/invitations",
      notifications: "/membership/notification-status",
    } as any
  )[tab];
  const q = useQuery({
    queryKey: ["membership-operations", w?.id, tab, page],
    queryFn: () => api(path),
    enabled: !!path,
    refetchInterval: tab === "notifications" ? 5000 : false,
  });
  async function action(path: string, body: any, method = "POST") {
    setBusy(true);
    setError(null);
    try {
      await api(path, method, body);
      qc.invalidateQueries({ queryKey: ["membership-operations"] });
      qc.invalidateQueries({ queryKey: ["members"] });
      qc.invalidateQueries({ queryKey: ["my-membership"] });
      qc.invalidateQueries({ queryKey: ["workspaces"] });
      setSelected(null);
      setMessage("The action was recorded.");
    } catch (e) {
      setError(e);
    } finally {
      setBusy(false);
    }
  }
  const items = Array.isArray(q.data) ? q.data : q.data?.items || [];
  return (
    <>
      <PageHeader
        eyebrow="TRUST THROUGH EVERY TRANSITION"
        title="Membership operations"
        description="Review transfers, verify account links, and track transactional delivery."
      />
      <ErrorBox error={error || q.error} />
      {message && (
        <div className="success" role="status">
          {message}
        </div>
      )}
      <div
        className="member-tabs"
        role="tablist"
        aria-label="Membership operations"
      >
        {tabs.map(([key, label]) => (
          <button
            role="tab"
            aria-selected={tab === key}
            key={key}
            className={tab === key ? "active" : ""}
            onClick={() => {
              setTab(key);
              setPage(1);
            }}
          >
            {label}
          </button>
        ))}
      </div>
      <section className="panel directory">
        {q.isPending ? (
          <Skeleton />
        ) : (
          <div className="table-scroll" tabIndex={0} role="region" aria-label="Membership operations · scroll for more columns">
            <table>
              <thead>
                <tr>
                  {(tab === "transfers"
                    ? [
                        "Member",
                        "Source → destination",
                        "Effective date",
                        "State",
                        "Review",
                      ]
                    : tab === "links"
                      ? [
                          "Existing person",
                          "Verified account",
                          "Requested",
                          "State",
                          "Review",
                        ]
                      : tab === "invitations"
                        ? [
                            "Intended email",
                            "Purpose",
                            "Expires",
                            "Usage / status",
                            "Actions",
                          ]
                        : [
                            "Notification",
                            "Recipient scope",
                            "Attempts",
                            "Delivery status",
                            "Last attempt",
                          ]
                  ).map((t) => (
                    <th key={t}>{t}</th>
                  ))}
                </tr>
              </thead>
              <tbody>
                {items.map((r: any) => (
                  <tr key={r.id}>
                    {tab === "transfers" ? (
                      <>
                        <td>{r.person}</td>
                        <td>
                          {r.source}
                          <small>→ {r.destination}</small>
                        </td>
                        <td>{new Date(r.effectiveAt).toLocaleDateString()}</td>
                        <td>
                          <MemberStatus status={r.status} />
                        </td>
                        <td>
                          {[
                            "PendingSource",
                            "PendingDestination",
                            "PendingAssociation",
                            "Blocked",
                          ].includes(r.status) && (
                            <button
                              className="secondary"
                              onClick={() => setSelected(r)}
                            >
                              Review transfer
                            </button>
                          )}
                        </td>
                      </>
                    ) : tab === "links" ? (
                      <>
                        <td>{r.person}</td>
                        <td>{r.account}</td>
                        <td>{new Date(r.at).toLocaleDateString()}</td>
                        <td>
                          <MemberStatus status={r.status} />
                        </td>
                        <td>
                          {r.status === "Pending" && (
                            <button
                              className="secondary"
                              onClick={() => setSelected(r)}
                            >
                              Review identity link
                            </button>
                          )}
                        </td>
                      </>
                    ) : tab === "invitations" ? (
                      <>
                        <td>{r.email || "Shareable invitation"}</td>
                        <td>{r.kind}</td>
                        <td>{new Date(r.expiresAt).toLocaleDateString()}</td>
                        <td>
                          {r.uses} / {r.usageLimit}{" "}
                          <MemberStatus
                            status={
                              r.revoked
                                ? "Revoked"
                                : new Date(r.expiresAt) < new Date()
                                  ? "Expired"
                                  : "Available"
                            }
                          />
                        </td>
                        <td>
                          {!r.revoked && (
                            <button
                              className="text-button"
                              onClick={() => {
                                if (window.confirm("Revoke this invitation?"))
                                  action(
                                    "/membership/invitations/" + r.id,
                                    undefined,
                                    "DELETE",
                                  );
                              }}
                            >
                              Revoke
                            </button>
                          )}
                        </td>
                      </>
                    ) : (
                      <>
                        <td>{r.subject}</td>
                        <td>{r.scope || "Authorized membership notice"}</td>
                        <td>{r.attempts}</td>
                        <td>
                          <MemberStatus status={r.status} />
                        </td>
                        <td>
                          {r.lastAttempt
                            ? new Date(r.lastAttempt).toLocaleString()
                            : "Not attempted"}
                        </td>
                      </>
                    )}
                  </tr>
                ))}
              </tbody>
            </table>
            {!items.length && (
              <div className="empty">
                No records are visible in this organizational scope.
              </div>
            )}
          </div>
        )}
        {tab === "transfers" && (
          <div className="pagination">
            <span>{q.data?.total || 0} transfer requests</span>
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
        )}
      </section>
      {selected && (
        <MemberDrawer
          title={
            tab === "transfers" ? "Review transfer" : "Review account identity"
          }
          onClose={() => setSelected(null)}
        >
          <ErrorBox error={error} />
          {tab === "transfers" ? (
            <>
              <h3>{selected.person}</h3>
              <p>
                {selected.source} → {selected.destination}
              </p>
              <MemberStatus status={selected.status} />
              <p className="spaced">{selected.reason}</p>
              {selected.clearanceRequired &&
                !selected.financialCleared &&
                grants.includes("members.manage") && (
                  <form
                    className="form-stack spaced"
                    onSubmit={(e) => {
                      e.preventDefault();
                      const reason = new FormData(e.currentTarget).get(
                        "reason",
                      );
                      action(
                        "/membership/transfers/" + selected.id + "/clearance",
                        { hold: false, reason, version: selected.version },
                      );
                    }}
                  >
                    <p>
                      Administrative financial clearance is required. This
                      records a review basis and does not record a payment.
                    </p>
                    <label>
                      Clearance basis
                      <textarea name="reason" required minLength={5} />
                    </label>
                    <button disabled={busy}>
                      Record administrative clearance
                    </button>
                  </form>
                )}
              {selected.status === "Blocked" ? (
                <>
                  <p>
                    The scheduled transfer was blocked by a changed source state
                    or clearance. Resolve the underlying condition before
                    rescheduling.
                  </p>
                  <button
                    disabled={busy}
                    onClick={() =>
                      action(
                        "/membership/transfers/" + selected.id + "/retry",
                        { version: selected.version },
                      )
                    }
                  >
                    Retry reviewed transfer
                  </button>
                </>
              ) : (
                <DecisionForm
                  busy={busy}
                  onSave={(decision: string, comment: string) =>
                    action(
                      "/membership/transfers/" + selected.id + "/decisions",
                      { decision, comment, version: selected.version },
                    )
                  }
                />
              )}
            </>
          ) : (
            <>
              <h3>{selected.person}</h3>
              <p>Verified account: {selected.account}</p>
              <div className="privacy-notice">
                Confirm the person’s identity through your authorized
                verification process. A matching family email or phone is
                insufficient by itself. No profiles are automatically merged.
              </div>
              <DecisionForm
                busy={busy}
                requiredComment
                onSave={(decision: string, reason: string) =>
                  action(
                    "/membership/link-requests/" + selected.id + "/decide",
                    { decision, reason },
                  )
                }
              />
            </>
          )}
        </MemberDrawer>
      )}
    </>
  );
}
function DecisionForm({ onSave, busy, requiredComment = false }: any) {
  const [decision, setDecision] = useState("Approve"),
    [comment, setComment] = useState("");
  return (
    <form
      className="form-stack spaced"
      onSubmit={(e) => {
        e.preventDefault();
        onSave(decision, comment);
      }}
    >
      <label>
        Decision
        <select value={decision} onChange={(e) => setDecision(e.target.value)}>
          <option>Approve</option>
          <option>Reject</option>
        </select>
      </label>
      <label>
        {requiredComment ? "Recorded identity-verification basis" : "Comments"}
        <textarea
          value={comment}
          required={requiredComment || decision === "Reject"}
          minLength={requiredComment ? 5 : undefined}
          maxLength={2000}
          onChange={(e) => setComment(e.target.value)}
        />
      </label>
      <button disabled={busy}>
        {busy ? "Recording…" : "Record review decision"}
      </button>
    </form>
  );
}
