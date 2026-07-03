import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { WorkflowsApi } from "../api/endpoints";
import { timeAgo } from "../utils/timeAgo";

/** Auto-captured save history for a workflow, with one-click restore (itself undoable). */
export default function WorkflowRevisions({ workflowId }: { workflowId: string }) {
  const queryClient = useQueryClient();
  const revisions = useQuery({
    queryKey: ["workflow-revisions", workflowId],
    queryFn: () => WorkflowsApi.revisions(workflowId),
  });

  const restoreMutation = useMutation({
    mutationFn: (revisionId: string) => WorkflowsApi.restoreRevision(workflowId, revisionId),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["workflow", workflowId] });
      queryClient.invalidateQueries({ queryKey: ["workflow-revisions", workflowId] });
      queryClient.invalidateQueries({ queryKey: ["workflows"] });
    },
  });

  return (
    <div className="card">
      <h3 style={{ marginTop: 0, fontSize: 14 }}>Revisions</h3>
      <p className="muted" style={{ fontSize: 12, marginTop: 0 }}>
        A snapshot is captured automatically before every save. Restoring replaces the current graph
        (the pre-restore state is snapshotted too, so you can restore back).
      </p>
      {revisions.data?.length ? (
        <table>
          <thead>
            <tr>
              <th>When</th>
              <th>Name at the time</th>
              <th>Nodes</th>
              <th></th>
            </tr>
          </thead>
          <tbody>
            {revisions.data.map((r) => (
              <tr key={r.id}>
                <td className="muted" title={new Date(r.createdAt).toLocaleString()}>
                  {timeAgo(r.createdAt)}
                </td>
                <td>{r.workflowName}</td>
                <td className="muted">{r.nodeCount}</td>
                <td>
                  <button
                    disabled={restoreMutation.isPending}
                    onClick={() => {
                      if (confirm("Replace the current workflow with this revision? Unsaved canvas changes will be lost."))
                        restoreMutation.mutate(r.id);
                    }}
                  >
                    Restore
                  </button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      ) : (
        <p className="muted">No revisions yet — they appear after the first save of an edit.</p>
      )}
    </div>
  );
}
