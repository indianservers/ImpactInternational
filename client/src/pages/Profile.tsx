import { IciCareerProfile } from "./IciCareerProfile";
import { MemberCertifications } from "./Certifications";
import SectionTabs from "../SectionTabs";
import { useState } from "react";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { api } from "../api";
import { PageHeader, ErrorBox, useWorkspace } from "../App";
import { useLanguage } from "../i18n";
export default function Profile() {
    const { t } = useLanguage();
    const [section, setSection] = useState("Profile");
    const { me, w } = useWorkspace(), qc = useQueryClient();
    const [error, setError] = useState<unknown>(), [message, setMessage] = useState("");
    const memberProfile = useQuery({ queryKey: ["my-membership"], queryFn: () => api("/membership/me") });
    const q = useQuery({
        queryKey: ["sessions"],
        queryFn: () => api("/auth/sessions"),
    });
    async function submit(e: React.FormEvent<HTMLFormElement>, path: string, method: string) {
        e.preventDefault();
        setError(null);
        try {
            await api(path, method, Object.fromEntries(new FormData(e.currentTarget)));
            setMessage("Your account has been updated.");
            qc.invalidateQueries({ queryKey: ["me"] });
            qc.invalidateQueries({ queryKey: ["sessions"] });
        }
        catch (e) {
            setError(e);
        }
    }
    return (<>
      <PageHeader eyebrow="YOUR IMPACT ACCOUNT" title={t("My Impact profile & security")} description="Manage your Impact International profile, preferences, password, and sessions."/>
      <ErrorBox error={error}/>
      {message && (<div className="success" role="status">
          {message}
        </div>)}
      <SectionTabs id="account" label="Account settings" tabs={["Profile", "Password", "Sessions", "Certifications", ...(w?.permissions.includes("ici.view") ? ["Career & bank"] : [])]} value={section} onChange={setSection}/>
      <div className="account-panels">
        <section className="panel" role="tabpanel" id="account-panel-0" aria-labelledby="account-tab-0" hidden={section !== "Profile"}>
          <h2>{t("Your profile")}</h2>
          <form className="form-stack" onSubmit={(e) => submit(e, "/users/profile", "PUT")}>
            <label>{t("Full name")}<input name="name" defaultValue={me.name} required minLength={2}/>
            </label>
            <label>{t("Email")}<input value={me.email} readOnly/>
            </label>
            <label>{t("Phone")}<input name="phone" defaultValue={me.phone}/>
            </label>
            <label>{t("Content language preference")}<select name="locale" defaultValue={me.locale || "en"}>
                <option value="en">{t("English")}</option>
                <option value="te">తెలుగు</option>
                <option value="hi">{t("Hindi")}</option>
              </select>
            </label>
            <label>{t("Personal time zone")}<input name="timeZone" required defaultValue={me.timeZone || "UTC"}/>
            </label>
            <label>{t("Theme")}<select name="theme" defaultValue={me.theme || "light"}>
                <option value="light">{t("Light")}</option>
                <option value="dark">{t("Dark")}</option>
              </select>
            </label>
            <button>{t("Save profile")}</button>
          </form>
        </section>
        <section className="panel" role="tabpanel" id="account-panel-1" aria-labelledby="account-tab-1" hidden={section !== "Password"}>
          <h2>{t("Change password")}</h2>
          <form className="form-stack" onSubmit={(e) => submit(e, "/auth/change-password", "POST")}>
            <label>{t("Current password")}<input type="password" name="current" required autoComplete="current-password"/>
            </label>
            <label>{t("New password")}<input type="password" name="password" minLength={12} required autoComplete="new-password"/>
            </label>
            <p>{t("Use at least 12 characters. Other sessions will be revoked.")}</p>
            <button>{t("Update password")}</button>
          </form>
        </section>
      </div>
      <section className="panel spaced" role="tabpanel" id="account-panel-2" aria-labelledby="account-tab-2" hidden={section !== "Sessions"}>
        <h2>{t("Active sessions")}</h2>
        <ErrorBox error={q.error}/>
        {q.data?.map((s: any) => (<div className="context-row" key={s.id}>
            <span>
              {t(s.current ? "This session" : "Browser session")}
              <small>{t("Created")}{new Date(s.createdAt).toLocaleString()}{t("\u00B7 Expires")}{" "}
                {new Date(s.expiresAt).toLocaleString()}
              </small>
            </span>
            <button className="secondary" onClick={async () => {
                try {
                    await api("/auth/sessions/" + s.id, "DELETE");
                    if (s.current)
                        location.assign("/login");
                    else
                        qc.invalidateQueries({ queryKey: ["sessions"] });
                }
                catch (e) {
                    setError(e);
                }
            }}>{t("Revoke session")}</button>
          </div>))}
      </section>
      <section role="tabpanel" id="account-panel-3" aria-labelledby="account-tab-3" hidden={section !== "Certifications"}>
        {section === "Certifications" && (memberProfile.data?.profile ? <MemberCertifications /> : <p>{t("Certifications are available once a member profile is linked to your account.")}</p>)}
      </section>
      {w?.permissions.includes("ici.view") && <section role="tabpanel" id="account-panel-4" aria-labelledby="account-tab-4" hidden={section !== "Career & bank"}>{section === "Career & bank" && <IciCareerProfile />}</section>}
    </>);
}
