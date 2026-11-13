import { useState } from "react";
import { ArrowDown, ArrowUp, Columns3 } from "lucide-react";
export type GridColumn<T> = {
  key: string;
  label: string;
  width?: number;
  render: (row: T) => React.ReactNode;
};
export type GridConfig = {
  order: string[];
  hidden: string[];
  widths: Record<string, number>;
};
export default function DataGrid<T extends { id: string }>({
  rows,
  columns,
  config,
  onConfig,
  selected,
  onSelection,
  onOpen,
  empty = "No matching records.",
  sort, onSort,
}: {
  rows: T[];
  columns: GridColumn<T>[];
  config: GridConfig;
  onConfig: (c: GridConfig) => void;
  selected?: Set<string>;
  onSelection?: (s: Set<string>) => void;
  onOpen?: (row: T) => void;
  empty?: string;
  sort?: string;
  onSort?: (sort:string)=>void;
}) {
  const [settings, setSettings] = useState(false);
  const ordered = [
    ...config.order,
    ...columns.map((x) => x.key).filter((k) => !config.order.includes(k)),
  ]
    .map((k) => columns.find((c) => c.key === k))
    .filter((c): c is GridColumn<T> => !!c);
  const visible = ordered.filter((c) => !config.hidden.includes(c.key));
  function resize(key: string, width: number) {
    onConfig({
      ...config,
      widths: { ...config.widths, [key]: Math.max(90, Math.min(500, width)) },
    });
  }
  function move(key: string, delta: number) {
    const order = ordered.map((c) => c.key),
      i = order.indexOf(key),
      j = i + delta;
    if (j < 0 || j >= order.length) return;
    [order[i], order[j]] = [order[j], order[i]];
    onConfig({ ...config, order });
  }
  return (
    <>
      <div className="grid-tools">
        {onSelection && <label className="mobile-select-all checkbox"><input type="checkbox" aria-label="Select all mobile records on this page" checked={rows.length > 0 && rows.every(row => selected?.has(row.id))} disabled={!rows.length} onChange={e => { const next = new Set(selected); for (const row of rows) e.target.checked ? next.add(row.id) : next.delete(row.id); onSelection(next); }} />Select page</label>}
        <span>
          {selected && selected.size > 0 ? (
            <>
              {selected.size} selected{" "}
              <button
                className="text-button"
                onClick={() => onSelection?.(new Set())}
              >
                Clear selection
              </button>
            </>
          ) : (
            `${rows.length} records on this page`
          )}
        </span>
        <button
          className="secondary"
          aria-expanded={settings}
          onClick={() => setSettings(!settings)}
        >
          <Columns3 size={14} />
          Columns
        </button>
      </div>
      {settings && (
        <div className="column-config" aria-label="Configure columns">
          {ordered.map((c, i) => (
            <div key={c.key}>
              <label className="checkbox">
                <input
                  type="checkbox"
                  checked={!config.hidden.includes(c.key)}
                  onChange={(e) =>
                    onConfig({
                      ...config,
                      hidden: e.target.checked
                        ? config.hidden.filter((k) => k !== c.key)
                        : [...config.hidden, c.key],
                    })
                  }
                />
                {c.label}
              </label>
              <button
                className="icon-button"
                disabled={i === 0}
                aria-label={"Move " + c.label + " left"}
                onClick={() => move(c.key, -1)}
              >
                <ArrowUp size={13} />
              </button>
              <button
                className="icon-button"
                disabled={i === ordered.length - 1}
                aria-label={"Move " + c.label + " right"}
                onClick={() => move(c.key, 1)}
              >
                <ArrowDown size={13} />
              </button>
            </div>
          ))}
        </div>
      )}
      <div className="mobile-records" aria-label="Records">
        {rows.map(row => <article className="mobile-record" key={row.id}>
          <header>
            {onSelection && <label className="checkbox"><input type="checkbox" aria-label={"Select record " + row.id} checked={selected?.has(row.id) || false} onChange={e => { const next = new Set(selected); e.target.checked ? next.add(row.id) : next.delete(row.id); onSelection(next); }} />Select</label>}
            {onOpen && <button className="secondary" onClick={() => onOpen(row)}>Open record</button>}
          </header>
          <dl>{visible.slice(0, 4).map(c => <div key={c.key}><dt>{c.label}</dt><dd>{c.render(row)}</dd></div>)}</dl>
          {visible.length > 4 && <details><summary>More details & actions</summary><dl>{visible.slice(4).map(c => <div key={c.key}><dt>{c.label}</dt><dd>{c.render(row)}</dd></div>)}</dl></details>}
        </article>)}
        {!rows.length && <div className="empty">{empty}</div>}
      </div>
      <div className="table-scroll data-grid desktop-records">
        <table
          style={{
            tableLayout: "fixed",
            minWidth: visible.reduce(
              (n, c) => n + (config.widths[c.key] || c.width || 160),
              onSelection ? 46 : 0,
            ),
          }}
        >
          <colgroup>
            {onSelection && <col style={{ width: 46 }} />}
            {visible.map((c) => (
              <col
                key={c.key}
                style={{ width: config.widths[c.key] || c.width || 160 }}
              />
            ))}
          </colgroup>
          <thead>
            <tr>
              {onSelection && (
                <th>
                  <input
                    type="checkbox"
                    aria-label="Select all records on this page"
                    checked={
                      !!rows.length && rows.every((r) => selected?.has(r.id))
                    }
                    onChange={(e) => {
                      const next = new Set(selected);
                      rows.forEach((r) =>
                        e.target.checked ? next.add(r.id) : next.delete(r.id),
                      );
                      onSelection(next);
                    }}
                  />
                </th>
              )}
              {visible.map((c) => (
                <th key={c.key}>
                  {onSort && ["name","number","club","category","status","joinedAt","endsAt"].includes(c.key) ? <button className="text-button grid-sort-heading" onClick={()=>{const key=c.key==="joinedAt"?"joined":c.key==="endsAt"?"renewal":c.key;onSort(sort===key?(key==="joined"?"joined-asc":key+"-desc"):key);}}>{c.label} {sort?.startsWith(c.key==="joinedAt"?"joined":c.key==="endsAt"?"renewal":c.key)?"↕":""}</button> : c.label}
                  <span
                    role="separator"
                    aria-orientation="vertical"
                    aria-label={"Resize " + c.label}
                    tabIndex={0}
                    className="column-resize"
                    onKeyDown={(e) => {
                      if (e.key === "ArrowLeft" || e.key === "ArrowRight") {
                        e.preventDefault();
                        resize(
                          c.key,
                          (config.widths[c.key] || c.width || 160) +
                            (e.key === "ArrowRight" ? 16 : -16),
                        );
                      }
                    }}
                    onPointerDown={(e) => {
                      const start = e.clientX,
                        width = config.widths[c.key] || c.width || 160;
                      const drag = (event: PointerEvent) =>
                        resize(c.key, width + event.clientX - start);
                      const finish = () => {
                        window.removeEventListener("pointermove", drag);
                        window.removeEventListener("pointerup", finish);
                      };
                      window.addEventListener("pointermove", drag);
                      window.addEventListener("pointerup", finish);
                    }}
                  />
                </th>
              ))}
            </tr>
          </thead>
          <tbody>
            {rows.map((row) => (
              <tr
                key={row.id}
                tabIndex={onOpen ? 0 : undefined}
                onKeyDown={(e) => {
                  if (e.key === "Enter" && e.target === e.currentTarget)
                    onOpen?.(row);
                }}
                onDoubleClick={() => onOpen?.(row)}
              >
                {onSelection && (
                  <td>
                    <input
                      type="checkbox"
                      aria-label={"Select record " + row.id}
                      checked={selected?.has(row.id) || false}
                      onChange={(e) => {
                        const next = new Set(selected);
                        e.target.checked
                          ? next.add(row.id)
                          : next.delete(row.id);
                        onSelection(next);
                      }}
                    />
                  </td>
                )}
                {visible.map((c) => (
                  <td key={c.key}>{c.render(row)}</td>
                ))}
              </tr>
            ))}
          </tbody>
        </table>
        {!rows.length && <div className="empty">{empty}</div>}
      </div>
    </>
  );
}
