import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useCallback, useEffect, useRef, useState } from "react";
import { useNavigate, useParams } from "react-router-dom";
import ReactFlow, {
  addEdge,
  Background,
  Controls,
  MiniMap,
  ReactFlowProvider,
  useEdgesState,
  useNodesState,
  type Connection,
  type Edge,
  type Node,
  type ReactFlowInstance,
} from "reactflow";
import "reactflow/dist/style.css";
import { WorkflowsApi } from "../api/endpoints";
import { catalogEntry, NODE_CATALOG } from "../components/nodeCatalog";
import NodeConfigPanel from "../components/NodeConfigPanel";
import WorkflowRunHistory from "../components/WorkflowRunHistory";
import { nodeTypes, type WeaverNodeData } from "../components/WeaverFlowNode";
import type { UpsertWorkflowRequest, WorkflowEdge, WorkflowNode as ApiWorkflowNode } from "../types";

function toRfNode(n: ApiWorkflowNode, onRun: (nodeId: string) => void): Node<WeaverNodeData> {
  return {
    id: n.id,
    type: "weaver",
    position: { x: n.positionX, y: n.positionY },
    data: {
      label: n.name,
      nodeType: n.type,
      config: (n.config as Record<string, unknown>) ?? {},
      onRun: n.type.startsWith("trigger.") ? () => onRun(n.id) : undefined,
    },
  };
}

function toRfEdge(e: WorkflowEdge): Edge {
  return {
    id: e.id,
    source: e.sourceNodeId,
    sourceHandle: e.sourceHandle ?? undefined,
    target: e.targetNodeId,
    targetHandle: e.targetHandle ?? undefined,
    label: e.sourceHandle ?? undefined,
  };
}

