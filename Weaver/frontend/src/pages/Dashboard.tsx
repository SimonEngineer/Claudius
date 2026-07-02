import { useQuery } from "@tanstack/react-query";
import { Link } from "react-router-dom";
import { DashboardApi, ScrapingProjectsApi, WorkflowsApi } from "../api/endpoints";
import StatusPill from "../components/StatusPill";
import { resourceLink } from "../utils/resourceLink";

export default function Dashboard() {
  const projects = useQuery({ queryKey: ["scraping-projects"], queryFn: ScrapingProjectsApi.list });
  const workflows = useQuery({ queryKey: ["workflows"], queryFn: WorkflowsApi.list });
  const stats = useQuery({ queryKey: ["dashboard-stats"], queryFn: DashboardApi.stats });

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

      <p className="muted" style={{ marginTop: 8 }}>
        <StatusPill status="Succeeded" /> Tip: create a scraping project, then build a workflow that
        runs it on a schedule and notifies you when something changes.
      </p>
    </div>
  );
}
