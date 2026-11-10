import { Link, useNavigate, useSearchParams } from "react-router-dom";
import { useState } from "react";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { Globe2, ShieldCheck, ShieldX, ArrowRight } from "lucide-react";
import { api } from "../api";
import { ErrorBox, Skeleton } from "../App";
export function JoinInvitation() {
  const [params] = useSearchParams(),
    nav = useNavigate(),
    qc = useQueryClient(),
    [error, setError] = useState<unknown>(),
    [busy, setBusy] = useState(false);
  const me = useQuery({
    queryKey: ["me"],
    queryFn: () => api("/auth/me"),
    retry: false,
  });
  const token = params.get("token") || "";
  return (
    <div className="verification-page">
      <section className="panel">
        <Link className="brand" to="/">
          <Globe2 />
          ClubSphere
        </Link>
        <div className="verification-icon">
          <ShieldCheck size={42} />
        </div>
        <div className="eyebrow">YOU’RE INVITED</div>
        <h1>A new community is waiting.</h1>
        <p>
          Accept a secure invitation to start a membership application, or
          request a reviewed link to an existing membership. Required approvals
          always apply.
        </p>
        <ErrorBox error={error} />
        {me.isPending ? (
          <Skeleton />
        ) : me.error ? (
          <div className="form-stack">
            <Link
              className="button"
              to={
                "/login?returnTo=" +
                encodeURIComponent(location.pathname + location.search)
              }
            >
              Sign in to continue <ArrowRight size={15} />
            </Link>
            <Link className="text-button" to="/signup">
              Create and verify an account first
            </Link>
            <small>After verification, return to this invitation link.</small>
          </div>
        ) : (
          <>
            <p>
              Continue as <b>{me.data.name}</b>.
            </p>
            <button
              disabled={busy || token.length !== 64}
              onClick={async () => {
                setBusy(true);
                try {
                  const r = await api(
                    "/membership/invitations/accept",
                    "POST",
                    { token },
                  );
                  qc.invalidateQueries({ queryKey: ["my-membership"] });
                  qc.invalidateQueries({ queryKey: ["applications"] });
                  nav(
                    r.kind === "Identity review required"
                      ? "/app/my-membership"
                      : "/app/apply",
                  );
                } catch (e) {
                  setError(e);
                } finally {
                  setBusy(false);
                }
              }}
            >
              {busy ? "Processing…" : "Accept secure invitation"}
              <ArrowRight size={16} />
            </button>
            <Link className="panel-link spaced" to="/app/apply">
              Already accepted? Open your applications →
            </Link>
          </>
        )}
      </section>
    </div>
  );
}
export function VerifyMembershipCard() {
  const [params] = useSearchParams(),
    token = params.get("token") || "";
  const q = useQuery({
    queryKey: ["public-card-verification", token],
    queryFn: () => api("/cards/verify/" + encodeURIComponent(token)),
    enabled: /^[a-fA-F0-9]{64}$/.test(token),
    retry: false,
    staleTime: 0,
  });
  return (
    <div className="verification-page">
      <section className="panel">
        <Link className="brand" to="/">
          <Globe2 />
          ClubSphere
        </Link>
        {q.isPending && token.length === 64 ? (
          <Skeleton />
        ) : (
          <>
            <div
              className={
                "verification-icon " + (q.data?.valid ? "" : "invalid")
              }
            >
              {q.data?.valid ? (
                <ShieldCheck size={48} />
              ) : (
                <ShieldX size={48} />
              )}
            </div>
            <div className="eyebrow">MEMBERSHIP VERIFICATION</div>
            <h1>
              {q.data?.valid
                ? "Current membership verified."
                : "Membership not verified."}
            </h1>
            <ErrorBox error={q.error} />
            {q.data ? (
              <>
                <h2>{q.data.name || "Card not recognized"}</h2>
                <p>{q.data.club}</p>
                <div className="context-row">
                  <span>Status</span>
                  <b>{q.data.status || "Unknown"}</b>
                </div>
                {q.data.expiresAt && (
                  <div className="context-row">
                    <span>Valid through</span>
                    <b>{new Date(q.data.expiresAt).toLocaleDateString()}</b>
                  </div>
                )}
                <p>{q.data.message}</p>
              </>
            ) : (
              <p>This verification link is incomplete or invalid.</p>
            )}
            <div className="privacy-notice">
              Public verification never displays birth dates, phone numbers,
              personal emails, or addresses.
            </div>
          </>
        )}
      </section>
    </div>
  );
}
