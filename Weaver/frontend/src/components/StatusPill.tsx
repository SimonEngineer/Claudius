import type { RunStatus } from "../types";

export default function StatusPill({ status }: { status: RunStatus }) {
  return <span className={`pill ${status}`}>{status}</span>;
}
