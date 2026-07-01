import { useQuery } from "@tanstack/react-query";
import { Link } from "react-router-dom";
import { ScrapingProjectsApi, WorkflowsApi } from "../api/endpoints";
import StatusPill from "../components/StatusPill";

export default function Dashboard() {
  const projects = useQuery({ queryKey: ["scraping-projects"], queryFn: ScrapingProjectsApi.list });
  const workflows = useQuery({ queryKey: ["workflows"], queryFn: WorkflowsApi.list });

  return (
    <div>
      <div className="page-header">
        <h2>Dashboard</h2>
      </div>

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

      <p className="muted" style={{ marginTop: 8 }}>
        <StatusPill status="Succeeded" /> Tip: create a scraping project, then build a workflow that
        runs it on a schedule and notifies you when something changes.
      </p>
    </div>
  );
}
