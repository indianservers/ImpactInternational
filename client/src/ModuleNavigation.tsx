import { Link, useLocation, useNavigate } from "react-router-dom";
import "./mobile.css";

type Item = readonly [string, string, string?];
export default function ModuleNavigation({ root, label, items, permissions, groups }: {
  root: string; label: string; items: Item[]; permissions?: string[];
  groups: Record<string, string[]>;
}) {
  const { pathname } = useLocation(), navigate = useNavigate();
  const available = items.filter(([, , permission]) => !permission || permissions?.includes(permission));
  const current = available.find(([path]) => path && (pathname === `${root}/${path}` || pathname.startsWith(`${root}/${path}/`))) || available.find(([path]) => root === "/app/id-cards" && pathname.startsWith(`${root}/designer/`) && path === "templates") || available.find(([path]) => !path) || available[0];
  const visibleGroups = Object.entries(groups).map(([name, paths]) => ({ name, items: available.filter(([path]) => paths.includes(path)) })).filter(g => g.items.length);
  const active = visibleGroups.find(g => g.items.some(([path]) => path === current?.[0]));
  const to = (path: string) => root + (path ? `/${path}` : "");
  return <section className="module-navigation" aria-label={`${label} navigation`}>
    <nav className="section-tabs" aria-label={`${label} tasks`}>
      {visibleGroups.map(group => <Link key={group.name} to={to(group.items[0][0])}
        className={group.name === active?.name ? "active" : undefined}
        aria-current={group.name === active?.name ? "true" : undefined}>{group.name}</Link>)}
    </nav>
    {active && active.items.length > 1 && <label className="tool-picker">
      <span>{active.name}</span>
      <select aria-label={`${label} tool`} value={current?.[0] || ""} onChange={e => navigate(to(e.target.value))}>
        {active.items.map(([path, name]) => <option key={path} value={path}>{name}</option>)}
      </select>
    </label>}
  </section>;
}
