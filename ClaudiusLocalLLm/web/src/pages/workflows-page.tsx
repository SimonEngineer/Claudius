import { useEffect, useState } from "react";
import { Link, useNavigate } from "react-router-dom";

import { Badge } from "@/components/ui/badge";
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
import { api } from "@/lib/api";
import type { Workflow } from "@/types/api";

const EMPTY_DEFINITION = JSON.stringify({ nodes: [], edges: [] });

export function WorkflowsPage() {
  const navigate = useNavigate();
  const [workflows, setWorkflows] = useState<Workflow[]>([]);
  const [loading, setLoading] = useState(true);
  const [open, setOpen] = useState(false);
  const [name, setName] = useState("");
  const [submitting, setSubmitting] = useState(false);

  const refresh = () => {
    setLoading(true);
    api
      .listWorkflows()
      .then(setWorkflows)
      .finally(() => setLoading(false));
  };

  useEffect(refresh, []);

  const handleCreate = async () => {
    if (!name) return;
    setSubmitting(true);
    try {
      const workflow = await api.createWorkflow({
        name,
        definitionJson: EMPTY_DEFINITION,
        isEnabled: true,
      });
      setOpen(false);
      setName("");
      navigate(`/automations/${workflow.id}`);
    } finally {
      setSubmitting(false);
    }
  };

  const toggleEnabled = async (workflow: Workflow) => {
    await api.updateWorkflow(workflow.id, {
      name: workflow.name,
      description: workflow.description ?? undefined,
      definitionJson: workflow.definitionJson,
      isEnabled: !workflow.isEnabled,
    });
    refresh();
  };

  const remove = async (workflow: Workflow) => {
    if (!confirm(`Delete workflow "${workflow.name}"?`)) return;
    await api.deleteWorkflow(workflow.id);
    refresh();
  };

  return (
    <div className="flex flex-col gap-6 p-8">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-semibold tracking-tight">Automations</h1>
          <p className="text-sm text-muted-foreground">
            Event/cron/webhook-triggered workflows, also fireable from code via IWorkflowEngine.
          </p>
        </div>
        <Dialog open={open} onOpenChange={setOpen}>
          <DialogTrigger asChild>
            <Button>New automation</Button>
          </DialogTrigger>
          <DialogContent>
            <DialogHeader>
              <DialogTitle>Create automation</DialogTitle>
            </DialogHeader>
            <div className="flex flex-col gap-1.5">
              <Label htmlFor="name">Name</Label>
              <Input id="name" value={name} onChange={(e) => setName(e.target.value)} />
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
      ) : workflows.length === 0 ? (
        <p className="text-sm text-muted-foreground">
          No automations yet. Create one to get started.
        </p>
      ) : (
        <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-3">
          {workflows.map((workflow) => (
            <Card key={workflow.id} className="h-full">
              <CardHeader>
                <div className="flex items-center gap-2">
                  <Link to={`/automations/${workflow.id}`} className="hover:underline">
                    <CardTitle>{workflow.name}</CardTitle>
                  </Link>
                  <Badge variant={workflow.isEnabled ? "success" : "secondary"}>
                    {workflow.isEnabled ? "Enabled" : "Disabled"}
                  </Badge>
                </div>
                {workflow.description && (
                  <CardDescription>{workflow.description}</CardDescription>
                )}
              </CardHeader>
              <CardContent className="flex items-center gap-2">
                <Button variant="outline" size="sm" onClick={() => toggleEnabled(workflow)}>
                  {workflow.isEnabled ? "Disable" : "Enable"}
                </Button>
                <Button variant="outline" size="sm" onClick={() => remove(workflow)}>
                  Delete
                </Button>
              </CardContent>
            </Card>
          ))}
        </div>
      )}
    </div>
  );
}
