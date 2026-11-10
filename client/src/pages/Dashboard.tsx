import { useQuery } from "@tanstack/react-query";
import { Link } from "react-router-dom";
import {
  Network,
  Building2,
  Clock3,
  ArrowUpRight,
  ArrowRight,
  Globe2,
  ShieldCheck,
} from "lucide-react";
import { api } from "../api";
import { PageHeader, Skeleton, ErrorBox, useWorkspace } from "../App";
export default function Dashboard() {
  const { w, me } = useWorkspace();
  const q = useQuery({
    queryKey: ["dashboard", w?.id],
    queryFn: () => api("/dashboard"),
  });
  return (
    <>
      <PageHeader
        eyebrow="YOUR COMMUNITY AT A GLANCE"
        title="Overview"
        description="A connected view of your organization and the work that matters."
      >
        <span className="date-label">
          {new Date().toLocaleDateString("en-IN", {
            day: "numeric",
            month: "long",
            year: "numeric",
          })}
        </span>
      </PageHeader>
      <section className="welcome-banner">
        <div>
          <span className="banner-tag">
            <i />
            YOUR IMPACT STARTS HERE
          </span>
          <h2>Welcome back, {me.name.split(" ")[0]}.</h2>
          <p>
            Great things happen when communities come together.
            <br />
            Here’s what’s happening across your workspace.
          </p>
          <Link to="/app/organizations">
            Explore your organization <ArrowRight size={16} />
          </Link>
        </div>
        <div className="banner-art">
          <Globe2 size={170} strokeWidth={0.7} />
          <span className="art-circle" />
        </div>
      </section>
      <ErrorBox error={q.error} />
      {q.isPending ? (
        <Skeleton />
      ) : (
        <div className="stats-grid">
          {[
            [
              Network,
              "Organizational units",
              q.data?.organizations,
              "Connected across your workspace",
            ],
            [
              Building2,
              "Clubs & chapters",
              q.data?.clubs,
              "Communities making a difference",
            ],
            [
              Clock3,
              "Pending actions",
              q.data?.pending,
              "Organizations awaiting review",
            ],
          ].map(([I, title, value, desc]: any) => (
            <article className="stat-card" key={title}>
              <div className="stat-title">
                {title}
                <span>
                  <I size={19} />
                </span>
              </div>
              <strong>{value ?? "—"}</strong>
              <p>{desc}</p>
            </article>
          ))}
        </div>
      )}
      <div className="dashboard-grid">
        <section className="panel">
          <div className="section-heading">
            <div>
              <h2>Your organization</h2>
              <p>The community you’re connected to.</p>
            </div>
            <Network size={20} />
          </div>
          <div className="organization-summary">
            <span className="org-logo">
              <Globe2 size={34} />
            </span>
            <div>
              <span className="eyebrow">ACTIVE ASSOCIATION</span>
              <h3>{w?.association}</h3>
              <span className="badge">Active workspace</span>
            </div>
          </div>
          <div className="context-row">
            <span>Organizational scope</span>
            <b>{w?.organization}</b>
          </div>
          <div className="context-row">
            <span>Your role</span>
            <b>
              <ShieldCheck size={14} />
              {w?.role}
            </b>
          </div>
          <Link className="panel-link" to="/app/organizations">
            View organization structure <ArrowUpRight size={16} />
          </Link>
        </section>
        <section className="panel">
          <div className="section-heading">
            <div>
              <h2>Quick access</h2>
              <p>A shorter path to your everyday work.</p>
            </div>
          </div>
          <div className="quick-grid">
            {[
              [
                "Organizations",
                "Explore your hierarchy",
                "/app/organizations",
                Network,
                "organization.view",
              ],
              [
                "Club directory",
                "Find your communities",
                "/app/clubs",
                Building2,
                "organization.view",
              ],
              [
                "Administration",
                "Manage roles & access",
                "/app/administration",
                ShieldCheck,
                "roles.view",
              ],
              [
                "Workspace settings",
                "Make it your own",
                "/app/settings",
                Globe2,
                "settings.view",
              ],
            ]
              .filter((x: any) => w?.permissions.includes(x[4]))
              .map(([t, d, url, I]: any) => (
                <Link key={url} to={url}>
                  <I size={21} />
                  <b>{t}</b>
                  <small>{d}</small>
                  <ArrowUpRight size={14} />
                </Link>
              ))}
          </div>
        </section>
      </div>
      <section className="panel activity-panel">
        <div className="section-heading">
          <div>
            <h2>Recent activity</h2>
            <p>The latest changes in your organizational scope.</p>
          </div>
          {w?.permissions.includes("audit.view") && (
            <Link to="/app/audit">View audit trail →</Link>
          )}
        </div>
        {q.data?.recent?.length ? (
          q.data.recent.map((x: any) => (
            <div className="activity-row" key={x.id}>
              <span className="activity-dot" />
              <div>
                <b>{x.action.replaceAll(".", " · ")}</b>
                <small>{x.resource}</small>
              </div>
              <time>{new Date(x.at).toLocaleString()}</time>
              <span className="badge">{x.outcome}</span>
            </div>
          ))
        ) : (
          <div className="empty">
            Activity will appear here as your community grows.
          </div>
        )}
      </section>
    </>
  );
}
