import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Link } from "react-router-dom";
import { WorkflowsApi } from "../api/endpoints";

export default function WorkflowsList() {
  const queryClient = useQueryClient();
  const workflows = useQuery({ queryKey: ["workflows"], queryFn: WorkflowsApi.list });
  const deleteMutation = useMutation({
    mutationFn: WorkflowsApi.remove,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ["workflows"] }),
  });
  const duplicateMutation = useMutation({
    mutationFn: WorkflowsApi.duplicate,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ["workflows"] }),
  });

  return (
    <div>
      <div className="page-header">
        <h2>Workflows</h2>
        <Link to="/workflows/new">
          <button className="primary">+ New Workflow</button>
        </Link>
      </div>

      <div className="card">
        {workflows.data?.length ? (
          <table>
            <thead>
              <tr>
                <th>Name</th>
                <th>Nodes</th>
                <th>Status</th>
                <th>Updated</th>
                <th></th>
              </tr>
            </thead>
            <tbody>
              {workflows.data.map((w) => (
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
                  <td className="muted">{new Date(w.updatedAt).toLocaleString()}</td>
                  <td style={{ display: "flex", gap: 6 }}>
                    <button disabled={duplicateMutation.isPending} onClick={() => duplicateMutation.mutate(w.id)}>
                      Duplicate
                    </button>
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
          <div className="empty-state">No workflows yet. Create one to automate something.</div>
        )}
      </div>
    </div>
  );
}
