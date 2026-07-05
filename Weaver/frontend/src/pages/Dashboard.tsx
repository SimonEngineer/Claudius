import { useQuery } from "@tanstack/react-query";
import { Link } from "react-router-dom";
import { DashboardApi, ScrapingProjectsApi, WorkflowsApi } from "../api/endpoints";
import { timeAgo } from "../utils/timeAgo";
import type { DailyRunCount } from "../types";
import StatusPill from "../components/StatusPill";
import { resourceLink } from "../utils/resourceLink";
import { usePageTitle } from "../utils/usePageTitle";

export default function Dashboard() {
  usePageTitle("Dashboard");
  const projects = useQuery({ queryKey: ["scraping-projects"], queryFn: ScrapingProjectsApi.list });
  const workflows = useQuery({ queryKey: ["workflows"], queryFn: WorkflowsApi.list });
  const stats = useQuery({ queryKey: ["dashboard-stats"], queryFn: DashboardApi.stats });
  const runsPerDay = useQuery({ queryKey: ["dashboard-runs-per-day"], queryFn: DashboardApi.runsPerDay });
  const workers = useQuery({ queryKey: ["dashboard-workers"], queryFn: DashboardApi.workers, refetchInterval: 30000 });

  return (
    <div>
      <div className="page-header">
        <h2>Dashboard</h2>
      </div>

      {stats.data && (
        <div className="row" style={{ marginBottom: 16 }}>
          <div className="card" style={{ flex: 1 }}>
            <h3 style={{ marginTop: 0, fontSize: 13 }} className="muted">Scrapes today</h3>
            <div style={{ display: "flex", gap: 16 }}>
              <span style={{ color: "var(--accent-2)" }}>{stats.data.scrapesSucceededToday} succeeded</span>
              <span style={{ color: "var(--danger)" }}>{stats.data.scrapesFailedToday} failed</span>
            </div>
          </div>
          <div className="card" style={{ flex: 1 }}>
            <h3 style={{ marginTop: 0, fontSize: 13 }} className="muted">Workflow runs today</h3>
            <div style={{ display: "flex", gap: 16 }}>
              <span style={{ color: "var(--accent-2)" }}>{stats.data.workflowRunsSucceededToday} succeeded</span>
              <span style={{ color: "var(--danger)" }}>{stats.data.workflowRunsFailedToday} failed</span>
            </div>
          </div>
        </div>
      )}

      {runsPerDay.data && (
        <div className="row" style={{ marginBottom: 16 }}>
          <RunsChart title="Scrape runs (14 days)" days={runsPerDay.data.scrapes} />
          <RunsChart title="Workflow runs (14 days)" days={runsPerDay.data.workflows} />
        </div>
      )}

      {stats.data && stats.data.recentFailures.length > 0 && (
        <div className="card" style={{ marginBottom: 16 }}>
          <h3 style={{ marginTop: 0, fontSize: 15 }}>Recent failures</h3>
          <table>
            <tbody>
              {stats.data.recentFailures.map((f, i) => (
                <tr key={i}>
                  <td className="muted" title={new Date(f.at).toLocaleString()}>{timeAgo(f.at)}</td>
                  <td className="muted">{f.kind}</td>
                  <td>
                    <Link to={f.kind === "scrape" ? `/scraping-projects/${f.resourceId}` : `/workflows/${f.resourceId}`}>
                      {f.resourceName}
                    </Link>
                  </td>
                  <td style={{ color: "var(--danger)", maxWidth: 380, overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap" }} title={f.error ?? undefined}>
                    {f.error ?? "(no error message)"}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      <div className="row">
        <div className="card">
          <div className="page-header">
            <h3 style={{ margin: 0, fontSize: 15 }}>Scraping Projects</h3>
            <Link to="/scraping-projects" className="muted">
              View all →
            </Link>
          </div>
          {projects.data?.length ? (
            <table>
              <tbody>
                {projects.data.slice(0, 6).map((p) => (
                  <tr key={p.id}>
                    <td>
                      <Link to={`/scraping-projects/${p.id}`}>{p.name}</Link>
                    </td>
                    <td className="muted">{p.mode}</td>
                    <td>
                      <span className={`pill ${p.isEnabled ? "Succeeded" : "Cancelled"}`}>
                        {p.isEnabled ? "enabled" : "disabled"}
                      </span>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          ) : (
            <p className="muted">No scraping projects yet.</p>
          )}
        </div>

        <div className="card">
          <div className="page-header">
            <h3 style={{ margin: 0, fontSize: 15 }}>Workflows</h3>
            <Link to="/workflows" className="muted">
              View all →
            </Link>
          </div>
          {workflows.data?.length ? (
            <table>
              <tbody>
                {workflows.data.slice(0, 6).map((w) => (
                  <tr key={w.id}>
                    <td>
                      <Link to={`/workflows/${w.id}`}>{w.name}</Link>
                    </td>
                    <td className="muted">{w.nodes.length} nodes</td>
                    <td>
                      <span className={`pill ${w.isEnabled ? "Succeeded" : "Cancelled"}`}>
                        {w.isEnabled ? "enabled" : "disabled"}
                      </span>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          ) : (
            <p className="muted">No workflows yet.</p>
          )}
        </div>
      </div>

      <div className="card" style={{ marginTop: 16 }}>
        <div className="page-header">
          <h3 style={{ margin: 0, fontSize: 15 }}>Recent activity</h3>
          <Link to="/activity" className="muted">
            View all →
          </Link>
        </div>
        {stats.data?.recentActivity.length ? (
          <table>
            <tbody>
              {stats.data.recentActivity.map((entry) => {
                const link = resourceLink(entry.resourceType, entry.resourceId);
                return (
                  <tr key={entry.id}>
                    <td className="muted">{new Date(entry.createdAt).toLocaleString()}</td>
                    <td>{entry.action}</td>
                    <td className="muted">{entry.resourceType}</td>
                    <td>{link && entry.action !== "Deleted" ? <Link to={link}>{entry.resourceName}</Link> : entry.resourceName}</td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        ) : (
          <p className="muted">No activity yet.</p>
        )}
      </div>

      <div className="card" style={{ marginTop: 16 }}>
        <h3 style={{ marginTop: 0, fontSize: 15 }}>Workers</h3>
        {workers.data?.length ? (
          <table>
            <thead>
              <tr>
                <th>Instance</th>
                <th>Started</th>
                <th>Last heartbeat</th>
              </tr>
            </thead>
            <tbody>
              {workers.data.map((w) => (
                <tr key={w.name}>
                  <td className="mono">{w.name}</td>
                  <td className="muted">{new Date(w.startedAt).toLocaleString()}</td>
                  <td className="muted">{timeAgo(w.lastSeen)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        ) : (
          <p className="muted" style={{ marginBottom: 0 }}>
            No live workers — scheduled and queued scrapes won't run until a worker process is up.
          </p>
        )}
      </div>

      <p className="muted" style={{ marginTop: 8 }}>
        <StatusPill status="Succeeded" /> Tip: create a scraping project, then build a workflow that
        runs it on a schedule and notifies you when something changes.
      </p>
    </div>
  );
}

/** Tiny dependency-free stacked bar chart: green = succeeded, red = failed, per day. */
function RunsChart({ title, days }: { title: string; days: DailyRunCount[] }) {
  const max = Math.max(...days.map((d) => d.succeeded + d.failed), 1);
  return (
    <div className="card" style={{ flex: 1 }}>
      <h3 style={{ marginTop: 0, fontSize: 13 }} className="muted">{title}</h3>
      <div style={{ display: "flex", alignItems: "flex-end", gap: 3, height: 72 }}>
        {days.map((d) => {
          const total = d.succeeded + d.failed;
          const height = (total / max) * 100;
          const failedShare = total > 0 ? (d.failed / total) * 100 : 0;
          return (
            <div
              key={d.day}
              title={`${d.day}: ${d.succeeded} succeeded, ${d.failed} failed`}
              style={{ flex: 1, height: `${Math.max(height, total > 0 ? 6 : 2)}%`, display: "flex", flexDirection: "column", borderRadius: 2, overflow: "hidden", background: total === 0 ? "var(--panel-2)" : undefined }}
            >
              {total > 0 && (
                <>
                  <div style={{ height: `${failedShare}%`, background: "var(--danger)" }} />
                  <div style={{ flex: 1, background: "var(--success)" }} />
                </>
              )}
            </div>
          );
        })}
      </div>
      <div className="muted" style={{ display: "flex", justifyContent: "space-between", fontSize: 10, marginTop: 4 }}>
        <span>{days[0]?.day}</span>
        <span>{days[days.length - 1]?.day}</span>
      </div>
    </div>
  );
}
