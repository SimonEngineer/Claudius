import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { Link } from "react-router-dom";
import { ScrapingProjectsApi } from "../api/endpoints";
import StatusPill from "../components/StatusPill";
import { onRunStatusChanged } from "../realtime/runStatusConnection";
import { timeAgo } from "../utils/timeAgo";
import { useEscapeKey } from "../utils/useEscapeKey";
import { usePageTitle } from "../utils/usePageTitle";

export default function ScrapingProjectsList() {
  usePageTitle("Scraping Projects");
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
  const [sortKey, setSortKey] = useState<"name" | "updated" | null>(null);
  const [sortAsc, setSortAsc] = useState(true);
  const toggleSort = (key: "name" | "updated") => {
    if (sortKey === key) setSortAsc((a) => !a);
    else {
      setSortKey(key);
      setSortAsc(key === "name");
    }
  };
  const sortIndicator = (key: "name" | "updated") => (sortKey === key ? (sortAsc ? " ↑" : " ↓") : "");
  const filtered = useMemo(() => {
    const term = search.trim().toLowerCase();
    const matched = term
      ? projects.data?.filter((p) => p.name.toLowerCase().includes(term) || p.startUrl.toLowerCase().includes(term))
      : projects.data;
    if (!matched || !sortKey) return matched;
    return [...matched].sort((a, b) => {
      const cmp =
        sortKey === "name"
          ? a.name.localeCompare(b.name)
          : new Date(a.updatedAt).getTime() - new Date(b.updatedAt).getTime();
      return sortAsc ? cmp : -cmp;
    });
  }, [projects.data, search, sortKey, sortAsc]);

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

  const toggleMutation = useMutation({
    mutationFn: ScrapingProjectsApi.toggleEnabled,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ["scraping-projects"] }),
  });

  const [importError, setImportError] = useState<string | null>(null);
  const importMutation = useMutation({
    mutationFn: (fileContents: string) => ScrapingProjectsApi.importProject(fileContents),
    onSuccess: () => {
      setImportError(null);
      queryClient.invalidateQueries({ queryKey: ["scraping-projects"] });
    },
    onError: () => setImportError("Import failed. Make sure the file is a Weaver project export."),
  });
  const importInputRef = useRef<HTMLInputElement>(null);
  const onImportFileChosen = async (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    e.target.value = "";
    if (!file) return;
    importMutation.mutate(await file.text());
  };

  const [showTemplates, setShowTemplates] = useState(false);
  useEscapeKey(useCallback(() => setShowTemplates(false), []));
  const templates = useQuery({ queryKey: ["project-templates"], queryFn: ScrapingProjectsApi.templates, enabled: showTemplates });
  const fromTemplateMutation = useMutation({
    mutationFn: ScrapingProjectsApi.createFromTemplate,
    onSuccess: () => {
      setShowTemplates(false);
      queryClient.invalidateQueries({ queryKey: ["scraping-projects"] });
    },
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
          <input ref={importInputRef} type="file" accept=".json" onChange={onImportFileChosen} style={{ display: "none" }} />
          <button onClick={() => importInputRef.current?.click()}>Import…</button>
          <button onClick={() => setShowTemplates(true)}>From template…</button>
          <Link to="/scraping-projects/new">
            <button className="primary">+ New Project</button>
          </Link>
        </div>
      </div>

      {importError && <p style={{ color: "var(--danger)" }}>{importError}</p>}

      {showTemplates && (
        <div
          style={{ position: "fixed", inset: 0, background: "rgba(0,0,0,0.5)", display: "flex", alignItems: "center", justifyContent: "center", zIndex: 100 }}
          onClick={() => setShowTemplates(false)}
        >
          <div className="card" style={{ width: 480 }} onClick={(e) => e.stopPropagation()}>
            <div className="page-header">
              <h3 style={{ margin: 0, fontSize: 14 }}>Start from a template</h3>
              <button className="close-btn" onClick={() => setShowTemplates(false)}>×</button>
            </div>
            {templates.data?.map((t) => (
              <div key={t.key} className="card" style={{ background: "var(--panel-2)", margin: "0 0 8px" }}>
                <div className="page-header" style={{ marginBottom: 4 }}>
                  <strong style={{ fontSize: 13 }}>{t.name}</strong>
                  <button disabled={fromTemplateMutation.isPending} onClick={() => fromTemplateMutation.mutate(t.key)}>
                    Create
                  </button>
                </div>
                <p className="muted" style={{ margin: 0, fontSize: 12 }}>{t.description}</p>
              </div>
            )) ?? <p className="muted">Loading…</p>}
            <p className="muted" style={{ fontSize: 12, marginBottom: 0 }}>
              Templates are created disabled so nothing runs until you've looked them over.
            </p>
          </div>
        </div>
      )}

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
                <th style={{ cursor: "pointer", userSelect: "none" }} onClick={() => toggleSort("name")}>
                  Name{sortIndicator("name")}
                </th>
                <th>Mode</th>
                <th>Start URL</th>
                <th>Status</th>
                <th style={{ cursor: "pointer", userSelect: "none" }} onClick={() => toggleSort("updated")}>
                  Last run{sortIndicator("updated")}
                </th>
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
                    <span
                      className={`pill ${p.isEnabled ? "Succeeded" : "Cancelled"}`}
                      style={{ cursor: "pointer" }}
                      title="Click to toggle"
                      onClick={() => toggleMutation.mutate(p.id)}
                    >
                      {p.isEnabled ? "enabled" : "disabled"}
                    </span>
                  </td>
                  <td>
                    {p.lastRunStatus ? (
                      <span title={p.lastRunAt ? new Date(p.lastRunAt).toLocaleString() : undefined}>
                        <StatusPill status={p.lastRunStatus} />
                        {p.lastRunAt && <span className="muted" style={{ marginLeft: 6, fontSize: 12 }}>{timeAgo(p.lastRunAt)}</span>}
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
                    <button onClick={() => ScrapingProjectsApi.exportProject(p.id, p.name)}>Export</button>
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
