import { CheckCircle2, XCircle, Loader2, Circle } from "lucide-react";
import { Card, CardBody, CardHeader, CardTitle } from "@/components/ui/card";
import { RunStatusBadge } from "@/components/StatusBadge";
import { useRun } from "@/hooks/useRuns";
import type { RunStatus } from "@/types/automation";

function StepIcon({ status }: { status: RunStatus }) {
  if (status === "Succeeded") return <CheckCircle2 className="h-4 w-4 text-emerald-600" />;
  if (status === "Failed") return <XCircle className="h-4 w-4 text-red-600" />;
  if (status === "Running") return <Loader2 className="h-4 w-4 animate-spin text-blue-600" />;
  return <Circle className="h-4 w-4 text-ink-200" />;
}

export function RunPanel({ runId }: { runId: string }) {
  const { data: run } = useRun(runId);
  if (!run) return null;

  return (
    <Card>
      <CardHeader className="flex items-center justify-between">
        <CardTitle>Latest run</CardTitle>
        <RunStatusBadge status={run.status} />
      </CardHeader>
      <CardBody className="space-y-2">
        {run.error && (
          <p className="rounded-md bg-red-50 p-2 text-sm text-red-700">{run.error}</p>
        )}
        {run.stepLogs.length === 0 && (
          <p className="text-sm text-ink-400">Waiting for the agent to report progress…</p>
        )}
        {run.stepLogs.map((log) => (
          <div key={log.stepId} className="flex items-center gap-2.5 text-sm">
            <StepIcon status={log.status} />
            <span className="font-mono text-xs text-ink-400">{log.stepOrder}</span>
            <span className="text-ink-800">{log.message ?? log.stepId}</span>
          </div>
        ))}
      </CardBody>
    </Card>
  );
}
