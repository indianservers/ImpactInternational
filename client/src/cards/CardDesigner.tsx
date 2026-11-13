import { useState, useRef, useEffect } from "react";
import { useParams, Link, useNavigate } from "react-router-dom";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import {
  Stage,
  Layer,
  Rect,
  Ellipse,
  Text,
  Image as CanvasImage,
  Transformer,
  Line,
  Group,
} from "react-konva";
import {
  ArrowLeft,
  Undo2,
  Redo2,
  Save,
  Upload,
  MousePointer2,
  Hand,
  Plus,
  Copy,
  Trash2,
  Lock,
  Unlock,
  Eye,
  Layers,
  Type,
  Image as ImageIcon,
  Shapes,
  QrCode,
  Palette,
  Users,
  ZoomIn,
  ZoomOut,
  AlignCenter,
  ArrowUp,
  ArrowDown,
  Check,
} from "lucide-react";
import { api } from "../api";
import { useWorkspace, ErrorBox, Skeleton } from "../App";
import {
  element,
  checkpoint,
  snap,
  cloneElements,
  physicalPixels,
  type CardDesign,
  type CardElement,
} from "./design";
import "./IdCards.css";
type Row = Record<string, any>;
function Picture({ src, ...props }: any) {
  const [image, setImage] = useState<HTMLImageElement>();
  useEffect(() => {
    let current = true;
    setImage(undefined);
    const img = new window.Image();
    img.onload = () => {
      if (current) setImage(img);
    };
    img.src = src;
    return () => {
      current = false;
    };
  }, [src]);
  if (!image) return <Rect {...props} fill="#e2e9e4" />;
  const ar = props.width / props.height;
  const sw = Math.min(image.width, image.height * ar) / (props.cropZoom || 1);
  const sh = sw / ar;
  const radius = props.radius || 0;
  const imageProps = { ...props };
  for (const key of [
    "x",
    "y",
    "rotation",
    "draggable",
    "id",
    "onClick",
    "onTap",
    "onDragStart",
    "onDragMove",
    "onDragEnd",
    "onTransformEnd",
  ])
    delete imageProps[key];
  return (
    <Group
      {...props}
      clipFunc={(ctx: any) => {
        ctx.beginPath();
        if (radius >= Math.min(props.width, props.height) / 2) {
          ctx.ellipse(
            props.width / 2,
            props.height / 2,
            props.width / 2,
            props.height / 2,
            0,
            0,
            Math.PI * 2,
          );
        } else {
          ctx.roundRect(0, 0, props.width, props.height, radius);
        }
        ctx.closePath();
      }}
    >
      <CanvasImage
        {...imageProps}
        x={0}
        y={0}
        rotation={0}
        draggable={false}
        listening={false}
        image={image}
        crop={{
          x: (image.width - sw) * (props.cropX ?? 0.5),
          y: (image.height - sh) * (props.cropY ?? 0.5),
          width: sw,
          height: sh,
        }}
      />
    </Group>
  );
}
export default function CardDesigner() {
  const [mobilePanel, setMobilePanel] = useState("canvas");
  const { id } = useParams(),
    { w } = useWorkspace(),
    qc = useQueryClient(),
    nav = useNavigate();
  const q = useQuery<any>({
    queryKey: ["card-template", id],
    queryFn: () => api("/id-cards/templates/" + id),
    refetchOnWindowFocus: false,
    staleTime: Infinity,
  });
  const assetLibrary = useQuery<any>({
    queryKey: ["card-assets", w?.id],
    queryFn: () => api("/id-cards/assets"),
  });
  const options = useQuery<any>({
    queryKey: ["card-options", w?.id],
    queryFn: () => api("/id-cards/options"),
  });
  const [doc, setDoc] = useState<CardDesign>(),
    [name, setName] = useState(""),
    [side, setSide] = useState<"front" | "back">("front"),
    [tab, setTab] = useState("Templates"),
    [selected, setSelected] = useState<string[]>([]),
    [zoom, setZoom] = useState(0.65),
    [pan, setPan] = useState(false),
    [grid, setGrid] = useState(true),
    [guide, setGuide] = useState<any>({}),
    [error, setError] = useState<unknown>(),
    [busy, setBusy] = useState(false),
    [saved, setSaved] = useState(""),
    [member, setMember] = useState(""),
    [query, setQuery] = useState(""),
    [sample, setSample] = useState<Row>(),
    [preview, setPreview] = useState(""),
    [previewing, setPreviewing] = useState(false),
    [assets, setAssets] = useState<Row[]>([]),
    [dirty, setDirty] = useState(false),
    [version, setVersion] = useState(1);
  const history = useRef<CardDesign[]>([]),
    cursor = useRef(0),
    stage = useRef<any>(null),
    transformer = useRef<any>(null),
    dragStart = useRef<any>({}),
    [revision, setRevision] = useState(0);
  const members = useQuery<any>({
    queryKey: ["card-preview-members", w?.id, query],
    queryFn: () => api("/id-cards/members?search=" + encodeURIComponent(query)),
    enabled: tab === "Member Fields" || previewing,
  });
  const bindingValues = useQuery<any>({
    queryKey: ["card-bindings", w?.id, member],
    queryFn: () => api("/id-cards/members/" + member + "/bindings"),
    enabled: !!member,
  });
  useEffect(() => {
    let active = true;
    document.fonts.load('12px "Noto Sans"').then(() => {
      if (active) stage.current?.batchDraw();
    });
    return () => {
      active = false;
    };
  }, []);
  useEffect(() => {
    if (q.data) {
      const d = q.data.design;
      setDoc(d);
      setName(q.data.template.name);
      setVersion(q.data.template.version);
      history.current = [structuredClone(d)];
      cursor.current = 0;
      setSelected([]);
      setDirty(false);
    }
  }, [q.data]);
  useEffect(() => {
    if (transformer.current && stage.current) {
      transformer.current.nodes(
        selected
          .filter((i) => !doc?.[side].elements.find((e) => e.id === i)?.locked)
          .map((i) => stage.current.findOne("#" + i))
          .filter(Boolean),
      );
      transformer.current.getLayer()?.batchDraw();
    }
  }, [selected, doc, side]);
  useEffect(() => {
    return () => {
      if (preview) URL.revokeObjectURL(preview);
    };
  }, [preview]);
  function commit(next: CardDesign) {
    history.current = checkpoint(history.current, cursor.current, next);
    cursor.current = history.current.length - 1;
    setDoc(next);
    setDirty(true);
    setRevision((v) => v + 1);
  }
  function undo(delta: number) {
    const next = cursor.current + delta;
    if (next >= 0 && next < history.current.length) {
      cursor.current = next;
      setDoc(structuredClone(history.current[next]));
      setDirty(true);
      setRevision((v) => v + 1);
      setSelected([]);
    }
  }
  function layers(next: CardElement[]) {
    if (doc) commit({ ...doc, [side]: { ...doc[side], elements: next } });
  }
  function change(patch: Partial<CardElement>) {
    if (doc)
      layers(
        doc[side].elements.map((e) =>
          selected.includes(e.id) &&
          (!e.locked || Object.hasOwn(patch, "locked"))
            ? { ...e, ...patch }
            : e,
        ),
      );
  }
  function add(kind: string, binding = "") {
    if (!doc) return;
    const e = element(kind, binding);
    layers([...doc[side].elements, e]);
    setSelected([e.id]);
  }
  function duplicate() {
    if (doc) {
      const copies = cloneElements(doc[side].elements, selected);
      layers([...doc[side].elements, ...copies]);
      setSelected(copies.map((e) => e.id));
    }
  }
  function remove() {
    if (doc) {
      layers(
        doc[side].elements.filter((e) => !selected.includes(e.id) || e.locked),
      );
      setSelected([]);
    }
  }
  useEffect(() => {
    function key(e: KeyboardEvent) {
      if ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === "s") {e.preventDefault(); if(!busy) void save(); return;}
      if ((e.target as HTMLElement).closest("input,textarea,select,button,a,[contenteditable=true]")) return;
      if ((e.ctrlKey || e.metaKey) && e.key === "z") {
        e.preventDefault();
        undo(e.shiftKey ? 1 : -1);
      } else if ((e.ctrlKey || e.metaKey) && e.key === "y") {
        e.preventDefault();
        undo(1);
      } else if ((e.ctrlKey || e.metaKey) && e.key === "d") {
        e.preventDefault();
        duplicate();
      } else if ((e.ctrlKey || e.metaKey) && e.key === "a") {
        e.preventDefault();
        setSelected(
          doc?.[side].elements.filter((e) => !e.locked).map((e) => e.id) || [],
        );
      } else if (e.key === "Delete" || e.key === "Backspace") {
        e.preventDefault();
        remove();
      } else if (e.key === "Escape") {
        setSelected([]);
        setPreviewing(false);
      } else if (
        ["ArrowUp", "ArrowDown", "ArrowLeft", "ArrowRight"].includes(e.key) &&
        doc
      ) {
        e.preventDefault();
        const step = e.shiftKey ? 1 : 0.2;
        layers(
          doc[side].elements.map((el) =>
            selected.includes(el.id) && !el.locked
              ? {
                  ...el,
                  x:
                    el.x +
                    (e.key === "ArrowRight"
                      ? step
                      : e.key === "ArrowLeft"
                        ? -step
                        : 0),
                  y:
                    el.y +
                    (e.key === "ArrowDown"
                      ? step
                      : e.key === "ArrowUp"
                        ? -step
                        : 0),
                }
              : el,
          ),
        );
      }
    }
    window.addEventListener("keydown", key);
    return () => window.removeEventListener("keydown", key);
  }, [doc, selected, side, revision, busy, version, name]);
  async function save(copy = false) {
    if (!doc) return;
    setBusy(true);
    setError(undefined);
    try {
      const result = await api(
        "/id-cards/templates" + (copy ? "" : "/" + id),
        copy ? "POST" : "PUT",
        {
          orgId: q.data.template.orgId,
          name: copy ? name + " copy" : name,
          design: doc,
          version,
        },
      );
      setVersion(result.version);
      setDirty(false);
      setSaved("Saved");
      await qc.invalidateQueries({ queryKey: ["id-cards"] });
      if (copy) nav("/app/id-cards/designer/" + result.id);
      return result;
    } catch (e) {
      setError(e);
      throw e;
    } finally {
      setBusy(false);
    }
  }
  async function publish() {
    try {
      await save();
      const v = await api("/id-cards/templates/" + id + "/publish", "POST", {});
      setSaved("Published version " + v.number);
      await qc.invalidateQueries({ queryKey: ["id-cards"] });
    } catch (e) {
      setError(e);
    }
  }
  async function upload(file: File) {
    setError(undefined);
    try {
      const csrf = await api("/auth/csrf"),
        form = new FormData();
      form.append("file", file);
      form.append("orgId", q.data.template.orgId);
      const r = await fetch("/api/v1/id-cards/assets", {
        method: "POST",
        headers: { "X-CSRF": csrf.token },
        body: form,
      });
      const result = await r.json();
      if (!r.ok)
        throw new Error(result.detail || result.error || "Upload failed");
      setAssets((a) => [...a, result]);
      assetLibrary.refetch();
      const e = {
        ...element("Image"),
        asset: result.id,
        width: 25,
        height: 25,
      };
      if (doc) layers([...doc[side].elements, e]);
      setSelected([e.id]);
    } catch (e) {
      setError(e);
    }
  }
  async function renderPreview() {
    if (!doc || !member) return;
    setBusy(true);
    setError(undefined);
    try {
      const csrf = await api("/auth/csrf");
      const r = await fetch(
        "/api/v1/id-cards/templates/" +
          id +
          "/preview?back=" +
          (side === "back"),
        {
          method: "POST",
          headers: { "Content-Type": "application/json", "X-CSRF": csrf.token },
          body: JSON.stringify({ membershipId: member, design: doc }),
        },
      );
      if (!r.ok) {
        const b = await r.json();
        throw new Error(b.detail || b.error || "Preview failed");
      }
      setPreview(URL.createObjectURL(await r.blob()));
      setPreviewing(true);
    } catch (e) {
      setError(e);
    } finally {
      setBusy(false);
    }
  }
  useEffect(() => {
    if (!doc) return;
    const fit = () => {
      if (window.matchMedia("(max-width: 760px)").matches) setZoom(Math.max(0.1, Math.min(1, (window.innerWidth - 72) / ((doc.width + 16 + 2 * doc.bleed) * 6))));
    };
    fit();
    window.addEventListener("resize", fit);
    return () => window.removeEventListener("resize", fit);
  }, [doc?.width, doc?.bleed]);
  if (q.isPending || !doc) return <Skeleton />;
  if (q.error) return <ErrorBox error={q.error} />;
  const current = doc[side],
    chosen = current.elements.find((e) => e.id === selected[0]),
    S = 6 * zoom,
    pad = 8 + doc.bleed;
  const tabs = [
    ["Templates", Layers],
    ["Text", Type],
    ["Member Fields", Users],
    ["Photos", ImageIcon],
    ["Logos", ImageIcon],
    ["Elements", Shapes],
    ["Shapes", Shapes],
    ["Backgrounds", Palette],
    ["QR/Barcode", QrCode],
    ["Uploads", Upload],
    ["Layers", Layers],
  ] as const;
  return (
    <div className="card-studio" data-mobile-panel={mobilePanel}>
      <header className="card-studio-toolbar">
        <Link to="/app/id-cards/templates" aria-label="Back to templates">
          <ArrowLeft size={19} />
        </Link>
        <span className="card-studio-mark">ID STUDIO</span>
        <input
          aria-label="Template name"
          value={name}
          onChange={(e) => {
            setName(e.target.value);
            setDirty(true);
          }}
        />
        <span className="card-save-status">
          {dirty ? "Unsaved changes" : saved || "Saved draft"}
        </span>
        <button
          aria-label="Undo"
          disabled={cursor.current === 0}
          onClick={() => undo(-1)}
        >
          <Undo2 size={17} />
        </button>
        <button
          aria-label="Redo"
          disabled={cursor.current === history.current.length - 1}
          onClick={() => undo(1)}
        >
          <Redo2 size={17} />
        </button>
        <button
          onClick={() => {
            setPreviewing(true);
            setTab("Member Fields");
          }}
        >
          <Eye size={16} /> Preview
        </button>
        <button onClick={() => save(true).catch(() => {})}>Save as</button>
        <button onClick={() => save().catch(() => {})} disabled={busy}>
          <Save size={16} /> Save
        </button>
        {w?.permissions.includes("cards.publish") && (
          <button className="card-primary" disabled={busy} onClick={publish}>
            <Check size={16} /> Publish template
          </button>
        )}
      </header>
      <ErrorBox error={error} />
      <nav className="designer-phone-tabs" aria-label="Designer views">{["canvas", "elements", "properties"].map(panel => <button type="button" className={mobilePanel === panel ? "secondary active" : "secondary"} aria-pressed={mobilePanel === panel} key={panel} onClick={() => setMobilePanel(panel)}>{panel === "canvas" ? "Canvas" : panel === "elements" ? "Elements" : "Properties"}</button>)}</nav>
      <div className="card-studio-body">
        <nav className="card-tool-tabs">
          {tabs.map(([label, Icon]) => (
            <button
              className={tab === label ? "active" : ""}
              key={label}
              onClick={() => setTab(label)}
            >
              <Icon size={20} />
              <span>{label}</span>
            </button>
          ))}
        </nav>
        <aside className="card-resource-panel">
          <label className="designer-resource-picker">Element tools<select aria-label="Element tools" value={tab} onChange={e => setTab(e.target.value)}>{tabs.map(([label]) => <option key={label}>{label}</option>)}</select></label>
          <h3>{tab}</h3>
          {tab === "Templates" && (
            <>
              <p>Your canvas, your identity.</p>
              <p className="card-muted">
                Dimensions are stored in millimetres. Publishing creates an
                immutable version; saving edits this draft.
              </p>
              <label>
                Card format
                <select
                  value={doc.width < doc.height ? "Portrait" : "Landscape"}
                  onChange={(e) =>
                    commit({
                      ...doc,
                      width: e.target.value === "Portrait" ? 53.98 : 85.6,
                      height: e.target.value === "Portrait" ? 85.6 : 53.98,
                    })
                  }
                >
                  <option>Landscape</option>
                  <option>Portrait</option>
                </select>
              </label>
              <div className="card-field-pair">
                {(["width", "height", "bleed", "safe"] as const).map((k) => (
                  <label key={k}>
                    {k} (mm)
                    <input
                      type="number"
                      step=".01"
                      value={doc[k]}
                      onChange={(e) => commit({ ...doc, [k]: +e.target.value })}
                    />
                  </label>
                ))}
              </div>
              <label>
                <input
                  type="checkbox"
                  checked={doc.doubleSided}
                  onChange={(e) =>
                    commit({ ...doc, doubleSided: e.target.checked })
                  }
                />{" "}
                Two-sided card
              </label>
              <label>
                Membership category
                <select
                  value={doc.categoryId || ""}
                  onChange={(e) =>
                    commit({ ...doc, categoryId: e.target.value || null })
                  }
                >
                  <option value="">All eligible categories</option>
                  {options.data?.categories.map((c: Row) => (
                    <option key={c.id} value={c.id}>
                      {c.name}
                    </option>
                  ))}
                </select>
              </label>
              <label>
                Leadership position
                <select
                  value={doc.positionId || ""}
                  onChange={(e) =>
                    commit({ ...doc, positionId: e.target.value || null })
                  }
                >
                  <option value="">No required position</option>
                  {options.data?.positions.map((c: Row) => (
                    <option key={c.id} value={c.id}>
                      {c.name}
                    </option>
                  ))}
                </select>
              </label>
            </>
          )}
          {tab === "Text" && (
            <>
              <button className="card-add-item" onClick={() => add("Text")}>
                Add a text box <Plus size={15} />
              </button>
              <p className="card-muted">
                Noto Sans · bundled open font. Text remains measured in physical
                units.
              </p>
            </>
          )}
          {tab === "Member Fields" && (
            <>
              <input
                placeholder="Find an authorized sample member"
                aria-label="Find sample member"
                value={query}
                onChange={(e) => setQuery(e.target.value)}
              />
              <select
                aria-label="Sample member"
                value={member}
                onChange={(e) => {
                  setMember(e.target.value);
                  setSample(
                    members.data?.items.find(
                      (m: Row) => m.id === e.target.value,
                    ),
                  );
                }}
              >
                <option value="">Choose an authorized member</option>
                {members.data?.items.map((m: Row) => (
                  <option key={m.id} value={m.id}>
                    {m.name} · {m.number}
                  </option>
                ))}
              </select>
              <button
                className="card-add-item"
                onClick={renderPreview}
                disabled={!member || busy}
              >
                Render member preview <Eye size={15} />
              </button>
              {options.data?.fields
                .filter((f: Row) => f.type !== "Image")
                .map((f: Row) => (
                  <button
                    className="card-field-token"
                    key={f.key}
                    title={f.source + " · " + f.privacy + " · " + f.fallback}
                    onClick={() => add("Text", f.key)}
                  >
                    {"{{" + f.key + "}}"}
                  </button>
                ))}
            </>
          )}
          {["Photos", "Logos", "Uploads"].includes(tab) && (
            <>
              <label className="card-upload-drop">
                <Upload size={23} />
                <strong>Upload a secure image</strong>
                <span>PNG or JPEG · up to 2 MB</span>
                <input
                  type="file"
                  accept="image/png,image/jpeg"
                  onChange={(e) =>
                    e.target.files?.[0] && upload(e.target.files[0])
                  }
                />
              </label>
              {tab === "Photos" && (
                <button
                  className="card-add-item"
                  onClick={() => add("Image", "member.photo")}
                >
                  Dynamic member photo <Plus size={15} />
                </button>
              )}
              {tab === "Logos" && (
                <p className="card-muted">
                  Upload official Impact International or club artwork. A published version
                  freezes the exact image; remote image URLs are not rendered.
                </p>
              )}
              {(assetLibrary.data || assets).map((a: Row) => (
                <button
                  className="card-asset"
                  key={a.id}
                  onClick={() => {
                    const e = { ...element("Image"), asset: a.id };
                    layers([...current.elements, e]);
                  }}
                >
                  <img src={a.url} alt={a.title} />
                  {a.title}
                </button>
              ))}
            </>
          )}
          {["Elements", "Shapes"].includes(tab) &&
            ["Rect", "Ellipse", "Line"].map((k) => (
              <button className="card-add-item" key={k} onClick={() => add(k)}>
                {k}
                <Plus size={16} />
              </button>
            ))}
          {tab === "Backgrounds" && (
            <>
              <label>
                Side background
                <input
                  type="color"
                  value={current.background}
                  onChange={(e) =>
                    commit({
                      ...doc,
                      [side]: { ...current, background: e.target.value },
                    })
                  }
                />
              </label>
              <div className="card-swatches">
                {[
                  "#ffffff",
                  "#f5f7f3",
                  "#174f46",
                  "#142d48",
                  "#e9d9ba",
                  "#27213c",
                ].map((c) => (
                  <button
                    key={c}
                    style={{ background: c }}
                    aria-label={"Background " + c}
                    onClick={() =>
                      commit({ ...doc, [side]: { ...current, background: c } })
                    }
                  />
                ))}
              </div>
            </>
          )}
          {tab === "QR/Barcode" && (
            <>
              <button
                className="card-add-item"
                onClick={() => add("Qr", "card.qr_code")}
              >
                Secure verification QR <QrCode size={18} />
              </button>
              <p className="card-muted">
                The official QR contains an opaque verification link. Preview
                codes cannot validate a card. Barcodes are not enabled.
              </p>
            </>
          )}
          {tab === "Layers" && (
            <>
              {[...current.elements].reverse().map((e) => (
                <button
                  className={
                    "card-layer " + (selected.includes(e.id) ? "selected" : "")
                  }
                  key={e.id}
                  aria-pressed={selected.includes(e.id)}
                  aria-label={`Select layer ${e.binding || e.text || e.kind}${e.locked ? ', locked' : ''}`}
                  onClick={(event)=>setSelected(event.shiftKey?[...new Set([...selected,e.id])]:[e.id])}
                >
                  <Layers size={13} />
                  <span>{e.binding || e.text || e.kind}</span>
                  {e.locked && <Lock size={12} />}
                </button>
              ))}
              <p className="card-muted">
                Shift-click to select several layers. Ctrl+A selects unlocked
                layers.
              </p>
            </>
          )}
        </aside>
        <section className="card-canvas-area" tabIndex={0} aria-label="Card canvas keyboard controls">
          <div className="card-canvas-tools">
            <button
              className={!pan ? "active" : ""}
              aria-label="Select tool"
              onClick={() => setPan(false)}
            >
              <MousePointer2 size={16} />
            </button>
            <button
              className={pan ? "active" : ""}
              aria-label="Pan tool"
              onClick={() => setPan(true)}
            >
              <Hand size={16} />
            </button>
            <span />
            <button
              onClick={() => setSide("front")}
              aria-pressed={side === "front"}
              className={side === "front" ? "active" : ""}
            >
              Front
            </button>
            <button
              disabled={!doc.doubleSided}
              onClick={() => setSide("back")}
              aria-pressed={side === "back"}
              className={side === "back" ? "active" : ""}
            >
              Back
            </button>
            <span />
            <label>
              <input
                type="checkbox"
                checked={grid}
                onChange={(e) => setGrid(e.target.checked)}
              />{" "}
              Snap
            </label>
          </div>
          <div className="card-stage-scroll">
            <div className="card-ruler" style={{width:(doc.width+2*pad)*S}}>
              {Array.from(
                { length: Math.floor(doc.width / 10) + 1 },
                (_, n) => (
                  <span key={n} style={{ left: (pad + n * 10) * S }}>
                    {n * 10}
                  </span>
                ),
              )}
            </div>
            <Stage
              ref={stage}
              width={(doc.width + 2 * pad) * S}
              height={(doc.height + 2 * pad) * S}
              draggable={pan}
              onMouseDown={(e) => {
                if (e.target === e.target.getStage()) setSelected([]);
              }}
              onTouchStart={(e) => {
                if (e.target === e.target.getStage()) setSelected([]);
              }}
            >
              <Layer>
                <Group x={pad * S} y={pad * S} scaleX={S} scaleY={S}>
                  <Rect
                    x={-doc.bleed}
                    y={-doc.bleed}
                    width={doc.width + 2 * doc.bleed}
                    height={doc.height + 2 * doc.bleed}
                    fill="#e6eae6"
                    stroke="#c7cdc9"
                    strokeWidth={0.15}
                    dash={[1, 1]}
                  />
                  <Rect
                    width={doc.width}
                    height={doc.height}
                    fill={current.background}
                    shadowColor="#173c42"
                    shadowBlur={2}
                    shadowOpacity={0.12}
                  />
                  {current.elements.map((el) => {
                    const fallback = sample
                      ? ({
                          "member.full_name": sample.name,
                          "member.member_id": sample.number,
                          "member.club_name": sample.club,
                          "association.name": w?.association,
                        } as Row)
                      : {};
                    const values = bindingValues.data?.values || fallback;
                    const text = el.binding
                      ? (values[el.binding] ?? "{{" + el.binding + "}}")
                      : el.text.replace(
                          /\{\{([^}]+)\}\}/g,
                          (_, key) => values[key] ?? "{{" + key + "}}",
                        );
                    const hidden =
                      !!sample &&
                      el.when &&
                      !values[el.when] &&
                      !(
                        el.when === "member.photo" &&
                        bindingValues.data?.hasPhoto
                      );
                    const props: any = {
                      ...el,
                      id: el.id,
                      x: el.x,
                      y: el.y,
                      width: el.width,
                      height: el.height,
                      rotation: el.rotation,
                      opacity: el.opacity,
                      visible: !hidden,
                      draggable: !el.locked && !pan,
                      stroke: el.color,
                      strokeWidth: el.border,
                      shadowColor: "#000000",
                      shadowBlur: el.shadow,
                      shadowOpacity: 0.18,
                      onClick: (e: any) => {
                        if (el.locked || pan) return;
                        e.cancelBubble = true;
                        const ids = el.group
                          ? current.elements
                              .filter((x) => x.group === el.group && !x.locked)
                              .map((x) => x.id)
                          : [el.id];
                        setSelected(
                          e.evt.shiftKey
                            ? [...new Set([...selected, ...ids])]
                            : ids,
                        );
                      },
                      onTap: () => !el.locked && setSelected([el.id]),
                      onDragStart: () => {
                        dragStart.current = Object.fromEntries(
                          current.elements
                            .filter(
                              (x) => selected.includes(x.id) || x.id === el.id,
                            )
                            .map((x) => [x.id, { x: x.x, y: x.y }]),
                        );
                      },
                      onDragMove: (e: any) => {
                        const sx = snap(
                            e.target.x(),
                            el.width,
                            doc.width,
                            grid,
                          ),
                          sy = snap(e.target.y(), el.height, doc.height, grid);
                        e.target.position({ x: sx.value, y: sy.value });
                        setGuide({ x: sx.guide, y: sy.guide });
                        const start = dragStart.current[el.id];
                        if (start)
                          for (const other of selected.filter(
                            (i) => i !== el.id,
                          )) {
                            const node = stage.current.findOne("#" + other),
                              old = dragStart.current[other];
                            if (node && old)
                              node.position({
                                x: old.x + sx.value - start.x,
                                y: old.y + sy.value - start.y,
                              });
                          }
                      },
                      onDragEnd: () => {
                        layers(
                          current.elements.map((x) => {
                            const node = stage.current.findOne("#" + x.id);
                            return node &&
                              (selected.includes(x.id) || x.id === el.id) &&
                              !x.locked
                              ? { ...x, x: node.x(), y: node.y() }
                              : x;
                          }),
                        );
                        setGuide({});
                      },
                      onTransformEnd: () => {
                        layers(
                          current.elements.map((x) => {
                            const n = stage.current.findOne("#" + x.id);
                            if (!n || !selected.includes(x.id)) return x;
                            const next = {
                              ...x,
                              x: n.x(),
                              y: n.y(),
                              width: Math.max(1, n.width() * n.scaleX()),
                              height: Math.max(1, n.height() * n.scaleY()),
                              rotation: n.rotation(),
                            };
                            n.scale({ x: 1, y: 1 });
                            return next;
                          }),
                        );
                      },
                    };
                    if (el.kind === "Text")
                      return (
                        <Text
                          key={el.id}
                          {...props}
                          text={text}
                          fontFamily="Noto Sans"
                          fontStyle={el.bold ? "bold" : "normal"}
                          fontSize={el.fontSize}
                          fill={el.color}
                          align={el.align.toLowerCase()}
                          lineHeight={1.25}
                        />
                      );
                    if (el.kind === "Ellipse")
                      return (
                        <Group key={el.id} {...props}>
                          <Ellipse
                            x={el.width / 2}
                            y={el.height / 2}
                            radiusX={el.width / 2}
                            radiusY={el.height / 2}
                            fill={el.fill}
                            stroke={el.color}
                            strokeWidth={el.border}
                            listening={false}
                          />
                        </Group>
                      );
                    if (el.kind === "Line")
                      return (
                        <Line
                          key={el.id}
                          {...props}
                          points={[0, 0, el.width, el.height]}
                          strokeWidth={el.border || 0.2}
                        />
                      );
                    if (el.kind === "Image" || el.kind === "Qr") {
                      const src =
                        el.kind === "Qr"
                          ? "/api/v1/id-cards/qr-preview"
                          : el.asset
                            ? doc.parentVersionId && !assetLibrary.data?.some((a:Row)=>a.id===el.asset)
                              ? `/api/v1/id-cards/versions/${doc.parentVersionId}/assets/${el.asset}`
                              : "/api/v1/documents/" + el.asset + "/download?preview=true"
                            : member
                              ? "/api/v1/members/" + member + "/photo"
                              : "";
                      return (
                        <Picture
                          key={el.id}
                          {...props}
                          src={src}
                          cornerRadius={el.radius}
                        />
                      );
                    }
                    return (
                      <Rect key={el.id} {...props} cornerRadius={el.radius} />
                    );
                  })}
                  <Rect
                    x={doc.safe}
                    y={doc.safe}
                    width={doc.width - 2 * doc.safe}
                    height={doc.height - 2 * doc.safe}
                    stroke="#16a085"
                    strokeWidth={0.12}
                    dash={[1, 1]}
                    listening={false}
                  />
                  {guide.x != null && (
                    <Line
                      points={[
                        guide.x,
                        -doc.bleed,
                        guide.x,
                        doc.height + doc.bleed,
                      ]}
                      stroke="#ec4899"
                      strokeWidth={0.15}
                    />
                  )}
                  {guide.y != null && (
                    <Line
                      points={[
                        -doc.bleed,
                        guide.y,
                        doc.width + doc.bleed,
                        guide.y,
                      ]}
                      stroke="#ec4899"
                      strokeWidth={0.15}
                    />
                  )}
                  <Transformer
                    ref={transformer}
                    rotateEnabled
                    flipEnabled={false}
                    borderStroke="#246f60"
                    anchorStroke="#246f60"
                    anchorFill="#ffffff"
                    anchorSize={1.3}
                    boundBoxFunc={(old, next) =>
                      next.width < 1 || next.height < 1 ? old : next
                    }
                  />
                </Group>
              </Layer>
            </Stage>
          </div>
          <footer className="card-canvas-footer">
            <span>
              {doc.width.toFixed(2)} × {doc.height.toFixed(2)} mm · 300 DPI ·{" "}
              {physicalPixels(doc.width)} × {physicalPixels(doc.height)} px
            </span>
            <div>
              <button
                aria-label="Zoom out"
                onClick={() => setZoom(Math.max(0.3, zoom - 0.1))}
              >
                <ZoomOut size={16} />
              </button>
              <span>{Math.round(zoom * 100)}%</span>
              <button
                aria-label="Zoom in"
                onClick={() => setZoom(Math.min(3, zoom + 0.1))}
              >
                <ZoomIn size={16} />
              </button>
            </div>
          </footer>
        </section>
        <aside className="card-properties">
          <h3>{chosen ? "Layer properties" : "Design properties"}</h3>
          {chosen ? (
            <>
              <div className="card-property-actions">
                <button title="Duplicate" onClick={duplicate}>
                  <Copy size={16} />
                </button>
                <button
                  title="Delete"
                  disabled={chosen.locked}
                  onClick={remove}
                >
                  <Trash2 size={16} />
                </button>
                <button
                  title="Lock or unlock"
                  onClick={() => change({ locked: !chosen.locked })}
                >
                  {chosen.locked ? <Unlock size={16} /> : <Lock size={16} />}
                </button>
                <button
                  title="Group selected"
                  disabled={selected.length < 2}
                  onClick={() => change({ group: crypto.randomUUID() })}
                >
                  Group
                </button>
                <button title="Ungroup" onClick={() => change({ group: "" })}>
                  Ungroup
                </button>
              </div>
              <p className="card-muted">
                {selected.length} selected · {chosen.kind}
              </p>
              <div className="card-field-pair">
                {(
                  ["x", "y", "width", "height", "rotation", "opacity"] as const
                ).map((k) => (
                  <label key={k}>
                    {k}
                    <input
                      type="number"
                      step={k === "opacity" ? ".05" : ".1"}
                      value={Math.round(chosen[k] * 100) / 100}
                      onChange={(e) => change({ [k]: +e.target.value })}
                    />
                  </label>
                ))}
              </div>
              {chosen.kind === "Text" && (
                <>
                  <label>
                    Text
                    <textarea
                      value={chosen.text}
                      onChange={(e) => change({ text: e.target.value })}
                    />
                  </label>
                  <label>
                    Dynamic binding
                    <select
                      value={chosen.binding}
                      onChange={(e) => change({ binding: e.target.value })}
                    >
                      <option value="">Static text</option>
                      {options.data?.fields
                        .filter((f: Row) => f.type !== "Image")
                        .map((f: Row) => (
                          <option key={f.key}>{f.key}</option>
                        ))}
                    </select>
                  </label>
                  <label>
                    Font size (mm)
                    <input
                      type="number"
                      step=".1"
                      min="1"
                      max="30"
                      value={chosen.fontSize}
                      onChange={(e) => change({ fontSize: +e.target.value })}
                    />
                  </label>
                  <label>
                    Alignment
                    <select
                      value={chosen.align}
                      onChange={(e) => change({ align: e.target.value })}
                    >
                      <option>Left</option>
                      <option>Center</option>
                      <option>Right</option>
                    </select>
                  </label>
                  <label>
                    <input
                      type="checkbox"
                      checked={chosen.bold}
                      onChange={(e) => change({ bold: e.target.checked })}
                    />{" "}
                    Bold
                  </label>
                </>
              )}
              {["Image", "Qr"].includes(chosen.kind) && (
                <>
                  <label>
                    Crop zoom
                    <input
                      type="range"
                      min="1"
                      max="5"
                      step=".05"
                      value={chosen.cropZoom}
                      onChange={(e) => change({ cropZoom: +e.target.value })}
                    />
                  </label>
                  {(["cropX", "cropY"] as const).map((k) => (
                    <label key={k}>
                      {k}
                      <input
                        type="range"
                        min="0"
                        max="1"
                        step=".01"
                        value={chosen[k]}
                        onChange={(e) => change({ [k]: +e.target.value })}
                      />
                    </label>
                  ))}
                  <button
                    onClick={() =>
                      change({
                        width: Math.min(chosen.width, chosen.height),
                        height: Math.min(chosen.width, chosen.height),
                        radius: Math.min(chosen.width, chosen.height) / 2,
                      })
                    }
                  >
                    Circular crop
                  </button>
                  <button onClick={() => change({ radius: 2 })}>
                    Rounded crop
                  </button>
                </>
              )}
              <div className="card-field-pair">
                {(["color", "fill"] as const).map((k) => (
                  <label key={k}>
                    {k}
                    <input
                      type="color"
                      value={chosen[k]}
                      onChange={(e) => change({ [k]: e.target.value })}
                    />
                  </label>
                ))}
                {(["border", "radius", "shadow"] as const).map((k) => (
                  <label key={k}>
                    {k}
                    <input
                      type="number"
                      min="0"
                      step=".1"
                      value={chosen[k]}
                      onChange={(e) => change({ [k]: +e.target.value })}
                    />
                  </label>
                ))}
              </div>
              <label>
                Visible when field has data
                <select
                  value={chosen.when}
                  onChange={(e) => change({ when: e.target.value })}
                >
                  <option value="">Always</option>
                  {options.data?.fields.map((f: Row) => (
                    <option key={f.key}>{f.key}</option>
                  ))}
                </select>
              </label>
              <label>
                <input
                  type="checkbox"
                  checked={chosen.required}
                  onChange={(e) => change({ required: e.target.checked })}
                />{" "}
                Required for issuance
              </label>
              <div className="card-property-actions">
                <button
                  title="Center horizontally"
                  onClick={() => change({ x: (doc.width - chosen.width) / 2 })}
                >
                  <AlignCenter size={17} />
                </button>
                <button
                  title="Bring forward"
                  onClick={() => {
                    const at = current.elements.findIndex(
                      (e) => e.id === chosen.id,
                    );
                    const next = [...current.elements];
                    if (at < next.length - 1) {
                      [next[at], next[at + 1]] = [next[at + 1], next[at]];
                      layers(next);
                    }
                  }}
                >
                  <ArrowUp size={17} />
                </button>
                <button
                  title="Send backward"
                  onClick={() => {
                    const at = current.elements.findIndex(
                      (e) => e.id === chosen.id,
                    );
                    const next = [...current.elements];
                    if (at > 0) {
                      [next[at], next[at - 1]] = [next[at - 1], next[at]];
                      layers(next);
                    }
                  }}
                >
                  <ArrowDown size={17} />
                </button>
              </div>
            </>
          ) : (
            <>
              <p className="card-muted">
                Select a layer to edit its properties. Drag on the canvas to
                position elements. Shift-click for multiple selection.
              </p>
              <ul>
                <li>Dimensions: millimetres</li>
                <li>Dashed green: safe margin</li>
                <li>Outer dashed edge: bleed</li>
                <li>Ctrl+Z / Ctrl+Shift+Z: undo/redo</li>
                <li>Ctrl+D: duplicate</li>
                <li>Arrow keys: 0.2 mm</li>
                <li>Shift+arrows: 1 mm</li>
              </ul>
            </>
          )}
        </aside>
      </div>
      {previewing && (
        <div className="card-modal-backdrop">
          <section className="card-modal">
            <div className="card-section-title">
              <h2>Authorized member preview</h2>
              <button onClick={() => setPreviewing(false)}>Close</button>
            </div>
            <p>
              Server-rendered at 300 DPI using the same physical layout as
              official output.
            </p>
            <input
              placeholder="Search authorized members"
              value={query}
              onChange={(e) => setQuery(e.target.value)}
            />
            <select
              aria-label="Preview member"
              value={member}
              onChange={(e) => {
                setMember(e.target.value);
                setSample(
                  members.data?.items.find((m: Row) => m.id === e.target.value),
                );
              }}
            >
              <option value="">Select member</option>
              {members.data?.items.map((m: Row) => (
                <option key={m.id} value={m.id}>
                  {m.name} · {m.number}
                </option>
              ))}
            </select>
            <button
              className="card-primary"
              disabled={!member || busy}
              onClick={renderPreview}
            >
              Render {side}
            </button>
            {preview && (
              <>
                <img
                  className="card-rendered-preview"
                  src={preview}
                  alt={"Server-rendered " + side + " preview"}
                />
                <a href={preview} download="card-preview.png">
                  Export preview PNG
                </a>
                <p className="card-muted">
                  Preview only. Official cards require independent approval and
                  issuance.
                </p>
              </>
            )}
          </section>
        </div>
      )}
    </div>
  );
}
