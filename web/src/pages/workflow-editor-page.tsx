import { useEffect, useMemo, useRef, useState } from "react";
import { useNavigate, useParams } from "react-router-dom";

import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";
import { api } from "@/lib/api";
import type {
  NodeTypeDescriptor,
  Workflow,
  WorkflowDefinition,
  WorkflowNode,
  WorkflowRun,
} from "@/types/api";

function emptyDefinition(): WorkflowDefinition {
  return { nodes: [], edges: [] };
}

function nextNodeId(definition: WorkflowDefinition): string {
  let i = definition.nodes.length + 1;
  while (definition.nodes.some((n) => n.id === `n${i}`)) i += 1;
  return `n${i}`;
}

export function WorkflowEditorPage() {
  const { workflowId } = useParams<{ workflowId: string }>();
  const navigate = useNavigate();

  const [workflow, setWorkflow] = useState<Workflow | null>(null);
  const [nodeTypes, setNodeTypes] = useState<NodeTypeDescriptor[]>([]);
  const [definition, setDefinition] = useState<WorkflowDefinition>(emptyDefinition());
  const [name, setName] = useState("");
  const [description, setDescription] = useState("");
  const [isEnabled, setIsEnabled] = useState(true);
  const [selectedNodeId, setSelectedNodeId] = useState<string | null>(null);
  const [connectingFrom, setConnectingFrom] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);
  const [saveError, setSaveError] = useState<string | null>(null);
  const [runPayload, setRunPayload] = useState("{}");
  const [running, setRunning] = useState(false);
  const [runs, setRuns] = useState<WorkflowRun[]>([]);

  const canvasRef = useRef<HTMLDivElement>(null);
  const dragState = useRef<{ nodeId: string; offsetX: number; offsetY: number } | null>(null);

  useEffect(() => {
    api.getWorkflowNodeTypes().then(setNodeTypes);
  }, []);

  const refreshRuns = (id: string) => {
    api.listWorkflowRuns(id).then(setRuns);
  };

  useEffect(() => {
    if (!workflowId) return;
    api.getWorkflow(workflowId).then((w) => {
      setWorkflow(w);
      setName(w.name);
      setDescription(w.description ?? "");
      setIsEnabled(w.isEnabled);
      setDefinition(JSON.parse(w.definitionJson) as WorkflowDefinition);
    });
    refreshRuns(workflowId);
  }, [workflowId]);

  const selectedNode = useMemo(
    () => definition.nodes.find((n) => n.id === selectedNodeId) ?? null,
    [definition, selectedNodeId],
  );
  const selectedNodeType = useMemo(
    () => nodeTypes.find((t) => t.type === selectedNode?.type) ?? null,
    [nodeTypes, selectedNode],
  );

  const addNode = (type: NodeTypeDescriptor) => {
    const id = nextNodeId(definition);
    const node: WorkflowNode = { id, type: type.type, config: {}, x: 60 + definition.nodes.length * 220, y: 60 };
    setDefinition({ ...definition, nodes: [...definition.nodes, node] });
    setSelectedNodeId(id);
  };

  const removeNode = (id: string) => {
    setDefinition({
      nodes: definition.nodes.filter((n) => n.id !== id),
      edges: definition.edges.filter((e) => e.from !== id && e.to !== id),
    });
    if (selectedNodeId === id) setSelectedNodeId(null);
  };

  const updateNodeConfig = (id: string, key: string, value: string) => {
    setDefinition({
      ...definition,
      nodes: definition.nodes.map((n) => (n.id === id ? { ...n, config: { ...n.config, [key]: value } } : n)),
    });
  };

  const handleNodeClick = (id: string) => {
    if (connectingFrom && connectingFrom !== id) {
      const exists = definition.edges.some((e) => e.from === connectingFrom && e.to === id);
      if (!exists) {
        setDefinition({ ...definition, edges: [...definition.edges, { from: connectingFrom, to: id }] });
      }
      setConnectingFrom(null);
      setSelectedNodeId(id);
      return;
    }
    setSelectedNodeId(id);
  };

  const removeEdge = (from: string, to: string) => {
    setDefinition({ ...definition, edges: definition.edges.filter((e) => !(e.from === from && e.to === to)) });
  };

  const onNodeMouseDown = (e: React.MouseEvent, node: WorkflowNode) => {
    if (e.button !== 0) return;
    const canvasRect = canvasRef.current?.getBoundingClientRect();
    if (!canvasRect) return;
    dragState.current = {
      nodeId: node.id,
      offsetX: e.clientX - canvasRect.left - node.x,
      offsetY: e.clientY - canvasRect.top - node.y,
    };
  };

  useEffect(() => {
    const onMouseMove = (e: MouseEvent) => {
      const drag = dragState.current;
      const canvasRect = canvasRef.current?.getBoundingClientRect();
      if (!drag || !canvasRect) return;
      const x = Math.max(0, e.clientX - canvasRect.left - drag.offsetX);
      const y = Math.max(0, e.clientY - canvasRect.top - drag.offsetY);
      setDefinition((prev) => ({
        ...prev,
        nodes: prev.nodes.map((n) => (n.id === drag.nodeId ? { ...n, x, y } : n)),
      }));
    };
    const onMouseUp = () => {
      dragState.current = null;
    };
    window.addEventListener("mousemove", onMouseMove);
    window.addEventListener("mouseup", onMouseUp);
    return () => {
      window.removeEventListener("mousemove", onMouseMove);
      window.removeEventListener("mouseup", onMouseUp);
    };
  }, []);

  const save = async () => {
    if (!workflowId) return;
    setSaving(true);
    setSaveError(null);
    try {
      await api.updateWorkflow(workflowId, {
        name,
        description: description || undefined,
        definitionJson: JSON.stringify(definition),
        isEnabled,
      });
      setWorkflow((w) => (w ? { ...w, name, description, isEnabled, definitionJson: JSON.stringify(definition) } : w));
    } catch (err) {
      setSaveError(err instanceof Error ? err.message : String(err));
    } finally {
      setSaving(false);
    }
  };

  const runNow = async () => {
    if (!workflowId) return;
    setRunning(true);
    try {
      await api.runWorkflow(workflowId, runPayload);
      refreshRuns(workflowId);
    } finally {
      setRunning(false);
    }
  };

  const remove = async () => {
    if (!workflowId || !confirm(`Delete workflow "${name}"?`)) return;
    await api.deleteWorkflow(workflowId);
    navigate("/automations");
  };

  if (!workflow) {
    return <p className="p-8 text-sm text-muted-foreground">Loading...</p>;
  }

  const NODE_WIDTH = 160;

  return (
    <div className="flex h-[calc(100vh-3.5rem)] flex-col">
      <div className="flex items-center justify-between gap-4 border-b px-6 py-3">
        <div className="flex items-center gap-3">
          <Input value={name} onChange={(e) => setName(e.target.value)} className="w-64 font-semibold" />
          <label className="flex items-center gap-2 text-sm">
            <input type="checkbox" checked={isEnabled} onChange={(e) => setIsEnabled(e.target.checked)} />
            Enabled
          </label>
        </div>
        <div className="flex items-center gap-2">
          {saveError && <span className="text-sm text-destructive">{saveError}</span>}
          <Button variant="outline" size="sm" onClick={remove}>
            Delete
          </Button>
          <Button size="sm" onClick={save} disabled={saving}>
            {saving ? "Saving..." : "Save"}
          </Button>
        </div>
      </div>

      <div className="flex flex-1 overflow-hidden">
        <div className="w-56 overflow-y-auto border-r p-3">
          <h2 className="mb-2 text-xs font-semibold uppercase text-muted-foreground">Triggers</h2>
          <div className="flex flex-col gap-2">
            {nodeTypes.filter((t) => t.category === "trigger").map((t) => (
              <button
                key={t.type}
                onClick={() => addNode(t)}
                className="rounded-md border bg-card p-2 text-left text-sm hover:bg-accent"
                title={t.description}
              >
                {t.label}
              </button>
            ))}
          </div>
          <h2 className="mb-2 mt-4 text-xs font-semibold uppercase text-muted-foreground">Actions</h2>
          <div className="flex flex-col gap-2">
            {nodeTypes.filter((t) => t.category === "action").map((t) => (
              <button
                key={t.type}
                onClick={() => addNode(t)}
                className="rounded-md border bg-card p-2 text-left text-sm hover:bg-accent"
                title={t.description}
              >
                {t.label}
              </button>
            ))}
          </div>
        </div>

        <div
          ref={canvasRef}
          className="relative flex-1 overflow-auto bg-muted/30"
          style={{ backgroundImage: "radial-gradient(circle, hsl(var(--border)) 1px, transparent 1px)", backgroundSize: "16px 16px" }}
        >
          <svg className="pointer-events-none absolute inset-0 h-full w-full">
            {definition.edges.map((edge) => {
              const from = definition.nodes.find((n) => n.id === edge.from);
              const to = definition.nodes.find((n) => n.id === edge.to);
              if (!from || !to) return null;
              const x1 = from.x + NODE_WIDTH;
              const y1 = from.y + 20;
              const x2 = to.x;
              const y2 = to.y + 20;
              return (
                <line
                  key={`${edge.from}-${edge.to}`}
                  x1={x1}
                  y1={y1}
                  x2={x2}
                  y2={y2}
                  stroke="hsl(var(--muted-foreground))"
                  strokeWidth={2}
                  markerEnd="url(#arrow)"
                />
              );
            })}
            <defs>
              <marker id="arrow" markerWidth="8" markerHeight="8" refX="6" refY="4" orient="auto">
                <path d="M0,0 L8,4 L0,8 Z" fill="hsl(var(--muted-foreground))" />
              </marker>
            </defs>
          </svg>

          {definition.nodes.map((node) => {
            const type = nodeTypes.find((t) => t.type === node.type);
            return (
              <div
                key={node.id}
                onMouseDown={(e) => onNodeMouseDown(e, node)}
                onClick={() => handleNodeClick(node.id)}
                className={`absolute cursor-move select-none rounded-md border bg-card p-2 text-xs shadow-sm ${
                  selectedNodeId === node.id ? "ring-2 ring-primary" : ""
                } ${connectingFrom === node.id ? "ring-2 ring-emerald-500" : ""}`}
                style={{ left: node.x, top: node.y, width: NODE_WIDTH }}
              >
                <Badge variant={type?.category === "trigger" ? "default" : "secondary"} className="mb-1">
                  {type?.category ?? node.type}
                </Badge>
                <div className="font-medium">{type?.label ?? node.type}</div>
                <div className="mt-1 flex gap-1">
                  <button
                    className="rounded border px-1 text-[10px] hover:bg-accent"
                    onClick={(e) => {
                      e.stopPropagation();
                      setConnectingFrom(connectingFrom === node.id ? null : node.id);
                    }}
                  >
                    {connectingFrom === node.id ? "Cancel" : "Connect →"}
                  </button>
                  <button
                    className="rounded border px-1 text-[10px] text-destructive hover:bg-accent"
                    onClick={(e) => {
                      e.stopPropagation();
                      removeNode(node.id);
                    }}
                  >
                    Delete
                  </button>
                </div>
              </div>
            );
          })}

          {definition.nodes.length === 0 && (
            <p className="p-8 text-sm text-muted-foreground">
              Add a trigger and some actions from the sidebar, then click "Connect" on a node and
              click another node to link them.
            </p>
          )}
        </div>

        <div className="w-80 overflow-y-auto border-l p-4">
          {selectedNode ? (
            <div className="flex flex-col gap-3">
              <div className="flex items-center justify-between">
                <h2 className="text-sm font-semibold">{selectedNodeType?.label ?? selectedNode.type}</h2>
                <span className="text-xs text-muted-foreground">{selectedNode.id}</span>
              </div>
              {selectedNodeType?.description && (
                <p className="text-xs text-muted-foreground">{selectedNodeType.description}</p>
              )}
              {selectedNodeType?.fields.map((field) => (
                <div key={field.name} className="flex flex-col gap-1.5">
                  <Label htmlFor={field.name}>{field.label}</Label>
                  {field.fieldType === "textarea" || field.fieldType === "code" ? (
                    <Textarea
                      id={field.name}
                      rows={field.fieldType === "code" ? 8 : 3}
                      className="font-mono text-xs"
                      value={selectedNode.config[field.name] ?? ""}
                      onChange={(e) => updateNodeConfig(selectedNode.id, field.name, e.target.value)}
                    />
                  ) : field.fieldType.startsWith("select:") ? (
                    <select
                      id={field.name}
                      className="h-9 rounded-md border bg-background px-3 text-sm"
                      value={selectedNode.config[field.name] ?? ""}
                      onChange={(e) => updateNodeConfig(selectedNode.id, field.name, e.target.value)}
                    >
                      <option value="" disabled>
                        Select...
                      </option>
                      {field.fieldType
                        .slice("select:".length)
                        .split(",")
                        .map((opt) => (
                          <option key={opt} value={opt}>
                            {opt}
                          </option>
                        ))}
                    </select>
                  ) : (
                    <Input
                      id={field.name}
                      value={selectedNode.config[field.name] ?? ""}
                      onChange={(e) => updateNodeConfig(selectedNode.id, field.name, e.target.value)}
                    />
                  )}
                </div>
              ))}
              {selectedNodeType && selectedNodeType.fields.length === 0 && (
                <p className="text-xs text-muted-foreground">This node has no configuration.</p>
              )}

              <h3 className="mt-2 text-xs font-semibold uppercase text-muted-foreground">Edges</h3>
              {definition.edges
                .filter((e) => e.from === selectedNode.id || e.to === selectedNode.id)
                .map((e) => (
                  <div key={`${e.from}-${e.to}`} className="flex items-center justify-between text-xs">
                    <span>
                      {e.from} → {e.to}
                    </span>
                    <button className="text-destructive hover:underline" onClick={() => removeEdge(e.from, e.to)}>
                      remove
                    </button>
                  </div>
                ))}
            </div>
          ) : (
            <div className="flex flex-col gap-3">
              <div className="flex flex-col gap-1.5">
                <Label htmlFor="description">Description</Label>
                <Textarea id="description" rows={3} value={description} onChange={(e) => setDescription(e.target.value)} />
              </div>
              <p className="text-xs text-muted-foreground">Select a node to edit its configuration.</p>
            </div>
          )}

          <div className="mt-6 flex flex-col gap-2 border-t pt-4">
            <Label htmlFor="runPayload">Test trigger payload (JSON)</Label>
            <Textarea id="runPayload" rows={3} className="font-mono text-xs" value={runPayload} onChange={(e) => setRunPayload(e.target.value)} />
            <Button size="sm" onClick={runNow} disabled={running}>
              {running ? "Running..." : "Run now"}
            </Button>
          </div>

          <div className="mt-6 flex flex-col gap-2 border-t pt-4">
            <h3 className="text-xs font-semibold uppercase text-muted-foreground">Run history</h3>
            {runs.length === 0 ? (
              <p className="text-xs text-muted-foreground">No runs yet.</p>
            ) : (
              runs.map((run) => (
                <div key={run.id} className="rounded-md border p-2 text-xs">
                  <div className="flex items-center justify-between">
                    <Badge variant={run.status === "Succeeded" ? "success" : run.status === "Failed" ? "destructive" : "secondary"}>
                      {run.status}
                    </Badge>
                    <span className="text-muted-foreground">{new Date(run.startedAt).toLocaleString()}</span>
                  </div>
                  {run.errorMessage && <p className="mt-1 text-destructive">{run.errorMessage}</p>}
                </div>
              ))
            )}
          </div>
        </div>
      </div>
    </div>
  );
}
