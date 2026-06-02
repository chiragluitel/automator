import { useState } from "react";
import { Link, useParams } from "react-router-dom";
import { Play, CheckCircle2, AlertTriangle } from "lucide-react";
import { AppShell } from "@/components/AppShell";
import { Button } from "@/components/ui/button";
import { Card, CardBody, CardHeader, CardTitle } from "@/components/ui/card";
import { Spinner } from "@/components/ui/spinner";
import { StepList } from "@/components/StepList";
import { RunPanel } from "@/components/RunPanel";
import { TriggerBadge } from "@/components/TriggerBadge";
import { VersionStatusBadge } from "@/components/StatusBadge";
import { formatDate } from "@/lib/utils";
import { useAutomation, useActivateVersion } from "@/hooks/useAutomations";
import { useAgentStatus, useTriggerRun } from "@/hooks/useRuns";

export function AutomationDetailPage() {
  const { id } = useParams<{ id: string }>();
  const { data, isLoading } = useAutomation(id);
  const activate = useActivateVersion();
  const trigger = useTriggerRun();
  const { data: agent } = useAgentStatus();
  const [runId, setRunId] = useState<string | null>(null);

  if (isLoading) {
    return (
      <AppShell>
        <div className="flex items-center gap-2 text-ink-400">
          <Spinner /> Loading…
        </div>
      </AppShell>
    );
  }
  if (!data) {
    return (
      <AppShell>
        <p className="text-sm text-ink-600">Automation not found.</p>
      </AppShell>
    );
  }

  const current =
    data.versions.find((v) => v.id === data.currentVersionId) ?? data.versions[0];
  const isActive = current?.status === "Active";
  const needsAnswers = current?.status === "NeedsClarification";
  const agentOnline = agent?.online ?? false;

  async function handleActivate() {
    if (!id || !current) return;
    await activate.mutateAsync({ automationId: id, versionId: current.id });
  }

  async function handleRun() {
    if (!id) return;
    const run = await trigger.mutateAsync(id);
    setRunId(run.id);
  }

  return (
    <AppShell>
      <div className="mb-6">
        <Link to="/" className="label-mono hover:text-ink-600">
          ← Automations
        </Link>
        <div className="mt-2 flex items-start justify-between gap-4">
          <div>
            <div className="flex items-center gap-2.5">
              <h1 className="text-2xl font-semibold tracking-tight">{data.name}</h1>
              {current && <VersionStatusBadge status={current.status} />}
            </div>
            {data.description && <p className="mt-1 text-sm text-ink-600">{data.description}</p>}
          </div>
          <div className="flex items-center gap-2">
            {!isActive && (
              <Button onClick={handleActivate} disabled={!current || needsAnswers || activate.isPending}>
                {activate.isPending ? <Spinner className="text-white" /> : <CheckCircle2 className="h-4 w-4" />}
                Activate
              </Button>
            )}
            {isActive && (
              <Button onClick={handleRun} disabled={!agentOnline || trigger.isPending}>
                {trigger.isPending ? <Spinner className="text-white" /> : <Play className="h-4 w-4" />}
                Run now
              </Button>
            )}
          </div>
        </div>
        {isActive && !agentOnline && (
          <p className="mt-2 text-xs text-ink-400">
            No agent is connected — start the AutoFlow agent on your machine to run this.
          </p>
        )}
      </div>

      {needsAnswers && (
        <div className="mb-5 flex items-start gap-2 rounded-lg bg-amber-50 p-3 text-sm text-amber-800">
          <AlertTriangle className="mt-0.5 h-4 w-4 shrink-0" />
          This version still has open questions. Re-author it in the builder to resolve them
          before activating.
        </div>
      )}

      <div className="grid gap-6 lg:grid-cols-3">
        <div className="lg:col-span-2 space-y-5">
          {current && (
            <Card>
              <CardHeader className="flex items-center justify-between">
                <CardTitle>
                  Steps <span className="label-mono ml-1">v{current.versionNumber}</span>
                </CardTitle>
                <TriggerBadge trigger={current.definition.trigger} />
              </CardHeader>
              <CardBody>
                <StepList ir={current.definition} />
              </CardBody>
            </Card>
          )}
          {runId && <RunPanel runId={runId} />}
        </div>

        <div>
          <Card>
            <CardHeader>
              <CardTitle>Versions</CardTitle>
            </CardHeader>
            <CardBody className="space-y-2">
              {data.versions.map((v) => (
                <div
                  key={v.id}
                  className="flex items-center justify-between rounded-lg border border-ink-200 p-2.5"
                >
                  <div>
                    <p className="font-mono text-xs font-semibold text-ink-900">v{v.versionNumber}</p>
                    <p className="label-mono">{formatDate(v.createdAt)}</p>
                  </div>
                  <VersionStatusBadge status={v.status} />
                </div>
              ))}
            </CardBody>
          </Card>
        </div>
      </div>
    </AppShell>
  );
}
