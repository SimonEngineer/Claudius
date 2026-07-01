export type ScrapeMode = "SingleItem" | "List";
export type RenderMode = "Http" | "Playwright";
export type FieldAttribute = "Text" | "Html" | "Href" | "Src" | "Attribute";
export type PaginationStrategy = "None" | "NextLinkSelector" | "UrlPattern";
export type RateLimitKeyScope = "PerHost" | "PerUrl" | "PerProject" | "Custom";
export type RunStatus = "Pending" | "Running" | "Succeeded" | "Failed" | "Cancelled";
export type TriggerKind = "Manual" | "Schedule" | "Http" | "Event" | "Code";

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
  rateLimitPolicyId: string | null;
  isEnabled: boolean;
  createdAt: string;
  updatedAt: string;
  fields: FieldSelector[];
}

export type UpsertScrapingProjectRequest = Omit<ScrapingProject, "id" | "createdAt" | "updatedAt">;

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

export interface WorkflowNode {
  id: string;
  type: string;
  name: string;
  config: Record<string, unknown> | null;
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
  updatedAt: string;
  nodes: WorkflowNode[];
  edges: WorkflowEdge[];
}

export type UpsertWorkflowRequest = Omit<Workflow, "id" | "updatedAt">;

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
