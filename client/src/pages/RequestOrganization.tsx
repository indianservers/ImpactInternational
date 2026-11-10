import { useState } from "react";
import { Link } from "react-router-dom";
import { Globe2 } from "lucide-react";
import { api } from "../api";
import { ErrorBox } from "../App";
export default function RequestOrganization() {
  const [message, setMessage] = useState(""),
    [error, setError] = useState<unknown>(),
    [busy, setBusy] = useState(false);
  return (
    <div className="auth-wrap">
      <Link className="brand" to="/">
        <Globe2 />
        ClubSphere
      </Link>
      <h1 style={{ marginTop: 40 }}>Bring your community together.</h1>
      <p>
        Tell us about your organization. Platform administrators review each
        registration request.
      </p>
      <form
        className="form-stack"
        onSubmit={async (e) => {
          e.preventDefault();
          setBusy(true);
          try {
            const r = await api(
              "/registration-requests",
              "POST",
              Object.fromEntries(new FormData(e.currentTarget)),
            );
            setMessage(r.message);
          } catch (e) {
            setError(e);
          } finally {
            setBusy(false);
          }
        }}
      >
        <label>
          Organization name
          <input name="name" required minLength={3} maxLength={200} />
        </label>
        <label>
          Contact email
          <input name="email" type="email" required />
        </label>
        <label>
          About your community
          <textarea name="description" maxLength={2000} rows={4} required />
        </label>
        <ErrorBox error={error} />
        {message ? (
          <div className="success" role="status">
            {message}
          </div>
        ) : (
          <button disabled={busy}>
            {busy ? "Submitting…" : "Request registration"}
          </button>
        )}
        <Link to="/login">Already have a workspace? Sign in →</Link>
      </form>
    </div>
  );
}
