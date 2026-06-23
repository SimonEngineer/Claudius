import { useEffect, useRef } from "react";

import { ScrollArea } from "@/components/ui/scroll-area";
import type { EventType } from "@/types/api";

export interface StreamEvent {
  id: number;
  type: EventType;
  timestamp: string;
  payload: string;
}

function renderPayload(event: StreamEvent): string {
  try {
    const data = JSON.parse(event.payload);
    switch (event.type) {
      case "Token":
        return data.text ?? "";
      case "Log":
        return data.message ?? "";
      case "StatusChange":
        return `→ ${data.status}${data.detail ? `: ${data.detail}` : ""}`;
      case "FileDiff":
        return `[diff] ${data.path}\n${data.diff}`;
      case "ToolCall":
        return `[tool] ${data.tool} ${data.argsJson}`;
      default:
        return event.payload;
    }
  } catch {
    return event.payload;
  }
}

export function EventStreamPane({ events }: { events: StreamEvent[] }) {
  const bottomRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    bottomRef.current?.scrollIntoView({ block: "end" });
  }, [events.length]);

  return (
    <ScrollArea className="h-[480px] rounded-md border bg-muted/30">
      <div className="flex flex-col gap-1 p-4 font-mono text-xs">
        {events.length === 0 && (
          <span className="text-muted-foreground">No events yet.</span>
        )}
        {events.map((event) => (
          <span
            key={event.id}
            className={
              event.type === "Token"
                ? "whitespace-pre-wrap"
                : "whitespace-pre-wrap text-muted-foreground"
            }
          >
            {event.type === "Token" ? renderPayload(event) : `${renderPayload(event)}\n`}
          </span>
        ))}
        <div ref={bottomRef} />
      </div>
    </ScrollArea>
  );
}
