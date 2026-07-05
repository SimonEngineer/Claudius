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

  const replayMutation = useMutation({
    mutationFn: (runId: string) => WorkflowsApi.replayRun(runId),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ["workflow-runs", workflowId] }),
  });

  const clearMutation = useMutation({
    mutationFn: () => WorkflowsApi.clearRuns(workflowId),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ["workflow-runs", workflowId] }),
  });

  const [showTimeline, setShowTimeline] = useState(false);

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
      <div className="page-header">
        <h3 style={{ margin: 0, fontSize: 14 }}>Run history</h3>
        {runs.data && runs.data.length > 0 && (
          <button
            className="danger"
            disabled={clearMutation.isPending}
            onClick={() => {
              if (confirm("Delete ALL runs for this workflow? This can't be undone.")) clearMutation.mutate();
            }}
          >
            Clear history
          </button>
        )}
      </div>
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
                    {(r.status === "Succeeded" || r.status === "Failed" || r.status === "Cancelled") && (
                      <button
                        disabled={replayMutation.isPending}
                        title="Start a fresh run from the same trigger with the same payload"
                        onClick={() => replayMutation.mutate(r.id)}
                      >
                        Re-run
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
                          <div>
                            <button onClick={() => setShowTimeline(!showTimeline)}>
                              {showTimeline ? "Show details" : "Show timeline"}
                            </button>
                          </div>
                          {showTimeline ? (
                            <Timeline nodeRuns={detail.data.nodeRuns} />
                          ) : (
                          detail.data.nodeRuns.map((nr) => (
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
                          )))}
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

/** Gantt-style per-node timing bars, scaled to the run's total wall time. */
function Timeline({ nodeRuns }: { nodeRuns: import("../types").NodeRun[] }) {
  const timed = nodeRuns.filter((nr) => nr.startedAt);
  if (timed.length === 0) return <p className="muted">No timing data.</p>;

  const starts = timed.map((nr) => new Date(nr.startedAt!).getTime());
  const ends = timed.map((nr) => new Date(nr.completedAt ?? nr.startedAt!).getTime());
  const min = Math.min(...starts);
  const max = Math.max(...ends);
  const span = Math.max(max - min, 1);

  const color = (status: string) =>
    status === "Succeeded" ? "var(--success)" : status === "Failed" ? "var(--danger)" : status === "Cancelled" ? "var(--warn)" : "var(--accent)";

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: 4 }}>
      {timed.map((nr) => {
        const start = ((new Date(nr.startedAt!).getTime() - min) / span) * 100;
        const width = Math.max(((new Date(nr.completedAt ?? nr.startedAt!).getTime() - new Date(nr.startedAt!).getTime()) / span) * 100, 1);
        const ms = nr.completedAt ? new Date(nr.completedAt).getTime() - new Date(nr.startedAt!).getTime() : null;
        return (
          <div key={nr.id} style={{ display: "flex", alignItems: "center", gap: 8 }}>
            <span className="mono muted" style={{ width: 160, fontSize: 11, overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap" }}>
              {nr.nodeType}
            </span>
            <div style={{ flex: 1, position: "relative", height: 14, background: "var(--panel-2)", borderRadius: 4 }}>
              <div
                title={`${nr.status}${ms !== null ? ` · ${ms}ms` : ""}`}
                style={{
                  position: "absolute",
                  left: `${start}%`,
                  width: `${width}%`,
                  top: 2,
                  bottom: 2,
                  borderRadius: 3,
                  background: color(nr.status),
                }}
              />
            </div>
            <span className="muted" style={{ width: 64, fontSize: 11, textAlign: "right" }}>{ms !== null ? `${ms}ms` : "…"}</span>
          </div>
        );
      })}
    </div>
  );
}
