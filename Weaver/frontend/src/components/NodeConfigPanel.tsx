import { useQuery } from "@tanstack/react-query";
import { ScrapingProjectsApi } from "../api/endpoints";
import { apiBaseUrl } from "../api/client";

interface Props {
  workflowId?: string;
  nodeId: string;
  nodeType: string;
  name: string;
  config: Record<string, unknown>;
  maxRetries: number;
  retryDelayMs: number;
  onChange: (patch: { name?: string; config?: Record<string, unknown>; maxRetries?: number; retryDelayMs?: number }) => void;
  onDelete: () => void;
  onClose: () => void;
}

function TextField({ label, value, onChange, placeholder, mono }: {
  label: string;
  value: string;
  onChange: (v: string) => void;
  placeholder?: string;
  mono?: boolean;
}) {
  return (
    <div className="field">
      <label>{label}</label>
      <input className={mono ? "mono" : ""} value={value} placeholder={placeholder} onChange={(e) => onChange(e.target.value)} />
    </div>
  );
}

function TextAreaField({ label, value, onChange, hint }: { label: string; value: string; onChange: (v: string) => void; hint?: string }) {
  return (
    <div className="field">
      <label>{label}</label>
      <textarea className="mono" rows={6} value={value} onChange={(e) => onChange(e.target.value)} />
      {hint && <p className="muted">{hint}</p>}
    </div>
  );
}

