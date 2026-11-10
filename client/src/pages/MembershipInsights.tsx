import { useQuery } from "@tanstack/react-query";
import { PageHeader, Skeleton, ErrorBox, useWorkspace } from "../App";
import { api } from "../api";
export default function MembershipInsights() {
  const { w } = useWorkspace();
  const q = useQuery({
    queryKey: ["member-metrics", w?.id],
    queryFn: () => api("/membership/metrics"),
  });
  if (q.isPending) return <Skeleton />;
  if (q.error) return <ErrorBox error={q.error} />;
  const d = q.data,
    max = Math.max(1, ...d.clubs.map((c: any) => c.count)),
    trendMax = Math.max(1, ...d.trends.map((t: any) => t.joined));
  return (
    <>
      <PageHeader
        eyebrow="MEMBERSHIP, UNDERSTOOD"
        title="Community insights"
        description="Live aggregations for your active organizational scope, with clear metric definitions."
      />
      <div className="member-kpis">
        {[
          ["People", d.totalMembers],
          ["Active people", d.activeMembers],
          ["Opening cohort", d.beginning],
          ["Retained cohort", d.retained],
        ].map(([t, v]) => (
          <section className="stat-card" key={String(t)}>
            <p>{t}</p>
            <strong>{v}</strong>
          </section>
        ))}
      </div>
      <div className="dashboard-grid">
        <section className="panel">
          <div className="section-heading">
            <div>
              <h2>Club comparison</h2>
              <p>Up to 30 clubs with the largest membership populations.</p>
            </div>
          </div>
          {d.clubs.map((c: any) => (
            <div className="comparison-row" key={c.clubId}>
              <div>
                <b>{c.name}</b>
                <span>
                  {c.active} active / {c.count} people
                </span>
              </div>
              <div className="comparison-track">
                <span style={{ width: (c.count / max) * 100 + "%" }} />
                <i style={{ width: (c.active / max) * 100 + "%" }} />
              </div>
            </div>
          ))}
        </section>
        <section className="panel">
          <h2>Joining trends</h2>
          <p>
            Membership affiliations joined in each month. Transfers preserve
            prior records and may create a new affiliation.
          </p>
          <div
            className="trend-chart"
            role="img"
            aria-label="Membership joining trend by month"
          >
            {d.trends.map((t: any) => (
              <div key={t.year + "-" + t.month}>
                <strong>{t.joined}</strong>
                <span
                  style={{ height: Math.max(3, (t.joined / trendMax) * 180) }}
                />
                <small>
                  {new Date(t.year, t.month - 1).toLocaleDateString("en", {
                    month: "short",
                  })}
                  <br />
                  {t.year}
                </small>
              </div>
            ))}
          </div>
          <div className="metric-definition">
            <h3>
              Retention{" "}
              {d.retentionPercent == null ? "—" : d.retentionPercent + "%"}
            </h3>
            <p>
              People active at the beginning of this period who remain active
              now ÷ the beginning active cohort × 100. New people are excluded
              from the retained cohort.
            </p>
            <small>
              Period start: {new Date(d.periodStart).toLocaleString()} · As of:{" "}
              {new Date(d.asOf).toLocaleString()}
            </small>
          </div>
        </section>
      </div>
      <div className="dashboard-grid spaced">
        <section className="panel">
          <h2>Category distribution</h2>
          <p>Affiliations by membership category.</p>
          {d.categories.map((c: any) => (
            <div className="context-row" key={c.name}>
              <span>{c.name}</span>
              <b>{c.count}</b>
            </div>
          ))}
        </section>
        <section className="panel">
          <h2>Country-level view</h2>
          <p>
            People grouped by their club’s country. No sensitive personal
            demographic details are exposed.
          </p>
          {d.countries.map((c: any) => (
            <div className="context-row" key={c.name}>
              <span>{c.name || "Not configured"}</span>
              <b>{c.count}</b>
            </div>
          ))}
        </section>
      </div>
      <section className="panel spaced">
        <h2>How these metrics are calculated</h2>
        <div className="detail-grid">
          <div>
            <h3>People & affiliations</h3>
            <p>
              Total people counts distinct person identities in scope, including
              preserved historical affiliations. Affiliations counts membership
              records. Active people have at least one active, unexpired scoped
              affiliation.
            </p>
          </div>
          <div>
            <h3>Growth</h3>
            <p>
              Change in active people since the beginning of the current UTC
              calendar month ÷ that opening cohort. A missing beginning cohort
              is displayed without a percentage.
            </p>
          </div>
          <div>
            <h3>Renewals & attention</h3>
            <p>
              Upcoming renewals are active affiliations ending within 30 days.
              Attention covers suspended or expired affiliations and explicit
              administrative financial holds.
            </p>
          </div>
          <div>
            <h3>Privacy & scope</h3>
            <p>
              All totals use the active workspace’s permitted organizational
              descendants. Aggregation access does not grant private contact,
              document, or export access.
            </p>
          </div>
        </div>
      </section>
    </>
  );
}
