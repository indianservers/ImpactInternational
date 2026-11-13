import * as d3 from "d3";
// OLS over completed monthly registration counts, with residual-based prediction bands and rolling holdout MAE.
export function forecast(data: any[], horizon: number) {
  const months = data.filter((d) => /^\d{4}-\d\d-01$/.test(d.date));
  if (
    months.length !== data.length ||
    months.some(
      (d, i) =>
        i > 0 &&
        d3.utcMonth.count(new Date(months[i - 1].date), new Date(d.date)) !== 1,
    )
  )
    return null;
  const completed = months.filter(
    (d, i) =>
      d.complete === true || (d.complete == null && i < months.length - 1),
  );
  if (completed.filter((d) => d.total > 0).length < 6) return null;
  const rows = completed.slice(-24),
    n = rows.length,
    x = d3.range(n),
    y = rows.map((d) => d.new || 0),
    mx = d3.mean(x)!,
    my = d3.mean(y)!;
  const sxx = d3.sum(x, (t) => (t - mx) ** 2);
  const slope = d3.sum(x, (t, i) => (t - mx) * (y[i] - my)) / sxx,
    intercept = my - slope * mx,
    sigma = Math.sqrt(
      d3.sum(y, (v, i) => (v - intercept - slope * i) ** 2) /
        Math.max(1, n - 2),
    );
  let cumulative = months.at(-1)?.total || 0;
  const last = new Date(months.at(-1)!.date + "T00:00:00Z");
  const points = d3.range(horizon).map((i) => {
    const registrations = Math.max(0, intercept + slope * (n + i)),
      margin =
        (n < 10 ? 2.78 : n < 20 ? 2.31 : 2.1) *
        sigma *
        Math.sqrt(
          i +
            1 +
            (i + 1) ** 2 / n +
            ((i + 1) * (n - mx) + (i * (i + 1)) / 2) ** 2 / sxx,
        );
    cumulative += registrations;
    return {
      date: d3.utcFormat("%Y-%m-%d")(d3.utcMonth.offset(last, i + 1)),
      total: Math.round(cumulative),
      registrations: Math.round(registrations),
      lower: Math.max(0, Math.round(cumulative - margin)),
      upper: Math.round(cumulative + margin),
    };
  });
  const errors: number[] = [];
  for (let stop = 4; stop < n; stop++) {
    const yy = y.slice(0, stop),
      xx = d3.range(stop),
      xm = d3.mean(xx)!,
      ym = d3.mean(yy)!,
      ss = d3.sum(xx, (t) => (t - xm) ** 2),
      b = d3.sum(xx, (t, j) => (t - xm) * (yy[j] - ym)) / ss;
    errors.push(Math.abs(y[stop] - Math.max(0, ym - b * xm + b * stop)));
  }
  return {
    points,
    sigma,
    mae: errors.length ? d3.mean(errors) : null,
    observations: n,
  };
}
