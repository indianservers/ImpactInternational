import { useState } from "react";
import { Link, useNavigate } from "react-router-dom";
import { Globe2, Eye, EyeOff, ArrowRight } from "lucide-react";
import { useQueryClient } from "@tanstack/react-query";
import { api } from "../api";
import { ErrorBox } from "../App";
export default function Auth({ mode }: { mode: string }) {
  const nav = useNavigate(),
    qc = useQueryClient();
  const [show, setShow] = useState(false),
    [busy, setBusy] = useState(false),
    [error, setError] = useState<unknown>(),
    [message, setMessage] = useState("");
  const signup = mode === "signup",
    forgot = mode === "forgot-password" || mode === "resend-verification",
    complete = mode === "complete",
    verify =
      complete && new URLSearchParams(location.search).get("kind") === "verify";
  async function submit(e: React.FormEvent<HTMLFormElement>) {
    e.preventDefault();
    setBusy(true);
    setError(null);
    const data = Object.fromEntries(new FormData(e.currentTarget));
    try {
      if (mode === "login") {
        await api("/auth/login", "POST", data);
        qc.clear();
        const target = new URLSearchParams(location.search).get("returnTo");
        nav(
          target?.startsWith("/") && !target.startsWith("//") ? target : "/app",
        );
      } else {
        const result = await api(
          signup
            ? "/auth/register"
            : forgot
              ? mode === "resend-verification"
                ? "/auth/resend-verification"
                : "/auth/forgot-password"
              : "/auth/complete",
          "POST",
          {
            ...data,
            agreed: data.agreed === "on",
            token: new URLSearchParams(location.search).get("token"),
            phone: String(data.countryCode || "") + String(data.phone || ""),
          },
        );
        setMessage(result.message);
      }
    } catch (e) {
      setError(e);
    } finally {
      setBusy(false);
    }
  }
  return (
    <div className="auth-page">
      <div className="auth-story">
        <Link className="brand" to="/">
          <Globe2 />
          ClubSphere
        </Link>
        <div>
          <div className="eyebrow">CONNECTED BY PURPOSE</div>
          <h1>
            A place for people
            <br />
            who make
            <br />
            <em>a difference.</em>
          </h1>
          <p>
            Your communities. Your leadership.
            <br />
            One beautifully connected workspace.
          </p>
          <Globe2 size={160} strokeWidth={0.6} />
        </div>
        <small>Leadership without borders.</small>
      </div>
      <div className="auth-form">
        <Link to="/" className="back-link">
          ← Back to ClubSphere
        </Link>
        <div className="eyebrow">WELCOME TO YOUR COMMUNITY</div>
        <h1>
          {signup
            ? "Start something meaningful."
            : forgot
              ? "Let’s get you back in."
              : complete
                ? "Secure your account."
                : "Good to see you again."}
        </h1>
        <p>
          {signup
            ? "Create your personal account. Join an association when you’re ready."
            : forgot
              ? "Enter your email to request a password reset."
              : complete
                ? "Complete your secure account request."
                : "Sign in to your connected world of impact."}
        </p>
        <form onSubmit={submit} className="form-stack">
          {signup && (
            <label>
              Full name
              <input name="name" autoComplete="name" required />
            </label>
          )}
          {!complete && (
            <label>
              Email address
              <input
                name="email"
                type="email"
                autoComplete="email"
                placeholder="you@organization.org"
                required
              />
            </label>
          )}
          {signup && (
            <div className="form-grid">
              <label>
                Country code
                <input
                  name="countryCode"
                  defaultValue="+91"
                  pattern="\+[0-9]{1,4}"
                  required
                />
              </label>
              <label>
                Mobile number
                <input name="phone" type="tel" required />
              </label>
            </div>
          )}
          {!forgot && !verify && (
            <label>
              {complete ? "New password" : "Password"}
              <div className="password-field">
                <input
                  name="password"
                  type={show ? "text" : "password"}
                  minLength={signup || complete ? 12 : 1}
                  autoComplete={
                    signup || complete ? "new-password" : "current-password"
                  }
                  required
                />
                <button
                  type="button"
                  className="icon-button"
                  aria-label={show ? "Hide password" : "Show password"}
                  onClick={() => setShow(!show)}
                >
                  {show ? <EyeOff size={18} /> : <Eye size={18} />}
                </button>
              </div>
              {signup && <small>At least 12 characters.</small>}
            </label>
          )}
          {signup && (
            <>
              <label>
                Confirm password
                <input
                  name="confirmation"
                  type="password"
                  minLength={12}
                  required
                />
              </label>
              <label className="checkbox">
                <input name="agreed" type="checkbox" required />I agree to the
                development terms and privacy notice below.
              </label>
              <details>
                <summary>Terms & privacy notice</summary>
                <p>
                  This development service stores your profile and security
                  activity to provide organizational access. Do not submit
                  sensitive production data. Contact your association
                  administrator for account and data requests. Production legal
                  terms must be reviewed before launch.
                </p>
              </details>
            </>
          )}
          {mode === "login" && (
            <Link className="forgot-link" to="/forgot-password">
              Forgot password?
            </Link>
          )}
          {mode === "login" && (
            <Link className="forgot-link" to="/resend-verification">
              Resend verification email
            </Link>
          )}
          <ErrorBox error={error} />
          {message && (
            <div className="success" role="status">
              {message}
            </div>
          )}
          <button disabled={busy}>
            {busy
              ? "Please wait…"
              : signup
                ? "Create account"
                : forgot
                  ? "Send recovery instructions"
                  : complete
                    ? "Complete request"
                    : "Sign in"}
            <ArrowRight size={16} />
          </button>
        </form>
        <p className="auth-bottom">
          {mode === "login" ? (
            <>
              New to ClubSphere? <Link to="/signup">Create an account</Link>
            </>
          ) : (
            <Link to="/login">Return to sign in</Link>
          )}
        </p>
      </div>
    </div>
  );
}
