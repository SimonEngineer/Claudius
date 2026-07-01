import { Handle, Position, type NodeProps } from "reactflow";

export interface WeaverNodeData {
  label: string;
  nodeType: string;
  config: Record<string, unknown>;
  maxRetries: number;
  retryDelayMs: number;
  onRun?: () => void;
  lastStatus?: string;
}

const CATEGORY_COLORS: Record<string, string> = {
  trigger: "#f59e0b",
  condition: "#a855f7",
  action: "#6366f1",
};

function categoryOf(nodeType: string) {
  if (nodeType.startsWith("trigger.")) return "trigger";
  if (nodeType === "condition") return "condition";
  return "action";
}

export default function WeaverFlowNode({ data }: NodeProps<WeaverNodeData>) {
  const category = categoryOf(data.nodeType);
  const color = CATEGORY_COLORS[category];
  const isCondition = data.nodeType === "condition";
  const isTrigger = category === "trigger";
  const isAction = category === "action";

  return (
    <div className="rf-node" style={{ borderColor: color }}>
      {!isTrigger && <Handle type="target" position={Position.Left} />}
      <div className="rf-node-header" style={{ background: `${color}22`, color }}>
        {data.label}
        {isTrigger && data.onRun && (
          <button
            style={{ float: "right", padding: "1px 8px", fontSize: 11 }}
            onClick={(e) => {
              e.stopPropagation();
              data.onRun?.();
            }}
          >
            ▶ Run
          </button>
        )}
      </div>
      <div className="rf-node-body">
        {data.nodeType}
        {data.maxRetries > 0 && <span title="Retries on failure"> · ↻{data.maxRetries}</span>}
      </div>

      {isCondition ? (
        <>
          <Handle type="source" position={Position.Right} id="true" style={{ top: "35%", background: "#22c55e" }} />
          <Handle type="source" position={Position.Right} id="false" style={{ top: "65%", background: "#ef4444" }} />
        </>
      ) : (
        <Handle type="source" position={Position.Right} />
      )}
      {isAction && <Handle type="source" position={Position.Bottom} id="error" style={{ background: "#ef4444" }} />}
    </div>
  );
}

export const nodeTypes = { weaver: WeaverFlowNode };
