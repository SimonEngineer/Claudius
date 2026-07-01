import { apiClient } from "./client";
import type {
  NodeRun,
  RateLimitPolicy,
  ScrapedItem,
  ScrapeRun,
  ScrapingProject,
  UpsertRateLimitPolicyRequest,
  UpsertScrapingProjectRequest,
  UpsertWorkflowRequest,
  Workflow,
  WorkflowRun,
  WorkflowRunDetail,
} from "../types";

export const ScrapingProjectsApi = {
  list: () => apiClient.get<ScrapingProject[]>("/api/scraping-projects").then((r) => r.data),
  get: (id: string) => apiClient.get<ScrapingProject>(`/api/scraping-projects/${id}`).then((r) => r.data),
  create: (body: UpsertScrapingProjectRequest) =>
    apiClient.post<ScrapingProject>("/api/scraping-projects", body).then((r) => r.data),
  update: (id: string, body: UpsertScrapingProjectRequest) =>
    apiClient.put<ScrapingProject>(`/api/scraping-projects/${id}`, body).then((r) => r.data),
  remove: (id: string) => apiClient.delete(`/api/scraping-projects/${id}`),
  run: (id: string) => apiClient.post<{ jobId: string }>(`/api/scraping-projects/${id}/run`).then((r) => r.data),
  runs: (id: string) => apiClient.get<ScrapeRun[]>(`/api/scraping-projects/${id}/runs`).then((r) => r.data),
  items: (id: string, runId?: string) =>
    apiClient
      .get<ScrapedItem[]>(`/api/scraping-projects/${id}/items`, { params: runId ? { runId } : {} })
      .then((r) => r.data),
};

export const RateLimitPoliciesApi = {
  list: () => apiClient.get<RateLimitPolicy[]>("/api/rate-limit-policies").then((r) => r.data),
  create: (body: UpsertRateLimitPolicyRequest) =>
    apiClient.post<RateLimitPolicy>("/api/rate-limit-policies", body).then((r) => r.data),
  update: (id: string, body: UpsertRateLimitPolicyRequest) =>
    apiClient.put<RateLimitPolicy>(`/api/rate-limit-policies/${id}`, body).then((r) => r.data),
  remove: (id: string) => apiClient.delete(`/api/rate-limit-policies/${id}`),
};

export const WorkflowsApi = {
  list: () => apiClient.get<Workflow[]>("/api/workflows").then((r) => r.data),
  get: (id: string) => apiClient.get<Workflow>(`/api/workflows/${id}`).then((r) => r.data),
  create: (body: UpsertWorkflowRequest) => apiClient.post<Workflow>("/api/workflows", body).then((r) => r.data),
  update: (id: string, body: UpsertWorkflowRequest) =>
    apiClient.put<Workflow>(`/api/workflows/${id}`, body).then((r) => r.data),
  remove: (id: string) => apiClient.delete(`/api/workflows/${id}`),
  runFromNode: (workflowId: string, nodeId: string, payload: unknown = null) =>
    apiClient
      .post<{ runId: string }>(`/api/workflows/${workflowId}/nodes/${nodeId}/run`, payload)
      .then((r) => r.data),
  runs: (id: string) => apiClient.get<WorkflowRun[]>(`/api/workflows/${id}/runs`).then((r) => r.data),
  runDetail: (runId: string) =>
    apiClient.get<WorkflowRunDetail>(`/api/workflows/runs/${runId}`).then((r) => r.data),
  nodeTypes: () => apiClient.get<string[]>("/api/node-types").then((r) => r.data),
};

export type { NodeRun };

export const proxyUrl = (targetUrl: string) => {
  const base = apiClient.defaults.baseURL ?? "";
  return `${base}/api/proxy?url=${encodeURIComponent(targetUrl)}`;
};
