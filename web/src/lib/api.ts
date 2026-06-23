import type { AgentTask, Approval, Goal, Project, TaskEventDto } from "@/types/api";

export const API_BASE_URL = import.meta.env.VITE_API_BASE_URL ?? "http://localhost:5139";

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  const res = await fetch(`${API_BASE_URL}${path}`, {
    headers: { "Content-Type": "application/json" },
    ...init,
  });
  if (!res.ok) {
    const text = await res.text();
    throw new Error(`${res.status} ${res.statusText}: ${text}`);
  }
  if (res.status === 204) {
    return undefined as T;
  }
  return (await res.json()) as T;
}

export interface CreateProjectInput {
  name: string;
  repoPath: string;
  gitRemote?: string;
  workerModel: string;
  supervisorModel: string;
  maxWorkerConcurrency?: number;
  priority?: number;
}

export interface CreateGoalInput {
  description: string;
  title?: string;
}

export interface ResolveApprovalInput {
  answer?: string;
  resolvedBy?: string;
}

export const api = {
  listProjects: () => request<Project[]>("/api/projects"),
  getProject: (id: string) => request<Project>(`/api/projects/${id}`),
  createProject: (body: CreateProjectInput) =>
    request<Project>("/api/projects", { method: "POST", body: JSON.stringify(body) }),

  listGoals: (projectId: string) => request<Goal[]>(`/api/projects/${projectId}/goals`),
  createGoal: (projectId: string, body: CreateGoalInput) =>
    request<Goal>(`/api/projects/${projectId}/goals`, {
      method: "POST",
      body: JSON.stringify(body),
    }),

  listTasksForProject: (projectId: string) =>
    request<AgentTask[]>(`/api/projects/${projectId}/tasks`),
  getTask: (id: string) => request<AgentTask>(`/api/tasks/${id}`),
  getRunEvents: (runId: string, sinceId = 0) =>
    request<TaskEventDto[]>(`/api/runs/${runId}/events?sinceId=${sinceId}`),

  listPendingApprovals: () => request<Approval[]>("/api/approvals/pending"),
  approve: (id: string, body: ResolveApprovalInput) =>
    request<void>(`/api/approvals/${id}/approve`, { method: "POST", body: JSON.stringify(body) }),
  reject: (id: string, body: ResolveApprovalInput) =>
    request<void>(`/api/approvals/${id}/reject`, { method: "POST", body: JSON.stringify(body) }),

  cancelTask: (id: string) => request<void>(`/api/tasks/${id}/cancel`, { method: "POST" }),

  getHealth: () => request<{ status: string; database: boolean }>("/api/health"),
};
