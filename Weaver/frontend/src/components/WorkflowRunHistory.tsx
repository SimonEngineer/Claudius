import { useQuery } from "@tanstack/react-query";
import { Fragment, useState } from "react";
import { WorkflowsApi } from "../api/endpoints";
import StatusPill from "./StatusPill";

export default function WorkflowRunHistory({ workflowId }: { workflowId: string }) {
  const runs = useQuery({
    queryKey: ["workflow-runs", workflowId],
    queryFn: () => WorkflowsApi.runs(workflowId),
    refetchInterval: 3000,
  });
  const [expanded, setExpanded] = useState<string | null>(null);

  const detail = useQuery({
    queryKey: ["workflow-run-detail", expanded],
    queryFn: () => WorkflowsApi.runDetail(expanded!),
    enabled: !!expanded,
  });

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
                  <td>
                    <button onClick={() => setExpanded(expanded === r.id ? null : r.id)}>
                      {expanded === r.id ? "Hide" : "Details"}
                    </button>
                  </td>
                </tr>
                {expanded === r.id && (
                  <tr>
                    <td colSpan={4}>
                      {detail.data ? (
                        <div style={{ display: "flex", flexDirection: "column", gap: 8 }}>
                          {detail.data.nodeRuns.map((nr) => (
                            <div key={nr.id} className="card" style={{ margin: 0 }}>
                              <div className="page-header" style={{ marginBottom: 6 }}>
                                <strong style={{ fontSize: 12 }}>{nr.nodeType}</strong>
                                <StatusPill status={nr.status} />
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
