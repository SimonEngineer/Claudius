import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useEffect, useMemo, useState } from "react";
import { Link } from "react-router-dom";
import { ScrapingProjectsApi } from "../api/endpoints";
import StatusPill from "../components/StatusPill";
import { onRunStatusChanged } from "../realtime/runStatusConnection";

export default function ScrapingProjectsList() {
  const queryClient = useQueryClient();
  const projects = useQuery({ queryKey: ["scraping-projects"], queryFn: ScrapingProjectsApi.list });

  useEffect(() => {
    return onRunStatusChanged((event) => {
      if (event.kind === "scrape") {
        queryClient.invalidateQueries({ queryKey: ["scraping-projects"] });
      }
    });
  }, [queryClient]);
  const [search, setSearch] = useState("");
  const filtered = useMemo(() => {
    const term = search.trim().toLowerCase();
    if (!term) return projects.data;
    return projects.data?.filter((p) => p.name.toLowerCase().includes(term) || p.startUrl.toLowerCase().includes(term));
  }, [projects.data, search]);

  const runMutation = useMutation({
    mutationFn: ScrapingProjectsApi.run,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ["scraping-projects"] }),
  });

  const deleteMutation = useMutation({
    mutationFn: ScrapingProjectsApi.remove,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ["scraping-projects"] }),
  });

  const [selectedIds, setSelectedIds] = useState<Set<string>>(new Set());
  const toggleSelected = (id: string) =>
    setSelectedIds((prev) => {
      const next = new Set(prev);
      if (next.has(id)) next.delete(id);
      else next.add(id);
      return next;
    });
  const bulkDeleteMutation = useMutation({
    mutationFn: async (ids: string[]) => {
      for (const bulkId of ids) await ScrapingProjectsApi.remove(bulkId);
    },
    onSuccess: () => {
      setSelectedIds(new Set());
      queryClient.invalidateQueries({ queryKey: ["scraping-projects"] });
    },
  });

  const duplicateMutation = useMutation({
    mutationFn: ScrapingProjectsApi.duplicate,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ["scraping-projects"] }),
  });

  return (
    <div>
      <div className="page-header">
        <h2>Scraping Projects</h2>
        <div style={{ display: "flex", gap: 8 }}>
          {selectedIds.size > 0 && (
            <button
              className="danger"
              disabled={bulkDeleteMutation.isPending}
              onClick={() => {
                if (confirm(`Delete ${selectedIds.size} selected project(s)?`)) bulkDeleteMutation.mutate([...selectedIds]);
              }}
            >
              Delete selected ({selectedIds.size})
            </button>
          )}
          <Link to="/scraping-projects/new">
            <button className="primary">+ New Project</button>
          </Link>
        </div>
      </div>

      {projects.data && projects.data.length > 0 && (
        <input
          placeholder="Search by name or URL…"
          value={search}
          onChange={(e) => setSearch(e.target.value)}
          style={{ marginBottom: 12, width: "100%", maxWidth: 360 }}
        />
      )}

      <div className="card">
        {filtered?.length ? (
          <table>
            <thead>
              <tr>
                <th>
                  <input
                    type="checkbox"
                    checked={filtered.length > 0 && filtered.every((p) => selectedIds.has(p.id))}
                    onChange={(e) => setSelectedIds(e.target.checked ? new Set(filtered.map((p) => p.id)) : new Set())}
                  />
                </th>
                <th>Name</th>
                <th>Mode</th>
                <th>Start URL</th>
                <th>Status</th>
                <th>Last run</th>
                <th></th>
              </tr>
            </thead>
            <tbody>
              {filtered.map((p) => (
                <tr key={p.id}>
                  <td>
                    <input type="checkbox" checked={selectedIds.has(p.id)} onChange={() => toggleSelected(p.id)} />
                  </td>
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
                  <td>
                    {p.lastRunStatus ? (
                      <span title={p.lastRunAt ? new Date(p.lastRunAt).toLocaleString() : undefined}>
                        <StatusPill status={p.lastRunStatus} />
                      </span>
                    ) : (
                      <span className="muted">never run</span>
                    )}
                  </td>
                  <td style={{ display: "flex", gap: 6 }}>
                    <button disabled={runMutation.isPending} onClick={() => runMutation.mutate(p.id)}>
                      Run
                    </button>
                    <button disabled={duplicateMutation.isPending} onClick={() => duplicateMutation.mutate(p.id)}>
                      Duplicate
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
          <div className="empty-state">
            {search ? "No projects match your search." : "No scraping projects yet. Create one to get started."}
          </div>
        )}
      </div>
    </div>
  );
}
