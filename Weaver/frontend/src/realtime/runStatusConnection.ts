import * as signalR from "@microsoft/signalr";
import { apiBaseUrl } from "../api/client";
import { getStoredToken } from "../auth/tokenStorage";

export interface RunStatusEvent {
  kind: "scrape" | "workflow";
  runId: string;
  status: string;
}

let connection: signalR.HubConnection | null = null;

/// Lazily starts (or reuses) a single shared hub connection for the whole app -- every page that
/// wants live run updates subscribes to the same connection rather than opening its own.
function getConnection(): signalR.HubConnection {
  if (connection) return connection;

  connection = new signalR.HubConnectionBuilder()
    // withCredentials false: auth rides on the access token, not cookies, and a credentialed
    // negotiate fails CORS preflight when the frontend runs on a different origin than the API.
    .withUrl(`${apiBaseUrl}/hubs/run-status`, { accessTokenFactory: () => getStoredToken() ?? "", withCredentials: false })
    .withAutomaticReconnect()
    .configureLogging(signalR.LogLevel.Warning)
    .build();

  connection.start().catch(() => {
    // Swallow -- the polling fallback in callers covers a connection that never comes up.
  });

  return connection;
}

/// Subscribes to run status changes; returns an unsubscribe function for cleanup in a useEffect.
export function onRunStatusChanged(callback: (event: RunStatusEvent) => void): () => void {
  const conn = getConnection();
  conn.on("runStatusChanged", callback);
  return () => conn.off("runStatusChanged", callback);
}
