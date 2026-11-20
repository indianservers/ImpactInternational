import { useEffect, useRef, useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { ArrowLeft, ArrowRight, Check, CheckCircle2, UserRound, Network, MapPin, FileText, X, Upload, ShieldCheck, Users, Save, Trash2, } from "lucide-react";
import { api, apiForm } from "../api";
import { useWorkspace } from "../App";
import { ClubCategoryFields } from "../MemberComponents";
import { useDialog } from "../useDialog";
import { CertificationRegistration } from "./Certifications";
import { memberDraft } from "../memberDraft";
import "./AddMemberWizard.css";
import { useLanguage } from "../i18n";
import { businessCategories } from "../BusinessProfile";
import { MemberChoice, MemberTags, memberChoices, memberTagSuggestions } from "./MemberEntryFields";

const businessCategoryChoices = Object.keys(businessCategories).filter(category => category !== "Other");
const tagLabels = {
    languages: "Languages",
    skills: "Skills",
    expertise: "Expertise",
    interests: "Areas of interest",
    businessTags: "Business tags",
};
type TagField = keyof typeof tagLabels;
function tagValues(form: any, field: TagField): string[] {
    return form[field] ?? (form[field + "Text"] || "").split(",").map((value: string) => value.trim()).filter(Boolean);
}
const steps = [
    { name: "Basic Details", icon: UserRound },
    { name: "Community & Membership", icon: Network },
    { name: "Address & Contact", icon: MapPin },
    { name: "Additional Information", icon: FileText },
];
const blankAddress = () => ({
    country: "India",
    state: "",
    city: "",
    postalCode: "",
    address: "",
    type: "Residential",
    isPrimary: true,
});
const hasAddress = (a: any) => !!(a.address ||
    a.city ||
    a.state ||
    a.postalCode ||
    (a.country.trim() && a.country.trim() !== "India"));
const initial = () => ({
    legalName: "",
    displayName: "",
    birthDate: "",
    gender: "",
    maritalStatus: "",
    spouseName: "",
    anniversary: "",
    contacts: [
        { kind: "Phone", label: "Primary mobile", value: "" },
        { kind: "Phone", label: "Alternative mobile", value: "" },
        { kind: "Email", label: "Primary email", value: "" },
        { kind: "Email", label: "Alternative email", value: "" },
        { kind: "Emergency", label: "Emergency contact", value: "" },
    ],
    addresses: [blankAddress()],
    socialLinks: {},
    company: "",
    designation: "",
    industry: "",
    profession: "",
    businessCategory: "",
    businessSubcategory: "",
    workLocation: "",
    website: "",
    networking: "",
    educationLevel: "",
    qualification: "",
    institution: "",
    employmentStatus: "",
    additionalInformation: "",
    certifications: [],
});
export default function AddMemberWizard({ onClose, onDone, }: {
    onClose: () => void;
    onDone: (member: any) => void;
}) {
    const { w } = useWorkspace();
    const { t } = useLanguage();
    const [today] = useState(() => new Date().toISOString().slice(0, 10));
    const [attempted, setAttempted] = useState(false);
    const [form, setForm] = useState<any>(initial), [target, setTarget] = useState<any>({
        clubId: "",
        categoryId: "",
        joinedAt: "",
        status: "Active",
    }), [step, setStep] = useState(0), [completed, setCompleted] = useState<number[]>([]), [review, setReview] = useState(false), [photo, setPhoto] = useState(""), [documents, setDocuments] = useState<File[]>([]), [error, setError] = useState(""), [notice, setNotice] = useState(""), [busy, setBusy] = useState(false), [photoBusy, setPhotoBusy] = useState(false), [confirm, setConfirm] = useState(false), [dirty, setDirty] = useState(false), [saved, setSaved] = useState<any>(null), [matches, setMatches] = useState<any[]>([]), [decision, setDecision] = useState(""), [personId, setPersonId] = useState<string | null>(null), [created, setCreated] = useState<any>(null), [uploaded, setUploaded] = useState<string[]>([]), [requestKey, setRequestKey] = useState(() => crypto.randomUUID());
    const knownBusinessCategory = businessCategoryChoices.includes(form.businessCategory);
    const businessSubcategories = knownBusinessCategory ? businessCategories[form.businessCategory] : [];
    const formRef = useRef<HTMLFormElement>(null), lock = useRef(false), lastPayload = useRef<any>(null), dialogRef = useRef<HTMLElement>(null);
    const me = useQuery({
        queryKey: ["wizard-session"],
        queryFn: () => api("/auth/me"),
    });
    const key = me.data?.user?.id || me.data?.id;
    const draftKey = key && w ? `${key}:${w.tenantId}:${w.id}` : "";
    const options = useQuery({
        queryKey: ["member-catalog", false, w?.tenantId, w?.id],
        queryFn: () => api("/membership/options"),
    });
    useEffect(() => {
        const regular = options.data?.categories?.find((c: any) => c.active !== false && c.name?.trim().toLowerCase() === "regular");
        if (regular)
            setTarget((previous: any) => previous.categoryId ? previous : { ...previous, categoryId: regular.id });
    }, [options.data?.categories]);
    const canDocuments = !!w?.permissions.includes("members.documents");
    useEffect(() => {
        if (draftKey)
            memberDraft(draftKey, "get")
                .then(setSaved)
                .catch(() => setNotice("Device draft storage is unavailable. You can still submit this registration."));
    }, [draftKey]);
    function requestClose() {
        if (lock.current || photoBusy)
            return;
        if (confirm) {
            setConfirm(false);
            return;
        }
        if (dirty && !created)
            setConfirm(true);
        else
            onClose();
    }
    useDialog(true, requestClose);
    useEffect(() => {
        const previous = document.body.style.overflow;
        document.body.style.overflow = "hidden";
        return () => {
            document.body.style.overflow = previous;
        };
    }, []);
    useEffect(() => {
        dialogRef.current
            ?.querySelector<HTMLElement>(".aw-body")
            ?.scrollTo({ top: 0 });
    }, [step, review]);
    function change(name: string, value: any) {
        setForm((p: any) => ({ ...p, [name]: value }));
        setDirty(true);
        setError("");
        setNotice("");
    }
    function sync() {
        const data = new FormData(formRef.current!);
        const next = { ...form };
        for (const [k, v] of data.entries()) {
            if (k.startsWith("field:"))
                next[k.slice(6)] = String(v);
            if (k === "joiningDate")
                setTarget((p: any) => ({ ...p, joinedAt: String(v) }));
        }
        setForm(next);
        return next;
    }
    function input(name: string, label: string, type = "text", required = false, maxLength = 200) {
        return (<label className="aw-field" key={name}>
        {t(label)}
        {required && <span className="aw-required"> *</span>}
        <input name={"field:" + name} type={name === "website" ? "url" : type} pattern={name === "website" ? "https?://.+" : undefined} min={type === "date" ? "1900-01-01" : undefined} max={type === "date" ? today : undefined} value={form[name] || ""} required={required} maxLength={maxLength} onChange={(e) => change(name, e.target.value)} onInput={type === "date"
                ? (e) => change(name, e.currentTarget.value)
                : undefined}/>
      </label>);
    }
    function select(name: string, label: string, values: string[]) {
        return (<label className="aw-field" key={name}>
        {t(label)}
        <select name={"field:" + name} value={form[name] || ""} onChange={(e) => change(name, e.target.value)}>
          <option value="">{t("Select")}</option>
          {values.map((v) => (<option key={v}>{v}</option>))}
        </select>
      </label>);
    }
    function choice(name: keyof typeof memberChoices, label: string) {
        return <MemberChoice label={label} value={form[name] || ""} choices={memberChoices[name]} onChange={value => change(name, value)}/>;
    }
    function tags(field: TagField, suggestions: readonly string[] = memberTagSuggestions[field]) {
        return <MemberTags label={tagLabels[field]} values={tagValues(form, field)} suggestions={suggestions} onChange={values => change(field, values)}/>;
    }
    function validate(index: number) {
        const panel = formRef.current?.querySelector<HTMLElement>(`[data-step="${index}"]`);
        const fields = Array.from(panel?.querySelectorAll<HTMLInputElement>("input,select,textarea") || []);
        const invalid = fields.find((f) => !f.checkValidity());
        if (invalid) {
            invalid.reportValidity();
            invalid.setAttribute("aria-invalid", "true");
            invalid.focus();
            setError("Please check the highlighted field before continuing.");
            return false;
        }
        if (index === 1 && (!target.clubId || !target.categoryId)) {
            setError("Select your authorized national unit, region, club and membership category.");
            return false;
        }
        if (index === 0 && !sync().legalName.trim()) {
            setError("Enter the member’s full legal name.");
            return false;
        }
        setCompleted((p) => Array.from(new Set([...p, index])));
        setError("");
        return true;
    }
    function next() {
        sync();
        if (validate(step))
            setStep((s) => Math.min(3, s + 1));
    }
    function person(f: any) {
        return {
            ...f,
            legalName: f.legalName.trim(),
            displayName: f.displayName.trim() || f.legalName.trim(),
            birthDate: f.birthDate || null,
            anniversary: f.anniversary || null,
            languages: tagValues(f, "languages"),
            skills: tagValues(f, "skills"),
            expertise: tagValues(f, "expertise"),
            interests: tagValues(f, "interests"),
            businessTags: tagValues(f, "businessTags"),
            contacts: f.contacts.filter((c: any) => c.value.trim()),
            addresses: f.addresses
                .filter(hasAddress)
                .map((a: any, _i: number, all: any[]) => ({
                ...a,
                isPrimary: a.isPrimary || (!all.some((x) => x.isPrimary) && _i === 0),
            })),
        };
    }
    async function saveDraft() {
        if (lock.current)
            return;
        lock.current = true;
        setBusy(true);
        try {
            if (!draftKey)
                throw Error("Your session is loading. Please try Save as Draft again.");
            const data = {
                form: sync(),
                target,
                photo,
                documents,
                step,
                completed,
                requestKey,
                created,
                uploaded,
                lastPayload: lastPayload.current,
                savedAt: new Date().toISOString(),
            };
            await memberDraft(draftKey, "put", data);
            setSaved(data);
            setDirty(false);
            setNotice("Draft saved on this device. You can safely close and resume later.");
        }
        catch (e: any) {
            setError(e.message || "Unable to save this draft.");
        }
        finally {
            lock.current = false;
            setBusy(false);
        }
    }
    async function removeDraft() {
        try {
            if (draftKey)
                await memberDraft(draftKey, "delete");
            setSaved(null);
        }
        catch {
            setNotice("Member saved. The device draft could not be removed; discard it before starting another registration.");
        }
    }
    async function prepare() {
        sync();
        for (let i = 0; i < 4; i++) {
            // The restored club ID is validated below while its hierarchy options load.
            const invalid = Array.from(formRef.current?.querySelectorAll<HTMLInputElement>(`[data-step="${i}"] input,[data-step="${i}"] select,[data-step="${i}"] textarea`) || [])
                .find((f) => !f.closest(".membership-location-picker") && !f.checkValidity());
            if (invalid) {
                setStep(i);
                setError("Please complete the required fields and correct any invalid values in this step.");
                invalid.setAttribute("aria-invalid", "true");
                requestAnimationFrame(() => {
                    invalid.focus();
                    invalid.reportValidity();
                });
                return;
            }
        }
        if (!target.clubId || !target.categoryId) {
            setStep(1);
            setError("Choose a club and membership category.");
            return;
        }
        if (!validate(3))
            return;
        for (let i = 0; i < 3; i++) {
            if (!completed.includes(i)) {
                setStep(i);
                setError("Complete this step before reviewing the registration.");
                return;
            }
        }
        if (lock.current)
            return;
        lock.current = true;
        setBusy(true);
        try {
            const found = await api("/members/duplicates", "POST", person(sync()));
            setMatches(found);
            setDecision("");
            setPersonId(null);
            setReview(true);
            setError("");
        }
        catch (e: any) {
            setError(e.message);
        }
        finally {
            lock.current = false;
            setBusy(false);
        }
    }
    async function submit() {
        if (lock.current)
            return;
        if (matches.length && !decision && !personId) {
            setError("Review the possible matches and choose how to proceed.");
            return;
        }
        lock.current = true;
        setBusy(true);
        setError("");
        let result = created;
        try {
            if (!result) {
                const payload = lastPayload.current || {
                    person: person(sync()),
                    ...target,
                    joinedAt: target.joinedAt
                        ? new Date(target.joinedAt + "T00:00:00Z").toISOString()
                        : null,
                    personId,
                    certifications: (form.certifications || []).map((c: any) => ({ ...c, issuedOn: c.issuedOn || null, validUntil: c.validUntil || null })),
                    duplicateDecision: decision,
                    photoBase64: photo && !personId ? photo.split(",")[1] : null,
                    requestKey,
                };
                lastPayload.current = payload;
                setAttempted(true);
                result = await api("/members", "POST", payload);
                setCreated(result);
            }
            const done = [...uploaded];
            for (const file of documents) {
                const id = file.name + ":" + file.size + ":" + file.lastModified;
                if (done.includes(id))
                    continue;
                const data = new FormData();
                data.append("file", file);
                data.append("kind", "Supporting");
                await apiForm(`/members/${result.id}/documents`, data);
                done.push(id);
                setUploaded([...done]);
            }
            await removeDraft();
            onDone(result);
        }
        catch (e: any) {
            if (!result && e.status && e.status < 500) {
                lastPayload.current = null;
                setAttempted(false);
                setRequestKey(crypto.randomUUID());
            }
            setError(result
                ? `Member ${result.number} was created. A document could not be uploaded: ${e.message}. Retry to upload remaining documents; the member will not be created again.`
                : e.message || "Unable to create member. Please try again.");
        }
        finally {
            lock.current = false;
            setBusy(false);
        }
    }
    async function photoChange(file?: File) {
        if (!file)
            return;
        if (!["image/png", "image/jpeg"].includes(file.type) ||
            file.size > 2097152) {
            setError("Choose a PNG or JPEG photo up to 2 MB.");
            return;
        }
        setPhotoBusy(true);
        try {
            const data = await new Promise<string>((resolve, reject) => {
                const reader = new FileReader();
                reader.onload = () => resolve(String(reader.result));
                reader.onerror = () => reject(Error("Unable to read photo."));
                reader.readAsDataURL(file);
            });
            setPhoto(data);
            setDirty(true);
            setError("");
        }
        catch (e: any) {
            setError(e.message);
        }
        finally {
            setPhotoBusy(false);
        }
    }
    const club = options.data?.clubs?.find((c: any) => c.id === target.clubId), category = options.data?.categories?.find((c: any) => c.id === target.categoryId);
    return (<div className="aw-backdrop">
      <section ref={dialogRef} className="aw-dialog" role="dialog" aria-modal="true" aria-labelledby="aw-title">
        <header className="aw-header">
          <div className="aw-brand">
            <img src="/brand/impact-logo.webp" alt="Impact International"/>
            <span>{t("Impact")}<br />
              <strong>{t("International")}</strong>
            </span>
          </div>
          <div>
            <div className="aw-eyebrow">{t("PEOPLE & MEMBERSHIP")}</div>
            <h2 id="aw-title">{t("Add a Community Member")}</h2>
            <p>{t("Create a member profile and connect them to the right community.")}</p>
          </div>
          <button type="button" className="aw-close" aria-label={t("Close registration")} disabled={busy || photoBusy} onClick={requestClose}>
            <X size={21}/>
          </button>
        </header>
        <div className="aw-middle">
          <aside className="aw-aside">
            <div className="aw-emblem">
              <Users size={42}/>
            </div>
            <h3>{t("New Member")}</h3>
            <p>{t("A new connection.")}<br />{t("A greater impact.")}</p>
            <div className="aw-aside-bottom">
              <ShieldCheck size={25}/>
              <p>{t("One community.")}<br />
                <strong>{t("Limitless possibilities.")}</strong>
              </p>
              <span>{t("Training \u00B7 Business")}<br />{t("Service \u00B7 Leadership")}</span>
            </div>
          </aside>
          <div className="aw-main">
            <nav className="aw-steps" aria-label={t("Registration steps")}>
              {steps.map((s, i) => {
            const Icon = s.icon;
            return (<button type="button" key={t(s.name)} aria-current={i === step && !review ? "step" : undefined} className={(i === step && !review ? "active " : "") +
                    (completed.includes(i) ? "complete" : "")} disabled={busy ||
                    !!created ||
                    attempted ||
                    (i > step && !completed.includes(i))} onClick={() => {
                    sync();
                    if (i <= step || validate(step)) {
                        setReview(false);
                        setStep(i);
                    }
                }}>
                    <span className="aw-step-icon">
                      {completed.includes(i) ? (<Check size={18}/>) : (<Icon size={18}/>)}
                    </span>
                    <span>
                      <small>{t("STEP")}{i + 1}</small>
                      {t(s.name)}
                    </span>
                  </button>);
        })}
            </nav>
            <div className="aw-progress" role="progressbar" aria-label={t("Registration progress")} aria-valuemin={0} aria-valuemax={4} aria-valuenow={review ? 4 : completed.length}>
              <span style={{ width: `${(review ? 4 : completed.length) * 25}%` }}/>
            </div>
            <div className="aw-body" tabIndex={-1}>
              {saved && !dirty && !created && !notice && (<div className="aw-draft-banner">
                  <div>
                    <Save size={18}/>
                    <span>{t("Saved draft \u00B7")}{new Date(saved.savedAt).toLocaleString()}
                    </span>
                  </div>
                  <button type="button" onClick={() => {
                setForm(saved.form);
                setTarget(saved.target);
                setPhoto(saved.photo);
                setDocuments(saved.documents || []);
                setStep(saved.step);
                setCompleted(saved.completed);
                setRequestKey(saved.requestKey);
                setCreated(saved.created);
                setUploaded(saved.uploaded || []);
                lastPayload.current = saved.lastPayload;
                setAttempted(!!saved.lastPayload);
                setReview(!!saved.created || !!saved.lastPayload);
                setDirty(true);
                setNotice("Draft restored. Continue where you left off.");
            }}>{t("Resume draft")}</button>
                  <button type="button" className="aw-link" onClick={removeDraft}>{t("Discard saved draft")}</button>
                </div>)}
              {notice && (<div className="aw-notice" role="status">
                  <CheckCircle2 size={18}/>
                  {notice}
                </div>)}
              {error && (<div className="aw-error" role="alert">
                  {error}
                </div>)}
              <form ref={formRef} noValidate onSubmit={(e) => e.preventDefault()} onChange={(e) => {
            setDirty(true);
            (e.target as HTMLElement).removeAttribute("aria-invalid");
            setNotice("");
        }}>
                <fieldset disabled={busy || !!created || attempted} className="aw-fieldset">
                  <section className="aw-card" data-step="0" hidden={step !== 0 || review}>
                    <h3>
                      <UserRound size={20}/>{t("Member Information")}<span>01 / 04</span>
                    </h3>
                    <p className="aw-section-note">{t("Start with their name. Other personal details can be added later.")}</p>
                    <div className="aw-grid">
                      <label className="aw-field">{t("Member ID")}<input value="Auto-generated when saved" readOnly/>
                      </label>
                      <div className="aw-photo">
                        <div>
                          {photo ? (<img src={photo} alt="Member photo preview"/>) : (<UserRound size={28}/>)}
                        </div>
                        <label className="aw-photo-upload">
                          <Upload size={18}/>
                          {t(photoBusy
            ? "Reading photo…"
            : photo
                ? "Change photo"
                : "Upload photo")}
                          <input type="file" accept="image/png,image/jpeg" aria-label={t("Member photo")} onChange={(e) => {
            void photoChange(e.target.files?.[0]);
            e.target.value = "";
        }}/>
                        </label>
                        <small>{t("PNG or JPEG, up to 2 MB")}<br />{t("Recommended: 400 \u00D7 400 px")}</small>
                        {photo && (<button type="button" className="aw-link" onClick={() => {
                setPhoto("");
                setDirty(true);
            }}>{t("Remove photo")}</button>)}
                      </div>
                      {input("legalName", "Full legal name", "text", true)}
                      {input("displayName", "Preferred display name")}
                      {input("birthDate", "Date of birth", "date")}
                      {select("gender", "Gender", [
            "Female",
            "Male",
            "Other",
            "Prefer not to say",
        ])}
                      {select("maritalStatus", "Marital status", [
            "Single",
            "Married",
            "Divorced",
            "Widowed",
            "Separated",
            "Prefer not to say",
        ])}
                      {input("spouseName", "Spouse name")}
                      {input("anniversary", "Marriage anniversary", "date")}
                      {tags("languages")}
                    </div>
                  </section>
                  <section className="aw-card" data-step="1" hidden={step !== 1 || review}>
                    <h3>
                      <Network size={20}/>{t("Community & Membership")}<span>02 / 04</span>
                    </h3>
                    <p className="aw-section-note">{t("Choose each level in order. Only your authorized community is available.")}</p>
                    <ClubCategoryFields {...target} onChange={(v: any) => {
            setTarget((p: any) => ({ ...p, ...v }));
            setDirty(true);
            setError("");
        }}/>
                    <div className="aw-grid">
                      <label className="aw-field">{t("Membership type")}<input value={club?.clubType || "Defined by the selected club"} readOnly/>
                      </label>
                      <label className="aw-field">{t("Joining date")}<input type="date" name="joiningDate" min="1900-01-01" max={today} onInput={(e) => {
            const value = e.currentTarget.value;
            setTarget((p: any) => ({ ...p, joinedAt: value }));
        }} value={target.joinedAt} onChange={(e) => setTarget((p: any) => ({
            ...p,
            joinedAt: e.target.value,
        }))}/>
                      </label>
                      <label className="aw-field">{t("Membership status")}<select value={target.status} onChange={(e) => setTarget((p: any) => ({
            ...p,
            status: e.target.value,
        }))}>
                          {["Active", "Inactive", "Suspended"].map((s) => (<option key={s}>{s}</option>))}
                        </select>
                      </label>
                      <label className="aw-field">{t("Benevolent Fund")}
                        <select value={target.benevolentFund == null ? "" : String(target.benevolentFund)} onChange={(e) => {
                            const value = e.target.value;
                            setTarget((p: any) => ({ ...p, benevolentFund: value === "" ? null : value === "true" }));
                            setDirty(true);
                        }}>
                          <option value="">{t("Select Yes or No")}</option>
                          <option value="true">{t("Yes")}</option>
                          <option value="false">{t("No")}</option>
                        </select>
                      </label>
                      <label className="aw-field">{t("Current office")}<input readOnly value="No office assigned"/>
                      </label>
                    </div>
                    <p className="aw-hint">
                      <ShieldCheck size={16}/>{t("Officer appointments are managed through Leadership & Appointments after registration.")}</p>
                  </section>
                  <section className="aw-card" data-step="2" hidden={step !== 2 || review}>
                    <h3>
                      <MapPin size={20}/>{t("Address & Contact")}<span>03 / 04</span>
                    </h3>
                    <p className="aw-section-note">{t("Help the community stay connected. Contact details are optional.")}</p>
                    <div className="aw-grid">
                      {form.contacts.map((c: any, i: number) => c.kind !== "Emergency" && (<label className="aw-field" key={i}>
                              {c.label}
                              <input type={c.kind === "Email" ? "email" : "tel"} maxLength={c.kind === "Phone" ? 30 : 200} pattern={c.kind === "Phone"
                ? "[+0-9 ()\\-]{5,30}"
                : undefined} value={c.value} onChange={(e) => change("contacts", form.contacts.map((x: any, n: number) => n === i
                ? { ...x, value: e.target.value }
                : x))}/>
                            </label>))}
                    </div>
                    <div className="aw-subhead">
                      <h4>{t("Addresses")}</h4>
                      <span>{t("Up to 5 addresses \u00B7 choose one primary")}</span>
                    </div>
                    {form.addresses.map((a: any, i: number) => (<div className="aw-address" key={i}>
                        <div className="aw-address-top">
                          <strong>{t("Address")}{i + 1}</strong>
                          <label>
                            <input type="radio" name="primaryAddress" checked={a.isPrimary} onChange={() => change("addresses", form.addresses.map((x: any, n: number) => ({
                ...x,
                isPrimary: n === i,
            })))}/>{t("Primary address")}</label>
                          {form.addresses.length > 1 && (<button type="button" className="aw-link" aria-label={`Remove address ${i + 1}`} onClick={() => {
                    const list = form.addresses.filter((_: any, n: number) => n !== i);
                    if (!list.some((x: any) => x.isPrimary))
                        list[0].isPrimary = true;
                    change("addresses", list);
                }}>
                              <Trash2 size={16}/>
                            </button>)}
                        </div>
                        <div className="aw-grid">
                          {[
                "type",
                "country",
                "state",
                "city",
                "postalCode",
                "address",
            ].map((k) => {
                const setAddressValue = (value: string) => change("addresses", form.addresses.map((x: any, n: number) => n === i ? { ...x, [k]: value } : x));
                if (k === "country")
                    return <MemberChoice key={k} label="Country" value={a.country} choices={memberChoices.country} onChange={setAddressValue}/>;
                if (k === "state" && a.country.trim().toLowerCase() === "india")
                    return <MemberChoice key={k} label="State / Province" value={a.state} choices={memberChoices.indianStates} onChange={setAddressValue}/>;
                if (k === "city" && a.country.trim().toLowerCase() === "india")
                    return <MemberChoice key={k} label="City" value={a.city} choices={memberChoices.indianCities} onChange={setAddressValue}/>;
                return (<label className={"aw-field " + (k === "address" ? "wide" : "")} key={k}>
                              {({
                    type: "Address type",
                    country: "Country",
                    state: "State / Province",
                    city: "City",
                    postalCode: "PIN / Postal code",
                    address: "Postal address",
                } as any)[k]}
                              {k === "type" ? (<select value={a[k]} onChange={e => setAddressValue(e.target.value)}>
                                  {["Residential", "Office", "Other"].map((v) => (<option key={v}>{v}</option>))}
                                </select>) : (<input maxLength={k === "postalCode"
                        ? 20
                        : k === "address"
                            ? 1000
                            : 200} pattern={k === "postalCode" &&
                        a.country.trim().toLowerCase() === "india"
                        ? "[1-9][0-9]{5}"
                        : undefined} value={a[k]} onChange={e => setAddressValue(e.target.value)}/>)}
                            </label>);
            })}
                        </div>
                      </div>))}
                    {form.addresses.length < 5 && (<button type="button" className="aw-secondary" onClick={() => change("addresses", [
                ...form.addresses,
                { ...blankAddress(), isPrimary: false },
            ])}>{t("+ Add another address")}</button>)}
                  </section>
                  <section className="aw-card" data-step="3" hidden={step !== 3 || review}>
                    <h3>
                      <FileText size={20}/>{t("Additional Information")}<span>04 / 04</span>
                    </h3>
                    <CertificationRegistration value={form.certifications || []} onChange={(certifications: any[]) => change("certifications", certifications)}/>
                    <p className="aw-section-note">{t("Complete their story. Everything in this section is optional.")}</p>
                    <div className="aw-grid">
                      {choice("profession", "Profession")}
                      {input("company", "Organization / Employer")}
                      {choice("designation", "Designation")}
                      {choice("industry", "Industry")}
                      <MemberChoice label="Business category" choices={businessCategoryChoices} value={form.businessCategory || ""} onChange={value => {
                          change("businessCategory", value);
                          change("businessSubcategory", "");
                      }}/>
                      {form.businessCategory && <MemberChoice key={knownBusinessCategory ? form.businessCategory : "custom-category"}
                          label="Business subcategory" choices={businessSubcategories}
                          value={form.businessSubcategory || ""} onChange={value => change("businessSubcategory", value)}/>}
                      {tags("businessTags", [...new Set([...businessSubcategories, ...memberTagSuggestions.businessTags])])}
                      {choice("employmentStatus", "Employment status")}
                      {input("workLocation", "Work location")}
                      {input("website", "Website")}
                      {choice("educationLevel", "Highest education level")}
                      {choice("qualification", "Educational qualifications")}
                      {input("institution", "Institution")}
                      {tags("interests")}
                      {tags("skills")}
                      {tags("expertise")}
                      {choice("networking", "Networking preferences")}
                      {form.contacts.map((c: any, i: number) => c.kind === "Emergency" && (<label className="aw-field" key={i}>{t("Emergency contact (name & phone)")}<input value={c.value} maxLength={200} onChange={(e) => change("contacts", form.contacts.map((x: any, n: number) => n === i
                ? { ...x, value: e.target.value }
                : x))}/>
                            </label>))}
                    </div>
                    <div className="aw-subhead">
                      <h4>{t("Social profiles")}</h4>
                      <span>{t("Optional handles or HTTPS profile links")}</span>
                    </div>
                    <div className="aw-grid">
                      {[
            "LinkedIn",
            "Facebook",
            "Instagram",
            "X",
            "YouTube",
            "Threads",
            "Telegram",
        ].map((p) => (<label className="aw-field" key={p}>
                          {p}
                          <input maxLength={500} value={form.socialLinks[p] || ""} placeholder={t("@yourname or HTTPS profile link")} onChange={(e) => change("socialLinks", {
                ...form.socialLinks,
                [p]: e.target.value,
            })}/>
                        </label>))}
                    </div>
                    <label className="aw-field aw-notes">{t("Notes & additional information")}<textarea name="field:additionalInformation" rows={3} maxLength={2000} value={form.additionalInformation} onChange={(e) => change("additionalInformation", e.target.value)}/>
                    </label>
                    <div className="aw-subhead">
                      <h4>{t("Additional documents")}</h4>
                      <span>{t("PDF, PNG or JPEG \u00B7 up to 5 MB each")}</span>
                    </div>
                    {canDocuments ? (<>
                        <label className="aw-document-upload">
                          <Upload size={19}/>
                          <span>{t("Choose supporting documents")}<small>{t("Up to 5 files. Uploaded securely after registration.")}</small>
                          </span>
                          <input type="file" multiple accept="application/pdf,image/png,image/jpeg" aria-label={t("Additional documents")} onChange={(e) => {
                const files = Array.from(e.target.files || []);
                if (documents.length + files.length > 5 ||
                    files.some((f) => f.size > 5242880 ||
                        ![
                            "application/pdf",
                            "image/png",
                            "image/jpeg",
                        ].includes(f.type))) {
                    setError("Choose up to 5 PDF, PNG or JPEG documents, each up to 5 MB.");
                }
                else {
                    setDocuments((p) => [...p, ...files]);
                    setDirty(true);
                }
                e.target.value = "";
            }}/>
                        </label>
                        {documents.map((f, i) => (<div className="aw-file" key={i}>
                            <FileText size={16}/>
                            {f.name}
                            <button type="button" className="aw-link" aria-label={`Remove ${f.name}`} onClick={() => {
                    setDocuments((p) => p.filter((_, n) => n !== i));
                    setDirty(true);
                }}>
                              <X size={16}/>
                            </button>
                          </div>))}
                      </>) : (<p className="aw-hint">{t("Document uploads require member document permissions.")}</p>)}
                  </section>
                </fieldset>
              </form>
              {review && (<section className="aw-card aw-review">
                  <h3>
                    <CheckCircle2 size={21}/>{t("Review Registration")}<span>{t("READY TO CONNECT")}</span>
                  </h3>
                  <p className="aw-section-note">{t("Check the details before adding this member to Impact International.")}</p>
                  <div className="aw-review-person">
                    {photo ? (<img src={photo} alt="Member photo review"/>) : (<UserRound size={34}/>)}
                    <div>
                      <h4>{form.legalName}</h4>
                      <p>{form.displayName || "Community member"}</p>
                      <small>{t("Member ID is generated when saved")}</small>
                    </div>
                  </div>
                  <dl>
                    <div>
                      <dt>{t("Club")}</dt>
                      <dd>{club?.name || target.clubId}</dd>
                    </div>
                    <div>
                      <dt>{t("Membership")}</dt>
                      <dd>
                        {category?.name} · {target.status}
                        <br />
                        {t("Benevolent Fund")}: {target.benevolentFund == null ? t("Not recorded") : t(target.benevolentFund ? "Yes" : "No")}
                      </dd>
                    </div>
                    <div>
                      <dt>{t("Joining date")}</dt>
                      <dd>{target.joinedAt || "Today"}</dd>
                    </div>
                    <div>
                      <dt>{t("Contact")}</dt>
                      <dd>
                        {form.contacts
                .filter((c: any) => c.kind !== "Emergency" && c.value)
                .map((c: any) => c.value)
                .join(" · ") || "Not provided"}
                      </dd>
                    </div>
                    <div>
                      <dt>{t("Addresses")}</dt>
                      <dd>
                        {form.addresses
                .filter(hasAddress)
                .map((a: any, i: number) => (<p key={i}>
                              {a.type}
                              {t(a.isPrimary ? " · Primary" : "")}:{" "}
                              {[
                    a.address,
                    a.city,
                    a.state,
                    a.postalCode,
                    a.country,
                ]
                    .filter(Boolean)
                    .join(", ")}
                            </p>))}
                        {!form.addresses.some(hasAddress) && "Not provided"}
                      </dd>
                    </div>
                    <div>
                      <dt>{t("Profession")}</dt>
                      <dd>
                        {[form.profession, form.company, form.designation]
                .filter(Boolean)
                .join(" · ") || "Not provided"}
                      </dd>
                    </div>
                    {form.businessCategory && <div>
                      <dt>{t("Business category")}</dt>
                      <dd>{[form.businessCategory, form.businessSubcategory].filter(Boolean).join(" · ")}</dd>
                    </div>}
                    {(Object.keys(tagLabels) as TagField[]).map(field => {
                        const values = tagValues(form, field);
                        return values.length > 0 && <div key={field}>
                          <dt>{t(tagLabels[field])}</dt>
                          <dd><ul className="aw-tag-list">{values.map((value, index) => <li className="aw-tag" key={index}>{value}</li>)}</ul></dd>
                        </div>;
                    })}
                    <div>
                      <dt>{t("Documents")}</dt>
                      <dd>
                        {documents.map((f) => f.name).join(", ") || "None"}
                      </dd>
                    </div>
                  </dl>
                  {matches.length > 0 && (<div className="aw-duplicates">
                      <h4>{t("Review possible matches")}</h4>
                      <p>{t("Shared contact details can belong to a family. Choose carefully.")}</p>
                      {matches.map((m) => (<label key={m.id}>
                          <input type="radio" name="duplicateDecision" checked={personId === m.id} disabled={busy || attempted} onChange={() => {
                        setPersonId(m.id);
                        setDecision("");
                    }}/>
                          <span>
                            <strong>{t("Use existing person:")}{m.name}</strong>
                            <small>
                              {m.reason}{t("\u00B7 Existing profile and photo will be retained.")}</small>
                          </span>
                        </label>))}
                      <label>
                        <input type="radio" name="duplicateDecision" checked={decision === "CreateSeparate"} disabled={busy || attempted} onChange={() => {
                    setDecision("CreateSeparate");
                    setPersonId(null);
                }}/>
                        <span>{t("I reviewed the matches \u2014 create a separate person")}</span>
                      </label>
                    </div>)}
                  {created && (<div className="aw-notice">{t("Member")}{created.number}{t("is saved. Only remaining document uploads will be retried.")}<button type="button" className="aw-link" disabled={busy} onClick={async () => {
                    await removeDraft();
                    onDone(created);
                }}>{t("Finish without remaining documents")}</button>
                    </div>)}
                </section>)}
            </div>
          </div>
        </div>
        <footer className="aw-footer">
          <button type="button" className="aw-secondary aw-cancel" disabled={busy || photoBusy} onClick={requestClose}>
            <X size={17}/>{t("Cancel")}</button>
          <span className="aw-footer-note">
            {review ? "Review & submit" : `Step ${step + 1} of 4`}
            <small>{t("* Required fields")}</small>
          </span>
          <div className="aw-footer-actions">
            {(step > 0 || review) && !created && !attempted && (<button type="button" className="aw-secondary" disabled={busy} onClick={() => {
                sync();
                if (review)
                    setReview(false);
                else
                    setStep((s) => s - 1);
                setError("");
            }}>
                <ArrowLeft size={17}/>{t("Previous")}</button>)}
            <button type="button" className="aw-secondary" disabled={busy || photoBusy} onClick={saveDraft}>
              <Save size={16}/>
              <span>{t("Save as Draft")}</span>
            </button>
            <button type="button" className="aw-primary" disabled={busy || photoBusy} onClick={review ? submit : step === 3 ? prepare : next}>
              {t(busy
            ? "Saving…"
            : review
                ? created
                    ? "Retry document uploads"
                    : "Add Member"
                : step === 3
                    ? "Review registration"
                    : "Next")}
              <ArrowRight size={18}/>
            </button>
          </div>
        </footer>
        {confirm && (<div className="aw-confirm-backdrop" onKeyDown={(e) => {
                if (e.key === "Escape") {
                    e.stopPropagation();
                    setConfirm(false);
                }
                if (e.key === "Tab") {
                    const buttons = Array.from(e.currentTarget.querySelectorAll<HTMLButtonElement>("button"));
                    if (e.shiftKey && document.activeElement === buttons[0]) {
                        e.preventDefault();
                        buttons.at(-1)?.focus();
                    }
                    else if (!e.shiftKey &&
                        document.activeElement === buttons.at(-1)) {
                        e.preventDefault();
                        buttons[0]?.focus();
                    }
                }
            }}>
            <div role="alertdialog" aria-modal="true" aria-labelledby="aw-discard-title" className="aw-confirm">
              <h3 id="aw-discard-title">{t("Discard entered information?")}</h3>
              <p>{t("Your unsaved changes will be lost. Save a draft to continue on this device later.")}</p>
              <div>
                <button autoFocus type="button" className="aw-secondary" onClick={() => setConfirm(false)}>{t("Keep editing")}</button>
                <button type="button" className="aw-primary" onClick={onClose}>{t("Discard changes")}</button>
              </div>
            </div>
          </div>)}
      </section>
    </div>);
}
