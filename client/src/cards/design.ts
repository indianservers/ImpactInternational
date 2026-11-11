export type CardElement = {
  id: string;
  kind: string;
  x: number;
  y: number;
  width: number;
  height: number;
  text: string;
  binding: string;
  color: string;
  fill: string;
  fontSize: number;
  rotation: number;
  opacity: number;
  border: number;
  radius: number;
  align: string;
  locked: boolean;
  group: string;
  asset: string;
  cropX: number;
  cropY: number;
  cropZoom: number;
  required: boolean;
  when: string;
  bold: boolean;
  shadow: number;
};
export type CardDesign = {
  width: number;
  height: number;
  bleed: number;
  safe: number;
  doubleSided: boolean;
  front: { background: string; elements: CardElement[] };
  back: { background: string; elements: CardElement[] };
  categoryId?: string | null;
  positionId?: string | null;
  parentVersionId?: string | null;
};
export function element(kind: string, binding = ""): CardElement {
  return {
    id: crypto.randomUUID(),
    kind,
    x: 8,
    y: 15,
    width: kind === "Qr" ? 25 : 35,
    height: kind === "Qr" ? 25 : kind === "Text" ? 10 : 20,
    text: kind === "Text" && !binding ? "Your text here" : "",
    binding,
    color: "#173c42",
    fill: "#ffffff",
    fontSize: 3.5,
    rotation: 0,
    opacity: 1,
    border: 0,
    radius: 0,
    align: "Left",
    locked: false,
    group: "",
    asset: "",
    cropX: 0.5,
    cropY: 0.5,
    cropZoom: 1,
    required: false,
    when: "",
    bold: false,
    shadow: 0,
  };
}
export function checkpoint(
  history: CardDesign[],
  index: number,
  next: CardDesign,
) {
  return [...history.slice(0, index + 1), structuredClone(next)].slice(-80);
}
export function snap(
  value: number,
  extent: number,
  card: number,
  grid: boolean,
) {
  for (const anchor of [0, (card - extent) / 2, card - extent])
    if (Math.abs(value - anchor) < 0.7)
      return { value: anchor, guide: anchor + extent / 2 };
  return { value: grid ? Math.round(value) : value, guide: null };
}
export function physicalPixels(mm: number, dpi = 300) {
  return Math.ceil((mm * dpi) / 25.4);
}
export function cloneElements(elements: CardElement[], ids: string[]) {
  return elements
    .filter((e) => ids.includes(e.id))
    .map((e) => ({
      ...e,
      id: crypto.randomUUID(),
      x: e.x + 2,
      y: e.y + 2,
      locked: false,
      group: "",
    }));
}
