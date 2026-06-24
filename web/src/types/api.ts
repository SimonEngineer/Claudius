export type Lane = "Supervisor" | "Worker";
export type EngineType = "ClaudeCode" | "Aider";
export type TaskState =
  | "Queued"
  | "Planning"
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
