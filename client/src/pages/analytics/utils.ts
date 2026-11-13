export const palette = [
  "#7845e8",
  "#36ad8a",
  "#527dff",
  "#e6aa49",
  "#d65286",
  "#9291d8",
  "#5cbac5",
];
export function saveFile(blob: Blob, name: string) {
  const url = URL.createObjectURL(blob),
    a = document.createElement("a");
  a.href = url;
  a.download = name;
  a.click();
  setTimeout(() => URL.revokeObjectURL(url), 1000);
}
export function exportCharts(container: HTMLElement, title = "Membership Intelligence", filename = "impact-membership-charts.svg") {
  const svgs = Array.from(
    container.querySelectorAll<SVGSVGElement>("svg[data-chart]"),
  );
  const height = svgs.reduce(
    (n, s) => n + (s.viewBox.baseVal.height || 300) + 55,
    65,
  );
  let y = 55;
  const content = svgs
    .map((s) => {
      const box = s.viewBox.baseVal,
        h = box.height || 300;
      const markup = new XMLSerializer().serializeToString(s);
      const legend = Array.from(
        s.closest(".a-growth")?.querySelectorAll(".a-legend button") || [],
      )
        .map((b) => b.textContent)
        .filter((v) => v && !/zoom/i.test(v))
        .join(" · ");
      const safe = (text: string) =>
        text
          .replaceAll("&", "&amp;")
          .replaceAll("<", "&lt;")
          .replaceAll(">", "&gt;");
      const result = `<text x="30" y="${y}" font-size="17" fill="#25213b">${safe(s.getAttribute("aria-label") || "Membership analytics")}</text><text x="30" y="${y + 14}" font-size="10" fill="#79628d">${safe(legend)}</text><svg x="25" y="${y + 26}" width="950" height="${h}" viewBox="${box.x} ${box.y} ${box.width || 950} ${h}">${markup}</svg>`;
      y += h + 55;
      return result;
    })
    .join("");
  saveFile(
    new Blob(
      [
        `<svg xmlns="http://www.w3.org/2000/svg" width="1000" height="${height}"><rect width="100%" height="100%" fill="white"/><text x="30" y="25" font-size="20" fill="#392562">Impact International · ${title.replaceAll("&", "&amp;").replaceAll("<", "&lt;").replaceAll(">", "&gt;")}</text>${content}</svg>`,
      ],
      { type: "image/svg+xml" },
    ),
    filename,
  );
}
