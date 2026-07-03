export type ScrapeMode = "SingleItem" | "List";
export type RenderMode = "Http" | "Playwright";
export type FieldAttribute = "Text" | "Html" | "Href" | "Src" | "Attribute";
export type PaginationStrategy = "None" | "NextLinkSelector" | "UrlPattern";
export type RateLimitKeyScope = "PerHost" | "PerUrl" | "PerProject" | "Custom";
export type RunStatus = "Pending" | "Running" | "Succeeded" | "Failed" | "Cancelled" | "Skipped";
export type TriggerKind = "Manual" | "Schedule" | "Http" | "Event" | "Code";
export type AuditAction = "Created" | "Updated" | "Deleted";

export interface ProxyConfig {
  enabled: boolean;
  protocol: "http" | "socks5";
  host: string;
  port: number;
  username: string | null;
  password: string | null;
}

export const emptyProxyConfig: ProxyConfig = { enabled: false, protocol: "http", host: "", port: 0, username: "", password: "" };

export interface FieldSelector {
  id: string | null;
  name: string;
  selector: string;
  attribute: FieldAttribute;
  attributeName: string | null;
  resolveUrl: boolean;
  isKey: boolean;
  required: boolean;
  order: number;
}

export interface ScrapingProject {
  id: string;
  name: string;
  description: string | null;
  startUrl: string;
  mode: ScrapeMode;
  renderMode: RenderMode;
  itemSelector: string | null;
  paginationStrategy: PaginationStrategy;
  nextPageSelector: string | null;
  pageUrlTemplate: string | null;
  maxPages: number;
  customHeaders: Record<string, string>;
  proxy: ProxyConfig;
  scheduleCron: string | null;
  startUrls: string[];
  respectRobotsTxt: boolean;
  dataRetentionDays: number | null;
  rateLimitPolicyId: string | null;
  isEnabled: boolean;
  createdAt: string;
  updatedAt: string;
  fields: FieldSelector[];
  lastRunStatus: RunStatus | null;
  lastRunAt: string | null;
}

export type UpsertScrapingProjectRequest = Omit<ScrapingProject, "id" | "createdAt" | "updatedAt" | "lastRunStatus" | "lastRunAt">;

export interface ScrapeRun {
  id: string;
  scrapingProjectId: string;
  status: RunStatus;
  triggeredBy: TriggerKind;
  pagesCrawled: number;
  itemsFound: number;
  itemsChanged: number;
  errorMessage: string | null;
  createdAt: string;
  startedAt: string | null;
  completedAt: string | null;
}

export interface ScrapedItem {
  id: string;
  sourceUrl: string;
  itemKey: string;
  data: Record<string, string | null>;
  createdAt: string;
}

export interface PagedResult<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export interface FieldDiff {
  before: string | null;
  after: string | null;
}

export interface ItemSnapshot {
  id: string;
  createdAt: string;
  data: Record<string, string | null>;
  changes: Record<string, FieldDiff> | null;
}

export interface TestExtractionResult {
  itemsFound: number;
  items: Record<string, string | null>[];
  errorMessage: string | null;
}

export interface RateLimitPolicy {
  id: string;
  name: string;
  keyScope: RateLimitKeyScope;
  customKeyTemplate: string | null;
  permitLimit: number;
  windowSeconds: number;
  burstCapacity: number;
}

export type UpsertRateLimitPolicyRequest = Omit<RateLimitPolicy, "id">;

export interface ApiKey {
  id: string;
  name: string;
  keyPrefix: string;
  createdAt: string;
  lastUsedAt: string | null;
  expiresAt: string | null;
}

export interface CreatedApiKey extends ApiKey {
  key: string;
}

export interface AuditLogEntry {
  id: string;
  action: AuditAction;
  resourceType: string;
  resourceId: string;
  resourceName: string;
  createdAt: string;
}

export interface Credential {
  id: string;
  name: string;
  createdAt: string;
  updatedAt: string;
}

export interface NotificationSettings {
  notifyOnScrapeFailure: boolean;
  notifyOnWorkflowFailure: boolean;
  emailEnabled: boolean;
  webhookUrl: string | null;
}

export interface WorkflowRevision {
  id: string;
  workflowName: string;
  nodeCount: number;
  createdAt: string;
}

export interface CronPreview {
  valid: boolean;
  error: string | null;
  nextOccurrences: string[];
}

export interface DashboardStats {
  scrapesSucceededToday: number;
  scrapesFailedToday: number;
  workflowRunsSucceededToday: number;
  workflowRunsFailedToday: number;
  recentActivity: AuditLogEntry[];
}

export interface WorkflowNode {
  id: string;
  type: string;
  name: string;
  config: Record<string, unknown> | null;
  isDisabled: boolean;
  maxRetries: number;
  retryDelayMs: number;
  positionX: number;
  positionY: number;
}

export interface WorkflowEdge {
  id: string;
  sourceNodeId: string;
  sourceHandle: string | null;
  targetNodeId: string;
  targetHandle: string | null;
}

export interface Workflow {
  id: string;
  name: string;
  description: string | null;
  isEnabled: boolean;
  runRetentionDays: number | null;
  updatedAt: string;
  nodes: WorkflowNode[];
  edges: WorkflowEdge[];
  lastRunStatus: RunStatus | null;
  lastRunAt: string | null;
}

export type UpsertWorkflowRequest = Omit<Workflow, "id" | "updatedAt" | "lastRunStatus" | "lastRunAt">;

export interface WorkflowRun {
  id: string;
  workflowId: string;
  status: RunStatus;
  triggerKind: TriggerKind;
  triggerNodeType: string | null;
  errorMessage: string | null;
  createdAt: string;
  startedAt: string | null;
  completedAt: string | null;
}

export interface NodeRun {
  id: string;
  workflowNodeId: string;
  nodeType: string;
  status: RunStatus;
  input: unknown;
  output: unknown;
  errorMessage: string | null;
  logText: string | null;
  startedAt: string | null;
  completedAt: string | null;
}

export interface WorkflowRunDetail {
  run: WorkflowRun;
  nodeRuns: NodeRun[];
}
