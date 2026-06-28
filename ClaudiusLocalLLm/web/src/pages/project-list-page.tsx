import { useEffect, useState } from "react";
import { Link } from "react-router-dom";

import { Button } from "@/components/ui/button";
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from "@/components/ui/card";
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
import { api, type CreateProjectInput } from "@/lib/api";
import type { Project } from "@/types/api";

// Defaults assume a fully local/offline setup (LocalAI for both lanes, no API key or internet
// required) -- swap supervisorModel for an "anthropic/..." id to opt a project into the cloud.
const EMPTY_FORM: CreateProjectInput = {
  name: "",
  repoPath: "",
  gitRemote: "",
  workerModel: "localai/qwen2.5-coder",
  supervisorModel: "localai/qwen2.5-coder-32b",
  maxWorkerConcurrency: 1,
  priority: 0,
  requirePlanApproval: false,
};

export function ProjectListPage() {
  const [projects, setProjects] = useState<Project[]>([]);
  const [loading, setLoading] = useState(true);
  const [open, setOpen] = useState(false);
  const [form, setForm] = useState<CreateProjectInput>(EMPTY_FORM);
  const [submitting, setSubmitting] = useState(false);

  const refresh = () => {
    setLoading(true);
    api
      .listProjects()
      .then(setProjects)
      .finally(() => setLoading(false));
  };

  useEffect(refresh, []);

  const handleCreate = async () => {
    if (!form.name || !form.repoPath || !form.workerModel || !form.supervisorModel) return;
    setSubmitting(true);
    try {
      await api.createProject(form);
      setOpen(false);
      setForm(EMPTY_FORM);
      refresh();
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <div className="flex flex-col gap-6 p-8">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-semibold tracking-tight">Projects</h1>
          <p className="text-sm text-muted-foreground">
            Multi-project task orchestration across local and cloud models.
          </p>
        </div>
        <Dialog open={open} onOpenChange={setOpen}>
          <DialogTrigger asChild>
            <Button>New project</Button>
          </DialogTrigger>
          <DialogContent>
            <DialogHeader>
              <DialogTitle>Create project</DialogTitle>
            </DialogHeader>
            <div className="flex flex-col gap-3">
              <div className="flex flex-col gap-1.5">
                <Label htmlFor="name">Name</Label>
                <Input
                  id="name"
                  value={form.name}
                  onChange={(e) => setForm({ ...form, name: e.target.value })}
                />
              </div>
              <div className="flex flex-col gap-1.5">
                <Label htmlFor="repoPath">Repo path</Label>
                <Input
                  id="repoPath"
                  value={form.repoPath}
                  onChange={(e) => setForm({ ...form, repoPath: e.target.value })}
                  placeholder="/home/user/projects/my-app"
                />
              </div>
              <div className="flex flex-col gap-1.5">
                <Label htmlFor="gitRemote">Git remote (optional)</Label>
                <Input
                  id="gitRemote"
                  value={form.gitRemote}
                  onChange={(e) => setForm({ ...form, gitRemote: e.target.value })}
                />
              </div>
              <div className="grid grid-cols-2 gap-3">
                <div className="flex flex-col gap-1.5">
                  <Label htmlFor="workerModel">Worker model</Label>
                  <Input
                    id="workerModel"
                    value={form.workerModel}
                    onChange={(e) => setForm({ ...form, workerModel: e.target.value })}
                    placeholder="ollama/qwen2.5-coder:32b"
                  />
                </div>
                <div className="flex flex-col gap-1.5">
                  <Label htmlFor="supervisorModel">Supervisor model</Label>
                  <Input
                    id="supervisorModel"
                    value={form.supervisorModel}
                    onChange={(e) => setForm({ ...form, supervisorModel: e.target.value })}
                    placeholder="localai/qwen2.5-coder-32b or anthropic/claude-sonnet-4-6"
                  />
                </div>
              </div>
              <div className="grid grid-cols-2 gap-3">
                <div className="flex flex-col gap-1.5">
                  <Label htmlFor="maxWorkerConcurrency">Max worker concurrency</Label>
                  <Input
                    id="maxWorkerConcurrency"
                    type="number"
                    min={1}
                    value={form.maxWorkerConcurrency}
                    onChange={(e) =>
                      setForm({ ...form, maxWorkerConcurrency: Number(e.target.value) })
                    }
                  />
                </div>
                <div className="flex flex-col gap-1.5">
                  <Label htmlFor="priority">Priority</Label>
                  <Input
                    id="priority"
                    type="number"
                    value={form.priority}
                    onChange={(e) => setForm({ ...form, priority: Number(e.target.value) })}
                  />
                </div>
              </div>
              <label className="flex items-center gap-2 text-sm">
                <input
                  type="checkbox"
                  checked={form.requirePlanApproval}
                  onChange={(e) => setForm({ ...form, requirePlanApproval: e.target.checked })}
                />
                Require plan approval before worker tasks start
              </label>
            </div>
            <DialogFooter>
              <Button variant="outline" onClick={() => setOpen(false)}>
                Cancel
              </Button>
              <Button onClick={handleCreate} disabled={submitting}>
                {submitting ? "Creating..." : "Create"}
              </Button>
            </DialogFooter>
          </DialogContent>
        </Dialog>
      </div>

      {loading ? (
        <p className="text-sm text-muted-foreground">Loading...</p>
      ) : projects.length === 0 ? (
        <p className="text-sm text-muted-foreground">
          No projects yet. Create one to get started.
        </p>
      ) : (
        <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-3">
          {projects.map((project) => (
            <Link key={project.id} to={`/projects/${project.id}`}>
              <Card className="h-full transition-colors hover:bg-accent/40">
                <CardHeader>
                  <div className="flex items-center gap-2">
                    <CardTitle>{project.name}</CardTitle>
                    {project.isPaused && (
                      <span className="rounded-full bg-amber-100 px-2 py-0.5 text-xs font-medium text-amber-800">
                        Paused
                      </span>
                    )}
                  </div>
                  <CardDescription className="truncate">{project.repoPath}</CardDescription>
                </CardHeader>
                <CardContent className="flex flex-col gap-1 text-sm text-muted-foreground">
                  <span>Worker: {project.workerModel}</span>
                  <span>Supervisor: {project.supervisorModel}</span>
                  <span>Worker concurrency: {project.maxWorkerConcurrency}</span>
                </CardContent>
              </Card>
            </Link>
          ))}
        </div>
      )}
    </div>
  );
}
