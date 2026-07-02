import { apiClient } from "./client";
import { getStoredToken } from "../auth/tokenStorage";
import type {
  ApiKey,
  AuditLogEntry,
  CreatedApiKey,
  DashboardStats,
  ProxyConfig,
  FieldSelector,
  ItemSnapshot,
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

async function downloadFromApi(url: string, params: Record<string, string>, fallbackFilename: string) {
  const response = await apiClient.get(url, { params, responseType: "blob" });
  const disposition = response.headers["content-disposition"] as string | undefined;
  const match = disposition?.match(/filename="?([^"]+)"?/);
  const filename = match?.[1] ?? fallbackFilename;
  const blobUrl = URL.createObjectURL(response.data as Blob);
  const link = document.createElement("a");
  link.href = blobUrl;
  link.download = filename;
  link.click();
  URL.revokeObjectURL(blobUrl);
}

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
  exportItems: (id: string, format: "csv" | "json", runId?: string) =>
    downloadFromApi(`/api/scraping-projects/${id}/items/export`, { format, ...(runId ? { runId } : {}) }, `items.${format}`),
  searchItems: (id: string, q: string, page = 1, pageSize = 50) =>
    apiClient
      .get<PagedResult<ScrapedItem>>(`/api/scraping-projects/${id}/items/search`, { params: { q, page, pageSize } })
      .then((r) => r.data),
  itemHistory: (id: string, itemKey: string) =>
    apiClient.get<ItemSnapshot[]>(`/api/scraping-projects/${id}/items/history/${encodeURIComponent(itemKey)}`).then((r) => r.data),
  exportRuns: (id: string, format: "csv" | "json") =>
    downloadFromApi(`/api/scraping-projects/${id}/runs/export`, { format }, `runs.${format}`),
  testExtract: (body: {
    url: string;
    mode: ScrapeMode;
    itemSelector: string | null;
    fields: FieldSelector[];
    rateLimitPolicyId: string | null;
    scrapingProjectId: string | null;
    renderMode: RenderMode;
    customHeaders: Record<string, string>;
    proxy?: ProxyConfig;
  }) => apiClient.post<TestExtractionResult>("/api/scraping-projects/test-extract", body).then((r) => r.data),
};

export const AuthApi = {
  changePassword: (currentPassword: string, newPassword: string) =>
    apiClient.post("/api/auth/change-password", { currentPassword, newPassword }),
};

export const ApiKeysApi = {
  list: () => apiClient.get<ApiKey[]>("/api/api-keys").then((r) => r.data),
  create: (name: string, expiresAt: string | null) =>
    apiClient.post<CreatedApiKey>("/api/api-keys", { name, expiresAt }).then((r) => r.data),
  remove: (id: string) => apiClient.delete(`/api/api-keys/${id}`),
};

export const AuditLogApi = {
  list: (page = 1, pageSize = 50) =>
    apiClient.get<PagedResult<AuditLogEntry>>("/api/audit-log", { params: { page, pageSize } }).then((r) => r.data),
};

export const DashboardApi = {
  stats: () => apiClient.get<DashboardStats>("/api/dashboard/stats").then((r) => r.data),
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
  exportWorkflow: (id: string, name: string) =>
    downloadFromApi(`/api/workflows/${id}/export`, {}, `${name}.weaver-workflow.json`),
  importWorkflow: (fileContents: string) =>
    apiClient.post<Workflow>("/api/workflows/import", JSON.parse(fileContents)).then((r) => r.data),
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
