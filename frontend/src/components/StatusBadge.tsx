import { Badge } from "@/components/ui/badge";
import type { RunStatus, VersionStatus } from "@/types/automation";

const versionTone: Record<VersionStatus, "neutral" | "amber" | "green"> = {
  Draft: "neutral",
  NeedsClarification: "amber",
  Active: "green",
  Archived: "neutral",
};

const runTone: Record<RunStatus, "neutral" | "blue" | "green" | "red"> = {
  Pending: "neutral",
  Dispatched: "blue",
  Running: "blue",
  Succeeded: "green",
  Failed: "red",
  Cancelled: "neutral",
};

export function VersionStatusBadge({ status }: { status: VersionStatus }) {
  const label = status === "NeedsClarification" ? "Needs answers" : status;
  return <Badge tone={versionTone[status]}>{label}</Badge>;
}

export function RunStatusBadge({ status }: { status: RunStatus }) {
  return <Badge tone={runTone[status]}>{status}</Badge>;
}
