import PageSections from '../PageSections';
import { lazy, Suspense, useEffect, useRef, useState } from "react";
import { Link, useParams, useSearchParams } from "react-router-dom";
import { useQuery } from "@tanstack/react-query";
import {
  Activity,
  ArrowUpRight,
  BarChart3,
  CalendarDays,
  ChartNoAxesCombined,
  ChevronRight,
  Download,
  Filter,
  Globe2,
  Layers,
  Lightbulb,
  Network,
  RefreshCw,
  Share2,
  ShieldCheck,
  Target,
  TrendingUp,
  Users,
  Wallet,
  Waypoints,
} from "lucide-react";
import { api } from "../api";
import { ErrorBox, Skeleton, useWorkspace } from "../App";
import {
  CohortHeatmap,
  RetentionCurves,
  Distribution,
  Empty,
  FlowChart,
  GrowthChart,
} from "./analytics/Charts";
import { exportCharts, saveFile } from "./analytics/utils";
import "./Members.css";
import "./MembershipInsights.css";
const GeoExplorer = lazy(() =>
  import("./analytics/Explorers").then((m) => ({ default: m.GeoExplorer })),
);
const HierarchyExplorer = lazy(() =>
  import("./analytics/Explorers").then((m) => ({
    default: m.HierarchyExplorer,
  })),
);
const ForecastChart = lazy(() =>
  import("./analytics/Explorers").then((m) => ({ default: m.ForecastChart })),
);
const tabs = [
  ["overview", "Overview", BarChart3],
  ["growth", "Growth", TrendingUp],
  ["organization", "Organization", Network],
  ["cohorts", "Cohorts & Retention", Users],
  ["lifecycle", "Lifecycle", Waypoints],
  ["renewals", "Renewals", CalendarDays],
  ["categories", "Categories", Layers],
  ["geography", "Geography", Globe2],
  ["engagement", "Engagement", Activity],
  ["forecasting", "Forecasting", ChartNoAxesCombined],
  ["actions", "Key Insights", Lightbulb],
  ["network", "Network", Network],
] as const;
const levels = ["National", "Region", "District", "Zone", "Club"];
const periods = [
  ["7d", "Last 7 days"],
  ["30d", "Last 30 days"],
  ["90d", "Last 90 days"],
  ["quarter", "Current quarter"],
  ["previous-quarter", "Previous quarter"],
  ["year", "Current year"],
  ["previous-year", "Previous year"],
  ["all", "All time"],
  ["custom", "Custom range"],
];
const fmt = (n: any) =>
  n == null
    ? "—"
    : Number(n).toLocaleString(undefined, { maximumFractionDigits: 1 });
const percent = (n: number, d: number) =>
  d ? +((100 * n) / d).toFixed(1) : null;
