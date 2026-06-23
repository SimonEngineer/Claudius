import { useEffect, useRef, useState } from "react";
import {
  HubConnection,
  HubConnectionBuilder,
  HubConnectionState,
  LogLevel,
} from "@microsoft/signalr";

import { API_BASE_URL } from "@/lib/api";
import type { LiveEvent, TaskState } from "@/types/api";

let sharedConnection: HubConnection | null = null;

function getConnection(): HubConnection {
  if (!sharedConnection) {
    sharedConnection = new HubConnectionBuilder()
      .withUrl(`${API_BASE_URL}/hubs/tasks`)
      .withAutomaticReconnect()
      .configureLogging(LogLevel.Warning)
      .build();
  }
  return sharedConnection;
}

export interface TaskStateChangedEvent {
  taskId: string;
  projectId: string;
  state: TaskState;
}

export interface ApprovalRequestedEvent {
  taskId: string;
  approvalId: string;
  question: string;
  options: string | null;
}

export function useHubConnection() {
  const [state, setState] = useState<HubConnectionState>(HubConnectionState.Disconnected);
  const connectionRef = useRef<HubConnection>(getConnection());

  useEffect(() => {
    const connection = connectionRef.current;
    let cancelled = false;

    const start = async () => {
      if (connection.state === HubConnectionState.Disconnected) {
        try {
          await connection.start();
        } catch {
          // withAutomaticReconnect retries; ignore the initial failure.
        }
      }
      if (!cancelled) setState(connection.state);
    };
    void start();

    connection.onreconnecting(() => setState(connection.state));
    connection.onreconnected(() => setState(connection.state));
    connection.onclose(() => setState(connection.state));

    return () => {
      cancelled = true;
    };
  }, []);

  return { connection: connectionRef.current, state };
}

export function useTaskStream(
  taskId: string | undefined,
  onEvent: (event: LiveEvent) => void,
  onStateChanged?: (event: TaskStateChangedEvent) => void,
  onApprovalRequested?: (event: ApprovalRequestedEvent) => void,
) {
  const { connection, state } = useHubConnection();

  useEffect(() => {
    if (!taskId) return;

    const eventHandler = (event: LiveEvent) => {
      if (event.taskId === taskId) onEvent(event);
    };
    const stateHandler = (event: TaskStateChangedEvent) => {
      if (event.taskId === taskId) onStateChanged?.(event);
    };
    const approvalHandler = (event: ApprovalRequestedEvent) => {
      if (event.taskId === taskId) onApprovalRequested?.(event);
    };

    connection.on("EventReceived", eventHandler);
    connection.on("TaskStateChanged", stateHandler);
    connection.on("ApprovalRequested", approvalHandler);

    const subscribe = () => connection.invoke("SubscribeToTask", taskId).catch(() => {});
    if (state === HubConnectionState.Connected) {
      void subscribe();
    }
    connection.onreconnected(subscribe);

    return () => {
      connection.off("EventReceived", eventHandler);
      connection.off("TaskStateChanged", stateHandler);
      connection.off("ApprovalRequested", approvalHandler);
      if (connection.state === HubConnectionState.Connected) {
        connection.invoke("UnsubscribeFromTask", taskId).catch(() => {});
      }
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [taskId, state]);
}

export function useProjectStream(
  projectId: string | undefined,
  onStateChanged?: (event: TaskStateChangedEvent) => void,
) {
  const { connection, state } = useHubConnection();

  useEffect(() => {
    if (!projectId) return;

    const stateHandler = (event: TaskStateChangedEvent) => {
      if (event.projectId === projectId) onStateChanged?.(event);
    };

    connection.on("TaskStateChanged", stateHandler);

    const subscribe = () => connection.invoke("SubscribeToProject", projectId).catch(() => {});
    if (state === HubConnectionState.Connected) {
      void subscribe();
    }
    connection.onreconnected(subscribe);

    return () => {
      connection.off("TaskStateChanged", stateHandler);
      if (connection.state === HubConnectionState.Connected) {
        connection.invoke("UnsubscribeFromProject", projectId).catch(() => {});
      }
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [projectId, state]);
}
