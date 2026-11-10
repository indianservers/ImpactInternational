import { lazy, Suspense, useState, createContext, useContext } from "react";
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
import { api, type Workspace } from "./api";
const RequestOrganization = lazy(() => import("./pages/RequestOrganization"));
const Dashboard = lazy(() => import("./pages/Dashboard"));
const Organizations = lazy(() => import("./pages/Organizations"));
const Administration = lazy(() => import("./pages/Administration"));
const SettingsPage = lazy(() => import("./pages/Settings"));
const Audit = lazy(() => import("./pages/Audit"));
const Profile = lazy(() => import("./pages/Profile"));
const Auth = lazy(() => import("./pages/Auth"));
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
  const workspaces = useQuery({
    queryKey: ["workspaces"],
    queryFn: () => api<Workspace[]>("/workspaces"),
    enabled: !!me.data,
  });
  const w = workspaces.data?.find(
    (x) => x.id === (active || me.data?.workspaceId),
  );
  async function activate(id: string) {
    try {
      await api("/workspaces/" + id + "/activate", "POST");
      setActive(id);
      qc.removeQueries({
        predicate: (q) => !["me", "workspaces"].includes(String(q.queryKey[0])),
      });
      setError(null);
    } catch (e) {
      setError(e);
    }
  }
  const menus = [
    ["Dashboard", "/app", LayoutDashboard, "organization.view"],
    ["Organizations", "/app/organizations", Network, "organization.view"],
    ["Club directory", "/app/clubs", Building2, "organization.view"],
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
      <div className="app-shell">
        <aside className={open ? "sidebar open" : "sidebar"}>
          <Link to="/" className="brand">
            <span className="brand-icon">
              <Globe2 size={23} />
            </span>
            Club<span className="brand-light">Sphere</span>
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
              .filter((x) => w?.permissions.includes(x[3]))
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
          <div className="sidebar-label upcoming-label">
            GROWING TOGETHER <span>UPCOMING</span>
          </div>
          <div className="upcoming">
            {[
              [Users, "Members"],
              [CalendarDays, "Events & meetings"],
              [Mail, "Communication"],
              [Briefcase, "Service projects"],
              [Wallet, "Finance"],
              [ChartNoAxesCombined, "Reports"],
            ].map(([I, label]: any) => (
              <div key={label}>
                <I size={18} />
                {label}
                <span>↗</span>
              </div>
            ))}
          </div>
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
            {!w && location.pathname !== "/app/profile" ? (
              <div className="welcome-select">
                <Globe2 size={48} />
                <h1>Find your place. Make an impact.</h1>
                <p>
                  Select an authorized workspace in the sidebar to get started.
                </p>
                {workspaces.data?.length === 0 && (
                  <p>
                    Your account is ready. Ask an association administrator to
                    assign your organizational role.
                  </p>
                )}
                <Link to="/app/profile">Profile & security</Link>
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
            ClubSphere <span>Connected leadership. Meaningful impact.</span>
            <span>Foundation · Phase 01</span>
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
          ClubSphere
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
        © {new Date().getFullYear()} ClubSphere · Independent community
        management platform
      </footer>
    </div>
  );
}
export default function App() {
  return (
    <Suspense fallback={<Skeleton />}>
      <Routes>
        <Route path="/" element={<Landing />} />
        <Route path="/request-organization" element={<RequestOrganization />} />
        {["login", "signup", "forgot-password", "complete"].map((p) => (
          <Route key={p} path={"/" + p} element={<Auth mode={p} />} />
        ))}
        <Route path="/app" element={<Shell />}>
          <Route index element={<Dashboard />} />
          <Route path="organizations" element={<Organizations />} />
          <Route path="clubs" element={<Organizations clubs />} />
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
