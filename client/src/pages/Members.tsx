import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import {
  Users,
  UserCheck,
  ClipboardList,
  TrendingUp,
  Plus,
  Search,
  Download,
  Upload,
  LayoutGrid,
  List,
  ArrowUpRight,
  Mail,
  Copy,
  Send,
} from "lucide-react";
import { api, apiDownload } from "../api";
import { PageHeader, Skeleton, ErrorBox, useWorkspace } from "../App";
import DataGrid, { type GridColumn, type GridConfig } from "../DataGrid";
import {
  MemberDrawer,
  MemberAvatar,
  MemberStatus,
  ClubCategoryFields,
  PersonEditor,
} from "../MemberComponents";
export default function Members({
  directory = false,
}: {
  directory?: boolean;
}) {
  const { w } = useWorkspace(),
    qc = useQueryClient();
  const grants = w?.permissions || [];
  const [search, setSearch] = useState(""),
    [term, setTerm] = useState(""),
    [statuses, setStatuses] = useState<string[]>([]),
    [clubId, setClub] = useState(""),
    [categoryId, setCategory] = useState(""),
    [city, setCity] = useState(""),
    [profession, setProfession] = useState(""),
    [industry, setIndustry] = useState(""),
    [expertise, setExpertise] = useState(""),
    [office, setOffice] = useState(""),
    [sort, setSort] = useState("name"),
    [page, setPage] = useState(1),
    [view, setView] = useState("list"),
    [advanced, setAdvanced] = useState(false),
    [selected, setSelected] = useState<Set<string>>(new Set()),
    [quick, setQuick] = useState<any>(null),
    [drawer, setDrawer] = useState(""),
    [error, setError] = useState<unknown>(),
    [message, setMessage] = useState(""),
    [busy, setBusy] = useState(false),
    [config, setConfig] = useState<GridConfig>({
      order: [
        "name",
        "number",
        "club",
        "category",
        "offices",
        "city",
        "joinedAt",
        "status",
        "renewalStatus",
        "updatedAt",
        "email",
        "phone",
        "profession",
        "industry",
        "actions",
      ],
      hidden: ["updatedAt", "email", "phone", "profession", "industry"],
      widths: {
        name: 230,
        club: 210,
        number: 140,
        category: 130,
        offices: 180,
        city: 140,
        status: 130,
        renewalStatus: 110,
        joinedAt: 120,
        actions: 100,
      },
    });
  useEffect(() => {
    const t = setTimeout(() => {
      setTerm(search);
      setPage(1);
    }, 250);
    return () => clearTimeout(t);
  }, [search]);
  useEffect(() => {
    setPage(1);
    setSelected(new Set());
  }, [
    statuses,
    clubId,
    categoryId,
    city,
    profession,
    industry,
    expertise,
    office,
    term,
  ]);
  const params = new URLSearchParams({
    search: term,
    statuses: statuses.join(","),
    sort,
    page: String(page),
    size: "20",
    city,
    profession,
    industry,
    expertise,
    office,
  });
  if (clubId) params.set("clubId", clubId);
  if (categoryId) params.set("categoryId", categoryId);
  const q = useQuery({
    queryKey: ["members", directory, w?.id, params.toString()],
    queryFn: () => api((directory ? "/directory" : "/members") + "?" + params),
    placeholderData: (p) => p,
  });
  const metrics = useQuery({
    queryKey: ["member-metrics", w?.id],
    queryFn: () => api("/membership/metrics"),
    enabled: !directory,
  });
  const choices = useQuery({
    queryKey: ["member-choices", w?.id],
    queryFn: () => api("/membership/options"),
    enabled: !!w,
  });
  const saved = useQuery({
    queryKey: ["member-views", w?.id],
    queryFn: () => api("/membership/views"),
    enabled: !directory,
  });
  const columns: GridColumn<any>[] = [
    {
      key: "name",
      label: "Member",
      render: (r) => (
        <button className="text-button member-name" onClick={() => setQuick(r)}>
          <MemberAvatar
            name={r.name}
            id={r.id}
            photo={r.hasPhoto}
            directory={directory}
          />
          <span>
            <b>{r.name}</b>
            <small>{r.profession || "Community member"}</small>
          </span>
        </button>
      ),
    },
    ...[
      "number",
      "club",
      "category",
      "offices",
      "city",
      "joinedAt",
      "status",
      "renewalStatus",
      "updatedAt",
      "email",
      "phone",
      "profession",
      "industry",
    ].map((key) => ({
      key,
      label:
        (
          {
            number: "Member ID",
            joinedAt: "Joined",
            renewalStatus: "Renewal",
            updatedAt: "Updated",
            offices: "Current office",
            city: "Location",
          } as any
        )[key] || key[0].toUpperCase() + key.slice(1),
      render: (r: any) =>
        key === "status" ? (
          <MemberStatus status={r.status} />
        ) : key === "offices" ? (
          r.offices?.join(", ") || "—"
        ) : key === "joinedAt" || key === "updatedAt" ? (
          new Date(r[key]).toLocaleDateString()
        ) : (
          r[key] || "—"
        ),
    })),
    {
      key: "actions",
      label: "Actions",
      render: (r) => (
        <button className="text-button" onClick={() => setQuick(r)}>
          Quick view <ArrowUpRight size={13} />
        </button>
      ),
    },
  ];
  async function exportRows(format: string) {
    setBusy(true);
    setError(null);
    try {
      await apiDownload(
        "/membership/export",
        {
          format,
          columns: columns
            .map((c) => c.key)
            .filter(
              (k) =>
                !config.hidden.includes(k) &&
                !["offices", "actions", "updatedAt", "renewalStatus"].includes(
                  k,
                ),
            ),
          selectedIds: selected.size ? [...selected] : null,
          search: term,
          statuses: statuses.join(","),
          clubId: clubId || null,
          categoryId: categoryId || null,
          city,
          profession,
          industry,
          expertise,
          office,
        },
        "members." + format,
      );
    } catch (e) {
      setError(e);
    } finally {
      setBusy(false);
    }
  }
  function loadView(value: string) {
    const v = saved.data?.find((x: any) => x.id === value);
    if (!v) return;
    try {
      const c = JSON.parse(v.configuration);
      setConfig(c.grid);
      setStatuses(c.statuses || []);
      setClub(c.clubId || "");
      setCategory(c.categoryId || "");
      setCity(c.city || "");
      setProfession(c.profession || "");
      setIndustry(c.industry || "");
      setExpertise(c.expertise || "");
      setOffice(c.office || "");
      setSearch(c.search || "");
      setSort(c.sort || "name");
    } catch (e) {
      setError(e);
    }
  }
  return (
    <>
      <PageHeader
        eyebrow={
          directory ? "YOUR COMMUNITY, CONNECTED" : "MEMBERSHIP INTELLIGENCE"
        }
        title={directory ? "Member directory" : "Members"}
        description={
          directory
            ? "Discover people, shared expertise, and new ways to make an impact."
            : "One person. Every affiliation. A clearer view of your community."
        }
      >
        <div className="header-actions">
          {!directory && grants.includes("members.invite") && (
            <button className="secondary" onClick={() => setDrawer("invite")}>
              <Mail size={16} />
              Invite
            </button>
          )}
          {!directory && grants.includes("members.manage") && (
            <button onClick={() => setDrawer("create")}>
              <Plus size={16} />
              Add member
            </button>
          )}
        </div>
      </PageHeader>
      {!directory &&
        (metrics.isPending ? (
          <Skeleton />
        ) : (
          metrics.data && (
            <>
              <div className="member-kpis">
                {[
                  [
                    Users,
                    "Total people",
                    metrics.data.totalMembers,
                    "Across your authorized scope",
                  ],
                  [
                    UserCheck,
                    "Active members",
                    metrics.data.activeMembers,
                    "Current, active memberships",
                  ],
                  [
                    ClipboardList,
                    "Pending applications",
                    metrics.data.pendingApplications,
                    "Ready for your next step",
                  ],
                  [
                    TrendingUp,
                    "Joined this month",
                    metrics.data.joinedThisMonth,
                    "New connections, shared purpose",
                  ],
                ].map(([I, t, v, d]: any) => (
                  <section className="stat-card" key={t}>
                    <div className="stat-title">
                      {t}
                      <span>
                        <I size={19} />
                      </span>
                    </div>
                    <strong>{v}</strong>
                    <p>{d}</p>
                  </section>
                ))}
              </div>
              <div className="intelligence-strip">
                <span>
                  Growth{" "}
                  <b>
                    {metrics.data.growthPercent == null
                      ? "No opening cohort"
                      : metrics.data.growthPercent + "%"}
                  </b>
                </span>
                <span>
                  Retention{" "}
                  <b>
                    {metrics.data.retentionPercent == null
                      ? "No opening cohort"
                      : metrics.data.retentionPercent + "%"}
                  </b>
                </span>
                <span>
                  Renewal due <b>{metrics.data.upcomingRenewals}</b>
                </span>
                <span>
                  Not currently active <b>{metrics.data.inactiveMembers}</b>
                </span>
                <span>
                  Needs attention <b>{metrics.data.requiringAttention}</b>
                </span>
                <Link to="/app/membership-insights">Explore insights →</Link>
              </div>
            </>
          )
        ))}
      <ErrorBox error={error || q.error || metrics.error} />
      {message && (
        <div className="success" role="status">
          {message}
        </div>
      )}
      <section className="panel directory">
        <div className="member-quick-filters">
          {[
            ["All members", []],
            ["Active", ["Active"]],
            ["Inactive", ["Inactive"]],
            ["Suspended", ["Suspended"]],
            ["Transferred", ["Transferred"]],
            ["Expired", ["Expired"]],
            ["Renewal due", ["Renewal Due"]],
          ].map(([label, values]: any) => (
            <button
              key={label}
              className={
                JSON.stringify(statuses) === JSON.stringify(values)
                  ? "active"
                  : ""
              }
              onClick={() => setStatuses(values)}
            >
              {label}
            </button>
          ))}
          {!directory && grants.includes("members.approve") && (
            <Link to="/app/approvals">
              Pending applications <span>↗</span>
            </Link>
          )}
        </div>
        <div className="filter-bar">
          <div className="search-field">
            <Search size={17} />
            <input
              aria-label="Search members"
              value={search}
              placeholder="Search people or membership IDs…"
              onChange={(e) => setSearch(e.target.value)}
            />
          </div>
          <button
            className="secondary"
            aria-expanded={advanced}
            onClick={() => setAdvanced(!advanced)}
          >
            Filters {advanced ? "−" : "+"}
          </button>
          <select
            aria-label="Sort members"
            value={sort}
            onChange={(e) => setSort(e.target.value)}
          >
            <option value="name">Name A–Z</option>
            <option value="number">Member ID</option>
            <option value="joined">Recently joined</option>
            <option value="updated">Recently updated</option>
            <option value="status">Status</option>
          </select>
          <div className="view-toggle">
            <button
              className={view === "list" ? "active" : ""}
              aria-label="List view"
              onClick={() => setView("list")}
            >
              <List size={17} />
            </button>
            <button
              className={view === "cards" ? "active" : ""}
              aria-label="Card view"
              onClick={() => setView("cards")}
            >
              <LayoutGrid size={17} />
            </button>
          </div>
        </div>
        {advanced && (
          <div className="advanced-filters">
            <label>
              Statuses
              <select
                multiple
                aria-label="Membership statuses"
                value={statuses}
                onChange={(e) =>
                  setStatuses([...e.target.selectedOptions].map((o) => o.value))
                }
              >
                {[
                  "Active",
                  "Inactive",
                  "Suspended",
                  "Expired",
                  "Transferred",
                  "Resigned",
                  "Terminated",
                  "Deceased",
                ].map((x) => (
                  <option key={x}>{x}</option>
                ))}
              </select>
            </label>
            {choices.data && (
              <>
                <label>
                  Club
                  <select
                    value={clubId}
                    onChange={(e) => setClub(e.target.value)}
                  >
                    <option value="">All authorized clubs</option>
                    {choices.data.clubs.map((c: any) => (
                      <option key={c.id} value={c.id}>
                        {c.name}
                      </option>
                    ))}
                  </select>
                </label>
                <label>
                  Category
                  <select
                    value={categoryId}
                    onChange={(e) => setCategory(e.target.value)}
                  >
                    <option value="">All categories</option>
                    {choices.data.categories.map((c: any) => (
                      <option key={c.id} value={c.id}>
                        {c.name}
                      </option>
                    ))}
                  </select>
                </label>
              </>
            )}
            {[
              ["City", city, setCity],
              ["Profession", profession, setProfession],
              ["Industry", industry, setIndustry],
              ["Expertise", expertise, setExpertise],
              ["Current office", office, setOffice],
            ].map(([label, value, set]: any) => (
              <label key={label}>
                {label}
                <input value={value} onChange={(e) => set(e.target.value)} />
              </label>
            ))}
          </div>
        )}
        {!directory && (
          <div className="saved-view-bar">
            <select
              aria-label="Saved member views"
              defaultValue=""
              onChange={(e) => loadView(e.target.value)}
            >
              <option value="">Saved views</option>
              {saved.data?.map((v: any) => (
                <option key={v.id} value={v.id}>
                  {v.name}
                </option>
              ))}
            </select>
            <button
              className="text-button"
              onClick={async () => {
                const name = window.prompt("Name this view");
                if (!name) return;
                try {
                  await api("/membership/views", "POST", {
                    name,
                    configuration: JSON.stringify({
                      grid: config,
                      statuses,
                      clubId,
                      categoryId,
                      city,
                      profession,
                      industry,
                      expertise,
                      office,
                      search,
                      sort,
                    }),
                  });
                  qc.invalidateQueries({ queryKey: ["member-views"] });
                  setMessage("View saved to your account.");
                } catch (e) {
                  setError(e);
                }
              }}
            >
              Save current view
            </button>
            <span />
            {grants.includes("members.import") && (
              <Link className="text-button" to="/app/imports">
                <Upload size={14} />
                Import
              </Link>
            )}
            {grants.includes("members.export") && (
              <>
                <button
                  className="text-button"
                  disabled={busy}
                  onClick={() => exportRows("csv")}
                >
                  <Download size={14} />
                  CSV
                </button>
                <button
                  className="text-button"
                  disabled={busy}
                  onClick={() => exportRows("xlsx")}
                >
                  Excel
                </button>
              </>
            )}
          </div>
        )}
        {q.isPending ? (
          <Skeleton />
        ) : (
          <>
            <div
              className={
                view === "cards" ? "hidden-grid" : "members-desktop-grid"
              }
            >
              <DataGrid
                rows={q.data?.items || []}
                columns={columns}
                config={config}
                onConfig={setConfig}
                selected={directory ? undefined : selected}
                onSelection={directory ? undefined : setSelected}
                onOpen={setQuick}
                empty={
                  directory
                    ? "No members have opted into this directory in your authorized scope."
                    : "No matching memberships. Adjust filters or add your first member."
                }
              />
            </div>
            <div
              className={
                view === "cards"
                  ? "member-card-grid"
                  : "member-card-grid mobile-only"
              }
            >
              {q.data?.items.map((m: any) => (
                <article className="directory-person-card" key={m.id}>
                  <MemberAvatar
                    name={m.name}
                    id={m.id}
                    photo={m.hasPhoto}
                    directory={directory}
                    size="large"
                  />
                  <MemberStatus status={m.status} />
                  <h3>{m.name}</h3>
                  <p>{m.profession || m.category}</p>
                  <span>{m.club}</span>
                  <small>{m.city || m.country || "Location not shared"}</small>
                  <button className="secondary" onClick={() => setQuick(m)}>
                    View member <ArrowUpRight size={14} />
                  </button>
                </article>
              ))}
            </div>
            <div className="pagination">
              <span>
                {q.data?.total || 0} memberships · Page {page}
                {q.isFetching ? " · Refreshing…" : ""}
              </span>
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
          </>
        )}
      </section>
      {quick && (
        <MemberDrawer title="Member quick view" onClose={() => setQuick(null)}>
          <div className="quick-member-hero">
            <MemberAvatar
              name={quick.name}
              id={quick.id}
              photo={quick.hasPhoto}
              directory={directory}
              size="xl"
            />
            <h2>{quick.name}</h2>
            <p>{quick.number}</p>
            <MemberStatus status={quick.status} />
          </div>
          {[
            ["Club", quick.club],
            ["Category", quick.category],
            ["Current office", quick.offices?.join(", ") || "—"],
            [
              "Location",
              [quick.city, quick.country].filter(Boolean).join(", ") ||
                "Not shared",
            ],
            ["Joined", new Date(quick.joinedAt).toLocaleDateString()],
            ["Email", quick.email || "Private / not provided"],
            ["Phone", quick.phone || "Private / not provided"],
            ["Renewal", quick.renewalStatus],
          ].map(([k, v]) => (
            <div className="context-row" key={k}>
              <span>{k}</span>
              <b>{v}</b>
            </div>
          ))}
          <Link
            className="button full-width"
            to={
              "/app/members/" + quick.id + (directory ? "?directory=true" : "")
            }
          >
            Open complete profile <ArrowUpRight size={16} />
          </Link>
          {directory && quick.contactRequestsAllowed && (
            <ContactRequest id={quick.id} />
          )}
        </MemberDrawer>
      )}
      {drawer === "create" && (
        <MemberDrawer
          title="Add a community member"
          onClose={() => setDrawer("")}
        >
          <CreateMember
            onDone={() => {
              setDrawer("");
              qc.invalidateQueries({ queryKey: ["members"] });
              qc.invalidateQueries({ queryKey: ["member-metrics"] });
            }}
          />
        </MemberDrawer>
      )}
      {drawer === "invite" && (
        <MemberDrawer
          title="Invite someone to join"
          onClose={() => setDrawer("")}
        >
          <InviteMember />
        </MemberDrawer>
      )}
    </>
  );
}
function CreateMember({ onDone }: any) {
  const [target, setTarget] = useState<any>({ clubId: "", categoryId: "" }),
    [draft, setDraft] = useState<any>(null),
    [duplicates, setDuplicates] = useState<any[]>([]),
    [error, setError] = useState<unknown>(),
    [busy, setBusy] = useState(false);
  async function create(
    person: any,
    decision = "",
    personId: string | null = null,
  ) {
    setBusy(true);
    setError(null);
    try {
      if (!target.clubId || !target.categoryId)
        throw new Error("Choose a club and category.");
      await api("/members", "POST", {
        person,
        ...target,
        personId,
        duplicateDecision: decision,
      });
      onDone();
    } catch (e) {
      setError(e);
    } finally {
      setBusy(false);
    }
  }
  return (
    <>
      <div className="form-stack">
        <ClubCategoryFields
          {...target}
          onChange={(v: any) => setTarget({ ...target, ...v })}
        />
        <label>
          Joining date
          <input
            type="date"
            onChange={(e) =>
              setTarget({
                ...target,
                joinedAt: e.target.value
                  ? new Date(e.target.value).toISOString()
                  : null,
              })
            }
          />
        </label>
      </div>
      <ErrorBox error={error} />
      {draft && duplicates.length ? (
        <section className="duplicate-review">
          <h3>Review possible matches</h3>
          <p>A shared email or family phone is not proof of the same person.</p>
          {duplicates.map((d) => (
            <div className="context-row" key={d.id}>
              <span>
                {d.name}
                <small>{d.reason}</small>
              </span>
              <button
                className="secondary"
                disabled={busy}
                onClick={() => create(draft, "", d.id)}
              >
                Use this person
              </button>
            </div>
          ))}
          <button
            disabled={busy}
            onClick={() => create(draft, "CreateSeparate")}
          >
            I reviewed the matches — create a separate person
          </button>
          <button className="text-button" onClick={() => setDraft(null)}>
            Back to profile details
          </button>
        </section>
      ) : (
        <PersonEditor
          busy={busy}
          onCancel={onDone}
          onSave={async (person: any) => {
            setBusy(true);
            try {
              const matches = await api("/members/duplicates", "POST", person);
              if (matches.length) {
                setDraft(person);
                setDuplicates(matches);
              } else await create(person);
            } catch (e) {
              setError(e);
            } finally {
              setBusy(false);
            }
          }}
        />
      )}
    </>
  );
}
export function InviteMember() {
  const [form, setForm] = useState<any>({
      clubId: "",
      categoryId: "",
      email: "",
      expiresInDays: 7,
      usageLimit: 1,
    }),
    [result, setResult] = useState<any>(null),
    [error, setError] = useState<unknown>(),
    [busy, setBusy] = useState(false);
  return (
    <form
      className="form-stack"
      onSubmit={async (e) => {
        e.preventDefault();
        setBusy(true);
        try {
          setResult(await api("/membership/invitations", "POST", form));
        } catch (e) {
          setError(e);
        } finally {
          setBusy(false);
        }
      }}
    >
      <ClubCategoryFields
        {...form}
        onChange={(v: any) => setForm({ ...form, ...v })}
      />
      <label>
        Intended email (optional for a shareable club link)
        <input
          type="email"
          value={form.email}
          onChange={(e) => setForm({ ...form, email: e.target.value })}
        />
      </label>
      <div className="form-grid">
        <label>
          Expires in days
          <input
            type="number"
            min={1}
            max={30}
            value={form.expiresInDays}
            onChange={(e) =>
              setForm({ ...form, expiresInDays: Number(e.target.value) })
            }
          />
        </label>
        <label>
          Usage limit
          <input
            type="number"
            min={1}
            max={20}
            value={form.usageLimit}
            onChange={(e) =>
              setForm({ ...form, usageLimit: Number(e.target.value) })
            }
          />
        </label>
      </div>
      <p>
        Invitees must verify their account and complete the club’s required
        approvals.
      </p>
      <ErrorBox error={error} />
      {result ? (
        <div className="invitation-result">
          <h3>Invitation created</h3>
          <p>
            Email delivery is queued when a recipient was supplied. A share link
            does not send a WhatsApp message.
          </p>
          <input
            readOnly
            aria-label="Invitation link"
            value={location.origin + result.link}
          />
          <div className="header-actions">
            <button
              type="button"
              className="secondary"
              onClick={async () => {
                try {
                  await navigator.clipboard.writeText(
                    location.origin + result.link,
                  );
                } catch (e) {
                  setError(e);
                }
              }}
            >
              <Copy size={14} />
              Copy link
            </button>
            <a
              className="button secondary"
              href={
                "https://wa.me/?text=" +
                encodeURIComponent(
                  "Join our club: " + location.origin + result.link,
                )
              }
            >
              <Send size={14} />
              WhatsApp share
            </a>
          </div>
          <img
            alt="Invitation QR code"
            width={180}
            height={180}
            src={"/api/v1/membership/invitations/" + result.id + "/qr"}
          />
        </div>
      ) : (
        <button disabled={busy}>
          {busy ? "Creating…" : "Create secure invitation"}
        </button>
      )}
    </form>
  );
}
function ContactRequest({ id }: any) {
  const [message, setMessage] = useState(""),
    [sent, setSent] = useState(false),
    [error, setError] = useState<unknown>();
  return (
    <form
      className="form-stack spaced"
      onSubmit={async (e) => {
        e.preventDefault();
        try {
          await api("/directory/" + id + "/contact-request", "POST", {
            message,
          });
          setSent(true);
        } catch (e) {
          setError(e);
        }
      }}
    >
      <label>
        Request contact
        <textarea
          required
          minLength={5}
          maxLength={1000}
          value={message}
          onChange={(e) => setMessage(e.target.value)}
          placeholder="Introduce yourself and explain why you’d like to connect."
        />
      </label>
      <ErrorBox error={error} />
      {sent ? (
        <div className="success">
          Your request is recorded for the member to review.
        </div>
      ) : (
        <button>Send contact request</button>
      )}
    </form>
  );
}
