import { useCallback, useEffect, useState } from "react";
import { Link, useParams } from "react-router-dom";

import { EventStreamPane, type StreamEvent } from "@/components/event-stream-pane";
import { TaskStateBadge } from "@/components/task-state-badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { useTaskStream } from "@/hooks/use-task-stream";
import { api } from "@/lib/api";
import type { AgentTask, Approval } from "@/types/api";

export function TaskDetailPage() {
  const { taskId } = useParams<{ taskId: string }>();
  const [task, setTask] = useState<AgentTask | null>(null);
  const [subtasks, setSubtasks] = useState<AgentTask[]>([]);
  const [events, setEvents] = useState<StreamEvent[]>([]);
  const [answer, setAnswer] = useState("");
  const [resolving, setResolving] = useState(false);
  const [cancelling, setCancelling] = useState(false);

  const refreshTask = useCallback(() => {
    if (!taskId) return;
    api.getTask(taskId).then(setTask);
  }, [taskId]);

  useEffect(refreshTask, [refreshTask]);

  useEffect(() => {
    if (!task) return;
    api
      .listTasksForProject(task.projectId)
      .then((all) => setSubtasks(all.filter((t) => t.parentTaskId === task.id)));
  }, [task]);

  const cancelTask = async () => {
    if (!task) return;
    setCancelling(true);
    try {
      await api.cancelTask(task.id);
      refreshTask();
    } finally {
      setCancelling(false);
    }
  };

  useEffect(() => {
    const latestRun = task?.runs.at(-1);
    if (!latestRun) return;
    api.getRunEvents(latestRun.id).then((dtos) =>
      setEvents(
        dtos.map((d) => ({ id: d.id, type: d.type, timestamp: d.timestamp, payload: d.payloadJson })),
      ),
    );
  }, [task?.runs]);

  useTaskStream(
    taskId,
    (event) =>
      setEvents((prev) =>
        prev.some((e) => e.id === event.id)
          ? prev
          : [...prev, { id: event.id, type: event.type, timestamp: event.timestamp, payload: event.payload }],
      ),
    () => refreshTask(),
    () => refreshTask(),
  );

  const pendingApproval: Approval | undefined = task?.approvals.find(
    (a) => a.status === "Pending",
  );

  const resolve = async (status: "approve" | "reject") => {
    if (!pendingApproval) return;
    setResolving(true);
    try {
      if (status === "approve") {
        await api.approve(pendingApproval.id, { answer: answer || undefined });
      } else {
        await api.reject(pendingApproval.id, { answer: answer || undefined });
      }
      setAnswer("");
      refreshTask();
    } finally {
      setResolving(false);
    }
  };

  if (!task) {
    return <p className="p-8 text-sm text-muted-foreground">Loading...</p>;
  }

  return (
    <div className="flex flex-col gap-6 p-8">
      <Link to={`/projects/${task.projectId}`} className="text-sm text-muted-foreground hover:underline">
        ← Back to project
      </Link>

      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-semibold tracking-tight">{task.title}</h1>
          <p className="text-sm text-muted-foreground">{task.description}</p>
        </div>
        <div className="flex items-center gap-3">
          <TaskStateBadge state={task.state} />
          {!["Done", "Failed", "DeadLetter"].includes(task.state) && (
            <Button size="sm" variant="destructive" onClick={cancelTask} disabled={cancelling}>
              Cancel
            </Button>
          )}
        </div>
      </div>

      {subtasks.length > 0 && (
        <Card>
          <CardHeader>
            <CardTitle className="text-base">Subtasks</CardTitle>
          </CardHeader>
          <CardContent className="flex flex-col gap-2">
            {subtasks.map((subtask) => (
              <Link
                key={subtask.id}
                to={`/tasks/${subtask.id}`}
                className="flex items-center justify-between rounded-md border p-3 text-sm hover:bg-muted/50"
              >
                <span>{subtask.title}</span>
                <TaskStateBadge state={subtask.state} />
              </Link>
            ))}
          </CardContent>
        </Card>
      )}

      {(task.planJson || pendingApproval?.kind === "PlanReview") && (
        <Card>
          <CardHeader>
            <CardTitle className="text-base">Plan</CardTitle>
          </CardHeader>
          <CardContent className="flex flex-col gap-2">
            {task.planJson && (
              <pre className="overflow-x-auto rounded-md bg-muted p-3 text-xs">{task.planJson}</pre>
            )}
            {task.acceptanceCriteriaJson && (
              <div>
                <p className="text-xs font-medium text-muted-foreground">Acceptance criteria</p>
                <pre className="overflow-x-auto rounded-md bg-muted p-3 text-xs">
                  {task.acceptanceCriteriaJson}
                </pre>
              </div>
            )}
          </CardContent>
        </Card>
      )}

      {pendingApproval && (
        <Card className="border-amber-400">
          <CardHeader>
            <CardTitle className="text-base">
              {pendingApproval.kind === "PlanReview" ? "Plan review needed" : "Approval needed"}
            </CardTitle>
          </CardHeader>
          <CardContent className="flex flex-col gap-3">
            <p className="text-sm">{pendingApproval.question}</p>
            <Input
              placeholder={
                pendingApproval.kind === "PlanReview"
                  ? "Feedback if rejecting (optional)"
                  : "Your answer (optional)"
              }
              value={answer}
              onChange={(e) => setAnswer(e.target.value)}
            />
            <div className="flex gap-2">
              <Button onClick={() => resolve("approve")} disabled={resolving}>
                Approve
              </Button>
              <Button variant="destructive" onClick={() => resolve("reject")} disabled={resolving}>
                Reject
              </Button>
            </div>
          </CardContent>
        </Card>
      )}

      <Card>
        <CardHeader>
          <CardTitle className="text-base">Live stream</CardTitle>
        </CardHeader>
        <CardContent>
          <EventStreamPane events={events} />
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle className="text-base">Runs</CardTitle>
        </CardHeader>
        <CardContent className="flex flex-col gap-2">
          {task.runs.length === 0 ? (
            <p className="text-sm text-muted-foreground">No runs yet.</p>
          ) : (
            task.runs.map((run) => (
              <div key={run.id} className="flex items-center justify-between rounded-md border p-3 text-sm">
                <span>
                  {run.engine} · {run.model}
                </span>
                <span className="text-muted-foreground">{run.status}</span>
              </div>
            ))
          )}
        </CardContent>
      </Card>
    </div>
  );
}
