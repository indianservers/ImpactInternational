import {
  lazy,
  Suspense,
  useState,
  useEffect,
  createContext,
  useContext,
} from "react";
import {
  Link,
  NavLink,
  Outlet,
  Route,
  Routes,
  useNavigate,
  useLocation,
} from "react-router-dom";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import {
  Globe2,
  Sun,
  Moon,
  UserRound,
  CreditCard,
  ClipboardCheck,
  ArrowRightLeft,
  Upload,
  SlidersHorizontal,
  LayoutDashboard,
  Network,
  Building2,
  ShieldCheck,
  Settings,
  ScrollText,
  ArrowUpRight,
  Menu,
  LogOut,
  ChevronDown,
  CalendarDays,
  Users,
  Mail,
  Briefcase,
  Wallet,
  ChartNoAxesCombined,
} from "lucide-react";
import { TenantBrand } from "./TenantBrand";
import { api, type Workspace } from "./api";
const IdCards = lazy(() => import("./cards/IdCards"));
const MyIdCard = lazy(() =>
  import("./cards/IdCards").then((m) => ({ default: m.MyIdCard })),
);
const IdVerification = lazy(() =>
  import("./cards/IdCards").then((m) => ({ default: m.IdVerification })),
);
const RequestOrganization = lazy(() => import("./pages/RequestOrganization"));
const Dashboard = lazy(() => import("./pages/Dashboard"));
const Organizations = lazy(() => import("./pages/Organizations"));
const Administration = lazy(() => import("./pages/Administration"));
const SettingsPage = lazy(() => import("./pages/Settings"));
const Audit = lazy(() => import("./pages/Audit"));
const Profile = lazy(() => import("./pages/Profile"));
const Auth = lazy(() => import("./pages/Auth"));
const Members = lazy(() => import("./pages/Members"));
const MemberProfile = lazy(() => import("./pages/MemberProfile"));
const MemberPortal = lazy(() => import("./pages/MemberPortal"));
const MembershipApplications = lazy(
  () => import("./pages/MembershipApplications"),
);
const MembershipSettings = lazy(() => import("./pages/MembershipSettings"));
const MemberImports = lazy(() => import("./pages/MemberImports"));
const MembershipInsights = lazy(() => import("./pages/MembershipInsights"));
const MembershipOperations = lazy(() => import("./pages/MembershipOperations"));
const JoinInvitation = lazy(() =>
  import("./pages/MembershipPublic").then((m) => ({
    default: m.JoinInvitation,
  })),
);
const VerifyMembershipCard = lazy(() =>
  import("./pages/MembershipPublic").then((m) => ({
    default: m.VerifyMembershipCard,
  })),
);
const ClubDirectory = lazy(() => import("./pages/GovernanceClubs"));
const ClubWorkspace = lazy(() =>
  import("./pages/GovernanceClubs").then((m) => ({ default: m.ClubWorkspace })),
);
const GovernanceDashboard = lazy(() =>
  import("./pages/GovernanceClubs").then((m) => ({
    default: m.GovernanceDashboard,
  })),
);
const LeadershipDirectory = lazy(() => import("./pages/GovernanceLeadership"));
const LeadershipCalendar = lazy(() =>
  import("./pages/GovernanceLeadership").then((m) => ({
    default: m.LeadershipCalendar,
  })),
);
const LeadershipHandovers = lazy(() =>
  import("./pages/GovernanceLeadership").then((m) => ({
    default: m.LeadershipHandovers,
  })),
);
const CommitteeDirectory = lazy(() => import("./pages/GovernanceCommittees"));
const CommitteeWorkspace = lazy(() =>
  import("./pages/GovernanceCommittees").then((m) => ({
    default: m.CommitteeWorkspace,
  })),
);
const GovernanceApprovals = lazy(() => import("./pages/GovernanceApprovals"));
const GovernanceSettings = lazy(() => import("./pages/GovernanceSettings"));
const GovernanceDocuments = lazy(() => import("./pages/GovernanceDocuments"));
const GovernanceResolutions = lazy(() =>
  import("./pages/GovernanceDocuments").then((m) => ({
    default: m.GovernanceResolutions,
  })),
);
const Intelligence = lazy(() => import("./pages/Intelligence"));
const SearchLauncher = lazy(() =>
  import("./pages/Intelligence").then((m) => ({ default: m.SearchLauncher })),
);
const Finance = lazy(() => import("./pages/Finance"));
const Operations = lazy(() => import("./pages/Operations"));
const VerifyCertificate = lazy(() =>
  import("./pages/Operations").then((m) => ({ default: m.VerifyCertificate })),
);
const Communications = lazy(() => import("./pages/Communications"));
const Events = lazy(() => import("./pages/Events"));
const EventWizard = lazy(() =>
  import("./pages/Events").then((m) => ({ default: m.EventWizard })),
);
const EventWorkspace = lazy(() => import("./pages/EventWorkspace"));
const MyEvents = lazy(() =>
  import("./pages/EventParticipation").then((m) => ({ default: m.MyEvents })),
);
const EventCheckin = lazy(() =>
  import("./pages/EventParticipation").then((m) => ({
    default: m.EventCheckin,
  })),
);
const EventSettings = lazy(() =>
  import("./pages/EventParticipation").then((m) => ({
    default: m.EventSettings,
  })),
);
const EventDashboard = lazy(() =>
  import("./pages/EventParticipation").then((m) => ({
    default: m.EventDashboard,
  })),
);
const PublicEvent = lazy(() =>
  import("./pages/EventParticipation").then((m) => ({
    default: m.PublicEvent,
  })),
);
const EventInvitation = lazy(() =>
  import("./pages/EventParticipation").then((m) => ({
    default: m.EventInvitation,
  })),
);
function WorkspaceHome() {
  const { w } = useWorkspace();
  return w?.permissions.includes("governance.reports") ? (
    <GovernanceDashboard />
  ) : w?.permissions.includes("members.view") ? (
    <Dashboard />
  ) : (
    <MemberPortal />
  );
}
const Context = createContext<{ w: Workspace | undefined; me: any }>({
  w: undefined,
  me: null,
});
export const useWorkspace = () => useContext(Context);
export function Skeleton() {
  return (
    <div className="skeleton" aria-label="Loading content">
      <div />
      <div />
      <div />
    </div>
  );
}
export function ErrorBox({ error }: { error: unknown }) {
  return error ? (
    <div role="alert" className="error">
      {error instanceof Error ? error.message : String(error)}
    </div>
  ) : null;
}
export function PageHeader({ eyebrow, title, description, children }: any) {
  return (
    <div className="page-header">
      <div>
        <div className="eyebrow">{eyebrow}</div>
        <h1>{title}</h1>
        <p>{description}</p>
      </div>
      {children}
    </div>
  );
}
function Shell() {
  const qc = useQueryClient(),
    nav = useNavigate(),
    location = useLocation();
  const [open, setOpen] = useState(false),
    [active, setActive] = useState(""),
    [error, setError] = useState<unknown>();
  const me = useQuery({ queryKey: ["me"], queryFn: () => api("/auth/me") });
  useEffect(() => {
    document.documentElement.dataset.theme = me.data?.theme || "light";
    return () => {
      document.documentElement.dataset.theme = "light";
    };
  }, [me.data?.theme]);
  const workspaces = useQuery({
    queryKey: ["workspaces"],
    queryFn: () => api<Workspace[]>("/workspaces"),
    enabled: !!me.data,
  });
  const w = workspaces.data?.find(
    (x) => x.id === (active || me.data?.workspaceId),
  );
  const experience = useQuery<any>({
    queryKey: ["tenant-experience", w?.id],
    queryFn: () => api("/intelligence/branding/current"),
    enabled: !!w,
    staleTime: 30000,
  });
  const identity = experience.data?.identity;
  async function activate(id: string) {
    try {
      await api("/workspaces/" + id + "/activate", "POST");
      setActive(id);
      nav("/app");
      setOpen(false);
      qc.removeQueries({
        predicate: (q) => !["me", "workspaces"].includes(String(q.queryKey[0])),
      });
      setError(null);
    } catch (e) {
      setError(e);
    }
  }
  const menus = [
    ["Event types", "/app/event-settings", CalendarDays, "events.configure"],
    [
      "Event intelligence",
      "/app/event-intelligence",
      CalendarDays,
      "events.reports",
    ],
    [
      "Global Intelligence",
      "/app/intelligence",
      ChartNoAxesCombined,
      "intelligence.assistant",
    ],
    ["ID Card Studio", "/app/id-cards", CreditCard, "members.cards"],
    ["Finance", "/app/finance", Wallet, "finance.self"],
    ["Club Operations", "/app/operations", Briefcase, "operations.view"],
    [
      "Communication Center",
      "/app/communications",
      Mail,
      "communications.view",
    ],
    ["Events & calendar", "/app/events", CalendarDays, "events.view"],
    ["Dashboard", "/app", LayoutDashboard, "organization.view"],
    ["Organizations", "/app/organizations", Network, "organization.view"],
    [
      (identity?.clubTerm || "Club") + " directory",
      "/app/clubs",
      Building2,
      "clubs.view",
    ],
    ["Leadership", "/app/leadership", ShieldCheck, "leadership.view"],
    [
      "Leadership calendar",
      "/app/leadership-calendar",
      CalendarDays,
      "leadership.view",
    ],
    ["Handovers", "/app/handovers", ArrowRightLeft, "leadership.view"],
    ["Committees", "/app/committees", Users, "committees.view"],
    [
      "Governance approvals",
      "/app/governance",
      ClipboardCheck,
      "governance.approve",
    ],
    ["Resolutions", "/app/resolutions", ScrollText, "governance.documents"],
    [
      "Governance documents",
      "/app/governance-documents",
      Briefcase,
      "governance.documents",
    ],
    [
      "Governance configuration",
      "/app/governance-settings",
      SlidersHorizontal,
      "governance.configure",
    ],
    ["Members", "/app/members", Users, "members.view"],
    ["Member directory", "/app/directory", UserRound, "directory.view"],
    ["Applications", "/app/approvals", ClipboardCheck, "members.approve"],
    [
      "Membership operations",
      "/app/transfers",
      ArrowRightLeft,
      "members.transfer.approve",
    ],
    ["Import members", "/app/imports", Upload, "members.import"],
    [
      "Membership settings",
      "/app/membership-settings",
      SlidersHorizontal,
      "members.workflow.manage",
    ],
    ["Administration", "/app/administration", ShieldCheck, "roles.view"],
    ["Audit trail", "/app/audit", ScrollText, "audit.view"],
    ["Settings", "/app/settings", Settings, "settings.view"],
  ] as const;
  if (me.isPending) return <Skeleton />;
  if (me.error)
    return (
      <div className="auth-wrap">
        <h1>Your workspace awaits.</h1>
        <p>Sign in to securely access your organizations.</p>
        <Link className="button" to="/login">
          Sign in
        </Link>
      </div>
    );
  return (
    <Context.Provider value={{ w, me: me.data }}>
      <div
        className="app-shell"
        style={
          {
            "--brand-accent": identity?.accent || w?.color || "#2563eb",
          } as React.CSSProperties
        }
      >
        <aside className={open ? "sidebar open" : "sidebar"}>
          <Link to="/" className="brand">
            <span className="brand-icon">
              <Globe2 size={23} />
            </span>
            <TenantBrand identity={identity} />
            <span className="brand-dot" />
          </Link>
          <div className="sidebar-label">YOUR WORKSPACE</div>
          <div className="workspace-select">
            <span className="workspace-avatar">GC</span>
            <select
              aria-label="Active workspace"
              value={active || me.data?.workspaceId || ""}
              onChange={(e) => activate(e.target.value)}
            >
              <option value="">Choose workspace</option>
              {workspaces.data?.map((x) => (
                <option key={x.id} value={x.id}>
                  {x.organization} · {x.role}
                </option>
              ))}
            </select>
            <ChevronDown size={14} />
          </div>
          <div className="sidebar-label">WORKSPACE</div>
          <nav>
            {menus
              .filter(
                (x) =>
                  !(experience.data?.disabledFeatures || []).some((f: string) =>
                    (
                      ({
                        assistant: ["intelligence.assistant"],
                        knowledge: ["intelligence.knowledge"],
                        workflows: ["intelligence.automations"],
                        events: ["events."],
                        operations: ["operations."],
                        finance: ["finance."],
                      })[f] || []
                    ).some((prefix) => x[3].startsWith(prefix)),
                  ),
              )
              .filter((x) =>
                x[1] === "/app/governance"
                  ? w?.permissions.some((p) =>
                      [
                        "governance.approve",
                        "clubs.manage",
                        "leadership.manage",
                        "committees.manage",
                        "governance.documents",
                      ].includes(p),
                    )
                  : w?.permissions.includes(x[3]),
              )
              .map(([name, to, Icon]) => (
                <NavLink
                  end={to === "/app"}
                  key={to}
                  to={to}
                  onClick={() => setOpen(false)}
                >
                  <Icon size={19} />
                  {name}
                </NavLink>
              ))}
          </nav>
          <div className="sidebar-label">MY COMMUNITY</div>
          <nav>
            <NavLink to="/app/my-id" onClick={() => setOpen(false)}>
              <CreditCard size={18} /> My Digital ID
            </NavLink>
            <NavLink to="/app/my-membership" onClick={() => setOpen(false)}>
              <CreditCard size={18} />
              My membership
            </NavLink>
            <NavLink to="/app/apply" onClick={() => setOpen(false)}>
              <ClipboardCheck size={18} />
              Join a club
            </NavLink>
          </nav>
          <Link className="panel-link" to="/app/my-events">
            My invitations & event passes →
          </Link>
          <Link className="panel-link" to="/app/committees">
            My committee workspaces →
          </Link>
          <details className="upcoming-details">
            <summary className="sidebar-label upcoming-label">
              GROWING TOGETHER <span>UPCOMING</span>
            </summary>
            <div className="upcoming">
              {[[ChartNoAxesCombined, "Reports"]].map(([I, label]: any) => (
                <div key={label}>
                  <I size={18} />
                  {label}
                  <span>↗</span>
                </div>
              ))}
            </div>
          </details>
          <div className="sidebar-bottom">
            <div className="help-card">
              <span className="gold-dot" />
              Built for a world of impact
              <p>
                One connected community.
                <br />
                Limitless possibilities.
              </p>
            </div>
            <Link to="/app/profile" className="profile-mini">
              <span className="avatar">{me.data.name[0]}</span>
              <span>
                <b>{me.data.name}</b>
                <small>{w?.role || "Personal account"}</small>
              </span>
              <Settings size={16} />
            </Link>
          </div>
        </aside>
        <div className="main-column">
          <header className="topbar">
            <button
              className="icon-button mobile-menu"
              aria-label="Toggle menu"
              onClick={() => setOpen(!open)}
            >
              <Menu />
            </button>
            <div className="top-crumb">
              Workspace <span>/</span> <b>{w?.association || "Your account"}</b>
            </div>
            <div className="top-actions">
              <Suspense fallback={null}>
                <SearchLauncher />
              </Suspense>
              <span className="environment">
                <i />
                Connected workspace
              </span>
              <Link
                aria-label="Profile and security"
                to="/app/profile"
                className="avatar small"
              >
                {me.data.name[0]}
              </Link>
              <button
                className="icon-button"
                aria-label={
                  me.data.theme === "dark"
                    ? "Switch to light theme"
                    : "Switch to dark theme"
                }
                onClick={async () => {
                  try {
                    const theme = me.data.theme === "dark" ? "light" : "dark";
                    await api("/users/profile", "PUT", {
                      name: me.data.name,
                      phone: me.data.phone,
                      locale: me.data.locale || "en",
                      timeZone: me.data.timeZone || "UTC",
                      theme,
                    });
                    qc.setQueryData(["me"], { ...me.data, theme });
                  } catch (e) {
                    setError(e);
                  }
                }}
              >
                {me.data.theme === "dark" ? (
                  <Sun size={18} />
                ) : (
                  <Moon size={18} />
                )}
              </button>
              <button
                className="icon-button"
                aria-label="Sign out"
                onClick={async () => {
                  await api("/auth/logout", "POST");
                  qc.clear();
                  nav("/login");
                }}
              >
                <LogOut size={18} />
              </button>
            </div>
          </header>
          <main>
            <ErrorBox error={error} />
            {!w &&
            ![
              "/app/profile",
              "/app/my-membership",
              "/app/my-id",
              "/app/events",
              "/app/my-events",
              "/app/apply",
              "/app/members/",
            ].some((p) => location.pathname.startsWith(p)) ? (
              <div className="welcome-select">
                <Globe2 size={48} />
                <h1>Find your place. Make an impact.</h1>
                <p>
                  Select an authorized workspace in the sidebar to get started.
                </p>
                {workspaces.data?.length === 0 && (
                  <p>
                    Your account is ready. Apply to a club or accept an
                    invitation to start your membership journey.
                  </p>
                )}
                <Link className="button" to="/app/apply">
                  Find a club and apply →
                </Link>
                <Link className="panel-link" to="/app/my-membership">
                  Open my membership portal →
                </Link>
                {me.data.platformAdmin && (
                  <Provision
                    onDone={() =>
                      qc.invalidateQueries({ queryKey: ["workspaces"] })
                    }
                  />
                )}
              </div>
            ) : (
              <Suspense fallback={<Skeleton />}>
                <Outlet key={w?.id} />
              </Suspense>
            )}
          </main>
          <footer>
            Impact International{" "}
            <span>Connected leadership. Meaningful impact.</span>
            <span>Connected community · Phase 08</span>
          </footer>
        </div>
      </div>
    </Context.Provider>
  );
}
export function Provision({ onDone }: any) {
  const [name, setName] = useState(""),
    [error, setError] = useState<unknown>(),
    [busy, setBusy] = useState(false);
  return (
    <form
      className="panel form-stack"
      onSubmit={async (e) => {
        e.preventDefault();
        setBusy(true);
        try {
          await api("/associations", "POST", { name });
          setName("");
          onDone();
        } catch (e) {
          setError(e);
        } finally {
          setBusy(false);
        }
      }}
    >
      <h3>Register an association</h3>
      <label>
        Association name
        <input
          required
          minLength={3}
          value={name}
          onChange={(e) => setName(e.target.value)}
        />
      </label>
      <ErrorBox error={error} />
      <button disabled={busy}>Create association</button>
    </form>
  );
}
function Landing() {
  return (
    <div className="landing">
      <header>
        <Link className="brand" to="/">
          <Globe2 />
          Impact International
        </Link>
        <nav>
          <a href="#possibilities">Possibilities</a>
          <Link to="/login">Sign in</Link>
          <Link className="button" to="/signup">
            Get started <ArrowUpRight size={16} />
          </Link>
        </nav>
      </header>
      <section className="landing-hero">
        <div className="eyebrow">ONE PLATFORM. A WORLD OF POSSIBILITY.</div>
        <h1>
          Great communities.
          <br />
          <em>Greater impact.</em>
        </h1>
        <p>
          The connected home for your clubs, chapters, and associations.
          <br />
          Bring your people together. Give your leadership room to grow.
        </p>
        <div className="hero-actions">
          <Link className="button" to="/signup">
            Join your community <ArrowUpRight size={17} />
          </Link>
          <Link to="/request-organization">
            Request organization registration →
          </Link>
        </div>
        <div className="orbit">
          <Globe2 size={160} strokeWidth={0.6} />
          <span className="orbit-tag a">◉ Connected communities</span>
          <span className="orbit-tag b">↗ Leadership without borders</span>
        </div>
      </section>
      <section id="possibilities" className="landing-features">
        {[
          [
            "01",
            "Every structure. One home.",
            "Build a hierarchy that reflects your association, from international headquarters to your local club.",
          ],
          [
            "02",
            "The right access. Everywhere.",
            "Work confidently with organizational scopes, secure sessions, and permission-based access.",
          ],
          [
            "03",
            "Less administration. More impact.",
            "Find your organizations, manage your clubs, and switch workspaces in one connected experience.",
          ],
        ].map(([n, t, d]) => (
          <article key={n}>
            <span>{n}</span>
            <h2>{t}</h2>
            <p>{d}</p>
          </article>
        ))}
      </section>
      <footer>
        © {new Date().getFullYear()} Impact International · Independent
        community management platform
      </footer>
    </div>
  );
}
export default function App() {
  return (
    <Suspense fallback={<Skeleton />}>
      <Routes>
        <Route path="/verify/id/:token" element={<IdVerification />} />
        <Route
          path="/verify/certificates/:token"
          element={<VerifyCertificate />}
        />
        <Route path="/event/:id" element={<PublicEvent />} />
        <Route path="/event-invitation" element={<EventInvitation />} />
        <Route path="/" element={<Landing />} />
        <Route path="/join" element={<JoinInvitation />} />
        <Route path="/verify" element={<VerifyMembershipCard />} />
        <Route path="/request-organization" element={<RequestOrganization />} />
        {[
          "login",
          "signup",
          "forgot-password",
          "resend-verification",
          "complete",
        ].map((p) => (
          <Route key={p} path={"/" + p} element={<Auth mode={p} />} />
        ))}
        <Route path="/app" element={<Shell />}>
          <Route index element={<WorkspaceHome />} />
          <Route path="members" element={<Members />} />
          <Route path="directory" element={<Members directory />} />
          <Route path="members/:id" element={<MemberProfile />} />
          <Route path="my-membership" element={<MemberPortal />} />
          <Route path="apply" element={<MembershipApplications />} />
          <Route path="approvals" element={<MembershipApplications review />} />
          <Route path="imports" element={<MemberImports />} />
          <Route path="membership-insights" element={<MembershipInsights />} />
          <Route path="membership-settings" element={<MembershipSettings />} />
          <Route path="transfers" element={<MembershipOperations />} />
          <Route path="organizations" element={<Organizations />} />
          <Route path="intelligence/*" element={<Intelligence />} />
          <Route path="id-cards/*" element={<IdCards />} />
          <Route path="my-id" element={<MyIdCard />} />
          <Route path="finance/*" element={<Finance />} />
          <Route path="operations/*" element={<Operations />} />
          <Route path="communications/*" element={<Communications />} />
          <Route path="events" element={<Events />} />
          <Route path="events/new" element={<EventWizard />} />
          <Route path="events/:id" element={<EventWorkspace />} />
          <Route path="events/:id/checkin" element={<EventCheckin />} />
          <Route path="my-events" element={<MyEvents />} />
          <Route path="event-settings" element={<EventSettings />} />
          <Route path="event-intelligence" element={<EventDashboard />} />
          <Route path="clubs" element={<ClubDirectory />} />
          <Route path="clubs/:id" element={<ClubWorkspace />} />
          <Route path="club-administration" element={<GovernanceDashboard />} />
          <Route path="leadership" element={<LeadershipDirectory />} />
          <Route path="leadership-calendar" element={<LeadershipCalendar />} />
          <Route path="handovers" element={<LeadershipHandovers />} />
          <Route path="committees" element={<CommitteeDirectory />} />
          <Route path="committees/:id" element={<CommitteeWorkspace />} />
          <Route path="governance" element={<GovernanceApprovals />} />
          <Route path="governance-settings" element={<GovernanceSettings />} />
          <Route
            path="governance-documents"
            element={<GovernanceDocuments />}
          />
          <Route path="resolutions" element={<GovernanceResolutions />} />
          <Route path="foundation-overview" element={<Dashboard />} />
          <Route path="administration" element={<Administration />} />
          <Route path="settings" element={<SettingsPage />} />
          <Route path="audit" element={<Audit />} />
          <Route path="profile" element={<Profile />} />
        </Route>
        <Route
          path="*"
          element={
            <div className="auth-wrap">
              <h1>Page not found</h1>
              <Link to="/">Return home</Link>
            </div>
          }
        />
      </Routes>
    </Suspense>
  );
}
