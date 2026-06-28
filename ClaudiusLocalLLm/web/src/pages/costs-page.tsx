import { useEffect, useState } from "react";
import { Link } from "react-router-dom";

import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { api } from "@/lib/api";
import type { CostSummary } from "@/types/api";

const usdFormatter = new Intl.NumberFormat("en-US", {
  style: "currency",
  currency: "USD",
  minimumFractionDigits: 2,
  maximumFractionDigits: 4,
});

function formatTokens(n: number): string {
  if (n >= 1_000_000) return `${(n / 1_000_000).toFixed(1)}M`;
  if (n >= 1_000) return `${(n / 1_000).toFixed(1)}k`;
  return n.toString();
}

export function CostsPage() {
  const [summary, setSummary] = useState<CostSummary | null>(null);

  useEffect(() => {
    api.getCosts().then(setSummary);
  }, []);

  if (!summary) {
    return <p className="p-8 text-sm text-muted-foreground">Loading...</p>;
  }

  const maxDailyCost = Math.max(1e-9, ...summary.last30Days.map((d) => d.costUsd));

  return (
    <div className="flex flex-col gap-6 p-8">
      <div>
        <h1 className="text-2xl font-semibold tracking-tight">Costs</h1>
        <p className="text-sm text-muted-foreground">
          Spend rolled up from every engine run's recorded cost and token usage.
        </p>
      </div>

      <div className="grid grid-cols-1 gap-4 sm:grid-cols-3">
        <Card>
          <CardHeader>
            <CardTitle className="text-base">Total spend</CardTitle>
          </CardHeader>
          <CardContent className="text-2xl font-semibold">
            {usdFormatter.format(summary.totalCostUsd)}
          </CardContent>
        </Card>
        <Card>
          <CardHeader>
            <CardTitle className="text-base">Tokens in</CardTitle>
          </CardHeader>
          <CardContent className="text-2xl font-semibold">
            {formatTokens(summary.totalTokensIn)}
          </CardContent>
        </Card>
        <Card>
          <CardHeader>
            <CardTitle className="text-base">Tokens out</CardTitle>
          </CardHeader>
          <CardContent className="text-2xl font-semibold">
            {formatTokens(summary.totalTokensOut)}
          </CardContent>
        </Card>
      </div>

      <Card>
        <CardHeader>
          <CardTitle className="text-base">Last 30 days</CardTitle>
        </CardHeader>
        <CardContent>
          {summary.last30Days.length === 0 ? (
            <p className="text-sm text-muted-foreground">No runs recorded yet.</p>
          ) : (
            <div className="flex items-end gap-1" style={{ height: 96 }}>
              {summary.last30Days.map((day) => (
                <div
                  key={day.date}
                  title={`${day.date}: ${usdFormatter.format(day.costUsd)}`}
                  className="flex-1 rounded-t-sm bg-primary/70"
                  style={{ height: `${Math.max(2, (day.costUsd / maxDailyCost) * 100)}%` }}
                />
              ))}
            </div>
          )}
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle className="text-base">By project</CardTitle>
        </CardHeader>
        <CardContent className="flex flex-col gap-2">
          {summary.byProject.length === 0 ? (
            <p className="text-sm text-muted-foreground">No runs recorded yet.</p>
          ) : (
            summary.byProject.map((project) => (
              <Link
                key={project.projectId}
                to={`/projects/${project.projectId}`}
                className="flex items-center justify-between rounded-md border p-3 text-sm transition-colors hover:bg-accent/40"
              >
                <div className="flex flex-col gap-0.5">
                  <span className="font-medium">{project.projectName}</span>
                  <span className="text-xs text-muted-foreground">
                    {project.runCount} run{project.runCount === 1 ? "" : "s"} ·{" "}
                    {formatTokens(project.tokensIn)} in / {formatTokens(project.tokensOut)} out
                  </span>
                </div>
                <span className="font-semibold">{usdFormatter.format(project.costUsd)}</span>
              </Link>
            ))
          )}
        </CardContent>
      </Card>
    </div>
  );
}