export function Panel({
  title,
  description,
  children,
  action,
  className = "",
}: any) {
  return (
    <section className={"a-panel " + className}>
      <header>
        <div>
          <h2>{title}</h2>
          {description && <p>{description}</p>}
        </div>
        {action}
      </header>
      <div className="a-panel-body">{children}</div>
    </section>
  );
}
function UnitFilter({ kind, parentId, value, onChange }: any) {
  const { w } = useWorkspace();
  const q = useQuery({
    queryKey: ["analytics-location", w?.id, kind, parentId],
    queryFn: () => api('/membership/hierarchy?'+new URLSearchParams({kind,...parentId?{parentId}:{}})),
    enabled: kind === "National" || !!parentId,
    staleTime: 60000,
  });
  return (
    <label>
      {kind === "National" ? "National unit" : kind}
      <select
        aria-label={"Analytics " + kind}
        value={value}
        disabled={(kind !== "National" && !parentId) || q.isPending}
        onChange={(e) => onChange(e.target.value)}
      >
        <option value="">
          {kind !== "National" && !parentId
            ? "Choose parent first"
            : "All authorized " + kind.toLowerCase() + " units"}
        </option>
        {q.data?.items?.map((r: any) => (
          <option key={r.id} value={r.id}>
            {r.name}
          </option>
        ))}
      </select>
      {q.error && <small>Could not load units.</small>}
      {q.data?.total > 100 && (
        <small>Narrow the parent unit to see its first 100 children.</small>
      )}
    </label>
  );
}
export default function MembershipInsights() {
  const { w } = useWorkspace(),
    { workspace = "overview" } = useParams();
  const [params, setParams] = useSearchParams(),
    [filtersOpen, setFiltersOpen] = useState(false),
    [notice, setNotice] = useState(""),
    [orgLevel, setOrgLevel] = useState("Region"),
    [distributionMode, setDistributionMode] = useState("donut"),
    [dimension, setDimension] = useState("category"),
    [horizon, setHorizon] = useState(6),
    [selected, setSelected] = useState<string[]>([]),
    [orgView, setOrgView] = useState("ranking"),
    [weights, setWeights] = useState({
      growth: 20,
      retention: 30,
      engagement: 20,
      officers: 15,
      renewals: 15,
    });
  const page = useRef<HTMLDivElement>(null);
  const forceRefresh = useRef(false);
  const filter = params.toString();
  const [queryFilter, setQueryFilter] = useState(filter);
  useEffect(() => {
    const timer = setTimeout(() => setQueryFilter(filter), 200);
    return () => clearTimeout(timer);
  }, [filter]);
  const change = (key: string, value: string) => {
    setParams((p) => {
      const n = new URLSearchParams(p);
      if(value)n.set(key,value);else n.delete(key);
      const idx = levels.findIndex((l) => l.toLowerCase() + "Id" === key);
      if (idx >= 0)
        levels.slice(idx + 1).forEach((l) => n.delete(l.toLowerCase() + "Id"));
      return n;
    });
  };
  const q = useQuery({
    queryKey: ["membership-analytics", w?.id, workspace, queryFilter],
    queryFn: () => {
      const fresh=forceRefresh.current;forceRefresh.current=false;
      return api(
        "/membership/analytics?" +
          new URLSearchParams({
            ...Object.fromEntries(new URLSearchParams(queryFilter)),
            workspace,
            ...(fresh?{refresh:'1'}:{}),
          }),
      );
    },
    enabled: !!w && w.permissions.includes("members.view"),
    staleTime: 15000,
    refetchInterval: 30000,
  });
  const options = useQuery({
    queryKey: ["analytics-reference", w?.id],
    queryFn: () => api("/membership/options"),
    enabled: !!w && w.permissions.includes("members.view"),
    staleTime: 60000,
  });
  const d = q.data;
  const acquisitionTrend:number[]=[];let acquired=0,trendYear='';
  for(const point of d?.trend||[]){const year=point.date.slice(0,4);if(year!==trendYear){trendYear=year;acquired=0;}acquired+=point.new;acquisitionTrend.push(acquired);}
  const organizations: any[] = d?.organizations || [];
  const grouped = (key: string) =>
    (d?.distributions || []).filter((r: any) => r.dimension === key);
  const crossUnit = (o: any) => {
    if (!o || !levels.includes(o.level)) return;
    setParams((p) => {
      const n = new URLSearchParams(p);
      levels.forEach((l) => n.delete(l.toLowerCase() + "Id"));
      let unit = o;
      while (unit) {
        if (levels.includes(unit.level))
          n.set(unit.level.toLowerCase() + "Id", unit.id);
        unit = organizations.find((parent) => parent.id === unit.parentId);
      }
      return n;
    });
  };
  const membersLink = (extra: Record<string, string> = {}) =>
    "/app/members?" +
    new URLSearchParams({
      ...Object.fromEntries(
        Array.from(params.entries()).filter(([k]) =>
          [
            "nationalId",
            "country",
            "state",
            "status",
            "regionId",
            "districtId",
            "zoneId",
            "clubId",
            "categoryId",
            "membershipType",
            "joinedFrom",
            "joinedTo",
            "renewalFrom",
            "renewalTo",
          ].includes(k),
        ),
      ),
      joinedTo: params.get("joinedTo") || d?.to || "",
      asOf: d?.to || "",
      ...extra,
    });
  const onPoint = (point: any) => {
    const start = new Date(point.date + "T00:00:00Z"),
      end = new Date(start),
      bucket = params.get("bucket") || "month";
    if (bucket === "week") end.setUTCDate(end.getUTCDate() + 6);
    else if (bucket !== "day") {
      end.setUTCMonth(
        end.getUTCMonth() +
          (bucket === "quarter" ? 3 : bucket === "year" ? 12 : 1),
      );
      end.setUTCDate(end.getUTCDate() - 1);
    }
    setParams((p) => {
      const n = new URLSearchParams(p);
      n.set("joinedFrom", point.date);
      n.set("joinedTo", end.toISOString().slice(0, 10));
      return n;
    });
    setNotice(
      "Filtered to joins in the selected bucket. Open member records for details.",
    );
  };
  const selectedOrgs = organizations
      .filter(
        (o) =>
          o.level === orgLevel && (!selected.length || selected.includes(o.id)),
      )
      .sort((a, b) => b.count - a.count),
    clubs = organizations.filter((o) => o.level === "Club");
  const retention = d ? percent(d.summary.retained, d.summary.opening) : null;
  const health = (o: any) => {
    const officer = d?.officers?.find((r: any) => r.id === o.id),
      engagement = d?.engagement?.clubs?.find((r: any) => r.id === o.id);
    const metrics: any = {
      growth:
        o.previous > 0
          ? Math.max(
              0,
              Math.min(100, 50 + (50 * (o.joined - o.previous)) / o.previous),
            )
          : null,
      retention: o.opening > 0 ? percent(o.retained, o.opening) : null,
      engagement:
        engagement && o.count
          ? percent(engagement.participants, o.count)
          : null,
      officers: officer?.required
        ? Math.min(100, (100 * officer.filled) / officer.required)
        : null,
      renewals:
        o.eligible > 0 ? Math.min(100, (100 * o.renewed) / o.eligible) : null,
    };
    const available = Object.keys(weights).filter(
        (k) => metrics[k] != null && (weights as any)[k] > 0,
      ),
      denominator = available.reduce((n, k) => n + (weights as any)[k], 0),
      all = Object.values(weights).reduce((a, b) => a + b, 0);
    return {
      score: denominator
        ? available.reduce((n, k) => n + metrics[k] * (weights as any)[k], 0) /
          denominator
        : null,
      coverage: all ? (denominator / all) * 100 : 0,
      available,
    };
  };
  const insights = (() => {
    if (!d) return [];
    const list: any[] = [],
      s = d.summary;
    if (s.opening && s.active < s.opening)
      list.push({
        severity: "High",
        title: "Opening membership declined",
        detail: `${s.active - s.opening} active people against ${s.opening} opening active people.`,
        action: "Review memberships",
        to: membersLink(),
      });
    if (!s.opening)
      list.push({
        severity: "Information",
        title: "Build your historical baseline",
        detail:
          "No opening active cohort is recorded. Retention and opening growth percentages are unavailable.",
        action: "Explore observed cohorts",
        to: "/app/membership-insights/cohorts?" + filter,
      });
    const due = d.renewals.windows.find((x: any) => x.days === 30)?.count || 0;
    if (due)
      list.push({
        severity: "Attention",
        title: `${due} affiliations expire in 30 days`,
        detail: `Upcoming validity end dates after ${d.to}.`,
        action: "Review renewals",
        to: "/app/membership-insights/renewals?" + filter,
      });
    const top = organizations
      .filter((o) => o.level === "Region")
      .sort((a, b) => b.count - a.count)[0];
    if (top?.count)
      list.push({
        severity: "Information",
        title: "Largest regional community",
        detail: `${top.name}: ${fmt(top.count)} distinct scoped people.`,
        action: "Compare regions",
        to: "/app/membership-insights/organization?" + filter,
      });
    const vacancies = d.officers?.reduce(
      (n: number, o: any) => n + Math.max(0, o.required - o.filled),
      0,
    );
    if (vacancies)
      list.push({
        severity: "Attention",
        title: `${vacancies} mandatory office vacancies`,
        detail:
          "Mandatory positions without an active appointment at the selected boundary.",
        action: "Review coverage",
        to: "/app/leadership",
      });
    if (s.unknownHistory)
      list.push({
        severity: "Attention",
        title: "Historical coverage needs attention",
        detail: `${s.unknownHistory} affiliations lack a recorded snapshot at the selected boundary.`,
        action: "Inspect lifecycle",
        to: "/app/membership-insights/lifecycle?" + filter,
      });
    if (d.trend.length >= 4) {
      const history = d.trend.slice(0, -1).map((r: any) => r.new),
        mean =
          history.reduce((a: number, b: number) => a + b, 0) / history.length,
        sd = Math.sqrt(
          history.reduce((n: number, x: number) => n + (x - mean) ** 2, 0) /
            history.length,
        ),
        latest = d.trend.at(-1);
      if (sd > 0 && Math.abs(latest.new - mean) > 3 * sd)
        list.push({
          severity: "Attention",
          title: "Unusual acquisition volume",
          detail: `${latest.date}: ${latest.new} joins; prior bucket mean ${mean.toFixed(1)}. Outside a three-standard-deviation threshold; partial periods may affect comparison.`,
          action: "Explore acquisition",
          to: "/app/membership-insights/growth?" + filter,
        });
    }
    return list;
  })();
  async function download(format = "csv") {
    try {
      const r = await fetch(
        "/api/v1/membership/analytics/export?" +
          new URLSearchParams({
            ...Object.fromEntries(new URLSearchParams(queryFilter)),
            workspace,
            format,
          }),
        { credentials: "same-origin" },
      );
      if (!r.ok) throw Error((await r.json()).detail || "Export failed.");
      saveFile(await r.blob(), "impact-membership-analytics." + format);
      setNotice("Filtered analytical data exported.");
    } catch (e: any) {
      setNotice(e.message);
    }
  }
  const link = (tab: string) =>
    "/app/membership-insights/" + tab + "?" + filter;
  const renewalFilter = (days: number) => {
    const start = new Date(d.renewals.from + "T00:00:00Z"),
      end = new Date(start);
    end.setUTCDate(end.getUTCDate() + days - 1);
    setParams((p) => {
      const n = new URLSearchParams(p);
      n.set("renewalFrom", start.toISOString().slice(0, 10));
      n.set("renewalTo", end.toISOString().slice(0, 10));
      return n;
    });
  };
  if (!w?.permissions.includes("members.view"))
    return (
      <Empty text="Administrative membership analytics are not available for this login." />
    );
  return (
    <div className="a-center" ref={page}>
      <header className="a-heading">
        <div className="a-heading-title">
          <span>
            <ChartNoAxesCombined size={25} />
          </span>
          <div>
            <div className="a-eyebrow">
              IMPACT INTERNATIONAL / MEMBERS INTELLIGENCE
            </div>
            <h1>Membership Intelligence & Analytics Center</h1>
            <p>
              A 360° view of growth, retention, engagement and organizational
              health.
            </p>
          </div>
        </div>
        <div className="a-heading-tools">
          <span className="a-organization">
            Impact International <ShieldCheck size={14} />
          </span>
          <label className="a-period">
            <CalendarDays size={15} />
            <select
              aria-label="Analytics date period"
              value={params.get("period") || "year"}
              onChange={(e) => change("period", e.target.value)}
            >
              {periods.map(([v, l]) => (
                <option key={v} value={v}>
                  {l}
                </option>
              ))}
            </select>
          </label>
          <button
            className="a-icon"
            aria-label="Refresh analytics"
            onClick={() => {
              forceRefresh.current = true;
              q.refetch();
            }}
          >
            <RefreshCw size={17} />
          </button>
          {d?.capabilities.export && (
            <button
              className="a-icon"
              aria-label="Export filtered analytical data"
              onClick={() => download()}
            >
              <Download size={17} />
            </button>
          )}
          <button
            className="a-icon"
            aria-label="Copy analytics view link"
            onClick={async () => {
              try {
                await navigator.clipboard.writeText(window.location.href);
                setNotice(
                  "View link copied. Recipients must sign in and use their own authorized scope.",
                );
              } catch {
                setNotice("Copy this page’s URL to share the filter view.");
              }
            }}
          >
            <Share2 size={17} />
          </button>
        </div>
      </header>
      <nav className="a-tabs" aria-label="Membership analytics workspaces">
        {tabs.map(([key, label, Icon]) => (
          <Link
            key={key}
            to={
              "/app/membership-insights" +
              (key === "overview" ? "" : "/" + key) +
              (filter ? "?" + filter : "")
            }
            className={workspace === key ? "active" : ""}
            aria-current={workspace === key ? "page" : undefined}
          >
            <Icon size={15} />
            {label}
          </Link>
        ))}
      </nav>
      <div className="a-filter-bar">
        <button
          className="a-filter-toggle"
          aria-expanded={filtersOpen}
          onClick={() => setFiltersOpen((p) => !p)}
        >
          <Filter size={14} />
          Filters{params.size > 0 && <b>{params.size}</b>}
        </button>
        <div className="a-breadcrumb">
          <span>
            {d?.from || "…"} — {d?.to || "…"}
          </span>
          {Array.from(params.entries())
            .filter(([k]) => !["period", "bucket", "from", "to"].includes(k))
            .map(([k, v]) => (
              <button
                key={k}
                onClick={() => change(k, "")}
                title="Remove filter"
              >
                {k.replace("Id", "")}:{" "}
                {organizations.find((o) => o.id === v)?.name ||
                  options.data?.categories?.find((c: any) => c.id === v)
                    ?.name ||
                  v}
                <span>×</span>
              </button>
            ))}
        </div>
        <button
          className="a-link"
          onClick={() => {
            setParams({});
            setSelected([]);
          }}
        >
          Reset all
        </button>
        <label className="a-interval">
          Aggregation
          <select
            aria-label="Analytics aggregation interval"
            value={params.get("bucket") || "month"}
            onChange={(e) => change("bucket", e.target.value)}
          >
            {["day", "week", "month", "quarter", "year"].map((v) => (
              <option key={v}>{v}</option>
            ))}
          </select>
        </label>
      </div>
      {(filtersOpen || params.get("period") === "custom") && (
        <section className="a-filters" aria-label="Analytics filters">
          {levels.map((l, i) => (
            <UnitFilter
              key={l}
              kind={l}
              value={params.get(l.toLowerCase() + "Id") || ""}
              parentId={
                i ? params.get(levels[i - 1].toLowerCase() + "Id") || "" : ""
              }
              onChange={(v: string) => change(l.toLowerCase() + "Id", v)}
            />
          ))}
          <label>
            Category
            <select
              aria-label="Analytics category"
              value={params.get("categoryId") || ""}
              onChange={(e) => change("categoryId", e.target.value)}
            >
              <option value="">All categories</option>
              {options.data?.categories?.map((c: any) => (
                <option key={c.id} value={c.id}>
                  {c.name}
                </option>
              ))}
            </select>
          </label>
          <label>
            Membership type
            <select
              value={params.get("membershipType") || ""}
              onChange={(e) => change("membershipType", e.target.value)}
            >
              <option value="">All club types</option>
              {options.data?.membershipTypes?.map((v: string) => (
                <option key={v}>{v}</option>
              ))}
            </select>
          </label>
          <label>
            Status
            <select
              value={params.get("status") || ""}
              onChange={(e) => change("status", e.target.value)}
            >
              <option value="">All statuses</option>
              {[
                "Active",
                "Inactive",
                "Suspended",
                "Transferred",
                "Expired",
                "Resigned",
                "Terminated",
                "Deceased",
              ].map((v) => (
                <option key={v}>{v}</option>
              ))}
            </select>
          </label>
          {["country", "state"].map((k) => (
            <label key={k}>
              {k === "country" ? "Country" : "State / province"}
              <input
                aria-label={"Analytics " + k}
                value={params.get(k) || ""}
                onChange={(e) => change(k, e.target.value)}
                placeholder="Exact recorded name"
              />
            </label>
          ))}
          {[
            ["from", "Period from"],
            ["to", "Period to"],
            ["joinedFrom", "Joined from"],
            ["joinedTo", "Joined to"],
            ["renewalFrom", "Renewal from"],
            ["renewalTo", "Renewal to"],
          ].map(([k, l]) => (
            <label key={k}>
              {l}
              <input
                type="date"
                value={params.get(k) || ""}
                onChange={(e) => {
                  const value = e.target.value;
                  setParams((p) => {
                    const n = new URLSearchParams(p);
                    if(value)n.set(k,value);else n.delete(k);
                    if (k === "from" || k === "to") n.set("period", "custom");
                    return n;
                  });
                }}
              />
            </label>
          ))}
        </section>
      )}
      {notice && (
        <div className="a-notice" role="status">
          {notice}
          <button
            aria-label="Dismiss analytics message"
            onClick={() => setNotice("")}
          >
            ×
          </button>
        </div>
      )}
      <ErrorBox error={q.error} />
      {q.isPending ? (
        <Skeleton />
      ) : (
        d && (
          <>
            <div
              className="a-data-context"
              aria-busy={q.isFetching || queryFilter !== filter}
            >
              <span>
                <i />
                {q.isFetching || queryFilter !== filter
                  ? "Updating filtered records…"
                  : "Scoped records · cache up to 20s"}
              </span>
              <span>
                As of {new Date(d.asOf).toLocaleString()} · {d.timeZone}
              </span>
              <span>
                {fmt(d.summary.total)} people · {fmt(d.summary.affiliations)}{" "}
                affiliations
              </span>
              <Link to={membersLink()}>
                Open member records <ArrowUpRight size={12} />
              </Link>
            </div>
            {workspace === "overview" && (
              <>
                <div className="a-kpis">
                  {[
                    [
                      "Total Members",
                      d.summary.total,
                      Users,
                      "Distinct people in selected scope",
                    ],
                    [
                      "Active Members",
                      d.summary.active,
                      Users,
                      "Active, unexpired at period end",
                    ],
                    [
                      "New This Month",
                      d.summary.monthNew,
                      TrendingUp,
                      "First scoped join · observed month",
                    ],
                    [
                      "Joined This Year",
                      d.summary.yearNew,
                      CalendarDays,
                      "First scoped join · observed year",
                    ],
                    [
                      "Renewals Due",
                      d.renewals.windows.find((r: any) => r.days === 30)
                        ?.count || 0,
                      Wallet,
                      "Affiliations due within 30 days",
                    ],
                    [
                      "Pending Applications",
                      d.summary.pendingApplications,
                      Layers,
                      "Current backlog · drafts excluded",
                    ],
                  ].map(([label, value, Icon, hint]: any, i) => (
                    <KpiDestination
                      className="a-kpi"
                      key={label}
                      to={
                        i === 4
                          ? link("renewals")
                          : i === 5
                            ? (w?.permissions.includes("members.approve") ? "/app/approvals?status=Pending" : undefined)
                            : membersLink(i === 1 ? { status: "Active" } : {})
                      }
                    >
                      <div>
                        <Icon size={19} />
                        <span>{label}</span>
                      </div>
                      <strong>{fmt(value)}</strong>
                      <small>{hint}</small>
                      <small>{i===0?`Opening population: ${fmt(d.summary.total-d.summary.new)}`:i===1?`Opening active cohort: ${fmt(d.summary.opening)}`:i===2&&(params.get('bucket')||'month')==='month'&&d.trend.length>1?`Prior bucket joins: ${fmt(d.trend.at(-2).new)}`:i===4?'Upcoming windows: 7–90 days':'Prior comparison unavailable'}</small>
                      {i!==5&&<svg viewBox="0 0 130 38" aria-hidden="true">
                        <title>{i===4?'Upcoming cumulative expiry windows':i===3?'Cumulative scoped joins within each observed year':'Recorded membership history'}</title>
                        <path
                          d={sparkline(
                            i===4?d.renewals.windows.map((r:any)=>r.count):i===3?acquisitionTrend:d.trend.map((t: any) => i === 1 ? t.active : i === 2 ? t.new : t.total),
                          )}
                          fill="none"
                          stroke={i % 2 ? "#55ae9c" : "#9a77e9"}
                          strokeWidth="1.8"
                        />
                      </svg>}
                    </KpiDestination>
                  ))}
                </div>
                <PageSections id="membership-summary" perPage={1} labels={["Footprint & Growth","Membership Details"]}><div className="a-overview-main">
                  <Panel
                    title="Global Membership Footprint"
                    className="a-footprint"
                    description="One community. Connected across every level."
                    action={
                      <Link to={link("geography")}>
                        Explore <ArrowUpRight size={13} />
                      </Link>
                    }
                  >
                    <Suspense fallback={<Skeleton />}>
                      <GeoExplorer
                        countries={grouped("country")}
                        organizations={organizations}
                        onCountry={(v) => change("country", v)}
                        globe
                      />
                    </Suspense>
                    <div className="a-footprint-counts">
                      {levels.map((l) => (
                        <div key={l}>
                          <Network size={13} />
                          <b>
                            {fmt(
                              organizations.filter((o) => o.level === l).length,
                            )}
                          </b>
                          <span>
                            {l === "National" ? "National unit" : l + "s"}
                          </span>
                        </div>
                      ))}
                    </div>
                  </Panel>
                  <Panel
                    title="Membership Growth & Trends"
                    description="Recorded people and scoped first joins"
                    action={
                      <Link to={link("growth")}>
                        Growth explorer <ArrowUpRight size={13} />
                      </Link>
                    }
                  >
                    <GrowthChart data={d.trend} onSelect={onPoint} compact />
                  </Panel>
                  <div className="a-vitals">
                    {[
                      [
                        "Growth vs opening cohort",
                        d.summary.opening
                          ? (
                              ((d.summary.active - d.summary.opening) /
                                d.summary.opening) *
                              100
                            ).toFixed(1) + "%"
                          : "—",
                        TrendingUp,
                        d.summary.opening
                          ? "Change in active people"
                          : "No opening baseline",
                      ],
                      [
                        "Retention Rate",
                        retention == null ? "—" : retention + "%",
                        Target,
                        d.summary.opening
                          ? `${d.summary.retained} / ${d.summary.opening} opening people`
                          : "Opening cohort unavailable",
                      ],
                      [
                        "Average Tenure",
                        fmt(d.summary.tenureYears) + " years",
                        CalendarDays,
                        "Since first scoped membership",
                      ],
                    ].map(([title, value, Icon, hint]: any) => (
                      <section key={title}>
                        <Icon size={24} />
                        <div>
                          <small>{title}</small>
                          <strong>{value}</strong>
                          <span>{hint}</span>
                        </div>
                      </section>
                    ))}
                  </div>
                </div>
                <PageSections id="membership-overview" className="a-overview-grid" perPage={2}>
                  <Panel
                    title="Membership by Category"
                    action={
                      <Link to={link("categories")}>
                        <ArrowUpRight size={14} />
                      </Link>
                    }
                  >
                    <Distribution
                      data={grouped("category")}
                      onSelect={(r) => change("categoryId", r.id)}
                    />
                  </Panel>
                  <Panel
                    title="Membership Lifecycle"
                    description="Actual status-transition events"
                    action={
                      <Link to={link("lifecycle")}>
                        <ArrowUpRight size={14} />
                      </Link>
                    }
                  >
                    <FlowChart data={d.flows || []} />
                  </Panel>
                  <Panel
                    title="Cohort Retention Analysis"
                    action={
                      <Link to={link("cohorts")}>
                        <ArrowUpRight size={14} />
                      </Link>
                    }
                  >
                    <CohortHeatmap data={(d.cohorts || []).slice(0, 28)} />
                  </Panel>
                  <Panel
                    title="Organization Performance"
                    action={
                      <Link to={link("organization")}>
                        <ArrowUpRight size={14} />
                      </Link>
                    }
                  >
                    <div className="a-segment">
                      {["Region", "District", "Zone", "Club"].map((l) => (
                        <button
                          key={l}
                          className={orgLevel === l ? "active" : ""}
                          onClick={() => setOrgLevel(l)}
                        >
                          {l}s
                        </button>
                      ))}
                    </div>
                    <Ranking
                      rows={selectedOrgs.slice(0, 5)}
                      onSelect={crossUnit}
                    />
                  </Panel>
                  <Panel title="Renewal Intelligence">
                    <div className="a-renewal-total">
                      <CalendarDays size={32} />
                      <div>
                        <strong>{fmt(d.renewals.renewed)}</strong>
                        <span>Recorded renewal events</span>
                      </div>
                    </div>
                    <RenewalWindows
                      data={d.renewals}
                      onSelect={renewalFilter}
                    />
                  </Panel>
                  <Panel
                    title="Club Health · Available Evidence"
                    action={
                      <Link to={link("organization")}>
                        Method <ArrowUpRight size={12} />
                      </Link>
                    }
                  >
                    <table className="a-table">
                      <thead>
                        <tr>
                          <th>Club</th>
                          <th>People</th>
                          <th>Score</th>
                          <th>Coverage</th>
                        </tr>
                      </thead>
                      <tbody>
                        {clubs.slice(0, 5).map((o) => {
                          const h = health(o);
                          return (
                            <tr key={o.id}>
                              <td>
                                <button onClick={() => crossUnit(o)}>
                                  {short(o.name)}
                                </button>
                              </td>
                              <td>{fmt(o.count)}</td>
                              <td>{fmt(h.score)}</td>
                              <td>{h.coverage.toFixed(0)}%</td>
                            </tr>
                          );
                        })}
                      </tbody>
                    </table>
                    <p className="a-chart-help">
                      Missing evidence is excluded, never scored as poor
                      performance.
                    </p>
                  </Panel>
                  <Panel
                    title="Membership Forecast"
                    description="Projections require enough observed history"
                  >
                    <Suspense fallback={<Skeleton />}>
                      <ForecastChart data={d.trend} />
                    </Suspense>
                  </Panel>
                  <Panel
                    title="Key Insights & Actions"
                    action={
                      <Link to={link("actions")}>
                        View all <ArrowUpRight size={12} />
                      </Link>
                    }
                  >
                    <InsightList items={insights.slice(0, 3)} />
                  </Panel>
                </PageSections>
              </PageSections></>
            )}
            {workspace === "growth" && (
              <>
                <WorkspaceTitle
                  icon={TrendingUp}
                  title="Growth Explorer"
                  description="Explore acquisition, active membership and recorded transfers. Zoom, brush and drill into the underlying cohort."
                />
                <Panel title="Multi-series Historical Growth">
                  <GrowthChart data={d.trend} onSelect={onPoint} />
                </Panel>
                <div className="a-two">
                  <Panel title="Acquisition by Bucket">
                    <DataTable
                      rows={d.trend}
                      columns={[
                        "date",
                        "new",
                        "total",
                        "active",
                        "transfers",
                        "renewals",
                      ]}
                    />
                  </Panel>
                  <Panel title="Regional Acquisition Comparison">
                    <Comparison
                      rows={organizations.filter((o) => o.level === "Region")}
                      selected={selected}
                      setSelected={setSelected}
                    />
                    <Distribution
                      mode="bars"
                      data={organizations
                        .filter(
                          (o) =>
                            o.level === "Region" &&
                            (!selected.length || selected.includes(o.id)),
                        )
                        .map((o) => ({ ...o, count: o.joined }))}
                      onSelect={crossUnit}
                    />
                  </Panel>
                </div>
              </>
            )}
            {workspace === "organization" && (
              <>
                <WorkspaceTitle
                  icon={Network}
                  title="Organization Performance"
                  description="Explore every authorized level, compare communities and inspect the evidence behind health scores."
                />
                <div className="a-controls">
                  <label>
                    Compare level
                    <select
                      aria-label="Organization ranking level"
                      value={orgLevel}
                      onChange={(e) => {
                        setOrgLevel(e.target.value);
                        setSelected([]);
                      }}
                    >
                      {levels.map((l) => (
                        <option key={l}>{l}</option>
                      ))}
                    </select>
                  </label>
                  <div className="a-segment">
                    {[
                      "ranking",
                      "comparison",
                      "trend",
                      "distribution",
                      "hierarchy",
                    ].map((v) => (
                      <button
                        key={v}
                        className={orgView === v ? "active" : ""}
                        onClick={() => setOrgView(v)}
                      >
                        {v}
                      </button>
                    ))}
                  </div>
                </div>
                <Panel title={orgLevel + " Intelligence"}>
                  <Comparison
                    rows={organizations.filter((o) => o.level === orgLevel)}
                    selected={selected}
                    setSelected={setSelected}
                  />
                  {orgView === "ranking" ? (
                    <Ranking
                      rows={selectedOrgs.slice(0, 100)}
                      onSelect={crossUnit}
                    />
                  ) : orgView === "hierarchy" ? (
                    <Suspense fallback={<Skeleton />}>
                      <HierarchyExplorer
                        organizations={organizations}
                        onSelect={crossUnit}
                      />
                    </Suspense>
                  ) : orgView === "trend" ? (
                    <>
                      <p className="a-chart-help">
                        Trend reflects the common scope. Select a unit below to
                        inspect its own filtered history.
                      </p>
                      <GrowthChart data={d.trend} onSelect={onPoint} />
                      <Ranking
                        rows={selectedOrgs.slice(0, 20)}
                        onSelect={crossUnit}
                      />
                    </>
                  ) : (
                    <Distribution
                      mode={orgView === "comparison" ? "bars" : "treemap"}
                      data={selectedOrgs}
                      onSelect={crossUnit}
                    />
                  )}
                </Panel>
                <Panel
                  title="Transparent Club Health"
                  description="Adjust exploratory weights. Only dimensions with recorded evidence contribute."
                >
                  <div className="a-weight-grid">
                    {Object.entries(weights).map(([key, value]) => (
                      <label key={key}>
                        {key}
                        <input
                          aria-label={key + " health weight"}
                          type="number"
                          min={0}
                          max={100}
                          value={value}
                          onChange={(e) =>
                            setWeights((p) => ({
                              ...p,
                              [key]: Math.max(
                                0,
                                Math.min(100, Number(e.target.value)),
                              ),
                            }))
                          }
                        />
                      </label>
                    ))}
                  </div>
                  <DataTable
                    rows={clubs.slice(0, 50).map((o) => {
                      const h = health(o);
                      return {
                        name: o.name,
                        people: o.count,
                        health:
                          h.score == null ? "Unavailable" : h.score.toFixed(1),
                        coverage: h.coverage.toFixed(0) + "%",
                        evidence:
                          h.available.join(", ") || "No scoreable observations",
                      };
                    })}
                    columns={[
                      "name",
                      "people",
                      "health",
                      "coverage",
                      "evidence",
                    ]}
                  />
                  <p className="a-chart-help">
                    Growth: 50 + 50 × acquisition change / prior acquisitions,
                    clamped 0–100. Other dimensions use observed percentages.
                    Weighted mean of available dimensions; coverage is the
                    available share of weights. Weights apply to this
                    exploration, not governance policy.
                  </p>
                </Panel>
              </>
            )}
            {workspace === "cohorts" && (
              <>
                <WorkspaceTitle
                  icon={Users}
                  title="Cohorts & Retention"
                  description="Separate observed churn from incomplete observation periods."
                />
                <div className="a-inline-kpis">
                  <div>
                    <small>Opening active people</small>
                    <strong>{fmt(d.summary.opening)}</strong>
                  </div>
                  <div>
                    <small>Retained people</small>
                    <strong>{fmt(d.summary.retained)}</strong>
                  </div>
                  <div>
                    <small>Retention</small>
                    <strong>
                      {retention == null ? "Unavailable" : retention + "%"}
                    </strong>
                  </div>
                </div>
                <Panel title="Monthly Joining Cohorts">
                  <CohortHeatmap data={d.cohorts || []} />
                </Panel>
                <div className="a-two">
                  <Panel title="Observed Cohort Retention Curves">
                    <RetentionCurves data={d.cohorts || []} />
                  </Panel>
                  <Panel title="Membership Tenure Distribution">
                    <Distribution mode="bars" data={d.tenure || []} />
                  </Panel>
                </div>
                <Panel title="Retention by Membership Category">
                  <DataTable
                    rows={(d.categoryRetention || []).map((r: any) => ({
                      ...r,
                      retention: r.opening
                        ? percent(r.retained, r.opening) + "%"
                        : "Not observed",
                    }))}
                    columns={["name", "opening", "retained", "retention"]}
                  />
                </Panel>
                <div className="a-two">
                  <Panel title="Retention by Region">
                    <DataTable
                      rows={organizations
                        .filter((o) => o.level === "Region")
                        .map((o) => ({
                          name: o.name,
                          opening: o.opening,
                          retained: o.retained,
                          retention: o.opening
                            ? percent(o.retained, o.opening) + "%"
                            : "Not observed",
                        }))}
                      columns={["name", "opening", "retained", "retention"]}
                    />
                  </Panel>
                  <Panel title="Observed Membership Trend">
                    <GrowthChart compact data={d.trend} />
                    <p className="a-chart-help">
                      Active population is not a retention rate. New people do
                      not enter the opening retention denominator.
                    </p>
                  </Panel>
                </div>
              </>
            )}
            {workspace === "lifecycle" && (
              <>
                <WorkspaceTitle
                  icon={Waypoints}
                  title="Membership Lifecycle"
                  description="Recorded activation, inactivity, transfers, expiry, suspension and reactivation."
                />
                <Panel title="Historical Status Flows">
                  <FlowChart data={d.flows || []} />
                </Panel>
                <div className="a-two">
                  <Panel title="Transition Event Register">
                    <DataTable
                      rows={(d.flows || []).map((r: any) => ({
                        ...r,
                        share:
                          percent(
                            r.count,
                            (d.flows || [])
                              .filter((x: any) => x.source === r.source)
                              .reduce((n: number, x: any) => n + x.count, 0),
                          ) + "%",
                      }))}
                      columns={["source", "target", "count", "share"]}
                    />
                  </Panel>
                  <Panel title="Status at Selected Boundary">
                    <Distribution
                      data={grouped("status")}
                      onSelect={(r) =>
                        r.id !== "Unknown" && change("status", r.id)
                      }
                    />
                  </Panel>
                </div>
                <p className="a-callout">
                  Application decisions are separate workflow records. Status
                  transitions do not imply application-to-member conversion;
                  missing links are not fabricated.
                </p>
              </>
            )}
            {workspace === "renewals" && (
              <>
                <WorkspaceTitle
                  icon={CalendarDays}
                  title="Renewal Intelligence"
                  description="Plan validity expiries and inspect actual renewal events."
                />
                <div className="a-two">
                  <Panel title="Upcoming Renewal Windows">
                    <RenewalWindows
                      data={d.renewals}
                      onSelect={renewalFilter}
                    />
                    <p className="a-chart-help">
                      Cumulative windows after the selected as-of date. Overdue:{" "}
                      {fmt(d.renewals.overdue)} affiliations.
                    </p>
                  </Panel>
                  <Panel title="Recorded Renewal Performance">
                    <div className="a-inline-kpis">
                      <div>
                        <small>Renewal events</small>
                        <strong>{fmt(d.renewals.renewed)}</strong>
                      </div>
                      <div>
                        <small>Expiry cohort</small>
                        <strong>{fmt(d.renewals.eligible)}</strong>
                      </div>
                      <div>
                        <small>Matched renewal conversion</small>
                        <strong>
                          {d.renewals.eligible
                            ? fmt(
                                percent(
                                  d.renewals.converted,
                                  d.renewals.eligible,
                                ),
                              ) + "%"
                            : "Unavailable"}
                        </strong>
                      </div>
                    </div>
                    <p className="a-chart-help">
                      Events and eligible affiliations have different
                      denominators. Repeated renewal events do not imply a
                      conversion percentage.
                    </p>
                  </Panel>
                </div>
                <Panel title="Upcoming Validity Calendar">
                  <div className="a-calendar">
                    {d.renewals.timeline.map((r: any) => (
                      <button
                        key={r.date}
                        onClick={() => {
                          setParams((p) => {
                            const n = new URLSearchParams(p);
                            n.set("renewalFrom", r.date);
                            n.set("renewalTo", r.date);
                            return n;
                          });
                        }}
                      >
                        <CalendarDays size={18} />
                        <strong>{r.date}</strong>
                        <span>{r.count} affiliations</span>
                      </button>
                    ))}
                  </div>
                  {!d.renewals.timeline.length && (
                    <Empty text="No recorded active affiliations expire in the next 90 days." />
                  )}
                </Panel>
                <Panel title="Recorded Renewal Trend">
                  <DataTable rows={d.trend} columns={["date", "renewals"]} />
                </Panel>
              </>
            )}
            {workspace === "categories" && (
              <>
                <WorkspaceTitle
                  icon={Layers}
                  title="Membership Distribution"
                  description="Select any segment to cross-filter every analytical workspace."
                />
                <div className="a-controls">
                  <label>
                    Dimension
                    <select
                      aria-label="Distribution dimension"
                      value={dimension}
                      onChange={(e) => setDimension(e.target.value)}
                    >
                      {[
                        "category",
                        "status",
                        "Region",
                        "District",
                        "Zone",
                        "Club",
                      ].map((v) => (
                        <option key={v}>{v}</option>
                      ))}
                    </select>
                  </label>
                  <label>
                    Visualization
                    <select
                      aria-label="Distribution visualization"
                      value={distributionMode}
                      onChange={(e) => setDistributionMode(e.target.value)}
                    >
                      {["donut", "bars", "treemap", "circles"].map((v) => (
                        <option key={v}>{v}</option>
                      ))}
                    </select>
                  </label>
                </div>
                <Panel title={dimension + " Distribution"}>
                  <Distribution
                    mode={distributionMode}
                    data={
                      ["category", "status"].includes(dimension)
                        ? grouped(dimension)
                        : organizations.filter((o) => o.level === dimension)
                    }
                    onSelect={(r) =>
                      dimension === "category"
                        ? change("categoryId", r.id)
                        : dimension === "status"
                          ? r.id !== "Unknown" && change("status", r.id)
                          : crossUnit(r)
                    }
                  />
                </Panel>
                <Panel title="Distribution Detail">
                  <DataTable
                    rows={
                      ["category", "status"].includes(dimension)
                        ? grouped(dimension)
                        : organizations.filter((o) => o.level === dimension)
                    }
                    columns={["name", "count", "active"]}
                  />
                </Panel>
              </>
            )}
            {workspace === "geography" && (
              <>
                <WorkspaceTitle
                  icon={Globe2}
                  title="Geographic Intelligence"
                  description="Recorded club locations, transparent coverage and community concentration."
                />
                <Panel title="Global Membership Map">
                  <Suspense fallback={<Skeleton />}>
                    <GeoExplorer
                      countries={grouped("country")}
                      organizations={organizations}
                      onCountry={(v) => change("country", v)}
                    />
                  </Suspense>
                </Panel>
                <div className="a-two">
                  <Panel title="Country Comparison">
                    <Distribution
                      mode="bars"
                      data={grouped("country")}
                      onSelect={(r) =>
                        r.name !== "Not recorded" && change("country", r.name)
                      }
                    />
                  </Panel>
                  <Panel title="Recorded State / Province">
                    <Distribution
                      mode="bars"
                      data={grouped("state")}
                      onSelect={(r) =>
                        r.name !== "Not recorded" && change("state", r.name)
                      }
                    />
                  </Panel>
                </div>
                <p className="a-callout">
                  {
                    organizations.filter(
                      (o) =>
                        o.level === "Club" &&
                        o.latitude != null &&
                        o.longitude != null,
                    ).length
                  }{" "}
                  of {clubs.length} clubs have recorded coordinates. Country
                  shading is a count, not population-normalized density. State
                  boundaries are unavailable; states remain in the comparison
                  table.
                </p>
              </>
            )}
            {workspace === "engagement" && (
              <>
                <WorkspaceTitle
                  icon={Activity}
                  title="Engagement Intelligence"
                  description="Actual check-ins, deduplicated by person and event. Attendance inactivity is distinct from membership inactivity."
                />
                {!d.capabilities.events ? (
                  <Empty text="Event reporting permission is required for engagement intelligence." />
                ) : (
                  <>
                    <div className="a-inline-kpis">
                      <div>
                        <small>Participating members</small>
                        <strong>{fmt(d.engagement?.participants || 0)}</strong>
                      </div>
                      <div>
                        <small>Person / event attendance</small>
                        <strong>{fmt(d.engagement?.attendance || 0)}</strong>
                      </div>
                      <div>
                        <small>Participation reach</small>
                        <strong>
                          {fmt(
                            percent(
                              d.engagement?.participants || 0,
                              d.summary.total,
                            ),
                          )}
                          %
                        </strong>
                      </div>
                    </div>
                    <Panel title="Event Participation Over Time">
                      {d.engagement?.timeline.length ? (
                        <GrowthChart
                          data={d.engagement.timeline.map((r: any) => ({
                            ...r,
                            total: r.participants,
                            active: r.attendance,
                            new: 0,
                          }))}
                        />
                      ) : (
                        <Empty text="No eligible checked-in attendance records in this period and scope." />
                      )}
                    </Panel>
                    <div className="a-two">
                      <Panel title="Club Participation">
                        <Distribution
                          mode="bars"
                          data={(d.engagement?.clubs || []).map((r: any) => ({
                            ...r,
                            name:
                              organizations.find((o) => o.id === r.id)?.name ||
                              "Scoped unit",
                            count: r.participants,
                          }))}
                          onSelect={(r) =>
                            crossUnit(organizations.find((o) => o.id === r.id))
                          }
                        />
                      </Panel>
                      <Panel title="Attendance Register · Aggregated">
                        <DataTable
                          rows={d.engagement?.timeline || []}
                          columns={["date", "participants", "attendance"]}
                        />
                      </Panel>
                    </div>
                  </>
                )}
              </>
            )}
            {workspace === "forecasting" && (
              <>
                <WorkspaceTitle
                  icon={ChartNoAxesCombined}
                  title="Membership Forecasting"
                  description="Observed outcomes and exploratory future acquisitions, clearly separated."
                />
                <div className="a-controls">
                  <label>
                    Projection horizon
                    <select
                      aria-label="Forecast horizon"
                      value={horizon}
                      onChange={(e) => setHorizon(+e.target.value)}
                    >
                      {[3, 6, 12].map((n) => (
                        <option key={n} value={n}>
                          {n} months
                        </option>
                      ))}
                    </select>
                  </label>
                  <button onClick={() => change("bucket", "month")}>
                    Use monthly observations
                  </button>
                  <button onClick={() => change("period", "all")}>
                    Use all available history
                  </button>
                </div>
                <Panel title="Historical versus Projected Membership">
                  <Suspense fallback={<Skeleton />}>
                    <ForecastChart data={d.trend} horizon={horizon} />
                  </Suspense>
                </Panel>
                <Panel title="Model Assumptions & Limits">
                  <p>
                    A linear acquisition model uses up to 24 completed monthly
                    observations and requires six months with recorded
                    membership. Negative acquisitions are clamped to zero. It
                    projects scoped total people, not active status, retention,
                    transfers, club openings or renewal behavior.
                  </p>
                  <p>
                    Approximate prediction intervals use residual variation.
                    Rolling holdout mean absolute error is shown when available.
                    Projections are exploratory, not actual outcomes or targets.
                  </p>
                </Panel>
              </>
            )}
            {workspace === "actions" && (
              <>
                <WorkspaceTitle
                  icon={Lightbulb}
                  title="Key Insights & Actions"
                  description="Deterministic findings with evidence, comparison context and authorized record links."
                />
                <Panel title="Your Community’s Attention Queue">
                  <InsightList items={insights} />
                  {!insights.length && (
                    <Empty text="No alerts meet the available evidence thresholds." />
                  )}
                </Panel>
              </>
            )}
            {workspace === "network" && (
              <>
                <WorkspaceTitle
                  icon={Network}
                  title="Relationship Explorer"
                  description="A progressive force-directed view of recorded organizational relationships."
                />
                <Panel title="Organizational Relationships">
                  <Suspense fallback={<Skeleton />}>
                    <HierarchyExplorer
                      organizations={[...organizations, ...(d.network?.nodes || [])]}
                      extraLinks={d.network?.edges || []}
                      onSelect={crossUnit}
                      network
                    />
                  </Suspense>
                </Panel>
                <p className="a-callout">
                  Select a club to explore up to 50 member display names and recorded
                  committee and officer relationships. Additional permissions govern
                  committee and leadership visibility. Contact details are excluded.
                </p>
              </>
            )}
            <details className="a-definitions">
              <summary>
                <ShieldCheck size={15} />
                Metric definitions, data sources & limitations{" "}
                <ChevronRight size={15} />
              </summary>
              <div>
                {d.definitions.map((text: string, i: number) => (
                  <p key={i}>{text}</p>
                ))}
                <p>
                  Sources: Memberships, MembershipHistory, MembershipRenewals,
                  MembershipApplications, Orgs, Levels;
                  EventRegistrations/EventCheckins and
                  OfficerPositions/OfficerAppointments where separately
                  authorized. Server aggregation: {d.elapsedMs} ms. Organization
                  metadata: {organizations.length} nodes. Member rows are not
                  downloaded.
                </p>
              </div>
            </details>
            <footer className="a-export-footer">
              <span>Connected by purpose. Informed by evidence.</span>
              <div>
                {d.capabilities.export && (
                  <button
                    onClick={() => page.current && exportCharts(page.current)}
                  >
                    <Download size={14} />
                    Export charts as SVG
                  </button>
                )}
                {d.capabilities.export && (
                  <button onClick={() => download("xlsx")}>Export Excel</button>
                )}
                <button onClick={() => window.print()}>Print / save PDF</button>
              </div>
            </footer>
          </>
        )
      )}
    </div>
  );
}
function sparkline(values: number[]) {
  const max = Math.max(1, ...values);
  return values
    .map(
      (v, i) =>
        (i ? "L" : "M") +
        (5 + (i * 120) / Math.max(1, values.length - 1)) +
        "," +
        (35 - (v / max) * 30),
    )
    .join(" ");
}
function short(name: string) {
  return name
    .replace(/^Demo /, "")
    .replace(
      /Region (\d+) District (\d+) Zone (\d+) Club (\d+)/,
      "R$1 · D$2 · Z$3 · Club $4",
    );
}
function WorkspaceTitle({ icon: Icon, title, description }: any) {
  return (
    <div className="a-workspace-title">
      <Icon size={26} />
      <div>
        <h2>{title}</h2>
        <p>{description}</p>
      </div>
    </div>
  );
}
function Ranking({ rows, onSelect }: any) {
  return (
    <div className="a-scroll">
      <table className="a-table">
        <thead>
          <tr>
            <th>#</th>
            <th>Community</th>
            <th>People</th>
            <th>Active</th>
            <th>Active %</th>
          </tr>
        </thead>
        <tbody>
          {rows.map((o: any, i: number) => (
            <tr key={o.id}>
              <td>{i + 1}</td>
              <td>
                <button onClick={() => onSelect(o)} title={o.name}>
                  {short(o.name)}
                </button>
              </td>
              <td>{fmt(o.count)}</td>
              <td>{fmt(o.active)}</td>
              <td>{o.count ? fmt(percent(o.active, o.count)) + "%" : "—"}</td>
            </tr>
          ))}
        </tbody>
      </table>
      {!rows.length && <Empty text="No units match the selection." />}
    </div>
  );
}
function DataTable({ rows, columns }: any) {
  return (
    <div className="a-scroll">
      <table className="a-table">
        <thead>
          <tr>
            {columns.map((k: string) => (
              <th key={k}>{k[0].toUpperCase() + k.slice(1)}</th>
            ))}
          </tr>
        </thead>
        <tbody>
          {rows.map((r: any, i: number) => (
            <tr key={i}>
              {columns.map((k: string) => (
                <td key={k}>
                  {r[k] == null
                    ? "—"
                    : typeof r[k] === "number"
                      ? fmt(r[k])
                      : r[k]}
                </td>
              ))}
            </tr>
          ))}
        </tbody>
      </table>
      {!rows.length && (
        <Empty text="No observations in this scope and period." />
      )}
    </div>
  );
}
function Comparison({ rows, selected, setSelected }: any) {
  return (
    <div className="a-comparison-picks">
      {rows.slice(0, 50).map((o: any) => (
        <label key={o.id}>
          <input
            type="checkbox"
            checked={selected.includes(o.id)}
            onChange={() =>
              setSelected((p: string[]) =>
                p.includes(o.id) ? p.filter((x) => x !== o.id) : [...p, o.id],
              )
            }
          />
          {short(o.name)}
        </label>
      ))}
      {selected.length > 0 && (
        <button onClick={() => setSelected([])}>Clear comparison</button>
      )}
    </div>
  );
}
function InsightList({ items }: any) {
  return (
    <div className="a-insights">
      {items.map((r: any, i: number) => (
        <article key={i}>
          <span
            className={
              r.severity === "High"
                ? "high"
                : r.severity === "Attention"
                  ? "attention"
                  : ""
            }
          >
            <Lightbulb size={15} />
            {r.severity}
          </span>
          <h3>{r.title}</h3>
          <p>{r.detail}</p>
          <Link to={r.to}>
            {r.action}
            <ArrowUpRight size={12} />
          </Link>
        </article>
      ))}
    </div>
  );
}
function RenewalWindows({ data, onSelect }: any) {
  return (
    <div className="a-renewal-windows">
      {data.windows.map((r: any) => (
        <button key={r.days} onClick={() => onSelect(r.days)}>
          <span>Due in {r.days} days</span>
          <strong>{fmt(r.count)}</strong>
          <ArrowUpRight size={13} />
        </button>
      ))}
    </div>
  );
}

function KpiDestination({to,children,className}: {to?:string;children:any;className:string}) {return to?<Link to={to} className={className}>{children}</Link>:<div className={className}>{children}</div>;}
