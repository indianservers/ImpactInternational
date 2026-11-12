import { useEffect, useState } from "react";
import { X, Plus, ArrowLeft } from "lucide-react";
import { useQuery } from "@tanstack/react-query";
import { api } from "./api";
import { ErrorBox, useWorkspace } from "./App";
import { useDialog } from "./useDialog";
export function MemberAvatar({
  name,
  id,
  photo,
  directory = false,
  size = "",
}: any) {
  return photo ? (
    <img
      className={"member-avatar " + size}
      alt={name + " profile"}
      src={
        "/api/v1/members/" +
        id +
        "/photo" +
        (directory ? "?directory=true" : "")
      }
    />
  ) : (
    <span className={"member-avatar " + size}>
      {String(name || "?")
        .split(" ")
        .map((x) => x[0])
        .slice(0, 2)
        .join("")}
    </span>
  );
}
export function MemberStatus({ status }: any) {
  return (
    <span
      className={
        "badge member-status " +
        String(status).toLowerCase().replaceAll(" ", "-")
      }
    >
      {status}
    </span>
  );
}
export function MemberDrawer({
  title,
  eyebrow = "MEMBERSHIP WORKSPACE",
  onClose,
  children,
}: any) {
  useDialog(true, onClose);
  return (
    <div className="modal-backdrop">
      <section
        role="dialog"
        aria-modal="true"
        aria-label={title}
        className="drawer member-drawer"
      >
        <div className="drawer-heading">
          <div>
            <div className="eyebrow">{eyebrow}</div>
            <h2>{title}</h2>
          </div>
          <button
            className="icon-button"
            aria-label="Close drawer"
            onClick={onClose}
          >
            <X />
          </button>
        </div>
        {children}
      </section>
    </div>
  );
}
export function ClubCategoryFields({
  clubId,
  categoryId,
  onChange,
  publicCatalog = false,
  tenantId,
  excludeClub,
  fixedTenant,
}: any) {
  const { w } = useWorkspace();
  const [search, setSearch] = useState(""),
    [term, setTerm] = useState(""),
    [association, setAssociation] = useState(tenantId || w?.tenantId || "");
  useEffect(() => {
    const t = setTimeout(() => setTerm(search), 250);
    return () => clearTimeout(t);
  }, [search]);
  const catalog = useQuery({
    queryKey: ["member-catalog", publicCatalog, association, term, w?.id],
    queryFn: () =>
      publicCatalog
        ? api(
            "/membership/catalog?" +
              (association ? "tenantId=" + association + "&" : "") +
              "search=" +
              encodeURIComponent(term),
          )
        : api("/membership/options?search=" + encodeURIComponent(term)),
  });
  const clubs = catalog.data?.clubs;
  const categories = catalog.data?.categories;
  const associations = useQuery({
    queryKey: ["associations-catalog"],
    queryFn: () => api("/membership/catalog"),
    enabled: publicCatalog,
  });
  return (
    <>
      {publicCatalog && !fixedTenant && (
        <label>
          Association
          <select
            required
            value={association}
            onChange={(e) => {
              setAssociation(e.target.value);
              onChange({
                clubId: "",
                categoryId: "",
                tenantId: e.target.value,
              });
            }}
          >
            <option value="">Choose an association</option>
            {associations.data?.associations.map((a: any) => (
              <option key={a.id} value={a.id}>
                {a.name}
              </option>
            ))}
          </select>
        </label>
      )}
      <label>
        Find a club
        <input
          value={search}
          placeholder="Search club names…"
          onChange={(e) => setSearch(e.target.value)}
        />
      </label>
      <div className="form-grid">
        <label>
          Club
          <select
            required
            value={clubId || ""}
            onChange={(e) => onChange({ clubId: e.target.value })}
          >
            <option value="">Choose club</option>
            {clubs
              ?.filter((c: any) => c.id !== excludeClub)
              .map((c: any) => (
                <option key={c.id} value={c.id}>
                  {c.name}
                </option>
              ))}
          </select>
        </label>
        <label>
          Membership category
          <select
            required
            value={categoryId || ""}
            onChange={(e) => onChange({ categoryId: e.target.value })}
          >
            <option value="">Choose category</option>
            {categories
              ?.filter((c: any) => c.active !== false)
              .map((c: any) => (
                <option key={c.id} value={c.id}>
                  {c.name}
                </option>
              ))}
          </select>
        </label>
      </div>
      <ErrorBox error={catalog.error} />
    </>
  );
}
export function PersonEditor({ person, onSave, onCancel, busy = false }: any) {
  const [tab, setTab] = useState("Personal");
  const [form, setForm] = useState<any>(() => ({
    legalName: person?.legalName || person?.name || "",
    displayName: person?.displayName || person?.name || "",
    birthDate: person?.birthDate || "",
    gender: person?.gender || "",
    maritalStatus: person?.maritalStatus || "",
    spouseName: person?.spouseName || "",
    educationLevel: person?.educationLevel || "",
    qualification: person?.qualification || "",
    institution: person?.institution || "",
    employmentStatus: person?.employmentStatus || "",
    additionalInformation: person?.additionalInformation || "",
    anniversary: person?.anniversary || "",
    company: person?.professional?.company || "",
    designation: person?.professional?.designation || "",
    industry: person?.professional?.industry || "",
    profession: person?.professional?.profession || "",
    businessCategory: person?.professional?.businessCategory || "",
    workLocation: person?.professional?.workLocation || "",
    website: person?.professional?.website || "",
    networking: person?.professional?.networking || "",
    contacts: person?.contacts || [],
    addresses: person?.addresses?.length
      ? person.addresses
      : [
          {
            country: "India",
            state: "",
            city: "",
            postalCode: "",
            address: "",
          },
        ],
    skills: person?.professional?.skills || [],
    expertise: person?.professional?.expertise || [],
    interests: person?.professional?.interests || [],
    languages: person?.languages || [],
    skillsText: (person?.professional?.skills || []).join(", "),
    expertiseText: (person?.professional?.expertise || []).join(", "),
    interestsText: (person?.professional?.interests || []).join(", "),
    languagesText: (person?.languages || []).join(", "),
    version: person?.version || 1,
  }));
  function input(k: string, label: string, type = "text", required = false) {
    return (
      <label key={k}>
        {label}
        <input
          type={type}
          required={required}
          maxLength={type === "text" ? 200 : undefined}
          value={form[k]}
          onChange={(e) => setForm({ ...form, [k]: e.target.value })}
        />
      </label>
    );
  }
  return (
    <form
      className="form-stack"
      onSubmit={(e) => {
        e.preventDefault();
        onSave({
          ...form,
          ...Object.fromEntries(
            ["skills", "expertise", "interests", "languages"].map((k) => [
              k,
              form[k + "Text"]
                .split(",")
                .map((x: string) => x.trim())
                .filter(Boolean),
            ]),
          ),
          birthDate: form.birthDate || null,
          anniversary: form.anniversary || null,
        });
      }}
    >
      <div
        className="member-tabs"
        role="tablist"
        aria-label="Profile editing sections"
      >
        {["Personal", "Contact", "Education", "Professional"].map((t) => (
          <button
            type="button"
            role="tab"
            aria-selected={tab === t}
            className={tab === t ? "active" : ""}
            key={t}
            onClick={() => setTab(t)}
          >
            {t}
          </button>
        ))}
      </div>
      {tab === "Personal" && (
        <>
          <div className="form-grid">
            {input("legalName", "Full legal name", "text", true)}
            {input("displayName", "Preferred display name", "text", true)}
            {input("birthDate", "Date of birth", "date")}
            {input("gender", "Gender (optional)")}
            <label>
              Marital status
              <select value={form.maritalStatus} onChange={(e) => setForm({ ...form, maritalStatus: e.target.value })}>
                <option value="">Choose marital status</option>
                {["Single", "Married", "Widowed", "Divorced", "Separated", "Prefer not to say"].map((v) => <option key={v}>{v}</option>)}
              </select>
            </label>
            {input("spouseName", "Spouse name (optional)")}
            {input("anniversary", "Marriage anniversary (optional)", "date")}
            <label>
              Languages
              <input
                value={form.languagesText}
                onChange={(e) =>
                  setForm({ ...form, languagesText: e.target.value })
                }
                placeholder="English, Telugu"
              />
            </label>
          </div>
          {form.addresses.map((a: any, i: number) => (
            <section className="form-section" key={i}>
              <h3>Address {i + 1}</h3>
              <div className="form-grid">
                {[
                  ["country", "Country"],
                  ["state", "State / province"],
                  ["city", "City"],
                  ["postalCode", "PIN code / postal code"],
                  ["address", "Postal address"],
                ].map(([k, l]) => (
                  <label key={k}>
                    {l}
                    <input
                      value={a[k] || ""}
                      onChange={(e) =>
                        setForm({
                          ...form,
                          addresses: form.addresses.map((x: any, n: number) =>
                            n === i ? { ...x, [k]: e.target.value } : x,
                          ),
                        })
                      }
                    />
                  </label>
                ))}
              </div>
              {i > 0 && (
                <button
                  type="button"
                  className="text-button"
                  onClick={() =>
                    setForm({
                      ...form,
                      addresses: form.addresses.filter(
                        (_: any, n: number) => n !== i,
                      ),
                    })
                  }
                >
                  Remove address
                </button>
              )}
            </section>
          ))}
          {form.addresses.length < 5 && (
            <button
              className="secondary"
              type="button"
              onClick={() =>
                setForm({
                  ...form,
                  addresses: [
                    ...form.addresses,
                    {
                      country: "",
                      state: "",
                      city: "",
                      postalCode: "",
                      address: "",
                    },
                  ],
                })
              }
            >
              Add another address
            </button>
          )}
        </>
      )}
      {tab === "Contact" && (
        <>
          <p>
            Shared family contact details are supported. Emergency contacts
            remain restricted.
          </p>
          {form.contacts.map((c: any, i: number) => (
            <div className="contact-editor" key={i}>
              <label>
                Contact type
                <select
                  value={c.kind}
                  onChange={(e) =>
                    setForm({
                      ...form,
                      contacts: form.contacts.map((x: any, n: number) =>
                        n === i ? { ...x, kind: e.target.value } : x,
                      ),
                    })
                  }
                >
                  {["Email", "Phone", "Emergency"].map((x) => (
                    <option key={x}>{x}</option>
                  ))}
                </select>
              </label>
              <label>
                Label
                <input
                  value={c.label}
                  onChange={(e) =>
                    setForm({
                      ...form,
                      contacts: form.contacts.map((x: any, n: number) =>
                        n === i ? { ...x, label: e.target.value } : x,
                      ),
                    })
                  }
                />
              </label>
              <label>
                Contact value
                <input
                  type={c.kind === "Email" ? "email" : "text"}
                  value={c.value}
                  onChange={(e) =>
                    setForm({
                      ...form,
                      contacts: form.contacts.map((x: any, n: number) =>
                        n === i ? { ...x, value: e.target.value } : x,
                      ),
                    })
                  }
                />
              </label>
              <button
                type="button"
                className="icon-button"
                aria-label={"Remove contact " + (i + 1)}
                onClick={() =>
                  setForm({
                    ...form,
                    contacts: form.contacts.filter(
                      (_: any, n: number) => n !== i,
                    ),
                  })
                }
              >
                <X size={15} />
              </button>
            </div>
          ))}
          {form.contacts.length < 12 && (
            <button
              type="button"
              className="secondary"
              onClick={() =>
                setForm({
                  ...form,
                  contacts: [
                    ...form.contacts,
                    { kind: "Email", label: "Personal", value: "" },
                  ],
                })
              }
            >
              <Plus size={15} />
              Add contact
            </button>
          )}
        </>
      )}
      {tab === "Education" && (
        <div className="form-grid">
          {input("educationLevel", "Highest education level")}
          {input("qualification", "Degree / qualification")}
          {input("institution", "School / college / university")}
        </div>
      )}
      {tab === "Personal" && (
        <label>Additional information
          <textarea maxLength={2000} rows={3} value={form.additionalInformation} onChange={(e) => setForm({ ...form, additionalInformation: e.target.value })} />
        </label>
      )}
      {tab === "Professional" && (
        <>
          <p>
            Professional details appear in the directory only after the member
            opts in.
          </p>
          <div className="form-grid">
            {[
              ["employmentStatus", "Employment status (employed, business, student, retired…)"],
              ["company", "Company / organization"],
              ["designation", "Designation"],
              ["businessCategory", "Business category"],
              ["industry", "Industry"],
              ["profession", "Profession"],
              ["workLocation", "Work location"],
              ["website", "Website"],
              ["networking", "Networking preferences"],
            ].map(([k, l]) => input(k, l, k === "website" ? "url" : "text"))}
          </div>
          {[
            ["skills", "Skills"],
            ["expertise", "Expertise"],
            ["interests", "Interests"],
          ].map(([k, l]) => (
            <label key={k}>
              {l}
              <input
                placeholder="Separate values with commas"
                value={form[k + "Text"]}
                onChange={(e) =>
                  setForm({ ...form, [k + "Text"]: e.target.value })
                }
              />
            </label>
          ))}
        </>
      )}
      <div className="drawer-footer">
        <button type="button" className="secondary" onClick={onCancel}>
          <ArrowLeft size={14} />
          Cancel
        </button>
        <button disabled={busy}>{busy ? "Saving…" : "Save profile"}</button>
      </div>
    </form>
  );
}
export function PrivacyEditor({ privacy, onSave, busy }: any) {
  const [v, setV] = useState(privacy);
  if (!v)
    return (
      <p>Only the verified member can change protected privacy preferences.</p>
    );
  return (
    <form
      className="form-stack"
      onSubmit={(e) => {
        e.preventDefault();
        onSave(v);
      }}
    >
      {[
        ["directoryVisible", "Appear in authorized member directories"],
        ["displayEmail", "Show email in the directory"],
        ["displayPhone", "Show telephone in the directory"],
        ["professionalVisible", "Opt in to the professional directory"],
        ["photoVisible", "Display my photograph to directory members"],
        ["cardNameVisible", "Display my name on public QR verification"],
        [
          "allowContactRequests",
          "Allow contact requests from directory members",
        ],
        ["birthdayAnnouncements", "Allow birthday greetings and announcements"],
        ["marketing", "Marketing communication (preference only)"],
        ["transactionalEmail", "Email me membership status notifications"],
      ].map(([k, l]) => (
        <label className="privacy-option" key={k}>
          <span>
            {l}
            <small>
              {k === "directoryVisible"
                ? "Visibility is limited to each viewer’s authorized organizational scope."
                : k === "cardNameVisible"
                  ? "Public verification never displays contact details, addresses, or birth dates."
                  : ""}
            </small>
          </span>
          <input
            type="checkbox"
            checked={v[k]}
            onChange={(e) => setV({ ...v, [k]: e.target.checked })}
          />
        </label>
      ))}
      <p>
        Account security email remains necessary. Birthday and marketing
        preferences do not start campaigns.
      </p>
      <button disabled={busy}>
        {busy ? "Saving…" : "Save privacy preferences"}
      </button>
    </form>
  );
}
