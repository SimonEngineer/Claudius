import { useEffect, useState } from "react";
import { Link, useParams } from "react-router-dom";

import { TaskStateBadge } from "@/components/task-state-badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import {
  Dialog,
  DialogContent,
  DialogFooter,
  DialogHeader,
  DialogTitle,
  DialogTrigger,
} from "@/components/ui/dialog";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Separator } from "@/components/ui/separator";
import { Textarea } from "@/components/ui/textarea";
import { useProjectStream } from "@/hooks/use-task-stream";
import { api } from "@/lib/api";
import type { AgentTask, Project } from "@/types/api";

export function ProjectDetailPage() {
  const { projectId } = useParams<{ projectId: string }>();
  const [project, setProject] = useState<Project | null>(null);
  const [tasks, setTasks] = useState<AgentTask[]>([]);
  const [open, setOpen] = useState(false);
  const [description, setDescription] = useState("");
  const [title, setTitle] = useState("");
  const [submitting, setSubmitting] = useState(false);

  const refreshTasks = () => {
    if (!projectId) return;
    api.listTasksForProject(projectId).then(setTasks);
  };

  useEffect(() => {
    if (!projectId) return;
    api.getProject(projectId).then(setProject);
    refreshTasks();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [projectId]);

  useProjectStream(projectId, () => refreshTasks());

  const handleCreateGoal = async () => {
    if (!projectId || !description) return;
    setSubmitting(true);
    try {
      await api.createGoal(projectId, { description, title: title || undefined });
      setOpen(false);
      setDescription("");
      setTitle("");
      refreshTasks();
    } finally {
      setSubmitting(false);
    }
  };

  if (!project) {
    return <p className="p-8 text-sm text-muted-foreground">Loading...</p>;
  }

  return (
    <div className="flex flex-col gap-6 p-8">
      <Link to="/" className="text-sm text-muted-foreground hover:underline">
        ← Projects
      </Link>

      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-semibold tracking-tight">{project.name}</h1>
          <p className="text-sm text-muted-foreground">{project.repoPath}</p>
        </div>
        <Dialog open={open} onOpenChange={setOpen}>
          <DialogTrigger asChild>
            <Button>New goal</Button>
          </DialogTrigger>
          <DialogContent>
            <DialogHeader>
              <DialogTitle>Create goal</DialogTitle>
            </DialogHeader>
            <div className="flex flex-col gap-3">
              <div className="flex flex-col gap-1.5">
                <Label htmlFor="title">Title (optional)</Label>
                <Input
                  id="title"
                  value={title}
                  onChange={(e) => setTitle(e.target.value)}
                />
              </div>
              <div className="flex flex-col gap-1.5">
                <Label htmlFor="description">Description</Label>
                <Textarea
                  id="description"
                  rows={5}
                  value={description}
                  onChange={(e) => setDescription(e.target.value)}
                  placeholder="What should the agents accomplish?"
                />
              </div>
            </div>
            <DialogFooter>
              <Button variant="outline" onClick={() => setOpen(false)}>
                Cancel
              </Button>
              <Button onClick={handleCreateGoal} disabled={submitting}>
                {submitting ? "Creating..." : "Create"}
              </Button>
            </DialogFooter>
          </DialogContent>
        </Dialog>
      </div>

      <Separator />

      <Card>
        <CardHeader>
          <CardTitle className="text-base">Tasks</CardTitle>
        </CardHeader>
        <CardContent className="flex flex-col gap-2">
          {tasks.length === 0 ? (
            <p className="text-sm text-muted-foreground">No tasks yet.</p>
          ) : (
            tasks.map((task) => (
              <Link
                key={task.id}
                to={`/tasks/${task.id}`}
                className="flex items-center justify-between rounded-md border p-3 text-sm transition-colors hover:bg-accent/40"
              >
                <div className="flex flex-col gap-0.5">
                  <span className="font-medium">{task.title}</span>
                  <span className="text-xs text-muted-foreground">
                    {task.lane} · priority {task.priority}
                  </span>
                </div>
                <TaskStateBadge state={task.state} />
              </Link>
            ))
          )}
        </CardContent>
      </Card>
    </div>
  );
}
