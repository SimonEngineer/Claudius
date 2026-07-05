import { useQuery } from "@tanstack/react-query";
import { ScrapingProjectsApi } from "../api/endpoints";
import { useEscapeKey } from "../utils/useEscapeKey";

export default function ItemHistoryModal({
  scrapingProjectId,
  itemKey,
  onClose,
}: {
  scrapingProjectId: string;
  itemKey: string;
  onClose: () => void;
}) {
  useEscapeKey(onClose);
  const history = useQuery({
    queryKey: ["item-history", scrapingProjectId, itemKey],
    queryFn: () => ScrapingProjectsApi.itemHistory(scrapingProjectId, itemKey),
  });

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
          <h3 style={{ margin: 0, fontSize: 14 }}>Item history</h3>
          <button className="close-btn" onClick={onClose}>×</button>
        </div>

        {history.isLoading && <p className="muted">Loading…</p>}
        {history.data && history.data.length === 0 && <p className="muted">No history found for this item.</p>}

        <div style={{ display: "flex", flexDirection: "column", gap: 12 }}>
          {history.data?.map((snapshot, i) => (
            <div key={snapshot.id} className="card" style={{ margin: 0, background: "var(--panel-2)" }}>
              <div className="page-header" style={{ marginBottom: 6 }}>
                <strong style={{ fontSize: 12 }}>{i === 0 ? "First seen" : `Snapshot #${i + 1}`}</strong>
                <span className="muted" style={{ fontSize: 12 }}>{new Date(snapshot.createdAt).toLocaleString()}</span>
              </div>

              {snapshot.changes ? (
                Object.keys(snapshot.changes).length > 0 ? (
                  <table>
                    <thead>
                      <tr>
                        <th>Field</th>
                        <th>Before</th>
                        <th>After</th>
                      </tr>
                    </thead>
                    <tbody>
                      {Object.entries(snapshot.changes).map(([field, diff]) => (
                        <tr key={field}>
                          <td>{field}</td>
                          <td style={{ color: "var(--danger)" }}>{diff.before ?? <span className="muted">(none)</span>}</td>
                          <td style={{ color: "var(--accent-2)" }}>{diff.after ?? <span className="muted">(none)</span>}</td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                ) : (
                  <p className="muted" style={{ margin: 0 }}>No fields changed from the previous snapshot.</p>
                )
              ) : (
                <table>
                  <tbody>
                    {Object.entries(snapshot.data).map(([field, value]) => (
                      <tr key={field}>
                        <td>{field}</td>
                        <td>{value ?? <span className="muted">(none)</span>}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              )}
            </div>
          ))}
        </div>
      </div>
    </div>
  );
}