export default function NodeConfigPanel({ workflowId, nodeId, nodeType, name, config, maxRetries, retryDelayMs, onChange, onDelete, onClose }: Props) {
  const setConfig = (patch: Record<string, unknown>) => onChange({ config: { ...config, ...patch } });
  const scrapingProjects = useQuery({ queryKey: ["scraping-projects"], queryFn: ScrapingProjectsApi.list, enabled: nodeType === "action.scrape" });
  const isTrigger = nodeType.startsWith("trigger.");

  return (
    <div className="card" style={{ position: "sticky", top: 0 }}>
      <div className="page-header">
        <h3 style={{ margin: 0, fontSize: 14 }}>{nodeType}</h3>
        <button className="close-btn" onClick={onClose}>×</button>
      </div>

      <TextField label="Node name" value={name} onChange={(v) => onChange({ name: v })} />

      {nodeType === "trigger.cron" && (
        <TextField
          label="Cron expression"
          mono
          value={(config.cronExpression as string) ?? ""}
          onChange={(v) => setConfig({ cronExpression: v })}
          placeholder="*/5 * * * *"
        />
      )}

      {nodeType === "trigger.http" && (
        <>
          <TextField label="Secret (optional)" value={(config.secret as string) ?? ""} onChange={(v) => setConfig({ secret: v })} />
          {workflowId && (
            <div className="field">
              <label>Webhook URL</label>
              <input className="mono" readOnly value={`${apiBaseUrl}/api/webhooks/${workflowId}/${nodeId}`} />
            </div>
          )}
        </>
      )}

      {nodeType === "trigger.event" && (
        <TextField
          label="Event name"
          mono
          value={(config.eventName as string) ?? ""}
          onChange={(v) => setConfig({ eventName: v })}
          placeholder="scrape.item.changed"
        />
      )}

      {nodeType === "condition" && (
        <>
          <TextField
            label="Field (dot path, e.g. current.price or Scrape Fixture.itemsChanged)"
            mono
            value={(config.field as string) ?? ""}
            onChange={(v) => setConfig({ field: v })}
          />
          <div className="field">
            <label>Operator</label>
            <select value={(config.operator as string) ?? "equals"} onChange={(e) => setConfig({ operator: e.target.value })}>
              <option value="equals">equals</option>
              <option value="notEquals">not equals</option>
              <option value="contains">contains</option>
              <option value="greaterThan">greater than</option>
              <option value="lessThan">less than</option>
              <option value="greaterThanOrEqual">greater or equal</option>
              <option value="lessThanOrEqual">less or equal</option>
              <option value="exists">exists</option>
              <option value="notExists">does not exist</option>
            </select>
          </div>
          <TextField label="Value" value={(config.value as string) ?? ""} onChange={(v) => setConfig({ value: v })} />
        </>
      )}

      {nodeType === "action.scrape" && (
        <div className="field">
          <label>Scraping project</label>
          <select
            value={(config.scrapingProjectId as string) ?? ""}
            onChange={(e) => setConfig({ scrapingProjectId: e.target.value })}
          >
            <option value="">Select a project…</option>
            {scrapingProjects.data?.map((p) => (
              <option key={p.id} value={p.id}>
                {p.name}
              </option>
            ))}
          </select>
        </div>
      )}

      {nodeType === "action.sendEmail" && (
        <>
          <TextField
            label="To (comma-separated, supports {{dot.path}} or {{Node Name.path}})"
            mono
            value={(config.to as string) ?? ""}
            onChange={(v) => setConfig({ to: v })}
            placeholder="you@example.com, {{current.ownerEmail}}"
          />
          <TextField
            label="Subject"
            mono
            value={(config.subject as string) ?? ""}
            onChange={(v) => setConfig({ subject: v })}
            placeholder="{{title}} changed"
          />
          <TextAreaField
            label="Body"
            value={(config.body as string) ?? ""}
            onChange={(v) => setConfig({ body: v })}
            hint="Use {{dot.path}} for the upstream node's output, or {{Node Name.path}} to reach back to any earlier node."
          />
        </>
      )}

      {nodeType === "action.sendDiscord" && (
        <>
          <TextField
            label="Webhook URL"
            mono
            value={(config.webhookUrl as string) ?? ""}
            onChange={(v) => setConfig({ webhookUrl: v })}
          />
          <TextAreaField
            label="Message"
            value={(config.message as string) ?? ""}
            onChange={(v) => setConfig({ message: v })}
            hint="Use {{dot.path}} for the upstream node's output, or {{Node Name.path}} to reach back to any earlier node."
          />
        </>
      )}

      {nodeType === "action.code" && (
        <>
          <TextAreaField
            label="C# script"
            value={(config.code as string) ?? ""}
            onChange={(v) => setConfig({ code: v })}
            hint="Globals available: Data, Nodes (any earlier node's output by name), Db (read-only queries), Log(string), PublishEventAsync(name, payload). Last expression is the node's output."
          />
          <TextField
            label="Timeout (seconds)"
            value={String((config.timeoutSeconds as number) ?? 30)}
            onChange={(v) => setConfig({ timeoutSeconds: Number(v) || 30 })}
          />
        </>
      )}

      {nodeType === "action.fileLogger" && (
        <>
          <TextField label="File path" mono value={(config.filePath as string) ?? ""} onChange={(v) => setConfig({ filePath: v })} />
          <div className="field">
            <label>Format</label>
            <select value={(config.format as string) ?? "json"} onChange={(e) => setConfig({ format: e.target.value })}>
              <option value="json">JSON (newline-delimited)</option>
              <option value="csv">CSV</option>
              <option value="txt">Plain text</option>
            </select>
          </div>
        </>
      )}

      {!isTrigger && (
        <div className="row" style={{ marginTop: 8 }}>
          <div className="field">
            <label>Retries on failure</label>
            <input
              type="number"
              min={0}
              value={maxRetries}
              onChange={(e) => onChange({ maxRetries: Math.max(0, Number(e.target.value)) })}
            />
          </div>
          <div className="field">
            <label>Retry delay (ms)</label>
            <input
              type="number"
              min={0}
              value={retryDelayMs}
              onChange={(e) => onChange({ retryDelayMs: Math.max(0, Number(e.target.value)) })}
            />
          </div>
        </div>
      )}

      <button className="danger" style={{ marginTop: 8 }} onClick={onDelete}>
        Delete node
      </button>
    </div>
  );
}
