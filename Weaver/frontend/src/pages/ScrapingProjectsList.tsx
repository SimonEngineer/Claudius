import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Link } from "react-router-dom";
import { ScrapingProjectsApi } from "../api/endpoints";

export default function ScrapingProjectsList() {
  const queryClient = useQueryClient();
  const projects = useQuery({ queryKey: ["scraping-projects"], queryFn: ScrapingProjectsApi.list });

  const runMutation = useMutation({
    mutationFn: ScrapingProjectsApi.run,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ["scraping-projects"] }),
  });

  const deleteMutation = useMutation({
    mutationFn: ScrapingProjectsApi.remove,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ["scraping-projects"] }),
  });

  return (
    <div>
      <div className="page-header">
        <h2>Scraping Projects</h2>
        <Link to="/scraping-projects/new">
          <button className="primary">+ New Project</button>
        </Link>
      </div>

      <div className="card">
        {projects.data?.length ? (
          <table>
            <thead>
              <tr>
                <th>Name</th>
                <th>Mode</th>
                <th>Start URL</th>
                <th>Status</th>
                <th></th>
              </tr>
            </thead>
            <tbody>
              {projects.data.map((p) => (
                <tr key={p.id}>
                  <td>
                    <Link to={`/scraping-projects/${p.id}`}>{p.name}</Link>
                  </td>
                  <td className="muted">{p.mode}</td>
                  <td className="muted mono" style={{ maxWidth: 300, overflow: "hidden", textOverflow: "ellipsis" }}>
                    {p.startUrl}
                  </td>
                  <td>
                    <span className={`pill ${p.isEnabled ? "Succeeded" : "Cancelled"}`}>
                      {p.isEnabled ? "enabled" : "disabled"}
                    </span>
                  </td>
                  <td style={{ display: "flex", gap: 6 }}>
                    <button disabled={runMutation.isPending} onClick={() => runMutation.mutate(p.id)}>
                      Run
                    </button>
                    <button
                      className="danger"
                      onClick={() => {
                        if (confirm(`Delete "${p.name}"?`)) deleteMutation.mutate(p.id);
                      }}
                    >
                      Delete
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        ) : (
          <div className="empty-state">No scraping projects yet. Create one to get started.</div>
        )}
      </div>
    </div>
  );
}
