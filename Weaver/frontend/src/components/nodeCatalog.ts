export interface NodeCatalogEntry {
  type: string;
  label: string;
  description: string;
  defaultConfig: Record<string, unknown>;
}

export const NODE_CATALOG: NodeCatalogEntry[] = [
  { type: "trigger.manual", label: "Manual Trigger", description: "Fire from the Run button", defaultConfig: {} },
  {
    type: "trigger.cron",
    label: "Cron Timer",
    description: "Fire on a schedule",
    defaultConfig: { cronExpression: "*/5 * * * *" },
  },
  {
    type: "trigger.http",
    label: "HTTP Trigger",
    description: "Fire on an inbound webhook",
    defaultConfig: { secret: "" },
  },
  {
    type: "trigger.event",
    label: "Event Trigger",
    description: "Fire on a named system event",
    defaultConfig: { eventName: "scrape.item.changed" },
  },
  {
    type: "condition",
    label: "Condition",
    description: "Branch true/false on a field",
    defaultConfig: { field: "", operator: "equals", value: "" },
  },
  {
    type: "action.scrape",
    label: "Run Scraping Project",
    description: "Execute a scraping project",
    defaultConfig: { scrapingProjectId: "" },
  },
  {
    type: "action.sendEmail",
    label: "Send Email",
    description: "Send an SMTP email",
    defaultConfig: { to: "", subject: "", body: "" },
  },
  {
    type: "action.sendDiscord",
    label: "Send Discord Message",
    description: "Post to a Discord webhook",
    defaultConfig: { webhookUrl: "", message: "" },
  },
  {
    type: "action.sendSlack",
    label: "Send Slack Message",
    description: "Post to a Slack incoming webhook",
    defaultConfig: { webhookUrl: "", message: "" },
  },
  {
    type: "action.code",
    label: "Code Block",
    description: "Run a C# script",
    defaultConfig: { code: "// Data, Db, Log, PublishEventAsync are available\nreturn Data;" },
  },
  {
    type: "action.fileLogger",
    label: "File Logger",
    description: "Append to a csv/json/txt file",
    defaultConfig: { filePath: "", format: "json" },
  },
  {
    type: "action.httpRequest",
    label: "HTTP Request",
    description: "Call any HTTP API",
    defaultConfig: { method: "GET", url: "", headers: {}, body: "", timeoutSeconds: 30 },
  },
  {
    type: "action.delay",
    label: "Delay",
    description: "Pause the run for N seconds",
    defaultConfig: { seconds: 5 },
  },
  {
    type: "action.splitIntoBatches",
    label: "Split into Batches",
    description: "Chunk an array into fixed-size batches",
    defaultConfig: { arrayPath: "", batchSize: 10 },
  },
];

export const catalogEntry = (type: string) => NODE_CATALOG.find((n) => n.type === type);