function WorkflowEditorInner() {
  const { id } = useParams();
  const isNew = !id;
  const navigate = useNavigate();
  const queryClient = useQueryClient();

  const existing = useQuery({ queryKey: ["workflow", id], queryFn: () => WorkflowsApi.get(id!), enabled: !isNew });

  const [name, setName] = useState("New Workflow");
  const [description, setDescription] = useState("");
  const [isEnabled, setIsEnabled] = useState(true);
  const [nodes, setNodes, onNodesChange] = useNodesState<WeaverNodeData>([]);
  const [edges, setEdges, onEdgesChange] = useEdgesState([]);
  const [selectedNodeId, setSelectedNodeId] = useState<string | null>(null);
  const [rfInstance, setRfInstance] = useState<ReactFlowInstance | null>(null);
  const wrapperRef = useRef<HTMLDivElement>(null);
  const workflowIdRef = useRef<string | undefined>(id);

  const runNodeMutation = useMutation({
    mutationFn: ({ nodeId }: { nodeId: string }) => WorkflowsApi.runFromNode(workflowIdRef.current!, nodeId),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ["workflow-runs", workflowIdRef.current] }),
  });

  const handleRun = useCallback(
    (nodeId: string) => {
      if (!workflowIdRef.current) {
        alert("Save the workflow before running it.");
        return;
      }
      runNodeMutation.mutate({ nodeId });
    },
    [runNodeMutation],
  );

  useEffect(() => {
    if (existing.data) {
      setName(existing.data.name);
      setDescription(existing.data.description ?? "");
      setIsEnabled(existing.data.isEnabled);
      setNodes(existing.data.nodes.map((n) => toRfNode(n, handleRun)));
      setEdges(existing.data.edges.map(toRfEdge));
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [existing.data]);

  const saveMutation = useMutation({
    mutationFn: () => {
      const body: UpsertWorkflowRequest = {
        name,
        description,
        isEnabled,
        nodes: nodes.map((n) => ({
          id: n.id,
          type: n.data.nodeType,
          name: n.data.label,
          config: n.data.config,
          positionX: n.position.x,
          positionY: n.position.y,
        })),
        edges: edges.map((e) => ({
          id: e.id,
          sourceNodeId: e.source,
          sourceHandle: e.sourceHandle ?? null,
          targetNodeId: e.target,
          targetHandle: e.targetHandle ?? null,
        })),
      };
      return isNew ? WorkflowsApi.create(body) : WorkflowsApi.update(id!, body);
    },
    onSuccess: (saved) => {
      workflowIdRef.current = saved.id;
      queryClient.invalidateQueries({ queryKey: ["workflows"] });
      if (isNew) navigate(`/workflows/${saved.id}`, { replace: true });
    },
  });

  const onConnect = useCallback(
    (connection: Connection) => setEdges((eds) => addEdge({ ...connection, id: crypto.randomUUID() }, eds)),
    [setEdges],
  );

  const onDrop = useCallback(
    (event: React.DragEvent) => {
      event.preventDefault();
      const type = event.dataTransfer.getData("application/weaver-node-type");
      const entry = catalogEntry(type);
      if (!entry || !rfInstance || !wrapperRef.current) return;

      const bounds = wrapperRef.current.getBoundingClientRect();
      const position = rfInstance.project({ x: event.clientX - bounds.left, y: event.clientY - bounds.top });
      const newNode: Node<WeaverNodeData> = {
        id: crypto.randomUUID(),
        type: "weaver",
        position,
        data: {
          label: entry.label,
          nodeType: entry.type,
          config: { ...entry.defaultConfig },
          onRun: entry.type.startsWith("trigger.") ? () => handleRun(newNode.id) : undefined,
        },
      };
      setNodes((nds) => [...nds, newNode]);
    },
    [rfInstance, setNodes, handleRun],
  );

  const selectedNode = nodes.find((n) => n.id === selectedNodeId);

  const updateSelectedNode = (patch: { name?: string; config?: Record<string, unknown> }) => {
    setNodes((nds) =>
      nds.map((n) =>
        n.id === selectedNodeId
          ? { ...n, data: { ...n.data, label: patch.name ?? n.data.label, config: patch.config ?? n.data.config } }
          : n,
      ),
    );
  };

  const deleteSelectedNode = () => {
    setNodes((nds) => nds.filter((n) => n.id !== selectedNodeId));
    setEdges((eds) => eds.filter((e) => e.source !== selectedNodeId && e.target !== selectedNodeId));
    setSelectedNodeId(null);
  };

  return (
    <div>
      <div className="page-header">
        <div className="row" style={{ maxWidth: 500 }}>
          <input value={name} onChange={(e) => setName(e.target.value)} placeholder="Workflow name" />
          <input value={description} onChange={(e) => setDescription(e.target.value)} placeholder="Description" />
        </div>
        <div style={{ display: "flex", gap: 8, alignItems: "center" }}>
          <label style={{ display: "flex", alignItems: "center", gap: 4 }}>
            <input type="checkbox" checked={isEnabled} onChange={(e) => setIsEnabled(e.target.checked)} /> enabled
          </label>
          <button className="primary" disabled={saveMutation.isPending} onClick={() => saveMutation.mutate()}>
            Save
          </button>
        </div>
      </div>

      <div style={{ display: "grid", gridTemplateColumns: "180px 1fr 320px", gap: 16, height: "calc(100vh - 140px)" }}>
        <div className="card" style={{ margin: 0, overflow: "auto" }}>
          <h3 style={{ marginTop: 0, fontSize: 13 }}>Blocks</h3>
          <div className="node-palette">
            {NODE_CATALOG.map((entry) => (
              <div
                key={entry.type}
                className="node-palette-item"
                draggable
                onDragStart={(e) => e.dataTransfer.setData("application/weaver-node-type", entry.type)}
              >
                {entry.label}
                <small>{entry.description}</small>
              </div>
            ))}
          </div>
        </div>

        <div ref={wrapperRef} style={{ border: "1px solid var(--border)", borderRadius: 10 }}>
          <ReactFlow
            nodes={nodes}
            edges={edges}
            onNodesChange={onNodesChange}
            onEdgesChange={onEdgesChange}
            onConnect={onConnect}
            nodeTypes={nodeTypes}
            onInit={setRfInstance}
            onDrop={onDrop}
            onDragOver={(e) => e.preventDefault()}
            onNodeClick={(_, node) => setSelectedNodeId(node.id)}
            onPaneClick={() => setSelectedNodeId(null)}
            fitView
          >
            <Background />
            <Controls />
            <MiniMap />
          </ReactFlow>
        </div>

        <div style={{ overflow: "auto" }}>
          {selectedNode ? (
            <NodeConfigPanel
              workflowId={workflowIdRef.current}
              nodeId={selectedNode.id}
              nodeType={selectedNode.data.nodeType}
              name={selectedNode.data.label}
              config={selectedNode.data.config}
              onChange={updateSelectedNode}
              onDelete={deleteSelectedNode}
              onClose={() => setSelectedNodeId(null)}
            />
          ) : (
            <div className="card empty-state">Drag a block onto the canvas, or click one to configure it.</div>
          )}
        </div>
      </div>

      {!isNew && <div style={{ marginTop: 16 }}><WorkflowRunHistory workflowId={id!} /></div>}
    </div>
  );
}

export default function WorkflowEditor() {
  return (
    <ReactFlowProvider>
      <WorkflowEditorInner />
    </ReactFlowProvider>
  );
}
