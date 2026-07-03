import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Fragment, useEffect, useState } from "react";
import { WorkflowsApi } from "../api/endpoints";
import StatusPill from "./StatusPill";
import { formatDuration } from "../utils/duration";
import { onRunStatusChanged } from "../realtime/runStatusConnection";

export default function WorkflowRunHistory({ workflowId }: { workflowId: string }) {
  const queryClient = useQueryClient();
  const runs = useQuery({
    queryKey: ["workflow-runs", workflowId],
    queryFn: () => WorkflowsApi.runs(workflowId),
    // SignalR pushes an update the moment a run's status actually changes; this is just the
    // fallback for a dropped/reconnecting connection, so it can be much less frequent than before.
    refetchInterval: 20000,
  });
  const [expanded, setExpanded] = useState<string | null>(null);

  const detail = useQuery({
    queryKey: ["workflow-run-detail", expanded],
    queryFn: () => WorkflowsApi.runDetail(expanded!),
    enabled: !!expanded,
  });

  const cancelMutation = useMutation({
    mutationFn: (runId: string) => WorkflowsApi.cancelRun(runId),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ["workflow-runs", workflowId] }),
  });

  useEffect(() => {
    return onRunStatusChanged((event) => {
      if (event.kind === "workflow") {
        queryClient.invalidateQueries({ queryKey: ["workflow-runs", workflowId] });
        queryClient.invalidateQueries({ queryKey: ["workflow-run-detail", event.runId] });
        queryClient.invalidateQueries({ queryKey: ["workflows"] });
      }
    });
  }, [workflowId, queryClient]);

  return (
    <div className="card">
      <h3 style={{ marginTop: 0, fontSize: 14 }}>Run history</h3>
      {runs.data?.length ? (
        <table>
          <thead>
            <tr>
              <th>Status</th>
              <th>Trigger</th>
              <th>Started</th>
              <th>Duration</th>
              <th></th>
            </tr>
          </thead>
          <tbody>
            {runs.data.map((r) => (
              <Fragment key={r.id}>
                <tr>
                  <td>
                    <StatusPill status={r.status} />
                  </td>
                  <td className="muted">
                    {r.triggerKind} {r.triggerNodeType ? `(${r.triggerNodeType})` : ""}
                  </td>
                  <td className="muted">{r.startedAt ? new Date(r.startedAt).toLocaleString() : "-"}</td>
                  <td className="muted">{formatDuration(r.startedAt, r.completedAt) ?? "-"}</td>
                  <td style={{ display: "flex", gap: 6 }}>
                    {(r.status === "Running" || r.status === "Pending") && (
                      <button disabled={cancelMutation.isPending} onClick={() => cancelMutation.mutate(r.id)}>
                        Cancel
                      </button>
                    )}
                    <button onClick={() => setExpanded(expanded === r.id ? null : r.id)}>
                      {expanded === r.id ? "Hide" : "Details"}
                    </button>
                  </td>
                </tr>
                {expanded === r.id && (
                  <tr>
                    <td colSpan={5}>
                      {detail.data ? (
                        <div style={{ display: "flex", flexDirection: "column", gap: 8 }}>
                          {detail.data.nodeRuns.map((nr) => (
                            <div key={nr.id} className="card" style={{ margin: 0 }}>
                              <div className="page-header" style={{ marginBottom: 6 }}>
                                <strong style={{ fontSize: 12 }}>{nr.nodeType}</strong>
                                <div style={{ display: "flex", gap: 8, alignItems: "center" }}>
                                  {formatDuration(nr.startedAt, nr.completedAt) && (
                                    <span className="muted" style={{ fontSize: 12 }}>
                                      {formatDuration(nr.startedAt, nr.completedAt)}
                                    </span>
                                  )}
                                  <StatusPill status={nr.status} />
                                </div>
                              </div>
                              {nr.errorMessage && <p style={{ color: "var(--danger)" }}>{nr.errorMessage}</p>}
                              {nr.logText && <pre className="mono muted">{nr.logText}</pre>}
                              <details>
                                <summary className="muted">input / output</summary>
                                <pre className="mono">{JSON.stringify({ input: nr.input, output: nr.output }, null, 2)}</pre>
                              </details>
                            </div>
                          ))}
                        </div>
                      ) : (
                        <span className="muted">Loading…</span>
                      )}
                    </td>
                  </tr>
                )}
              </Fragment>
            ))}
          </tbody>
        </table>
      ) : (
        <p className="muted">No runs yet.</p>
      )}
    </div>
  );
}
