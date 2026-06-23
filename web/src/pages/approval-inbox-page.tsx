import { useEffect, useState } from "react";
import { Link } from "react-router-dom";

import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { api } from "@/lib/api";
import type { Approval } from "@/types/api";

export function ApprovalInboxPage() {
  const [approvals, setApprovals] = useState<Approval[]>([]);
  const [loading, setLoading] = useState(true);

  const refresh = () => {
    setLoading(true);
    api
      .listPendingApprovals()
      .then(setApprovals)
      .finally(() => setLoading(false));
  };

  useEffect(refresh, []);

  const resolve = async (id: string, action: "approve" | "reject") => {
    await (action === "approve" ? api.approve(id, {}) : api.reject(id, {}));
    refresh();
  };

  return (
    <div className="flex flex-col gap-6 p-8">
      <div>
        <h1 className="text-2xl font-semibold tracking-tight">Approvals</h1>
        <p className="text-sm text-muted-foreground">
          Tasks waiting on a human decision before the scheduler can continue them.
        </p>
      </div>

      {loading ? (
        <p className="text-sm text-muted-foreground">Loading...</p>
      ) : approvals.length === 0 ? (
        <p className="text-sm text-muted-foreground">Nothing pending.</p>
      ) : (
        <div className="flex flex-col gap-3">
          {approvals.map((approval) => (
            <Card key={approval.id}>
              <CardHeader>
                <CardTitle className="text-base">{approval.question}</CardTitle>
              </CardHeader>
              <CardContent className="flex items-center justify-between">
                <Link
                  to={`/tasks/${approval.taskId}`}
                  className="text-sm text-muted-foreground hover:underline"
                >
                  View task
                </Link>
                <div className="flex gap-2">
                  <Button size="sm" onClick={() => resolve(approval.id, "approve")}>
                    Approve
                  </Button>
                  <Button size="sm" variant="destructive" onClick={() => resolve(approval.id, "reject")}>
                    Reject
                  </Button>
                </div>
              </CardContent>
            </Card>
          ))}
        </div>
      )}
    </div>
  );
}
