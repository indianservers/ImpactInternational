import { useEffect, useMemo, useRef, useState } from "react";
import { useQuery } from "@tanstack/react-query";
import * as d3 from "d3";
import { Empty } from "./Charts";
import { palette } from "./utils";
import { forecast } from "./forecast";

export function GeoExplorer({
  countries,
  organizations,
  onCountry,
  globe = false,
  measureLabel = "affiliations",
}: {
  countries: any[];
  organizations: any[];
  onCountry: (name: string) => void;
  globe?: boolean;
  measureLabel?: string;
}) {
  const ref = useRef<SVGSVGElement>(null),
    [rotation, setRotation] = useState<[number, number]>([-30, -15]),
    [zoom, setZoom] = useState(1),
    [hover, setHover] = useState("");
  const world = useQuery({
    queryKey: ["natural-earth-110m"],
    queryFn: async () => {
      const r = await fetch("/maps/world-countries.geojson");
      if (!r.ok) throw Error("Unable to load map boundaries.");
      return r.json();
    },
    staleTime: Infinity,
  });
  const projection = useMemo(
    () =>
      globe
        ? d3
            .geoOrthographic()
            .rotate(rotation)
            .scale(142 * zoom)
            .translate([310, 165])
        : d3
            .geoNaturalEarth1()
            .scale(98 * zoom)
            .translate([310, 168]),
    [globe, rotation, zoom],
  );
  const path = d3.geoPath(projection);
  const counts = new Map(countries.map((c) => [c.name.toLowerCase(), c.count]));
  const color = d3
    .scaleSequential(d3.interpolatePurples)
    .domain([0, d3.max(countries, (c) => c.count) || 1]);
  useEffect(() => {
    if (!ref.current) return;
    const svg = d3.select(ref.current);
    if (globe) {
      svg.call(
        d3
          .drag<SVGSVGElement, unknown>()
          .on("drag", (e) =>
            setRotation((p) => [
              p[0] + e.dx * 0.3,
              Math.max(-80, Math.min(80, p[1] - e.dy * 0.3)),
            ]),
          ),
      );
    } else
      svg.call(
        d3
          .zoom<SVGSVGElement, unknown>()
          .scaleExtent([1, 8])
          .on("zoom", (e) =>
            svg
              .select(".a-map-shapes")
              .attr("transform", e.transform.toString()),
          ),
      );
    return () => {
      svg.on(".drag", null).on(".zoom", null);
    };
  }, [globe, world.data]);
  return (
    <div className={"a-geo " + (globe ? "globe" : "")}>
      <div className="a-map-controls">
        <button
          aria-label="Zoom map in"
          onClick={() => setZoom((z) => Math.min(4, z + 0.3))}
        >
          +
        </button>
        <button
          aria-label="Zoom map out"
          onClick={() => setZoom((z) => Math.max(1, z - 0.3))}
        >
          −
        </button>
        <button
          onClick={() => {
            setZoom(1);
            setRotation([-30, -15]);
            if (ref.current)
              d3.select(ref.current)
                .select(".a-map-shapes")
                .attr("transform", null);
          }}
        >
          Reset map
        </button>
      </div>
      {world.isPending ? (
        <p>Loading geographic boundaries…</p>
      ) : world.error ? (
        <p>Map unavailable. Country totals remain available below.</p>
      ) : (
        <svg
          ref={ref}
          data-chart
          viewBox="0 0 620 330"
          role="img"
          aria-label={
            globe
              ? "Global membership footprint globe"
              : `Country ${measureLabel} choropleth map`
          }
        >
          <defs>
            <radialGradient id="a-ocean">
              <stop stopColor="#403067" />
              <stop offset="1" stopColor="#1e1937" />
            </radialGradient>
          </defs>
          <g className="a-map-shapes">
            <path
              d={path({ type: "Sphere" }) || ""}
              fill={globe ? "url(#a-ocean)" : "#f5f1fb"}
              stroke={globe ? "#9a78cc" : "#ddd1ed"}
            />
            <path
              d={path(d3.geoGraticule10()) || ""}
              fill="none"
              stroke={globe ? "#bba5df30" : "#ded5eb"}
              strokeWidth=".45"
            />
            {world.data.features.map((f: any) => {
              const count = counts.get(f.properties.name.toLowerCase()) || 0;
              return (
                <path
                  key={f.properties.iso + f.properties.name}
                  d={path(f) || ""}
                  fill={
                    count
                      ? globe
                        ? "#c5a1ff"
                        : color(count)
                      : globe
                        ? "#67518e"
                        : "#e9e1f3"
                  }
                  stroke={globe ? "#a387c5" : "#fff"}
                  strokeWidth=".45"
                  role="button"
                  tabIndex={count ? 0 : -1}
                  aria-label={`${f.properties.name}: ${count} ${measureLabel}`}
                  onMouseEnter={() =>
                    setHover(
                      `${f.properties.name} · ${count.toLocaleString()} ${measureLabel}`,
                    )
                  }
                  onMouseLeave={() => setHover("")}
                  onClick={() => count && onCountry(f.properties.name)}
                  onKeyDown={(e) =>
                    e.key === "Enter" && count && onCountry(f.properties.name)
                  }
                >
                  <title>
                    {f.properties.name}: {count.toLocaleString()} {measureLabel}
                  </title>
                </path>
              );
            })}
            {organizations
              .filter(
                (o) =>
                  o.level === "Club" &&
                  o.latitude != null &&
                  o.longitude != null,
              )
              .slice(0, 1000)
              .map((o) => {
                const location = projection([o.longitude, o.latitude]);
                if (
                  !location ||
                  (globe &&
                    d3.geoDistance(
                      [o.longitude, o.latitude],
                      [-rotation[0], -rotation[1]],
                    ) >
                      Math.PI / 2)
                )
                  return null;
                return (
                  <circle
                    key={o.id}
                    cx={location[0]}
                    cy={location[1]}
                    r="2.6"
                    fill="#f0c664"
                  >
                    <title>
                      {o.name}: {o.count} people · recorded club coordinates
                    </title>
                  </circle>
                );
              })}
          </g>
        </svg>
      )}
      <div className="a-map-caption">
        {hover || "Select a recorded country · drag to explore"}
        <small>
          Natural Earth · public domain boundaries. Pins use recorded club
          coordinates only.
        </small>
      </div>
    </div>
  );
}
export function HierarchyExplorer({
  organizations,
  onSelect,
  network = false,
  extraLinks = [],
}: {
  organizations: any[];
  onSelect: (o: any) => void;
  network?: boolean;
  extraLinks?: any[];
}) {
  const [rootId, setRootId] = useState(""),
    [collapsed, setCollapsed] = useState<string[]>(() =>
      organizations
        .filter((o) => ["District", "Zone"].includes(o.level))
        .map((o) => o.id),
    ),
    [layout, setLayout] = useState("tree");
  const ref = useRef<SVGSVGElement>(null);
  const root = useMemo(() => {
    const index = new Map(
      organizations.map((o) => [o.id, { ...o, children: [] as any[] }]),
    );
    const nodes = Array.from(index.values());
    nodes.forEach((n) => {
      if (index.has(n.parentId)) index.get(n.parentId)!.children.push(n);
    });
    const first = rootId
      ? index.get(rootId)
      : nodes.find((n) => !index.has(n.parentId));
    if (!first) return null;
    return d3
      .hierarchy<any>(first, (d) =>
        !network && collapsed.includes(d.id) ? null : d.children,
      )
      .sum((d) =>
        !d.children.length || (!network && collapsed.includes(d.id))
          ? d.count
          : 0,
      );
  }, [organizations, collapsed, rootId, network]);
  useEffect(() => {
    if (!ref.current) return;
    const svg = d3.select(ref.current);
    svg.call(
      d3
        .zoom<SVGSVGElement, unknown>()
        .scaleExtent([0.3, 6])
        .on("zoom", (e) =>
          svg.select(".a-tree-nodes").attr("transform", e.transform.toString()),
        ),
    );
    return () => {
      svg.on(".zoom", null);
    };
  }, [root, network]);
  const nodes = root ? d3.tree<any>().nodeSize([36, 210])(root) : null;
  const { forceNodes, forceLinks } = useMemo(() => {
    if (!network || !root) return { forceNodes: [], forceLinks: [] };
    const ns = root
        .descendants()
        .slice(0, 100)
        .map((n) => ({ id: n.data.id, data: n.data, x: 500, y: 220 })),
      ids = new Set(ns.map((n) => n.id)),
      ls = root
        .links()
        .filter((l) => ids.has(l.source.data.id) && ids.has(l.target.data.id))
        .map((l) => ({ source: l.source.data.id, target: l.target.data.id }));
    extraLinks.filter(l=>ids.has(l.source)&&ids.has(l.target)).forEach(l=>ls.push({source:l.source,target:l.target}));
    const sim = d3
      .forceSimulation<any>(ns)
      .force(
        "link",
        d3
          .forceLink<any, any>(ls)
          .id((d) => d.id)
          .distance(75),
      )
      .force("charge", d3.forceManyBody().strength(-140))
      .force("center", d3.forceCenter(450, 230))
      .force("collision", d3.forceCollide(20))
      .stop();
    for (let i = 0; i < 180; i++) sim.tick();
    return {
      forceNodes: ns.map((n) => ({ ...n })),
      forceLinks: ls.map((l) => ({ ...l })),
    };
  }, [root, network, extraLinks]);
  if (!root || !nodes)
    return <Empty text="No organizational units in this scope." />;
  const min = d3.min(nodes.descendants(), (n) => n.x) || 0,
    max = d3.max(nodes.descendants(), (n) => n.x) || 0;
  return (
    <div className="a-hierarchy">
      <div className="a-controls">
        <button
          onClick={() => {
            setRootId("");
            setCollapsed([]);
          }}
        >
          Reset hierarchy
        </button>
        {!network && (
          <select
            aria-label="Hierarchy visualization"
            value={layout}
            onChange={(e) => setLayout(e.target.value)}
          >
            <option value="tree">Expandable tree</option>
            <option value="sunburst">Sunburst</option>
          </select>
        )}
        <small>
          {network
            ? "First 100 organizational relationships · choose a unit to expand progressively"
            : "Click a circle to expand/collapse · click its name to filter · scroll to zoom"}
        </small>
      </div>
      {layout === "sunburst" && !network ? (
        <svg
          data-chart
          viewBox="0 0 900 470"
          role="img"
          aria-label="Organizational membership sunburst"
        >
          {d3
            .partition<any>()
            .size([Math.PI * 2, 200])(root)
            .descendants()
            .filter((n) => n.depth)
            .map((n: any) => (
              <path
                key={n.data.id}
                transform="translate(450 235)"
                d={
                  d3
                    .arc<any>()
                    .startAngle((d) => d.x0)
                    .endAngle((d) => d.x1)
                    .innerRadius((d) => d.y0)
                    .outerRadius((d) => d.y1)(n) || ""
                }
                fill={palette[n.depth % palette.length]}
                stroke="white"
                strokeWidth=".6"
                role="button"
                tabIndex={0}
                onClick={() => onSelect(n.data)}
                onKeyDown={(e) => e.key === "Enter" && onSelect(n.data)}
              >
                <title>
                  {n.data.name}: {n.data.count} people
                </title>
              </path>
            ))}
        </svg>
      ) : (
        <svg
          ref={ref}
          data-chart
          viewBox={`0 0 1050 ${network ? 470 : Math.max(350, Math.min(1500, max - min + 80))}`}
          role="img"
          aria-label={
            network
              ? "Progressive organizational relationship network"
              : "Expandable organizational hierarchy tree"
          }
        >
          <g className="a-tree-nodes">
            {network ? (
              <>
                {forceLinks.map((l: any, i) => (
                  <line
                    key={i}
                    x1={l.source.x}
                    y1={l.source.y}
                    x2={l.target.x}
                    y2={l.target.y}
                    stroke="#d9cbe9"
                  />
                ))}
                {forceNodes.map((n) => (
                  <g
                    key={n.id}
                    transform={`translate(${n.x},${n.y})`}
                    onClick={() => n.data.level==='Club'?onSelect(n.data):setRootId(n.id)}
                    role="button"
                    tabIndex={0}
                    onKeyDown={(e) => e.key === "Enter" && (n.data.level==='Club'?onSelect(n.data):setRootId(n.id))}
                  >
                    <circle
                      r={Math.max(5, Math.min(16, Math.sqrt(n.data.count)))}
                      fill={
                        palette[
                          Math.max(
                            0,
                            [
                              "International",
                              "National",
                              "Region",
                              "District",
                              "Zone",
                              "Club",
                              "Committee", "Member", "Officer",
                            ].indexOf(n.data.level),
                          ) % palette.length
                        ]
                      }
                    />
                    <title>
                      {n.data.name}: {n.data.count} people · click to focus this
                      branch
                    </title>
                    <text
                      y="-19"
                      textAnchor="middle"
                      fill="#6c5b7b"
                      fontSize="8"
                    >
                      {n.data.name.replace("Demo ", "").slice(-21)}
                    </text>
                  </g>
                ))}
              </>
            ) : (
              <>
                {nodes.links().map((l: any, i) => (
                  <path
                    key={i}
                    d={
                      d3
                        .linkHorizontal<any, any>()
                        .x((d) => d.y + 25)
                        .y((d) => d.x - min + 35)(l) || ""
                    }
                    fill="none"
                    stroke="#ddd1ec"
                  />
                ))}
                {nodes.descendants().map((n: any) => (
                  <g
                    key={n.data.id}
                    transform={`translate(${n.y + 25},${n.x - min + 35})`}
                  >
                    <circle
                      r={n.children || n.data.children.length ? 7 : 4}
                      fill={
                        collapsed.includes(n.data.id) ? "#d7ad53" : "#8c5dc4"
                      }
                      role="button"
                      tabIndex={0}
                      aria-label={`Expand or collapse ${n.data.name}`}
                      onClick={() =>
                        setCollapsed((p) =>
                          p.includes(n.data.id)
                            ? p.filter((x) => x !== n.data.id)
                            : [...p, n.data.id],
                        )
                      }
                      onKeyDown={(e) =>
                        e.key === "Enter" &&
                        setCollapsed((p) =>
                          p.includes(n.data.id)
                            ? p.filter((x) => x !== n.data.id)
                            : [...p, n.data.id],
                        )
                      }
                    />
                    <text
                      x="13"
                      dy="4"
                      fontSize="10"
                      fill="#4a365d"
                      role="button"
                      tabIndex={0}
                      onClick={() => onSelect(n.data)}
                      onKeyDown={(e) => e.key === "Enter" && onSelect(n.data)}
                    >
                      {n.data.name.replace("Demo ", "").slice(-29)} ·{" "}
                      {n.data.count}
                      <title>
                        {n.data.name}: {n.data.count} people / {n.data.active}{" "}
                        active · {n.data.status}
                      </title>
                    </text>
                  </g>
                ))}
              </>
            )}
          </g>
        </svg>
      )}
    </div>
  );
}
export function ForecastChart({
  data,
  horizon = 6,
}: {
  data: any[];
  horizon?: number;
}) {
  const f = forecast(data, horizon);
  if (!f)
    return (
      <Empty text="Forecast unavailable: at least six completed months with observed membership are required. No historical values have been invented." />
    );
  const actual = data.slice(-6),
    all = [...actual, ...f.points],
    x = d3
      .scalePoint()
      .domain(all.map((d) => d.date))
      .range([50, 840]),
    y = d3
      .scaleLinear()
      .domain([0, d3.max(all, (d) => d.upper || d.total) || 1])
      .nice()
      .range([260, 25]);
  return (
    <>
      <svg
        data-chart
        viewBox="0 0 900 310"
        role="img"
        aria-label="Actual versus projected membership with prediction interval"
      >
        <path
          d={
            d3
              .area<any>()
              .x((d) => x(d.date)!)
              .y0((d) => y(d.lower))
              .y1((d) => y(d.upper))(f.points) || ""
          }
          fill="#edc26b38"
        />
        <path
          d={
            d3
              .line<any>()
              .x((d) => x(d.date)!)
              .y((d) => y(d.total))(actual) || ""
          }
          fill="none"
          stroke="#7845e8"
          strokeWidth="3"
        />
        <path
          d={
            d3
              .line<any>()
              .x((d) => x(d.date)!)
              .y((d) => y(d.total))([actual.at(-1), ...f.points]) || ""
          }
          fill="none"
          stroke="#d5a13a"
          strokeWidth="2"
          strokeDasharray="5 4"
        />
        {all.map((d, i) => (
          <g key={d.date}>
            <circle
              cx={x(d.date)}
              cy={y(d.total)}
              r="4"
              fill={i < actual.length ? "#7845e8" : "#d5a13a"}
            >
              <title>
                {d.date}: {d.total}{" "}
                {i < actual.length
                  ? "recorded people"
                  : `projected people; approximate 95% interval ${d.lower}–${d.upper}`}
              </title>
            </circle>
            <text
              x={x(d.date)}
              y="284"
              textAnchor="middle"
              fontSize="9"
              fill="#82708f"
            >
              {d.date.slice(0, 7)}
            </text>
          </g>
        ))}
      </svg>
      <p className="a-chart-help">
        Purple: actual. Gold: projection of scoped first-join acquisitions.
        Approximate 95% residual-based intervals. Linear acquisition model,{" "}
        {f.observations} completed months; rolling holdout MAE{" "}
        {f.mae?.toFixed(1) ?? "unavailable"} registrations. No assumed future
        transfers, retention or renewals. Not a forecast of active people.
      </p>
    </>
  );
}
