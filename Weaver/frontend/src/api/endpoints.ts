import { apiClient } from "./client";
import { getStoredToken } from "../auth/tokenStorage";
import type {
  FieldSelector,
  NodeRun,
  PagedResult,
  RateLimitPolicy,
  RenderMode,
  ScrapedItem,
  ScrapeMode,
  ScrapeRun,
  ScrapingProject,
  TestExtractionResult,
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
  duplicate: (id: string) => apiClient.post<ScrapingProject>(`/api/scraping-projects/${id}/duplicate`).then((r) => r.data),
  run: (id: string) => apiClient.post<{ jobId: string }>(`/api/scraping-projects/${id}/run`).then((r) => r.data),
  runs: (id: string) => apiClient.get<ScrapeRun[]>(`/api/scraping-projects/${id}/runs`).then((r) => r.data),
  items: (id: string, page = 1, pageSize = 50, runId?: string) =>
    apiClient
      .get<PagedResult<ScrapedItem>>(`/api/scraping-projects/${id}/items`, {
        params: { page, pageSize, ...(runId ? { runId } : {}) },
      })
      .then((r) => r.data),
  exportItems: async (id: string, format: "csv" | "json", runId?: string) => {
    const response = await apiClient.get(`/api/scraping-projects/${id}/items/export`, {
      params: { format, ...(runId ? { runId } : {}) },
      responseType: "blob",
    });
    const disposition = response.headers["content-disposition"] as string | undefined;
    const match = disposition?.match(/filename="?([^"]+)"?/);
    const filename = match?.[1] ?? `items.${format}`;
    const url = URL.createObjectURL(response.data as Blob);
    const link = document.createElement("a");
    link.href = url;
    link.download = filename;
    link.click();
    URL.revokeObjectURL(url);
  },
  testExtract: (body: {
    url: string;
    mode: ScrapeMode;
    itemSelector: string | null;
    fields: FieldSelector[];
    rateLimitPolicyId: string | null;
    scrapingProjectId: string | null;
    renderMode: RenderMode;
    customHeaders: Record<string, string>;
  }) => apiClient.post<TestExtractionResult>("/api/scraping-projects/test-extract", body).then((r) => r.data),
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
  duplicate: (id: string) => apiClient.post<Workflow>(`/api/workflows/${id}/duplicate`).then((r) => r.data),
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

export const proxyUrl = (
  targetUrl: string,
  rateLimitPolicyId?: string | null,
  scrapingProjectId?: string,
  renderMode?: RenderMode,
) => {
  const base = apiClient.defaults.baseURL ?? "";
  const params = new URLSearchParams({ url: targetUrl });
  if (rateLimitPolicyId) params.set("rateLimitPolicyId", rateLimitPolicyId);
  if (scrapingProjectId) params.set("scrapingProjectId", scrapingProjectId);
  if (renderMode) params.set("renderMode", renderMode);
  // The iframe's `src` GET can't carry an Authorization header, so the proxy endpoint alone
  // also accepts the token via this query param (see Program.cs's JwtBearerEvents).
  const token = getStoredToken();
  if (token) params.set("access_token", token);
  return `${base}/api/proxy?${params.toString()}`;
};
