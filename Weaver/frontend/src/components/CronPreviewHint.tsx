import { useQuery } from "@tanstack/react-query";
import { useEffect, useState } from "react";
import { CronApi } from "../api/endpoints";

/** Live "this will run at ..." hint under a cron expression input (debounced server-side parse). */
export default function CronPreviewHint({ expression }: { expression: string }) {
  const [debounced, setDebounced] = useState(expression.trim());
  useEffect(() => {
    const handle = setTimeout(() => setDebounced(expression.trim()), 400);
    return () => clearTimeout(handle);
  }, [expression]);

  const preview = useQuery({
    queryKey: ["cron-preview", debounced],
    queryFn: () => CronApi.preview(debounced),
    enabled: debounced.length > 0,
    staleTime: 60_000,
  });

  if (!debounced || !preview.data) return null;
  if (!preview.data.valid) {
    return <p style={{ color: "var(--danger)", fontSize: 12, marginTop: 4 }}>{preview.data.error}</p>;
  }
  return (
    <p className="muted" style={{ fontSize: 12, marginTop: 4 }}>
      Next runs: {preview.data.nextOccurrences.slice(0, 3).map((d) => new Date(d).toLocaleString()).join("  ·  ")}
    </p>
  );
}
