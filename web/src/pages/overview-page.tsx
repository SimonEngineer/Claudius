import { useEffect, useState } from "react";
import { Link } from "react-router-dom";

import { TaskStateBadge } from "@/components/task-state-badge";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { useApprovalsStream } from "@/hooks/use-task-stream";
import { api } from "@/lib/api";
import type { Overview, TaskState } from "@/types/api";

const POLL_INTERVAL_MS = 10_000;

const ALL_STATES: TaskState[] = [
  "Queued",
  "Planning",
  "ReadyForWork",
  "InProgress",
  "Verifying",
  "NeedsFix",
  "AwaitingInput",
  "Decomposed",
  "Done",
  "Failed",
  "DeadLetter",
];

function timeAgo(iso: string): string {
  const seconds = Math.max(0, (Date.now() - new Date(iso).getTime()) / 1000);
  if (seconds < 60) return "just now";
  const minutes = Math.floor(seconds / 60);
  if (minutes < 60) return `${minutes}m ago`;
  const hours = Math.floor(minutes / 60);
  if (hours < 24) return `${hours}h ago`;
  return `${Math.floor(hours / 24)}d ago`;
}

export function OverviewPage() {
  const [overview, setOverview] = useState<Overview | null>(null);

  const refresh = () => {
    api.getOverview().then(setOverview);
  };

  useEffect(() => {
    refresh();
    const interval = setInterval(refresh, POLL_INTERVAL_MS);
    return () => clearInterval(interval);
  }, []);

  // Approvals can also change a task's life cycle, so a resolved/requested approval is a good
  // signal to refresh the whole overview immediately rather than waiting for the next poll.
  useApprovalsStream(refresh, refresh);

  if (!overview) {
    return <p className="p-8 text-sm text-muted-foreground">Loading...</p>;
  }

  return (
    <div className="flex flex-col gap-6 p-8">
      <div>
        <h1 className="text-2xl font-semibold tracking-tight">Overview</h1>
        <p className="text-sm text-muted-foreground">
          What every project's agents are doing right now, across {overview.projectCount}{" "}
          project{overview.projectCount === 1 ? "" : "s"}.
        </p>
      </div>

      <div className="flex flex-wrap gap-2">
        {ALL_STATES.filter((state) => overview.taskCountsByState[state]).map((state) => (
          <div
            key={state}
            className="flex items-center gap-2 rounded-md border px-3 py-1.5 text-sm"
          >
            <TaskStateBadge state={state} />
            <span className="font-medium">{overview.taskCountsByState[state]}</span>
          </div>
        ))}
      </div>

      <div className="grid grid-cols-1 gap-4 lg:grid-cols-2">
        <Card>
          <CardHeader>
            <CardTitle className="text-base">
              In progress ({overview.inProgress.length})
            </CardTitle>
          </CardHeader>
          <CardContent className="flex flex-col gap-2">
            {overview.inProgress.length === 0 ? (
              <p className="text-sm text-muted-foreground">Nothing running right now.</p>
            ) : (
              overview.inProgress.map((task) => (
                <Link
                  key={task.id}
                  to={`/tasks/${task.id}`}
                  className="flex items-center justify-between rounded-md border p-3 text-sm transition-colors hover:bg-accent/40"
                >
                  <div className="flex flex-col gap-0.5">
                    <span className="font-medium">{task.title}</span>
                    <span className="text-xs text-muted-foreground">
                      {task.projectName} · {task.lane} · updated {timeAgo(task.updatedAt)}
                    </span>
                  </div>
                  <TaskStateBadge state={task.state} />
                </Link>
              ))
            )}
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle className="text-base">
              Needs attention ({overview.needsAttention.length})
            </CardTitle>
          </CardHeader>
          <CardContent className="flex flex-col gap-2">
            {overview.needsAttention.length === 0 ? (
              <p className="text-sm text-muted-foreground">Nothing needs attention.</p>
            ) : (
              overview.needsAttention.map((task) => (
                <Link
                  key={task.id}
                  to={`/tasks/${task.id}`}
                  className="flex items-center justify-between rounded-md border p-3 text-sm transition-colors hover:bg-accent/40"
                >
                  <div className="flex flex-col gap-0.5">
                    <span className="font-medium">{task.title}</span>
                    <span className="text-xs text-muted-foreground">
                      {task.projectName} · retry {task.retryCount} · updated{" "}
                      {timeAgo(task.updatedAt)}
                    </span>
                  </div>
                  <TaskStateBadge state={task.state} />
                </Link>
              ))
            )}
          </CardContent>
        </Card>
      </div>

      <Card>
        <CardHeader>
          <CardTitle className="text-base">
            Pending approvals ({overview.pendingApprovals.length})
          </CardTitle>
        </CardHeader>
        <CardContent className="flex flex-col gap-2">
          {overview.pendingApprovals.length === 0 ? (
            <p className="text-sm text-muted-foreground">Nothing pending.</p>
          ) : (
            overview.pendingApprovals.map((approval) => (
              <Link
                key={approval.id}
                to={`/tasks/${approval.taskId}`}
                className="flex items-center justify-between rounded-md border p-3 text-sm transition-colors hover:bg-accent/40"
              >
                <div className="flex flex-col gap-0.5">
                  <span className="font-medium">{approval.question}</span>
                  <span className="text-xs text-muted-foreground">
                    {approval.projectName} · {approval.taskTitle} · asked{" "}
                    {timeAgo(approval.createdAt)}
                  </span>
                </div>
              </Link>
            ))
          )}
        </CardContent>
      </Card>
    </div>
  );
}
