import { useQuery } from "@tanstack/react-query";
import { useState } from "react";
import { ScrapingProjectsApi } from "../api/endpoints";
import { apiBaseUrl } from "../api/client";

interface Props {
  workflowId?: string;
  nodeId: string;
  nodeType: string;
  name: string;
  config: Record<string, unknown>;
  isDisabled: boolean;
  maxRetries: number;
  retryDelayMs: number;
  onChange: (patch: {
    name?: string;
    config?: Record<string, unknown>;
    isDisabled?: boolean;
    maxRetries?: number;
    retryDelayMs?: number;
  }) => void;
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

export default function NodeConfigPanel({
  workflowId,
  nodeId,
  nodeType,
  name,
  config,
  isDisabled,
  maxRetries,
  retryDelayMs,
  onChange,
  onDelete,
  onClose,
}: Props) {
  const setConfig = (patch: Record<string, unknown>) => onChange({ config: { ...config, ...patch } });
  const scrapingProjects = useQuery({ queryKey: ["scraping-projects"], queryFn: ScrapingProjectsApi.list, enabled: nodeType === "action.scrape" });
  const isTrigger = nodeType.startsWith("trigger.");
  const [copied, setCopied] = useState(false);

  return (
    <div className="card" style={{ position: "sticky", top: 0 }}>
      <div className="page-header">
        <h3 style={{ margin: 0, fontSize: 14 }}>{nodeType}</h3>
        <button className="close-btn" onClick={onClose}>×</button>
      </div>

      <TextField label="Node name" value={name} onChange={(v) => onChange({ name: v })} />

      {!isTrigger && (
        <label style={{ display: "flex", alignItems: "center", gap: 6, marginBottom: 12 }}>
          <input type="checkbox" checked={!isDisabled} onChange={(e) => onChange({ isDisabled: !e.target.checked })} />
          <span className="muted">Enabled (uncheck to skip this node without deleting it)</span>
        </label>
      )}

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
          <div className="field">
            <label>
              <input
                type="checkbox"
                checked={(config.hmacSignature as boolean) ?? false}
                onChange={(e) => setConfig({ hmacSignature: e.target.checked })}
                style={{ marginRight: 6 }}
              />
              Verify via HMAC-SHA256 signature instead of a raw header
            </label>
            <p className="muted" style={{ marginTop: 4 }}>
              {config.hmacSignature
                ? <>Sender computes HMAC-SHA256 of the raw request body using Secret as the key, and sends it as <code>X-Weaver-Signature: sha256=&lt;hex&gt;</code> -- the secret itself never goes over the wire.</>
                : <>Sender sends Secret verbatim in the <code>X-Weaver-Secret</code> header.</>}
            </p>
          </div>
          {workflowId && (
            <div className="field">
              <label>Webhook URL</label>
              <div style={{ display: "flex", gap: 8 }}>
                <input className="mono" readOnly value={`${apiBaseUrl}/api/webhooks/${workflowId}/${nodeId}`} />
                <button
                  onClick={() => {
                    navigator.clipboard.writeText(`${apiBaseUrl}/api/webhooks/${workflowId}/${nodeId}`);
                    setCopied(true);
                    setTimeout(() => setCopied(false), 1500);
                  }}
                >
                  {copied ? "Copied!" : "Copy"}
                </button>
              </div>
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
              <option value="matchesRegex">matches regex</option>
              <option value="greaterThan">greater than</option>
              <option value="lessThan">less than</option>
              <option value="greaterThanOrEqual">greater or equal</option>
              <option value="lessThanOrEqual">less or equal</option>
              <option value="exists">exists</option>
              <option value="notExists">does not exist</option>
            </select>
          </div>
          <TextField
            label={(config.operator as string) === "matchesRegex" ? "Regex pattern" : "Value"}
            mono={(config.operator as string) === "matchesRegex"}
            value={(config.value as string) ?? ""}
            onChange={(v) => setConfig({ value: v })}
          />
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

      {nodeType === "action.httpRequest" && (
        <>
          <div className="row">
            <div className="field">
              <label>Method</label>
              <select value={(config.method as string) ?? "GET"} onChange={(e) => setConfig({ method: e.target.value })}>
                <option>GET</option>
                <option>POST</option>
                <option>PUT</option>
                <option>PATCH</option>
                <option>DELETE</option>
              </select>
            </div>
            <div className="field">
              <label>Timeout (seconds)</label>
              <input
                type="number"
                min={1}
                value={(config.timeoutSeconds as number) ?? 30}
                onChange={(e) => setConfig({ timeoutSeconds: Number(e.target.value) || 30 })}
              />
            </div>
          </div>

          <TextField
            label="URL"
            mono
            value={(config.url as string) ?? ""}
            onChange={(v) => setConfig({ url: v })}
            placeholder="https://api.example.com/{{Input.id}}"
          />

          <div className="field">
            <label>Headers</label>
            {Object.entries((config.headers as Record<string, string>) ?? {}).map(([key, value], i) => (
              <div key={i} style={{ display: "flex", gap: 4, marginBottom: 4 }}>
                <input
                  className="mono"
                  placeholder="Name"
                  value={key}
                  onChange={(e) => {
                    const entries = Object.entries((config.headers as Record<string, string>) ?? {});
                    entries[i] = [e.target.value, entries[i][1]];
                    setConfig({ headers: Object.fromEntries(entries) });
                  }}
                />
                <input
                  className="mono"
                  placeholder="Value"
                  value={value}
                  onChange={(e) => {
                    const entries = Object.entries((config.headers as Record<string, string>) ?? {});
                    entries[i] = [entries[i][0], e.target.value];
                    setConfig({ headers: Object.fromEntries(entries) });
                  }}
                />
                <button
                  className="danger"
                  onClick={() => {
                    const entries = Object.entries((config.headers as Record<string, string>) ?? {}).filter((_, ri) => ri !== i);
                    setConfig({ headers: Object.fromEntries(entries) });
                  }}
                >
                  ×
                </button>
              </div>
            ))}
            <button onClick={() => setConfig({ headers: { ...((config.headers as Record<string, string>) ?? {}), "": "" } })}>
              + Add header
            </button>
          </div>

          <TextAreaField
            label="Body (optional)"
            value={(config.body as string) ?? ""}
            onChange={(v) => setConfig({ body: v })}
            hint="Template-rendered like other fields. Sent as-is for POST/PUT/PATCH; ignored for GET/HEAD. A 'Content-Type' header above controls how it's labeled (defaults to application/json)."
          />
        </>
      )}

      {nodeType === "action.delay" && (
        <TextField
          label="Seconds"
          value={String((config.seconds as number) ?? 5)}
          onChange={(v) => setConfig({ seconds: Math.max(0, Number(v) || 0) })}
        />
      )}

      {nodeType === "action.splitIntoBatches" && (
        <>
          <TextField
            label="Array path (optional)"
            mono
            value={(config.arrayPath as string) ?? ""}
            onChange={(v) => setConfig({ arrayPath: v })}
            placeholder="e.g. Scrape Fixture.items -- leave blank to use this node's whole input"
          />
          <TextField
            label="Batch size"
            value={String((config.batchSize as number) ?? 10)}
            onChange={(v) => setConfig({ batchSize: Math.max(1, Number(v) || 1) })}
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

      <button
        className="danger"
        style={{ marginTop: 8 }}
        onClick={() => {
          if (confirm(`Delete "${name || nodeType}"? This also removes any edges connected to it.`)) onDelete();
        }}
      >
        Delete node
      </button>
    </div>
  );
}
