import { TenantBrand } from "../TenantBrand";
import { useEffect, useState } from "react";
import { Link, useNavigate } from "react-router-dom";
import { Eye, EyeOff, ArrowRight, Mail, LockKeyhole, CircleAlert, ArrowUpRight } from "lucide-react";
import "./Auth.css";
import "./Login.css";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { api } from "../api";
import { ErrorBox } from "../App";
import { googleIdToken, googleSignInError } from "../firebase";
import { LanguagePicker, useLanguage } from "../i18n";
export default function Auth({ mode }: {
    mode: string;
}) {
    const { t } = useLanguage();
    const nav = useNavigate(), qc = useQueryClient();
    const [show, setShow] = useState(false), [busy, setBusy] = useState(false), [error, setError] = useState<unknown>(), [message, setMessage] = useState("");
    const [passwordMode, setStaff] = useState(false);
    useEffect(() => { setStaff(new URLSearchParams(location.search).get('applicant') === '1'); setChallenge(undefined); setMessage(''); setError(null); }, [mode]);
    const methods = useQuery<any>({ queryKey: ["auth-methods"], queryFn: () => api("/auth/methods"), enabled: mode === "login", staleTime: 0 });
    const otpEnabled = methods.data?.memberOtpEnabled === true;
    const staff = !otpEnabled || passwordMode;
    const [identifier, setIdentifier] = useState("");
    const [challenge, setChallenge] = useState<string>();
    const [resendAt, setResendAt] = useState(0);
    function finishSignIn(signedIn: {
        pendingMembership?: boolean;
    }) {
        qc.clear();
        const target = new URLSearchParams(location.search).get("returnTo");
        nav(target?.startsWith("/") && !target.startsWith("//") ? target : signedIn.pendingMembership ? "/app/apply" : "/app");
    }
    async function googleLogin() {
        setBusy(true);
        setError(null);
        setMessage("");
        try {
            const idToken = await googleIdToken();
            finishSignIn(await api("/auth/google", "POST", { idToken }));
        }
        catch (error) {
            setError(googleSignInError(error));
        }
        finally {
            setBusy(false);
        }
    }
    const signup = mode === "signup", forgot = mode === "forgot-password" || mode === "resend-verification", complete = mode === "complete", verify = complete && new URLSearchParams(location.search).get("kind") === "verify";
    const [portalOnly, setPortalOnly] = useState(new URLSearchParams(location.search).get('portal') === '1');
    const [nationalId, setNationalId] = useState('');
    const [regionId, setRegionId] = useState(''), [clubId, setClubId] = useState('');
    const [clubSearch, setClubSearch] = useState(''), [querySearch, setQuerySearch] = useState(''), [clubPage, setClubPage] = useState(1);
    useEffect(() => { const timer = setTimeout(() => { setQuerySearch(clubSearch); setClubPage(1); }, 300); return () => clearTimeout(timer); }, [clubSearch]);
    const catalog = useQuery<any>({ queryKey: ['public-membership-options', nationalId, regionId, querySearch, clubPage],
        queryFn: () => api('/public/membership-options?page=' + clubPage + (nationalId ? '&nationalId=' + nationalId : '') + (regionId ? '&regionId=' + regionId : '') + (querySearch ? '&search=' + encodeURIComponent(querySearch) : '')),
        enabled: signup && !portalOnly, staleTime: 30000 });
    async function submit(e: React.FormEvent<HTMLFormElement>) {
        e.preventDefault();
        setBusy(true);
        setError(null);
        const form = e.currentTarget;
        const data = Object.fromEntries(new FormData(form));
        try {
            if (mode === "login") {
                if (!staff && !challenge) {
                    const result = await api("/auth/otp/request", "POST", { identifier });
                    setChallenge(result.challengeId);
                    setResendAt(Date.now() + 60000);
                    setMessage(result.deliveryConfigured ? result.message : "Email OTP is ready, but Amazon SES delivery has not been configured yet. Contact your administrator to enable delivery.");
                    return;
                }
                const signedIn = await api(staff ? "/auth/login" : "/auth/otp/verify", "POST", staff ? data : { challengeId: challenge, code: data.code });
                finishSignIn(signedIn);
            }
            else {
                const result = await api(signup
                    ? portalOnly ? "/auth/register" : "/public/membership-signup"
                    : forgot
                        ? mode === "resend-verification"
                            ? "/auth/resend-verification"
                            : "/auth/forgot-password"
                        : "/auth/complete", "POST", {
                    ...data,
                    agreed: data.agreed === "on",
                    token: new URLSearchParams(location.search).get("token"),
                    phone: String(data.countryCode || "") + String(data.phone || ""),
                });
                setMessage(result.message);
                if (signup) {
                    form.reset();
                    setNationalId('');
                    setRegionId('');
                    setClubId('');
                }
            }
        }
        catch (e) {
            setError(e);
        }
        finally {
            setBusy(false);
        }
    }
    return (<div className={"auth-page impact-auth-page" + (signup ? " public-signup-page" : "") + " reference-login"}>
      <div className="auth-story">
        <img className="login-scene" src="/brand/impact-networking-login.webp" alt="Impact community members connecting at a leadership conference" width="1122" height="1402" fetchPriority="high"/>
        <Link className="brand" to="/">
          <TenantBrand />
        </Link>
        <div className="auth-story-copy">
          <h1>{t("Learn. Connect.")}{" "}<span>{t("Lead.")}</span></h1>
          <p>{t("Training \u00B7 Business \u00B7 Service \u00B7 Leadership")}</p>
          <figure className="impact-auth-founder"><span className="auth-founder-portrait"><img src="/brand/gampa-nageshwer-rao.webp" alt="Gampa Nageshwer Rao, founder of Impact International" width="800" height="800"/></span><figcaption>{t("Gampa Nageshwer Rao")}<span>{t("Founder, Impact International")}</span></figcaption></figure>
        </div>
      </div>
      <div className="auth-side">
      <header className="login-topbar">
        <Link className="brand login-mobile-brand" to="/" aria-label={t("Impact International home")}><TenantBrand /></Link>
        <div className="login-header-actions"><LanguagePicker /><Link className="login-about" to="/about" aria-label={t("About Impact International")}><span>{t("About Impact International")}</span><ArrowUpRight size={20} aria-hidden="true"/></Link></div>
      </header>
      <div className="auth-form">
        <div className="eyebrow">{t(signup ? "Join the community" : mode === "forgot-password" ? "Account recovery" : mode === "resend-verification" ? "Email verification" : complete ? "Account security" : "Member portal")}</div>
        <h1>{t(signup ? "Create your account" : forgot ? mode === "resend-verification" ? "Verify your email" : "Forgot password?" : complete ? "Secure your account" : "Welcome back")}</h1>
        <p>{t(signup ? portalOnly ? "Join workshops and certifications. No club membership is required." : "Choose your community and introduce yourself. Your club or region president will review your application." : forgot ? mode === "resend-verification" ? "Enter your registered email and we’ll send a new verification link." : "Enter your registered email and we’ll send a link to reset your password." : complete ? "Complete your secure account request." : otpEnabled ? "Members sign in with a one-time email code. Enter your registered email or phone number." : "Sign in to your Impact International account.")}</p>
        {signup && message ? <section className="signup-pending success" role="status"><h2>{t(portalOnly ? "Verify your account" : "Pending approval")}</h2><p>{t(message)}</p><p>{t(portalOnly ? "After verifying your email, sign in and open My certifications to join the learning portal. No club is required." : "Your signup does not grant active membership or club access.")}</p><Link className="button" to="/login?applicant=1">{t("Sign in after verifying your email")}</Link></section> : <form onSubmit={submit} className="form-stack">
          {mode === "login" && methods.data?.googleEnabled && <>
            <button className="auth-google" type="button" disabled={busy} onClick={googleLogin}>
              <svg aria-hidden="true" viewBox="0 0 48 48" width="20" height="20"><path fill="#4285F4" d="M43.6 24.5c0-1.4-.1-2.8-.4-4.2H24v8h11a9.4 9.4 0 0 1-4.1 6.2v5.2h6.7c3.9-3.6 6-8.8 6-15.2Z"/><path fill="#34A853" d="M24 44c5.5 0 10.1-1.8 13.5-4.9l-6.7-5.2c-1.8 1.2-4.1 1.9-6.8 1.9-5.3 0-9.9-3.6-11.5-8.5H5.6v5.4A20.4 20.4 0 0 0 24 44Z"/><path fill="#FBBC05" d="M12.5 27.3a12.2 12.2 0 0 1 0-7.7v-5.4H5.6a20.3 20.3 0 0 0 0 18.5l6.9-5.4Z"/><path fill="#EA4335" d="M24 11.1c3 0 5.6 1 7.7 3l5.8-5.8A19.5 19.5 0 0 0 24 3a20.4 20.4 0 0 0-18.4 11.2l6.9 5.4c1.6-4.9 6.2-8.5 11.5-8.5Z"/></svg>{t("Continue with Google")}</button>
            <div className="auth-divider"><span>{t("or sign in with email")}</span></div>
          </>}
          {signup && <label>{t("How would you like to join?")}<select aria-label={t("How would you like to join?")} value={portalOnly ? "portal" : "club"} onChange={e => setPortalOnly(e.target.value === "portal")}><option value="club">{t("Apply for club membership")}</option><option value="portal">{t("Learning & certifications \u2014 no club required")}</option></select></label>}
          {signup && !portalOnly && <fieldset className="signup-community"><legend>{t("Choose your community")}</legend>
            <p>{catalog.data ? `${catalog.data.nationals.length} national units · ${catalog.data.availableRegions} available regions · ${catalog.data.availableClubs} active clubs` : 'Loading available regions and clubs…'}</p>
            <ErrorBox error={catalog.error}/>
            <label>{t("National")}<select name="nationalId" aria-label={t("National")} required value={nationalId} disabled={catalog.isPending || busy} onChange={e => { setNationalId(e.target.value); setRegionId(''); setClubId(''); setClubSearch(''); setQuerySearch(''); setClubPage(1); }}><option value="">{t("Choose an available national")}</option>{catalog.data?.nationals.map((d: any) => <option value={d.id} key={d.id}>{d.name} · {d.regionCount}{t("regions")}</option>)}</select></label>
            <label>{t("Region")}<select name="regionId" aria-label={t("Region")} required value={regionId} disabled={!nationalId || catalog.isPending || busy} onChange={e => { setRegionId(e.target.value); setClubId(''); setClubSearch(''); setQuerySearch(''); setClubPage(1); }}><option value="">{t("Choose an available region")}</option>{catalog.data?.regions.map((z: any) => <option value={z.id} key={z.id}>{z.name} · {z.clubCount}{t("clubs")}</option>)}</select></label>
            {regionId && <><label>{t("Find a club")}<input type="search" value={clubSearch} placeholder={t("Search club name, code or city")} onChange={e => { setClubSearch(e.target.value); setClubId(''); }}/></label>
            <label>{t("Club")}<select name="clubId" aria-label={t("Club")} required value={clubId} disabled={catalog.isFetching || busy} onChange={e => setClubId(e.target.value)}><option value="">{t("Choose a club in your region")}</option>{catalog.data?.clubs.map((c: any) => <option value={c.id} key={c.id}>{c.name}{c.city ? ' · ' + c.city : ''} · {c.code}</option>)}</select></label>
            {!catalog.isFetching && catalog.data?.total === 0 && <p>{t("No available clubs match your search. Try another name or region.")}</p>}
            {catalog.data?.total > 50 && <div className="signup-club-pages"><button type="button" className="secondary" disabled={clubPage === 1 || catalog.isFetching} onClick={() => { setClubPage(clubPage - 1); setClubId(''); }}>{t("Previous clubs")}</button><span>{t("Page")}{clubPage}{t("of")}{Math.ceil(catalog.data.total / 50)}</span><button type="button" className="secondary" disabled={clubPage * 50 >= catalog.data.total || catalog.isFetching} onClick={() => { setClubPage(clubPage + 1); setClubId(''); }}>{t("More clubs")}</button></div>}
            {clubId && catalog.data?.clubs.filter((c: any) => c.id === clubId).map((c: any) => <div className="signup-club-choice" key={c.id}><strong>{c.name}</strong><span>{[c.city, c.country].filter(Boolean).join(', ')}</span><small>{[c.national, c.region].filter(Boolean).join(' / ')}</small><small>{catalog.data.category}{t("membership \u00B7 Pending until approved")}</small></div>)}
            </>}
            {catalog.data && !catalog.data.signupAvailable && <p>{t("Signup is currently unavailable. Please contact the association.")}</p>}
          </fieldset>}
          {signup && (<label>{t("Full name")}<input name="name" autoComplete="name" minLength={2} maxLength={200} required/>
            </label>)}
          {mode === "login" && !staff && <>
            <label>{t("Email or phone number")}<input name="identifier" autoComplete="username" required maxLength={200} value={identifier} readOnly={!!challenge} onChange={e => setIdentifier(e.target.value)} placeholder={t("Email or phone with country code")}/></label>
            <small>{t("The code is sent to your registered email. WhatsApp OTP will be available later.")}</small>
            {challenge && <><label>{t("One-time code")}<input name="code" inputMode="numeric" autoComplete="one-time-code" pattern="[0-9]{6}" maxLength={6} required placeholder={t("6-digit code")}/></label><small>{t("Codes expire in 10 minutes.")}</small><button type="button" className="secondary" disabled={busy} onClick={() => { setChallenge(undefined); setMessage(""); setError(null); }}>{t("Change email or phone")}</button><button type="button" className="secondary" disabled={busy} onClick={async () => {
                        if (Date.now() < resendAt) {
                            setError(new Error("Please wait 60 seconds before requesting another code."));
                            return;
                        }
                        setBusy(true);
                        setError(null);
                        try {
                            const result = await api("/auth/otp/request", "POST", { identifier });
                            setChallenge(result.challengeId);
                            setResendAt(Date.now() + 60000);
                            setMessage(result.deliveryConfigured ? result.message : "Amazon SES email delivery is not configured yet.");
                        }
                        catch (e) {
                            setError(e);
                        }
                        finally {
                            setBusy(false);
                        }
                    }}>{t("Resend code")}</button></>}
          </>}
          {!complete && (mode !== "login" || staff) && (<label>
              <span className="auth-field-title"><Mail size={20} aria-hidden="true"/>{t("Email address")}</span>
              <input name="email" type="email" autoComplete="email" placeholder={t("you@example.com")} required/>
            </label>)}
          {signup && (<div className="form-grid">
              <label>{t("Country code")}<input name="countryCode" defaultValue="+91" pattern="\+[0-9]{1,4}" required/>
              </label>
              <label>{t("Mobile number")}<input name="phone" type="tel" autoComplete="tel-national" inputMode="tel" pattern="[0-9 ()-]{6,20}" maxLength={20} required/>
              </label>
            </div>)}
          {!forgot && !verify && (mode !== "login" || staff) && (<label>
              <span className="auth-field-title"><LockKeyhole size={20} aria-hidden="true"/><span>{t(complete ? "New password" : "Password")}</span>{mode === "login" && <Link className="login-forgot" to="/forgot-password">{t("Forgot password?")}</Link>}</span>
              <div className="password-field">
                <input name="password" aria-label={t(complete ? "New password" : "Password")} type={show ? "text" : "password"} minLength={signup || complete ? 12 : 1} autoComplete={signup || complete ? "new-password" : "current-password"} required/>
                <button type="button" className="icon-button" aria-label={t(show ? "Hide password" : "Show password")} onClick={() => setShow(!show)}>
                  {show ? <EyeOff size={18}/> : <Eye size={18}/>}
                </button>
              </div>
              {signup && <small>{t("At least 12 characters.")}</small>}
            </label>)}
          {signup && (<>
              <label>{t("Confirm password")}<input name="confirmation" type="password" minLength={12} required/>
              </label>
              <label className="checkbox">
                <input name="agreed" type="checkbox" required/>{t("I agree to the development terms and privacy notice below.")}</label>
              <details>
                <summary>{t("Terms & privacy notice")}</summary>
                <p>{t("This Impact International test service stores your profile and security activity to provide organizational access. Do not submit sensitive production data. Contact your Impact administrator for account and data requests. Production legal terms must be reviewed before launch.")}</p>
              </details>
            </>)}
          {!!(error || methods.error) && <div className="auth-error"><CircleAlert size={27} aria-hidden="true"/><div><strong>{t("Request failed")}</strong><ErrorBox error={error || methods.error}/></div></div>}
          {message && (<div className="success" role="status">
              {message}
            </div>)}
          <button className="auth-submit" type="submit" disabled={busy || mode === "login" && methods.isPending || signup && !portalOnly && (!nationalId || !regionId || !clubId || !catalog.data?.signupAvailable || catalog.isFetching)}>
            {t(busy ? "Please wait…" : signup ? portalOnly ? "Create learning portal account" : "Sign up — pending approval" : forgot ? mode === "resend-verification" ? "Resend verification email" : "Send reset link" : complete ? "Complete request" : staff ? "Sign in" : challenge ? "Verify code and sign in" : "Send email code")}
            <ArrowRight size={16}/>
          </button>
        </form>}
        {mode === "login" && otpEnabled && <button type="button" className="secondary" onClick={() => { setStaff(!staff); setChallenge(undefined); setMessage(""); setError(null); }}>{t(staff ? "Member sign in with OTP" : "Use email and password")}</button>}
        <p className="auth-bottom">
          {mode === "login" ? (<>{t("New to Impact International?")}<Link to="/signup">{t("Create an account")}</Link>
            </>) : (<>{t(signup ? "Already part of Impact International?" : mode === "forgot-password" ? "Remember your password?" : "Ready to sign in?")} <Link to="/login">{t("Sign in")}</Link></>)}
        </p>
        {mode === "login" && <Link className="login-resend" to="/resend-verification">{t("Resend verification email")}</Link>}
      </div>
      </div>
    </div>);
}
