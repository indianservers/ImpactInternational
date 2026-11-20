import { useEffect, useId, useMemo, useRef, useState } from "react";
import * as d3 from "d3";
import { ArrowUpRight, Expand, Minus, Plus, RotateCcw, Search, Network, CircleDot, Orbit, ChevronRight } from "lucide-react";
import type { UnitNode } from "./pages/OrganizationHierarchyCharts";
import { useLanguage } from "./i18n";
import "./HierarchyExplorer.css";

const palette: Record<string, string> = { International: "#153454", National: "#b88b35", Region: "#238a8b", Club: "#7462af" };
const color = (node: UnitNode) => palette[node.level] || "#667b94";
const flatten = (node: UnitNode): UnitNode[] => [node, ...node.children.flatMap(flatten)];
const short = (text: string, length = 27) => text.length > length ? text.slice(0, length - 1) + "…" : text;
type Props = { root: UnitNode; mode?: string; metric?: string; membersAllowed?: boolean; leadershipAllowed?: boolean; onSelect?: (id: string) => void; onOpen?: (id: string) => void; affiliation?: boolean };

export default function HierarchyExplorer({ root, mode: initialMode = "tree", metric = "clubs", membersAllowed = true, leadershipAllowed = true, onSelect, onOpen, affiliation = false }: Props) {
  const { t } = useLanguage(), uid = useId().replace(/:/g, "");
  const wrap = useRef<HTMLDivElement>(null), svg = useRef<SVGSVGElement>(null);
  const controls = useRef({ zoom: (_: number) => {}, fit: () => {} });
  const [width, setWidth] = useState(900), [mode, setMode] = useState(initialMode), [query, setQuery] = useState("");
  const [selected, setSelected] = useState(affiliation ? flatten(root).at(-1)?.id || root.id : root.id), [hovered, setHovered] = useState<string | null>(null);
  const [expanded, setExpanded] = useState<Set<string>>(() => new Set(affiliation ? flatten(root).map(n => n.id) : [root.id, ...root.children.map(n => n.id)]));
  const [limits, setLimits] = useState<Record<string, number>>({});
  useEffect(() => setMode(initialMode), [initialMode]);
  useEffect(() => { setSelected(affiliation ? flatten(root).at(-1)?.id || root.id : root.id); setQuery(""); setLimits({}); setExpanded(new Set(affiliation ? flatten(root).map(n => n.id) : [root.id, ...root.children.map(n => n.id)])); }, [root.id, affiliation]);
  useEffect(() => {
    if (!wrap.current) return;
    const observer = new ResizeObserver(entries => setWidth(Math.max(260, Math.round(entries[0].contentRect.width))));
    observer.observe(wrap.current); return () => observer.disconnect();
  }, []);
  const all = useMemo(() => flatten(root), [root]);
  const index = useMemo(() => new Map(all.map(n => [n.id, n])), [all]);
  const active = index.get(selected) || root, hoveredNode = hovered ? index.get(hovered) : undefined;
  const matches = query.trim() ? all.filter(n => (n.name + " " + n.code).toLowerCase().includes(query.trim().toLowerCase())) : [];
  const visibleTree = useMemo(() => {
    const copy = (node: UnitNode): UnitNode => ({ ...node, children: expanded.has(node.id) ? node.children.slice(0, limits[node.id] || 12).map(copy) : [] });
    return copy(root);
  }, [root, expanded, limits]);
  const hierarchy = useMemo(() => d3.hierarchy(visibleTree, n => n.children), [visibleTree]);
  const narrow = width < 640 || affiliation;
  const height = narrow && mode === "tree" ? Math.min(780, Math.max(390, hierarchy.descendants().length * 104 + 55)) : narrow ? 450 : 590;
  const cardWidth = narrow ? Math.max(216, width - 84) : 232, cardHeight = 82;
  const tree = useMemo(() => d3.tree<UnitNode>().nodeSize([108, 278])(hierarchy), [hierarchy]);
  const treeNodes = tree.descendants();
  const positions = new Map(treeNodes.map((n, i) => [n.data.id, narrow ? { x: 22 + n.depth * 17, y: i * 104 + 24 } : { x: n.y, y: n.x }]));
  const radial = useMemo(() => d3.tree<UnitNode>().size([2 * Math.PI, Math.min(width, height) * .34])(d3.hierarchy(visibleTree, n => n.children)), [visibleTree, width, height]);
  const radialPoint = (node: d3.HierarchyPointNode<UnitNode>) => ({ x: Math.cos(node.x - Math.PI / 2) * node.y, y: Math.sin(node.x - Math.PI / 2) * node.y });
  const partition = useMemo(() => d3.partition<UnitNode>().size([2 * Math.PI, Math.min(width, height) * .43])(
    d3.hierarchy(root, n => n.children).sum(n => n.children.length ? 0 : Math.max(1, metric === "members" ? n.members : metric === "active" ? n.activeMembers : n.clubs))), [root, metric, width, height]);
  const arc = d3.arc<d3.HierarchyRectangularNode<UnitNode>>().startAngle(n => n.x0).endAngle(n => n.x1).padAngle(n => Math.min(.003, (n.x1 - n.x0) * .12)).innerRadius(n => n.y0 + 15).outerRadius(n => n.y1 + 13).cornerRadius(2);
  const bounds = treeNodes.reduce((b, n) => { const p = positions.get(n.data.id)!; return { minX: Math.min(b.minX, p.x), maxX: Math.max(b.maxX, p.x + cardWidth), minY: Math.min(b.minY, p.y), maxY: Math.max(b.maxY, p.y + cardHeight) }; }, { minX: Infinity, maxX: -Infinity, minY: Infinity, maxY: -Infinity });
  const layoutKey = `${root.id}:${mode}:${width}:${height}:${treeNodes.map(n => n.data.id).join()}:${[...expanded].join()}:${JSON.stringify(limits)}`;
  useEffect(() => {
    if (!svg.current) return;
    const canvas = d3.select(svg.current);
    const behavior = d3.zoom<SVGSVGElement, unknown>().extent([[0, 0], [width, height]]).scaleExtent([.12, 3])
      .filter(event => !event.button && (event.type !== "wheel" || event.ctrlKey || event.metaKey))
      .on("zoom", event => canvas.select(".hx-scene").attr("transform", event.transform.toString()));
    canvas.call(behavior);
    const topPadding = narrow ? 82 : 22, availableHeight = height - topPadding - 28;
    const scale = mode === "tree" ? Math.min(narrow ? 1 : 1.05, (width - 44) / Math.max(1, bounds.maxX - bounds.minX), availableHeight / Math.max(1, bounds.maxY - bounds.minY)) : 1;
    const fit = mode === "tree" ? d3.zoomIdentity.translate((width - (bounds.maxX - bounds.minX) * scale) / 2 - bounds.minX * scale, topPadding + (availableHeight - (bounds.maxY - bounds.minY) * scale) / 2 - bounds.minY * scale).scale(scale) : d3.zoomIdentity.translate(width / 2, height / 2);
    const duration = window.matchMedia("(prefers-reduced-motion: reduce)").matches ? 0 : 350;
    canvas.call(behavior.transform, fit);
    controls.current = { zoom: factor => { canvas.transition().duration(duration).call(behavior.scaleBy, factor); }, fit: () => { canvas.transition().duration(duration).call(behavior.transform, fit); } };
    return () => { canvas.interrupt(); canvas.on(".zoom", null); };
  }, [layoutKey]);
  function toggle(node: UnitNode) { setExpanded(previous => { const next = new Set(previous); next.has(node.id) ? next.delete(node.id) : next.add(node.id); return next; }); }
  function focus(node: UnitNode) {
    setSelected(node.id); setHovered(null);
    const ancestors: string[] = []; let cursor: UnitNode | undefined = node;
    while (cursor) { ancestors.push(cursor.id); cursor = cursor.parentId ? index.get(cursor.parentId) : undefined; }
    setExpanded(previous => new Set([...previous, ...ancestors]));
    if (node.parentId) { const parent = index.get(node.parentId); const at = parent?.children.findIndex(n => n.id === node.id) ?? -1; if (at >= 12) setLimits(previous => ({ ...previous, [node.parentId!]: at + 1 })); }
  }
  const ancestry: UnitNode[] = []; let cursor: UnitNode | undefined = active;
  while (cursor) { ancestry.unshift(cursor); cursor = cursor.parentId ? index.get(cursor.parentId) : undefined; }
  const title = (node: UnitNode) => `${node.name} · ${node.level}\n${node.code}\n${node.immediateChildren} child units${membersAllowed ? ` · ${node.members} members` : ""}`;
  const activateKey = (event: React.KeyboardEvent, node: UnitNode) => { if (event.key === "Enter" || event.key === " ") { event.preventDefault(); focus(node); } };
  return <section className={"hierarchy-explorer" + (affiliation ? " hx-affiliation" : "")} aria-label={affiliation ? "Member affiliation chart" : "Interactive organization hierarchy"}>
    <header className="hx-heading"><div><span className="hx-eyebrow"><Network size={13}/>{affiliation ? "YOUR PLACE IN THE COMMUNITY" : "CONNECTED AT EVERY LEVEL"}</span><h3>{affiliation ? "One member. One connected community." : "Explore the Impact network"}</h3><p>{affiliation ? "Follow the path from headquarters to the member’s club." : "Select a unit to discover its people, reach and connections."}</p></div><div className="hx-summary"><strong>{affiliation ? all.length : all.length.toLocaleString()}</strong><span>{affiliation ? "connected levels" : "loaded units"}</span></div></header>
    <div className="hx-toolbar"><div className="hx-view-switch" role="group" aria-label="Hierarchy chart layout">{[{ key: "tree", name: "Tree", Icon: Network }, { key: "radial", name: "Radial", Icon: Orbit }, { key: "sunburst", name: "Sunburst", Icon: CircleDot }].map(({ key, name, Icon }) => <button type="button" key={key} aria-pressed={mode === key} className={mode === key ? "active" : ""} onClick={() => setMode(key)}><Icon size={15}/>{name}</button>)}</div><label className="hx-search"><Search size={16}/><input aria-label="Find a unit in the chart" value={query} onChange={e => setQuery(e.target.value)} placeholder="Find a unit or code…"/></label></div>
    {query.trim() && <div className="hx-results" aria-live="polite">{matches.slice(0, 8).map(node => <button type="button" key={node.id} onClick={() => { if (onSelect) onSelect(node.id); else focus(node); setQuery(""); }}><span style={{ background: color(node) }}/><strong>{node.name}</strong><small>{node.level}</small><ChevronRight size={14}/></button>)}{!matches.length && <p>No matching unit in this loaded branch.</p>}{matches.length > 8 && <p>{matches.length} matches. Refine your search to find a specific unit.</p>}</div>}
    <div className="hx-workspace"><div className="hx-canvas-wrap" ref={wrap}>
      <div className="hx-canvas-tools"><button type="button" aria-label="Zoom hierarchy in" onClick={() => controls.current.zoom(1.35)}><Plus size={17}/></button><button type="button" aria-label="Zoom hierarchy out" onClick={() => controls.current.zoom(1 / 1.35)}><Minus size={17}/></button><button type="button" aria-label="Fit hierarchy to screen" onClick={() => controls.current.fit()}><Expand size={16}/></button>{mode === "tree" && <button type="button" aria-label="Reset expanded branches" onClick={() => setExpanded(new Set(affiliation ? all.map(n => n.id) : [root.id, ...root.children.map(n => n.id)]))}><RotateCcw size={15}/></button>}</div>
      <svg ref={svg} data-chart viewBox={`0 0 ${width} ${height}`} style={{ height }} role="group" aria-label={`${mode} chart of ${root.name}`}>
        <defs><pattern id={uid + "grid"} width="22" height="22" patternUnits="userSpaceOnUse"><circle cx="1" cy="1" r=".7" fill="#d6e0eb"/></pattern><filter id={uid + "shadow"} x="-15%" y="-20%" width="140%" height="160%"><feDropShadow dx="0" dy="4" stdDeviation="6" floodColor="#153454" floodOpacity=".07"/></filter></defs>
        <rect width={width} height={height} fill={`url(#${uid}grid)`}/>
        <g className="hx-scene">
          {mode === "tree" && <>{tree.links().map(link => { const a = positions.get(link.source.data.id)!, b = positions.get(link.target.data.id)!; const path = narrow ? `M${a.x + 13},${a.y + cardHeight}V${b.y + 30}H${b.x}` : `M${a.x + cardWidth},${a.y + 41}C${a.x + cardWidth + 28},${a.y + 41} ${b.x - 28},${b.y + 41} ${b.x},${b.y + 41}`; return <path key={link.target.data.id} d={path} fill="none" stroke={color(link.target.data)} strokeOpacity=".4" strokeWidth="1.7"/>; })}
            {treeNodes.map(node => { const p = positions.get(node.data.id)!, chosen = selected === node.data.id, tint = color(node.data); return <g key={node.data.id} transform={`translate(${p.x},${p.y})`} className={"hx-node" + (chosen ? " selected" : "")}>
              <rect data-unit={node.data.id} data-parent={node.data.parentId || ""} width={cardWidth} height={cardHeight} rx="12" fill={chosen ? "#fefaf0" : "#fff"} stroke={chosen ? "#c99e45" : "#dce5ef"} strokeWidth={chosen ? 1.8 : 1} filter={`url(#${uid}shadow)`} tabIndex={0} role="button" aria-label={`Select ${node.data.name}`} onClick={() => setSelected(node.data.id)} onDoubleClick={() => onSelect?.(node.data.id)} onKeyDown={e => activateKey(e, node.data)} onMouseEnter={() => setHovered(node.data.id)} onMouseLeave={() => setHovered(null)}><title>{title(node.data)}</title></rect>
              <rect width="4" x="0" y="13" height="55" rx="2" fill={tint} pointerEvents="none"/><circle cx="24" cy="25" r="11" fill={tint} fillOpacity=".1" pointerEvents="none"/><text x="24" y="29" fill={tint} fontSize="11" fontWeight="750" textAnchor="middle" pointerEvents="none">{node.data.level[0]}</text>
              <text x="43" y="28" fill={tint} fontSize="10" fontWeight="700" letterSpacing=".8" pointerEvents="none">{node.data.level.toUpperCase()}</text><text x="15" y="51" fill="#17314f" fontSize="13" fontWeight="650" pointerEvents="none">{short(node.data.name, narrow ? 28 : 27)}</text><text x="15" y="69" fill="#718096" fontSize="10" pointerEvents="none">{affiliation ? node.data.code : `${node.data.immediateChildren} child units${membersAllowed ? ` · ${node.data.members.toLocaleString()} people` : ""}`}</text>
              {!!index.get(node.data.id)?.children.length && <g transform={`translate(${cardWidth - 24},24)`} role="button" tabIndex={0} aria-label={`${expanded.has(node.data.id) ? "Collapse" : "Expand"} ${node.data.name}`} onClick={() => toggle(node.data)} onKeyDown={e => { if (e.key === "Enter" || e.key === " ") { e.preventDefault(); toggle(node.data); } }} className="hx-expand"><circle r="11" fill="#edf2f7"/><path d={expanded.has(node.data.id) ? "M-4,0H4" : "M-4,0H4M0,-4V4"} stroke="#365273" strokeWidth="1.5"/></g>}
            </g>; })}</>}
          {mode === "radial" && <>{radial.links().map(link => <path key={link.target.data.id} d={d3.linkRadial<d3.HierarchyPointLink<UnitNode>, d3.HierarchyPointNode<UnitNode>>().angle(n => n.x).radius(n => n.y)(link) || ""} fill="none" stroke={color(link.target.data)} strokeWidth="1.5" strokeOpacity=".35"/>)}{radial.descendants().map(node => { const p = radialPoint(node); return <g key={node.data.id} transform={`translate(${p.x},${p.y})`}><circle data-unit={node.data.id} r={node.depth ? 10 : 24} fill={color(node.data)} stroke={selected === node.data.id ? "#e7bd60" : "white"} strokeWidth="3" role="button" tabIndex={0} aria-label={`Select ${node.data.name}`} onClick={() => focus(node.data)} onKeyDown={e => activateKey(e, node.data)}><title>{title(node.data)}</title></circle><text x={node.depth ? p.x < 0 ? -16 : 16 : 0} y={node.depth ? 4 : 42} textAnchor={node.depth ? p.x < 0 ? "end" : "start" : "middle"} fill="#254565" fontSize="11" fontWeight={node.depth ? "500" : "700"} pointerEvents="none">{short(node.data.name, narrow ? 14 : 23)}</text></g>; })}</>}
          {mode === "sunburst" && <>{partition.descendants().slice(1).map(node => <path key={node.data.id} data-unit={node.data.id} d={arc(node) || ""} fill={color(node.data)} fillOpacity={selected === node.data.id ? 1 : .78} stroke={selected === node.data.id ? "#f0c76d" : "white"} strokeWidth={selected === node.data.id ? 2 : node.x1 - node.x0 < .03 ? .2 : 1} tabIndex={0} role="button" aria-label={`Select ${node.data.name}`} onClick={() => setSelected(node.data.id)} onKeyDown={e => activateKey(e, node.data)} onMouseEnter={() => setHovered(node.data.id)} onMouseLeave={() => setHovered(null)}><title>{title(node.data)}</title></path>)}<circle r={Math.max(38, partition.y1 + 13)} fill="#153454"/><image href="/brand/impact-logo.webp" x="-20" y="-32" width="40" height="40"/><text y="26" textAnchor="middle" fill="white" fontSize="11">{root.level}</text></>}
        </g>
      </svg>
      {hoveredNode && <div className="hx-hover" role="status"><strong>{hoveredNode.name}</strong><span>{hoveredNode.level} · {hoveredNode.code}</span></div>}
      <div className="hx-canvas-caption"><span>Drag to pan · pinch or Ctrl + scroll to zoom</span><span>{mode === "sunburst" ? all.length : treeNodes.length} visible units</span></div>
    </div><aside className="hx-details" aria-label="Selected hierarchy unit" aria-live="polite"><span className="hx-unit-badge" style={{ color: color(active), background: color(active) + "12" }}>{t(active.level)}</span><h4>{active.name}</h4><p className="hx-unit-code">{active.code || "Organizational unit"}</p><div className="hx-path">{ancestry.map((unit, i) => <span key={unit.id}>{i > 0 && <ChevronRight size={12}/>}<button type="button" onClick={() => focus(unit)}>{unit.name}</button></span>)}</div>
      {!affiliation && <dl><div><dt>Child units</dt><dd>{active.immediateChildren.toLocaleString()}</dd></div><div><dt>Clubs in branch</dt><dd>{active.clubs.toLocaleString()}</dd></div>{membersAllowed && <><div><dt>Members</dt><dd>{active.members.toLocaleString()}</dd></div><div><dt>Active members</dt><dd>{active.activeMembers.toLocaleString()}</dd></div></>}{leadershipAllowed && <div><dt>Offices filled · branch</dt><dd>{active.filled} / {active.mandatory}</dd></div>}</dl>}
      <div className="hx-detail-actions">{onSelect && <button type="button" onClick={() => { onSelect(active.id); }}>{active.truncated ? "Load deeper branch" : "Explore this branch"}<ArrowUpRight size={15}/></button>}{onOpen && <button type="button" className="secondary" onClick={() => onOpen(active.id)}>Open unit details<ArrowUpRight size={15}/></button>}{!!active.children.length && <button type="button" className="secondary" onClick={() => toggle(active)}>{expanded.has(active.id) ? "Collapse children" : "Expand children"}</button>}{active.children.length > (limits[active.id] || 12) && <button type="button" className="secondary" onClick={() => { setExpanded(previous => new Set([...previous, active.id])); setLimits(previous => ({ ...previous, [active.id]: (previous[active.id] || 12) + 12 })); }}>Show 12 more children</button>}</div>
      {active.truncated && <p className="hx-note">More units continue beyond the loaded chart. Explore this branch to load them.</p>}{!active.children.length && !active.truncated && <p className="hx-note">{affiliation ? "This is the member’s club in the recorded affiliation." : "No child units are recorded in this loaded branch."}</p>}
    </aside></div>
    <footer className="hx-legend">{Object.entries(palette).map(([level, tint]) => <span key={level}><i style={{ background: tint }}/>{t(level)}</span>)}</footer>
    {mode === "sunburst" && <p className="hx-sizing-note">Segment area follows the selected metric. Empty units retain a minimum marker so they can still be selected.</p>}
  </section>;
}

export function MemberHierarchyDiagram({ path }: { path: any[] }) {
  const ordered = [...path].reverse();
  if (!ordered.length) return <p>No organizational affiliation is shared in this workspace.</p>;
  const nodes: UnitNode[] = ordered.map((node, i) => ({ id: node.id, parentId: i ? ordered[i - 1].id : null, levelId: node.levelId || "", name: node.name, code: node.code || "", level: node.kind || node.level || "Unit", status: "Active", country: "", state: "", city: "", createdAt: "", immediateChildren: i < ordered.length - 1 ? 1 : 0, clubs: 0, members: 0, activeMembers: 0, joined: 0, unitsCreated: 0, previousCreated: 0, filled: 0, mandatory: 0, truncated: false, children: [] }));
  nodes.forEach((node, i) => { if (nodes[i + 1]) node.children = [nodes[i + 1]]; });
  return <HierarchyExplorer root={nodes[0]} membersAllowed={false} leadershipAllowed={false} affiliation />;
}
