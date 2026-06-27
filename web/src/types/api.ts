export type Lane = "Supervisor" | "Worker";
export type EngineType = "ClaudeCode" | "Aider";
export type TaskState =
  | "Queued"
  | "Planning"
  | "Blocked"
  | "ReadyForWork"
  | "InProgress"
  | "Verifying"
  | "NeedsFix"
  | "AwaitingInput"
  | "Decomposed"
  | "Done"
  | "Failed"
  | "DeadLetter";
export type RunStatus = "Running" | "Succeeded" | "Failed" | "Cancelled";
export type EventType = "Token" | "ToolCall" | "FileDiff" | "Log" | "StatusChange";
export type ApprovalStatus = "Pending" | "Approved" | "Rejected";
export type ApprovalKind = "WorkerInput" | "PlanReview";
export type GoalStatus = "Active" | "Completed" | "Cancelled";

export interface Project {
  id: string;
  name: string;
  repoPath: string;
  gitRemote: string | null;
  workerModel: string;
  supervisorModel: string;
  maxWorkerConcurrency: number;
  priority: number;
  isPaused: boolean;
  requirePlanApproval: boolean;
  createdAt: string;
}

export interface Skill {
  id: string;
  projectId: string;
  name: string;
  content: string;
  createdByRunId: string | null;
  createdAt: string;
}

export interface Goal {
  id: string;
  projectId: string;
  description: string;
  status: GoalStatus;
  createdAt: string;
}

export interface Run {
  id: string;
  taskId: string;
  engine: EngineType;
  model: string;
  status: RunStatus;
  startedAt: string;
  finishedAt: string | null;
  costUsd: number | null;
  tokensIn: number | null;
  tokensOut: number | null;
  exitSummaryJson: string | null;
}

export interface Approval {
  id: string;
  taskId: string;
  runId: string | null;
  question: string;
  optionsJson: string | null;
  kind: ApprovalKind;
  status: ApprovalStatus;
  answer: string | null;
  resolvedBy: string | null;
  createdAt: string;
  resolvedAt: string | null;
}

export interface AgentTask {
  id: string;
  projectId: string;
  goalId: string | null;
  parentTaskId: string | null;
  title: string;
  description: string | null;
  lane: Lane;
  state: TaskState;
  priority: number;
  retryCount: number;
  planJson: string | null;
  acceptanceCriteriaJson: string | null;
  lockedBy: string | null;
  leaseExpiresAt: string | null;
  createdAt: string;
  updatedAt: string;
  runs: Run[];
  approvals: Approval[];
}

export interface TaskEventDto {
  id: number;
  runId: string;
  type: EventType;
  timestamp: string;
  payloadJson: string;
}

export interface LiveEvent {
  taskId: string;
  runId: string;
  id: number;
  type: EventType;
  timestamp: string;
  payload: string;
}

export interface TaskSummary {
  id: string;
  projectId: string;
  projectName: string;
  title: string;
  state: TaskState;
  lane: Lane;
  retryCount: number;
  updatedAt: string;
}

export interface ApprovalSummary {
  id: string;
  taskId: string;
  projectId: string;
  projectName: string;
  taskTitle: string;
  question: string;
  createdAt: string;
}

export interface Overview {
  projectCount: number;
  taskCountsByState: Partial<Record<TaskState, number>>;
  inProgress: TaskSummary[];
  needsAttention: TaskSummary[];
  pendingApprovals: ApprovalSummary[];
}

export interface ProjectCost {
  projectId: string;
  projectName: string;
  costUsd: number;
  tokensIn: number;
  tokensOut: number;
  runCount: number;
}

export interface DailyCost {
  date: string;
  costUsd: number;
}

export interface CostSummary {
  totalCostUsd: number;
  totalTokensIn: number;
  totalTokensOut: number;
  byProject: ProjectCost[];
  last30Days: DailyCost[];
}

export interface LaneHealth {
  queueDepth: number;
  inFlight: number;
  capacity: number;
}

export interface AuditLogEntry {
  id: string;
  projectId: string | null;
  taskId: string | null;
  action: string;
  actor: string | null;
  details: string | null;
  createdAt: string;
}

export interface SystemHealth {
  databaseHealthy: boolean;
  supervisor: LaneHealth;
  worker: LaneHealth;
  staleLeaseCount: number;
  deadLetterCount: number;
  pausedProjectCount: number;
}

export type WorkflowRunStatus = "Running" | "Succeeded" | "Failed";

export interface Workflow {
  id: string;
  name: string;
  description: string | null;
  isEnabled: boolean;
  definitionJson: string;
  createdAt: string;
  updatedAt: string;
}

export interface WorkflowRun {
  id: string;
  workflowId: string;
  status: WorkflowRunStatus;
  triggerPayloadJson: string | null;
  stepLogJson: string;
  errorMessage: string | null;
  startedAt: string;
  finishedAt: string | null;
}

export interface NodeFieldDescriptor {
  name: string;
  label: string;
  fieldType: string;
}

export interface NodeTypeDescriptor {
  type: string;
  category: "trigger" | "action";
  label: string;
  description: string;
  fields: NodeFieldDescriptor[];
}

export interface WorkflowNode {
  id: string;
  type: string;
  config: Record<string, string>;
  x: number;
  y: number;
}

export interface WorkflowEdge {
  from: string;
  to: string;
}

export interface WorkflowDefinition {
  nodes: WorkflowNode[];
  edges: WorkflowEdge[];
}
