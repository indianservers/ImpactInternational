import {TenantBrand} from "../TenantBrand";
import { useState } from "react";
import { Link, useNavigate } from "react-router-dom";
import { Eye, EyeOff, ArrowRight, Mail, LockKeyhole, UsersRound, CircleAlert } from "lucide-react";
import "./Auth.css";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { api } from "../api";
import { ErrorBox } from "../App";
export default function Auth({ mode }: { mode: string }) {
  const nav = useNavigate(),
    qc = useQueryClient();
  const [show, setShow] = useState(false),
    [busy, setBusy] = useState(false),
    [error, setError] = useState<unknown>(),
    [message, setMessage] = useState("");
  const [passwordMode, setStaff] = useState(false);
  const methods = useQuery<any>({ queryKey: ["auth-methods"], queryFn: () => api("/auth/methods"), enabled: mode === "login", staleTime: 0 });
  const otpEnabled = methods.data?.memberOtpEnabled === true;
  const staff = !otpEnabled || passwordMode;
  const [identifier, setIdentifier] = useState("");
  const [challenge, setChallenge] = useState<string>();
  const [resendAt, setResendAt] = useState(0);
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
        if (!staff && !challenge) {
          const result = await api("/auth/otp/request", "POST", { identifier });
          setChallenge(result.challengeId); setResendAt(Date.now() + 60000);
          setMessage(result.deliveryConfigured ? result.message : "Email OTP is ready, but Amazon SES delivery has not been configured yet. Contact your administrator to enable delivery.");
          return;
        }
        await api(staff ? "/auth/login" : "/auth/otp/verify", "POST", staff ? data : { challengeId: challenge, code: data.code });
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
    <div className="auth-page impact-auth-page">
      <div className="auth-story">
        <Link className="brand" to="/">
          <TenantBrand/>
        </Link>
        <div className="auth-story-copy">
          <div className="eyebrow">CONNECTED BY PURPOSE</div>
          <h1>
            A place for people
            <br />
            who make
            <br />
            <em>a difference.</em>
          </h1>
          <p>
            Your Impact. Your leadership.
            <br />
            Training. Business. Service. Leadership.
          </p>
          <figure className="impact-auth-founder"><span className="auth-founder-portrait"><img src="/brand/gampa-nageshwer-rao.webp" alt="Gampa Nageshwer Rao, founder of Impact International" width="800" height="800" /></span><figcaption>Gampa Nageshwer Rao<span>Founder, Impact International</span></figcaption></figure>
        </div>
        <small className="auth-story-footer"><UsersRound size={26} aria-hidden="true" />Leadership without borders.</small>
      </div>
      <div className="auth-side">
      <div className="auth-form">
        <Link to="/about" className="back-link">
          About Impact International
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
            ? "Create your Impact International account, then apply to an Impact club."
            : forgot
              ? "Enter your email to request a password reset."
              : complete
                ? "Complete your secure account request."
                : otpEnabled ? "Members sign in with a one-time email code. Enter your registered email or phone number." : "Sign in with your email and password. Email OTP login is temporarily paused."}
        </p>
        <form onSubmit={submit} className="form-stack">
          {signup && (
            <label>
              Full name
              <input name="name" autoComplete="name" required />
            </label>
          )}
          {mode === "login" && !staff && <>
            <label>Email or phone number<input name="identifier" autoComplete="username" required maxLength={200} value={identifier} readOnly={!!challenge} onChange={e => setIdentifier(e.target.value)} placeholder="Email or phone with country code" /></label>
            <small>The code is sent to your registered email. WhatsApp OTP will be available later.</small>
            {challenge && <><label>One-time code<input name="code" inputMode="numeric" autoComplete="one-time-code" pattern="[0-9]{6}" maxLength={6} required placeholder="6-digit code" /></label><small>Codes expire in 10 minutes.</small><button type="button" className="secondary" disabled={busy} onClick={() => { setChallenge(undefined); setMessage(""); setError(null); }}>Change email or phone</button><button type="button" className="secondary" disabled={busy} onClick={async () => {
              if (Date.now() < resendAt) { setError(new Error("Please wait 60 seconds before requesting another code.")); return; }
              setBusy(true); setError(null);
              try { const result = await api("/auth/otp/request", "POST", { identifier }); setChallenge(result.challengeId); setResendAt(Date.now() + 60000); setMessage(result.deliveryConfigured ? result.message : "Amazon SES email delivery is not configured yet."); } catch (e) { setError(e); } finally { setBusy(false); }
            }}>Resend code</button></>}
          </>}
          {!complete && (mode !== "login" || staff) && (
            <label>
              <span className="auth-field-title"><Mail size={20} aria-hidden="true" />Email address</span>
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
          {!forgot && !verify && (mode !== "login" || staff) && (
            <label>
              <span className="auth-field-title"><LockKeyhole size={20} aria-hidden="true" />{complete ? "New password" : "Password"}</span>
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
                  This Impact International test service stores your profile and security
                  activity to provide organizational access. Do not submit
                  sensitive production data. Contact your Impact
                  administrator for account and data requests. Production legal
                  terms must be reviewed before launch.
                </p>
              </details>
            </>
          )}
          {mode === "login" && staff && <div className="auth-links">
            <Link className="forgot-link" to="/forgot-password">
              Forgot password?
            </Link>
            <span aria-hidden="true">|</span>
            <Link className="forgot-link" to="/resend-verification">
              Resend verification email
            </Link>
          </div>}
          {!!(error || methods.error) && <div className="auth-error"><CircleAlert size={27} aria-hidden="true" /><div><strong>Request failed</strong><ErrorBox error={error || methods.error} /></div></div>}
          {message && (
            <div className="success" role="status">
              {message}
            </div>
          )}
          <button className="auth-submit" type="submit" disabled={busy || mode === "login" && methods.isPending}>
            {busy
              ? "Please wait…"
              : signup
                ? "Create account"
                : forgot
                  ? "Send recovery instructions"
                  : complete
                    ? "Complete request"
                    : staff ? "Sign in" : challenge ? "Verify code and sign in" : "Send email code"}
            <ArrowRight size={16} />
          </button>
        </form>
        {mode === "login" && otpEnabled && <button type="button" className="secondary" onClick={() => { setStaff(!staff); setChallenge(undefined); setMessage(""); setError(null); }}>{staff ? "Member sign in with OTP" : "Administrator / staff password sign in"}</button>}
        <p className="auth-bottom">
          {mode === "login" ? (
            <>
              New to Impact International? <Link to="/signup">Create an account</Link>
            </>
          ) : (
            <Link to="/login">Return to sign in</Link>
          )}
        </p>
      </div>
      </div>
    </div>
  );
}
