import { useState } from "react";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { api } from "../api";
import { PageHeader, ErrorBox, useWorkspace } from "../App";
export default function Profile() {
  const { me } = useWorkspace(),
    qc = useQueryClient();
  const [error, setError] = useState<unknown>(),
    [message, setMessage] = useState("");
  const q = useQuery({
    queryKey: ["sessions"],
    queryFn: () => api("/auth/sessions"),
  });
  async function submit(
    e: React.FormEvent<HTMLFormElement>,
    path: string,
    method: string,
  ) {
    e.preventDefault();
    setError(null);
    try {
      await api(
        path,
        method,
        Object.fromEntries(new FormData(e.currentTarget)),
      );
      setMessage("Your account has been updated.");
      qc.invalidateQueries({ queryKey: ["me"] });
      qc.invalidateQueries({ queryKey: ["sessions"] });
    } catch (e) {
      setError(e);
    }
  }
  return (
    <>
      <PageHeader
        eyebrow="YOUR PERSONAL SPACE"
        title="Profile & security"
        description="Manage your account and stay in control of your sessions."
      />
      <ErrorBox error={error} />
      {message && (
        <div className="success" role="status">
          {message}
        </div>
      )}
      <div className="dashboard-grid">
        <section className="panel">
          <h2>Your profile</h2>
          <form
            className="form-stack"
            onSubmit={(e) => submit(e, "/users/profile", "PUT")}
          >
            <label>
              Full name
              <input
                name="name"
                defaultValue={me.name}
                required
                minLength={2}
              />
            </label>
            <label>
              Email
              <input value={me.email} readOnly />
            </label>
            <label>
              Phone
              <input name="phone" defaultValue={me.phone} />
            </label>
            <button>Save profile</button>
          </form>
        </section>
        <section className="panel">
          <h2>Change password</h2>
          <form
            className="form-stack"
            onSubmit={(e) => submit(e, "/auth/change-password", "POST")}
          >
            <label>
              Current password
              <input
                type="password"
                name="current"
                required
                autoComplete="current-password"
              />
            </label>
            <label>
              New password
              <input
                type="password"
                name="password"
                minLength={12}
                required
                autoComplete="new-password"
              />
            </label>
            <p>Use at least 12 characters. Other sessions will be revoked.</p>
            <button>Update password</button>
          </form>
        </section>
      </div>
      <section className="panel spaced">
        <h2>Active sessions</h2>
        <ErrorBox error={q.error} />
        {q.data?.map((s: any) => (
          <div className="context-row" key={s.id}>
            <span>
              {s.current ? "This session" : "Browser session"}
              <small>
                Created {new Date(s.createdAt).toLocaleString()} · Expires{" "}
                {new Date(s.expiresAt).toLocaleString()}
              </small>
            </span>
            <button
              className="secondary"
              onClick={async () => {
                try {
                  await api("/auth/sessions/" + s.id, "DELETE");
                  if (s.current) location.assign("/login");
                  else qc.invalidateQueries({ queryKey: ["sessions"] });
                } catch (e) {
                  setError(e);
                }
              }}
            >
              Revoke session
            </button>
          </div>
        ))}
      </section>
    </>
  );
}
