import { useEffect, useState } from "react";
import { Link } from "react-router-dom";

import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { api } from "@/lib/api";
import type { AuditLogEntry } from "@/types/api";

function formatTimestamp(iso: string): string {
  return new Date(iso).toLocaleString();
}

export function AuditLogPage() {
  const [entries, setEntries] = useState<AuditLogEntry[] | null>(null);

  useEffect(() => {
    api.getAuditLog().then(setEntries);
  }, []);

  return (
    <div className="flex flex-col gap-6 p-8">
      <div>
        <h1 className="text-2xl font-semibold tracking-tight">Audit log</h1>
        <p className="text-sm text-muted-foreground">
          Who approved, rejected, paused, resumed, or cancelled what -- most recent 200 entries.
        </p>
      </div>

      <Card>
        <CardHeader>
          <CardTitle className="text-base">Entries {entries ? `(${entries.length})` : ""}</CardTitle>
        </CardHeader>
        <CardContent className="flex flex-col gap-2">
          {!entries ? (
            <p className="text-sm text-muted-foreground">Loading...</p>
          ) : entries.length === 0 ? (
            <p className="text-sm text-muted-foreground">No audit entries yet.</p>
          ) : (
            entries.map((entry) => (
              <div key={entry.id} className="flex items-center justify-between rounded-md border p-3 text-sm">
                <div className="flex flex-col gap-0.5">
                  <span className="font-medium">{entry.action}</span>
                  <span className="text-xs text-muted-foreground">
                    {entry.actor ? `${entry.actor} · ` : ""}
                    {entry.taskId && (
                      <>
                        <Link to={`/tasks/${entry.taskId}`} className="hover:underline">
                          task {entry.taskId.slice(0, 8)}
                        </Link>
                        {" · "}
                      </>
                    )}
                    {formatTimestamp(entry.createdAt)}
                  </span>
                  {entry.details && (
                    <span className="text-xs text-muted-foreground">{entry.details}</span>
                  )}
                </div>
              </div>
            ))
          )}
        </CardContent>
      </Card>
    </div>
  );
}
