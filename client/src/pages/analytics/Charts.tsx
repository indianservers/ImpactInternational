import { useEffect, useRef, useState } from "react";
import * as d3 from "d3";
import { sankey, sankeyLinkHorizontal } from "d3-sankey";
import { palette } from "./utils";
export function RetentionCurves({ data }: { data: any[] }) {
  const groups = d3
      .groups(
        data.filter((d) => d.retained != null),
        (d) => d.cohort,
      )
      .slice(0, 8),
    x = d3.scaleLinear().domain([0, 24]).range([45, 650]),
    y = d3.scaleLinear().domain([0, 100]).range([220, 20]);
  if (!groups.length)
    return (
      <Empty text="Retention curves appear after joining cohorts reach their first observation boundary." />
    );
  return (
    <>
      <svg
        data-chart
        viewBox="0 0 700 260"
        role="img"
        aria-label="Observed monthly cohort retention curves"
      >
        {[0, 25, 50, 75, 100].map((v) => (
          <g key={v}>
            <line x1="45" x2="650" y1={y(v)} y2={y(v)} stroke="#eee7f5" />
            <text
              x="35"
              y={y(v) + 3}
              textAnchor="end"
              fontSize="9"
              fill="#9b83ad"
            >
              {v}%
            </text>
          </g>
        ))}
        {[1, 3, 6, 9, 12, 18, 24].map((m) => (
          <text
            key={m}
            x={x(m)}
            y="240"
            textAnchor="middle"
            fontSize="9"
            fill="#9b83ad"
          >
            {m}M
          </text>
        ))}
        {groups.map(([name, rows], i) => (
          <g key={name}>
            <path
              d={
                d3
                  .line<any>()
                  .x((d) => x(d.months))
                  .y((d) => y((d.retained / d.size) * 100))(rows) || ""
              }
              fill="none"
              stroke={palette[i % palette.length]}
              strokeWidth="2"
            />
            {rows.map((r) => (
              <circle
                key={r.months}
                cx={x(r.months)}
                cy={y((r.retained / r.size) * 100)}
                r="4"
                fill={palette[i % palette.length]}
              >
                <title>
                  {name} at {r.months}M: {r.retained}/{r.size} retained
                </title>
              </circle>
            ))}
          </g>
        ))}
      </svg>
      <div className="a-legend">
        {groups.map(([name], i) => (
          <span
            key={name}
            style={{ color: palette[i % palette.length], fontSize: 10 }}
          >
            {name}
          </span>
        ))}
      </div>
    </>
  );
}
const series = ["total", "active", "new", "inactive", "expired", "transfers"];
export function Empty({ text }: { text: string }) {
  return (
    <div className="a-empty">
      <span>◇</span>
      <p>{text}</p>
    </div>
  );
}
export function GrowthChart({
  data,
  onSelect,
  compact = false,
}: {
  data: any[];
  onSelect?: (d: any) => void;
  compact?: boolean;
}) {
  const ref = useRef<SVGSVGElement>(null),
    actions = useRef({ inside: () => {}, outside: () => {} }),
    [hidden, setHidden] = useState<string[]>([]),
    [reset, setReset] = useState(0),
    [tip, setTip] = useState<any>(null);
  useEffect(() => {
    if (!ref.current || !data.length) return;
    const svg = d3.select(ref.current);
    svg.selectAll("*").remove();
    const W = 900,
      H = compact ? 250 : 360,
      m = { top: 15, right: 25, bottom: compact ? 43 : 75, left: 54 },
      bottom = H - m.bottom,
      parse = (d: any) => new Date(d.date + "T00:00:00Z");
    const extent = d3.extent(data, parse) as [Date, Date];
    if (+extent[0] === +extent[1]) extent[1] = d3.utcDay.offset(extent[1], 1);
    const x = d3
        .scaleUtc()
        .domain(extent)
        .range([m.left, W - m.right]),
      y = d3
        .scaleLinear()
        .domain([
          0,
          d3.max(data, (d) =>
            d3.max(
              series.filter((s) => !hidden.includes(s)),
              (s) => +d[s] || 0,
            ),
          ) || 1,
        ])
        .nice()
        .range([bottom, m.top]);
    const id = "clip-" + Math.random().toString(36).slice(2);
    svg
      .append("defs")
      .append("clipPath")
      .attr("id", id)
      .append("rect")
      .attr("x", m.left)
      .attr("y", m.top)
      .attr("width", W - m.left - m.right)
      .attr("height", bottom - m.top);
    svg
      .append("g")
      .attr("transform", `translate(${m.left},0)`)
      .call(
        d3
          .axisLeft(y)
          .ticks(5)
          .tickSize(-(W - m.left - m.right)),
      )
      .call((g) => g.select(".domain").remove())
      .call((g) => g.selectAll("line").attr("stroke", "#e9e5f1"))
      .call((g) => g.selectAll("text").attr("fill", "#8b819b"));
    const axis = svg.append("g").attr("transform", `translate(0,${bottom})`),
      lines = svg.append("g").attr("clip-path", `url(#${id})`),
      cursor = svg
        .append("line")
        .attr("y1", m.top)
        .attr("y2", bottom)
        .attr("stroke", "#a295be")
        .attr("stroke-dasharray", "4 4")
        .style("display", "none");
    function draw(scale: typeof x) {
      axis
        .call(
          d3
            .axisBottom(scale)
            .ticks(compact ? 5 : 7)
            .tickFormat((d) => d3.utcFormat("%b %d")(d as Date)),
        )
        .call((g) => g.selectAll("text").attr("fill", "#8b819b"));
      lines.selectAll("*").remove();
      series.forEach((s, i) => {
        if (hidden.includes(s)) return;
        if (s === "total")
          lines
            .append("path")
            .datum(data)
            .attr("fill", "#7845e812")
            .attr(
              "d",
              d3
                .area<any>()
                .x((d) => scale(parse(d)))
                .y0(bottom)
                .y1((d) => y(d[s] || 0))
                .curve(d3.curveMonotoneX),
            );
        lines
          .append("path")
          .datum(data)
          .attr("fill", "none")
          .attr("stroke", palette[i])
          .attr("stroke-width", s === "total" ? 3 : 2)
          .attr(
            "d",
            d3
              .line<any>()
              .x((d) => scale(parse(d)))
              .y((d) => y(d[s] || 0))
              .curve(d3.curveMonotoneX),
          );
        lines
          .selectAll(`circle.s${i}`)
          .data(data)
          .join("circle")
          .attr("class", `s${i}`)
          .attr("cx", (d) => scale(parse(d)))
          .attr("cy", (d) => y(d[s] || 0))
          .attr("r", compact ? 2 : 3)
          .attr("fill", palette[i]);
      });
    }
    let current = x;
    draw(x);
    const overlay = svg
      .append("rect")
      .attr("class", "a-growth-overlay")
      .attr("x", m.left)
      .attr("y", m.top)
      .attr("width", W - m.left - m.right)
      .attr("height", bottom - m.top)
      .attr("fill", "transparent")
      .style("cursor", "crosshair");
    overlay
      .on("pointermove", (event) => {
        const date = current.invert(d3.pointer(event)[0]),
          index = d3.bisector((d: any) => +parse(d)).center(data, +date),
          point = data[index];
        cursor
          .attr("x1", current(parse(point)))
          .attr("x2", current(parse(point)))
          .style("display", null);
        setTip(point);
      })
      .on("pointerleave", () => {
        cursor.style("display", "none");
        setTip(null);
      })
      .on("click", (event) => {
        const date = current.invert(d3.pointer(event)[0]);
        onSelect?.(
          data[d3.bisector((d: any) => +parse(d)).center(data, +date)],
        );
      });
    const zoom = d3
      .zoom<SVGRectElement, unknown>()
      .scaleExtent([1, 20])
      .extent([
        [m.left, m.top],
        [W - m.right, bottom],
      ])
      .translateExtent([
        [m.left, m.top],
        [W - m.right, bottom],
      ])
      .on("zoom", (event) => {
        current = event.transform.rescaleX(x);
        draw(current);
      });
    overlay.call(zoom);
    actions.current = {
      inside: () => overlay.call(zoom.scaleBy, 1.6),
      outside: () => overlay.call(zoom.scaleBy, 1 / 1.6),
    };
    if (!compact) {
      const brush = d3
        .brushX()
        .extent([
          [m.left, H - 30],
          [W - m.right, H - 8],
        ])
        .on("end", (event) => {
          if (!event.sourceEvent) return;
          current = event.selection
            ? x.copy().domain(event.selection.map(x.invert))
            : x;
          draw(current);
        });
      svg.append("g").attr("class", "a-brush").call(brush);
      svg
        .append("text")
        .attr("x", m.left)
        .attr("y", H - 37)
        .attr("font-size", 9)
        .attr("fill", "#8c7ca0")
        .text("Drag below to focus dates · scroll to zoom · drag chart to pan");
    }
    return () => {
      svg.selectAll("*").interrupt();
      svg.selectAll("*").remove();
    };
  }, [data, hidden, reset, compact, onSelect]);
  return (
    <div className="a-growth">
      <div className="a-legend">
        {series.map((s, i) => (
          <button
            key={s}
            aria-pressed={!hidden.includes(s)}
            className={hidden.includes(s) ? "muted" : ""}
            onClick={() =>
              setHidden((p) =>
                p.includes(s) ? p.filter((x) => x !== s) : [...p, s],
              )
            }
          >
            <i style={{ background: palette[i] }} />
            {s === "new" ? "New members" : s[0].toUpperCase() + s.slice(1)}
          </button>
        ))}
        {!compact && (
          <>
            <button
              aria-label="Zoom growth chart in"
              onClick={() => actions.current.inside()}
            >
              Zoom +
            </button>
            <button
              aria-label="Zoom growth chart out"
              onClick={() => actions.current.outside()}
            >
              Zoom −
            </button>
            <button onClick={() => setReset((p) => p + 1)}>Reset zoom</button>
          </>
        )}
      </div>
      <svg
        ref={ref}
        data-chart
        viewBox={`0 0 900 ${compact ? 250 : 360}`}
        role="img"
        aria-label="Historical membership growth explorer"
      />
      {tip && (
        <div className="a-tooltip" role="status">
          <strong>{tip.date}</strong>
          {series
            .filter((s) => !hidden.includes(s))
            .map((s) => (
              <span key={s}>
                {s}: <b>{Number(tip[s] || 0).toLocaleString()}</b>
              </span>
            ))}
        </div>
      )}
      {onSelect && (
        <p className="a-chart-help">
          Click a point to filter join records in that bucket.
        </p>
      )}
    </div>
  );
}
export function Distribution({
  data,
  onSelect,
  mode = "donut",
  label = "Membership distribution",
}: {
  data: any[];
  onSelect?: (d: any) => void;
  mode?: string;
  label?: string;
}) {
  const rows = data.filter((d) => d.count > 0).slice(0, 60),
    total = d3.sum(rows, (d) => d.count),
    root = d3
      .hierarchy<any>({ name: "All", children: rows })
      .sum((d) => (d.children ? 0 : d.count || 0)),
    activate = (d: any) => onSelect?.(d);
  if (!rows.length) return <Empty text="No memberships match these filters." />;
  if (mode === "bars")
    return (
      <div className="a-bars">
        {rows.slice(0, 20).map((r, i) => (
          <button
            key={r.id || r.name}
            onClick={() => activate(r)}
            title={`${r.name}: ${r.count}`}
          >
            <span>{r.name}</span>
            <i>
              <b
                style={{
                  width: `${(r.count / Math.max(...rows.map((d) => d.count))) * 100}%`,
                  background: palette[i % palette.length],
                }}
              />
            </i>
            <strong>{r.count.toLocaleString()}</strong>
          </button>
        ))}
      </div>
    );
  if (mode === "treemap") {
    const tree = d3.treemap<any>().size([600, 280]).padding(4)(root);
    return (
      <svg data-chart viewBox="0 0 600 280" role="img" aria-label={label}>
        {tree.leaves().map((n: any, i) => (
          <g
            key={i}
            role="button"
            tabIndex={0}
            aria-label={`${n.data.name}: ${n.value}`}
            onClick={() => activate(n.data)}
            onKeyDown={(e) => e.key === "Enter" && activate(n.data)}
          >
            <rect
              x={n.x0}
              y={n.y0}
              width={n.x1 - n.x0}
              height={n.y1 - n.y0}
              rx={7}
              fill={palette[i % palette.length]}
            />
            <title>
              {n.data.name}: {n.value}
            </title>
            {n.x1 - n.x0 > 70 && (
              <text x={n.x0 + 9} y={n.y0 + 24} fill="white" fontSize="11">
                {n.data.name.slice(0, 18)} · {n.value}
              </text>
            )}
          </g>
        ))}
      </svg>
    );
  }
  if (mode === "circles") {
    const pack = d3.pack<any>().size([600, 300]).padding(6)(root);
    return (
      <svg data-chart viewBox="0 0 600 300" role="img" aria-label={label}>
        {pack.leaves().map((n: any, i) => (
          <g
            key={i}
            role="button"
            tabIndex={0}
            onClick={() => activate(n.data)}
            onKeyDown={(e) => e.key === "Enter" && activate(n.data)}
          >
            <circle
              cx={n.x}
              cy={n.y}
              r={n.r}
              fill={palette[i % palette.length]}
              opacity=".85"
            />
            <title>
              {n.data.name}: {n.value}
            </title>
            {n.r > 22 && (
              <text
                x={n.x}
                y={n.y}
                textAnchor="middle"
                fill="white"
                fontSize="11"
              >
                {n.data.name.slice(0, 14)}
                <tspan x={n.x} dy="17">
                  {n.value}
                </tspan>
              </text>
            )}
          </g>
        ))}
      </svg>
    );
  }
  const arcs = d3
      .pie<any>()
      .sort(null)
      .value((d) => d.count)(rows),
    arc = d3
      .arc<any>()
      .innerRadius(62)
      .outerRadius(88)
      .padAngle(0.025)
      .cornerRadius(3);
  return (
    <div className="a-distribution">
      <svg data-chart viewBox="0 0 210 210" role="img" aria-label={label}>
        {arcs.map((a, i) => (
          <path
            key={i}
            transform="translate(105 105)"
            d={arc(a) || ""}
            fill={palette[i % palette.length]}
            role="button"
            tabIndex={0}
            aria-label={`${a.data.name}: ${a.data.count}`}
            onClick={() => activate(a.data)}
            onKeyDown={(e) => e.key === "Enter" && activate(a.data)}
          >
            <title>
              {a.data.name}: {a.data.count} (
              {((a.data.count / total) * 100).toFixed(1)}%)
            </title>
          </path>
        ))}
        <text
          x="105"
          y="102"
          textAnchor="middle"
          fontWeight="750"
          fontSize="24"
          fill="#24203e"
        >
          {total.toLocaleString()}
        </text>
        <text x="105" y="122" textAnchor="middle" fontSize="10" fill="#8e829d">
          Affiliations
        </text>
      </svg>
      <div>
        {rows.slice(0, 8).map((r, i) => (
          <button key={r.id || r.name} onClick={() => activate(r)}>
            <i style={{ background: palette[i % palette.length] }} />
            <span>{r.name}</span>
            <small>{((r.count / total) * 100).toFixed(1)}%</small>
            <b>{r.count.toLocaleString()}</b>
          </button>
        ))}
      </div>
    </div>
  );
}
export function CohortHeatmap({ data }: { data: any[] }) {
  const groups = d3.groups(data, (d) => d.cohort);
  return (
    <div className="a-scroll">
      <table className="a-heatmap">
        <thead>
          <tr>
            <th>Join cohort</th>
            <th>People</th>
            {[1, 3, 6, 9, 12, 18, 24].map((m) => (
              <th key={m}>{m}M</th>
            ))}
          </tr>
        </thead>
        <tbody>
          {groups.map(([month, cells]) => (
            <tr key={month}>
              <th>{month}</th>
              <td>{cells[0].size.toLocaleString()}</td>
              {cells.map((c) => (
                <td
                  key={c.months}
                  style={{
                    background:
                      c.retained == null
                        ? "#f2eef8"
                        : d3.interpolatePurples(
                            0.15 + (0.8 * c.retained) / c.size,
                          ),
                    color:
                      c.retained != null && c.retained / c.size > 0.6
                        ? "white"
                        : "#77658e",
                  }}
                  title={
                    c.retained == null
                      ? c.observed == null
                        ? "Observation period not complete"
                        : `Historical coverage incomplete: ${c.observed}/${c.size} observed`
                      : `${c.retained} retained of ${c.size} people at ${c.months} months`
                  }
                >
                  {c.retained == null
                    ? "—"
                    : ((c.retained / c.size) * 100).toFixed(1) + "%"}
                </td>
              ))}
            </tr>
          ))}
        </tbody>
      </table>
      {!groups.length && <Empty text="No joining cohorts in this period." />}
      <p className="a-chart-help">
        — Future observation or incomplete history. Missing observations are
        never treated as churn.
      </p>
    </div>
  );
}
export function FlowChart({ data }: { data: any[] }) {
  if (!data?.length)
    return <Empty text="No recorded membership transitions in this period." />;
  const sources = Array.from(new Set(data.map((d) => "From · " + d.source))),
    targets = Array.from(new Set(data.map((d) => "To · " + d.target))),
    nodes = [...sources, ...targets].map((name) => ({ name })),
    graph = sankey<any, any>()
      .nodeWidth(12)
      .nodePadding(15)
      .extent([
        [15, 15],
        [685, 285],
      ])({
      nodes,
      links: data.map((d) => ({
        source: sources.indexOf("From · " + d.source),
        target: sources.length + targets.indexOf("To · " + d.target),
        value: d.count,
      })),
    });
  return (
    <>
      <svg
        data-chart
        viewBox="0 0 700 310"
        role="img"
        aria-label="Recorded membership status transitions"
      >
        {graph.links.map((l: any, i) => (
          <path
            key={i}
            d={sankeyLinkHorizontal()(l) || ""}
            fill="none"
            stroke={palette[i % palette.length]}
            strokeOpacity=".28"
            strokeWidth={Math.max(1, l.width)}
          >
            <title>
              {l.source.name} → {l.target.name}: {l.value} events
            </title>
          </path>
        ))}
        {graph.nodes.map((n: any, i) => (
          <g key={i}>
            <rect
              x={n.x0}
              y={n.y0}
              width={n.x1 - n.x0}
              height={Math.max(1, n.y1 - n.y0)}
              rx={3}
              fill={palette[i % palette.length]}
            />
            <text
              x={n.x0 < 350 ? n.x1 + 6 : n.x0 - 6}
              y={(n.y0 + n.y1) / 2}
              textAnchor={n.x0 < 350 ? "start" : "end"}
              fontSize="11"
              fill="#514260"
            >
              {n.name.replace(/^(From|To) · /, "")} · {n.value}
            </text>
          </g>
        ))}
      </svg>
      <p className="a-chart-help">
        From-state → to-state events. A person may appear repeatedly; this is
        not a unique-person conversion funnel.
      </p>
    </>
  );
}
