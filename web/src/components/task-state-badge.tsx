import { Badge, type BadgeProps } from "@/components/ui/badge";
import type { TaskState } from "@/types/api";

const VARIANT_BY_STATE: Record<TaskState, BadgeProps["variant"]> = {
  Queued: "secondary",
  Planning: "outline",
  ReadyForWork: "outline",
  InProgress: "default",
  Verifying: "default",
  NeedsFix: "warning",
  AwaitingInput: "warning",
  Done: "success",
  Failed: "destructive",
  DeadLetter: "destructive",
};

export function TaskStateBadge({ state }: { state: TaskState }) {
  return <Badge variant={VARIANT_BY_STATE[state]}>{state}</Badge>;
}
