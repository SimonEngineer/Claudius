/** Formats an ISO timestamp as a compact relative time ("just now", "5m ago", "3d ago").
 * Falls back to the locale date for anything older than a month. */
export function timeAgo(iso: string | null | undefined): string {
  if (!iso) return "-";
  const then = new Date(iso).getTime();
  const seconds = Math.floor((Date.now() - then) / 1000);
  if (seconds < 45) return "just now";
  if (seconds < 3600) return `${Math.max(1, Math.floor(seconds / 60))}m ago`;
  if (seconds < 86400) return `${Math.floor(seconds / 3600)}h ago`;
  if (seconds < 30 * 86400) return `${Math.floor(seconds / 86400)}d ago`;
  return new Date(iso).toLocaleDateString();
}
