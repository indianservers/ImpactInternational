import { useRef } from "react";

export default function SectionTabs({ id, label, tabs, value, onChange }: {
  id: string; label: string; tabs: readonly string[]; value: string; onChange: (value: string) => void;
}) {
  const ref = useRef<HTMLDivElement>(null);
  return <div ref={ref} className="screen-tabs" role="tablist" aria-label={label}>
    {tabs.map((tab, index) => <button type="button" role="tab" key={tab} id={`${id}-tab-${index}`}
      aria-controls={`${id}-panel-${index}`} aria-selected={value === tab} tabIndex={value === tab ? 0 : -1}
      className={value === tab ? "secondary active" : "secondary"} onClick={() => onChange(tab)}
      onKeyDown={event => {
        const next = event.key === "ArrowRight" ? (index + 1) % tabs.length : event.key === "ArrowLeft" ? (index + tabs.length - 1) % tabs.length : event.key === "Home" ? 0 : event.key === "End" ? tabs.length - 1 : -1;
        if (next < 0) return;
        event.preventDefault(); onChange(tabs[next]); ref.current?.querySelectorAll<HTMLButtonElement>('[role="tab"]')[next]?.focus();
      }}>{tab}</button>)}
  </div>;
}
