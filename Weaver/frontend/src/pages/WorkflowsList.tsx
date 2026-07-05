import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useEffect, useMemo, useRef, useState } from "react";
import { Link } from "react-router-dom";
import { WorkflowsApi } from "../api/endpoints";
import StatusPill from "../components/StatusPill";
import { onRunStatusChanged } from "../realtime/runStatusConnection";
import { timeAgo } from "../utils/timeAgo";
import { usePageTitle } from "../utils/usePageTitle";

export default function WorkflowsList() {
  usePageTitle("Workflows");
  const queryClient = useQueryClient();
  const workflows = useQuery({ queryKey: ["workflows"], queryFn: WorkflowsApi.list });

  useEffect(() => {
    return onRunStatusChanged((event) => {
      if (event.kind === "workflow") {
        queryClient.invalidateQueries({ queryKey: ["workflows"] });
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
    const matched = term ? workflows.data?.filter((w) => w.name.toLowerCase().includes(term)) : workflows.data;
    if (!matched || !sortKey) return matched;
    return [...matched].sort((a, b) => {
      const cmp =
        sortKey === "name"
          ? a.name.localeCompare(b.name)
          : new Date(a.updatedAt).getTime() - new Date(b.updatedAt).getTime();
      return sortAsc ? cmp : -cmp;
    });
  }, [workflows.data, search, sortKey, sortAsc]);
  const deleteMutation = useMutation({
    mutationFn: WorkflowsApi.remove,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ["workflows"] }),
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
      for (const bulkId of ids) await WorkflowsApi.remove(bulkId);
    },
    onSuccess: () => {
      setSelectedIds(new Set());
      queryClient.invalidateQueries({ queryKey: ["workflows"] });
    },
  });
  const duplicateMutation = useMutation({
    mutationFn: WorkflowsApi.duplicate,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ["workflows"] }),
  });

  const runMutation = useMutation({
    mutationFn: ({ workflowId, nodeId }: { workflowId: string; nodeId: string }) =>
      WorkflowsApi.runFromNode(workflowId, nodeId),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ["workflows"] }),
  });

  const toggleMutation = useMutation({
    mutationFn: WorkflowsApi.toggleEnabled,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ["workflows"] }),
  });
  const manualTriggerOf = (w: { nodes: { id: string; type: string }[] }) =>
    w.nodes.find((n) => n.type === "trigger.manual");
  const [importError, setImportError] = useState<string | null>(null);
  const importMutation = useMutation({
    mutationFn: (fileContents: string) => WorkflowsApi.importWorkflow(fileContents),
    onSuccess: () => {
      setImportError(null);
      queryClient.invalidateQueries({ queryKey: ["workflows"] });
    },
    onError: () => setImportError("Import failed. Make sure the file is a Weaver workflow export."),
  });

  const importInputRef = useRef<HTMLInputElement>(null);
  const onImportFileChosen = async (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    e.target.value = ""; // allow re-choosing the same file next time
    if (!file) return;
    const contents = await file.text();
    importMutation.mutate(contents);
  };

  return (
    <div>
      <div className="page-header">
        <h2>Workflows</h2>
        <div style={{ display: "flex", gap: 8 }}>
          {selectedIds.size > 0 && (
            <button
              className="danger"
              disabled={bulkDeleteMutation.isPending}
              onClick={() => {
                if (confirm(`Delete ${selectedIds.size} selected workflow(s)?`)) bulkDeleteMutation.mutate([...selectedIds]);
              }}
            >
              Delete selected ({selectedIds.size})
            </button>
          )}
          <input ref={importInputRef} type="file" accept=".json" onChange={onImportFileChosen} style={{ display: "none" }} />
          <button onClick={() => importInputRef.current?.click()}>Import…</button>
          <Link to="/workflows/new">
            <button className="primary">+ New Workflow</button>
          </Link>
        </div>
      </div>

      {importError && <p style={{ color: "var(--danger)" }}>{importError}</p>}

      {workflows.data && workflows.data.length > 0 && (
        <input
          placeholder="Search by name…"
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
                    checked={filtered.length > 0 && filtered.every((w) => selectedIds.has(w.id))}
                    onChange={(e) => setSelectedIds(e.target.checked ? new Set(filtered.map((w) => w.id)) : new Set())}
                  />
                </th>
                <th style={{ cursor: "pointer", userSelect: "none" }} onClick={() => toggleSort("name")}>
                  Name{sortIndicator("name")}
                </th>
                <th>Nodes</th>
                <th>Status</th>
                <th>Last run</th>
                <th style={{ cursor: "pointer", userSelect: "none" }} onClick={() => toggleSort("updated")}>
                  Updated{sortIndicator("updated")}
                </th>
                <th></th>
              </tr>
            </thead>
            <tbody>
              {filtered.map((w) => (
                <tr key={w.id}>
                  <td>
                    <input type="checkbox" checked={selectedIds.has(w.id)} onChange={() => toggleSelected(w.id)} />
                  </td>
                  <td>
                    <Link to={`/workflows/${w.id}`}>{w.name}</Link>
                  </td>
                  <td className="muted">{w.nodes.length}</td>
                  <td>
                    <span
                      className={`pill ${w.isEnabled ? "Succeeded" : "Cancelled"}`}
                      style={{ cursor: "pointer" }}
                      title="Click to toggle"
                      onClick={() => toggleMutation.mutate(w.id)}
                    >
                      {w.isEnabled ? "enabled" : "disabled"}
                    </span>
                  </td>
                  <td>
                    {w.lastRunStatus ? (
                      <span title={w.lastRunAt ? new Date(w.lastRunAt).toLocaleString() : undefined}>
                        <StatusPill status={w.lastRunStatus} />
                        {w.lastRunAt && <span className="muted" style={{ marginLeft: 6, fontSize: 12 }}>{timeAgo(w.lastRunAt)}</span>}
                      </span>
                    ) : (
                      <span className="muted">never run</span>
                    )}
                  </td>
                  <td className="muted" title={new Date(w.updatedAt).toLocaleString()}>
                    {timeAgo(w.updatedAt)}
                  </td>
                  <td style={{ display: "flex", gap: 6 }}>
                    {manualTriggerOf(w) && (
                      <button
                        disabled={runMutation.isPending || !w.isEnabled}
                        title={w.isEnabled ? "Fire this workflow's manual trigger" : "Enable the workflow to run it"}
                        onClick={() => runMutation.mutate({ workflowId: w.id, nodeId: manualTriggerOf(w)!.id })}
                      >
                        Run
                      </button>
                    )}
                    <button disabled={duplicateMutation.isPending} onClick={() => duplicateMutation.mutate(w.id)}>
                      Duplicate
                    </button>
                    <button onClick={() => WorkflowsApi.exportWorkflow(w.id, w.name)}>Export</button>
                    <button
                      className="danger"
                      onClick={() => {
                        if (confirm(`Delete "${w.name}"?`)) deleteMutation.mutate(w.id);
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
            {search ? "No workflows match your search." : "No workflows yet. Create one to automate something."}
          </div>
        )}
      </div>
    </div>
  );
}
