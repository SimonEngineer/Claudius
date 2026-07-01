import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useEffect, useMemo, useRef, useState } from "react";
import { Link } from "react-router-dom";
import { WorkflowsApi } from "../api/endpoints";
import StatusPill from "../components/StatusPill";
import { onRunStatusChanged } from "../realtime/runStatusConnection";

export default function WorkflowsList() {
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
  const filtered = useMemo(() => {
    const term = search.trim().toLowerCase();
    if (!term) return workflows.data;
    return workflows.data?.filter((w) => w.name.toLowerCase().includes(term));
  }, [workflows.data, search]);
  const deleteMutation = useMutation({
    mutationFn: WorkflowsApi.remove,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ["workflows"] }),
  });
  const duplicateMutation = useMutation({
    mutationFn: WorkflowsApi.duplicate,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ["workflows"] }),
  });
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
                <th>Name</th>
                <th>Nodes</th>
                <th>Status</th>
                <th>Last run</th>
                <th>Updated</th>
                <th></th>
              </tr>
            </thead>
            <tbody>
              {filtered.map((w) => (
                <tr key={w.id}>
                  <td>
                    <Link to={`/workflows/${w.id}`}>{w.name}</Link>
                  </td>
                  <td className="muted">{w.nodes.length}</td>
                  <td>
                    <span className={`pill ${w.isEnabled ? "Succeeded" : "Cancelled"}`}>
                      {w.isEnabled ? "enabled" : "disabled"}
                    </span>
                  </td>
                  <td>
                    {w.lastRunStatus ? (
                      <span title={w.lastRunAt ? new Date(w.lastRunAt).toLocaleString() : undefined}>
                        <StatusPill status={w.lastRunStatus} />
                      </span>
                    ) : (
                      <span className="muted">never run</span>
                    )}
                  </td>
                  <td className="muted">{new Date(w.updatedAt).toLocaleString()}</td>
                  <td style={{ display: "flex", gap: 6 }}>
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
