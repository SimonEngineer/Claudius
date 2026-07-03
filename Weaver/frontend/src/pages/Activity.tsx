import { useQuery } from "@tanstack/react-query";
import { useState } from "react";
import { Link } from "react-router-dom";
import { AuditLogApi } from "../api/endpoints";
import { resourceLink } from "../utils/resourceLink";
import { usePageTitle } from "../utils/usePageTitle";

export default function Activity() {
  usePageTitle("Activity");
  const [page, setPage] = useState(1);
  const pageSize = 50;
  const log = useQuery({ queryKey: ["audit-log", page], queryFn: () => AuditLogApi.list(page, pageSize) });

  return (
    <div>
      <div className="page-header">
        <h2>Activity</h2>
        <button onClick={() => AuditLogApi.exportCsv()}>Export CSV</button>
      </div>

      <div className="card">
        {log.data?.items.length ? (
          <>
            <table>
              <thead>
                <tr>
                  <th>When</th>
                  <th>Action</th>
                  <th>Type</th>
                  <th>Name</th>
                </tr>
              </thead>
              <tbody>
                {log.data.items.map((entry) => {
                  const link = resourceLink(entry.resourceType, entry.resourceId);
                  return (
                    <tr key={entry.id}>
                      <td className="muted">{new Date(entry.createdAt).toLocaleString()}</td>
                      <td>{entry.action}</td>
                      <td className="muted">{entry.resourceType}</td>
                      <td>{link && entry.action !== "Deleted" ? <Link to={link}>{entry.resourceName}</Link> : entry.resourceName}</td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
            <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginTop: 8 }}>
              <button disabled={page <= 1} onClick={() => setPage((p) => Math.max(1, p - 1))}>
                ← Prev
              </button>
              <span className="muted" style={{ fontSize: 12 }}>
                Page {page} of {Math.max(1, Math.ceil(log.data.totalCount / pageSize))}
              </span>
              <button disabled={page * pageSize >= log.data.totalCount} onClick={() => setPage((p) => p + 1)}>
                Next →
              </button>
            </div>
          </>
        ) : (
          <div className="empty-state">No activity yet.</div>
        )}
      </div>
    </div>
  );
}
