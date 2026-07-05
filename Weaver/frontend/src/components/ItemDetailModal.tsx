import { useState } from "react";
import type { ScrapedItem } from "../types";
import { useEscapeKey } from "../utils/useEscapeKey";

/** Full view of one scraped item: field table, raw JSON with copy, and a delete action. */
export default function ItemDetailModal({
  item,
  onDelete,
  onClose,
}: {
  item: ScrapedItem;
  onDelete: () => void;
  onClose: () => void;
}) {
  useEscapeKey(onClose);
  const [copied, setCopied] = useState(false);
  const rawJson = JSON.stringify(item.data, null, 2);

  return (
    <div
      style={{
        position: "fixed",
        inset: 0,
        background: "rgba(0,0,0,0.5)",
        display: "flex",
        alignItems: "center",
        justifyContent: "center",
        zIndex: 100,
      }}
      onClick={onClose}
    >
      <div className="card" style={{ maxWidth: 640, maxHeight: "80vh", overflow: "auto", width: "100%" }} onClick={(e) => e.stopPropagation()}>
        <div className="page-header">
          <h3 style={{ margin: 0, fontSize: 14 }}>Scraped item</h3>
          <button className="close-btn" onClick={onClose}>×</button>
        </div>

        <p className="muted" style={{ fontSize: 12, marginTop: 0 }}>
          From <span className="mono">{item.sourceUrl}</span> at {new Date(item.createdAt).toLocaleString()}
        </p>

        <table>
          <thead>
            <tr>
              <th>Field</th>
              <th>Value</th>
            </tr>
          </thead>
          <tbody>
            {Object.entries(item.data).map(([field, value]) => (
              <tr key={field}>
                <td className="mono" style={{ whiteSpace: "nowrap" }}>{field}</td>
                <td style={{ wordBreak: "break-word" }}>{value ?? <span className="muted">(empty)</span>}</td>
              </tr>
            ))}
          </tbody>
        </table>

        <details style={{ marginTop: 12 }}>
          <summary className="muted">raw JSON</summary>
          <pre className="mono" style={{ maxHeight: 240, overflow: "auto" }}>{rawJson}</pre>
        </details>

        <div style={{ display: "flex", gap: 8, marginTop: 12 }}>
          <button
            onClick={() => {
              navigator.clipboard.writeText(rawJson);
              setCopied(true);
              setTimeout(() => setCopied(false), 1500);
            }}
          >
            {copied ? "Copied!" : "Copy JSON"}
          </button>
          <button
            className="danger"
            onClick={() => {
              if (confirm("Delete this scraped item? This can't be undone.")) onDelete();
            }}
          >
            Delete item
          </button>
        </div>
      </div>
    </div>
  );
}
