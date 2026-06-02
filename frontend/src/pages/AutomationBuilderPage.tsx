import { useMemo, useState } from "react";
import { Link } from "react-router-dom";
import { Plus, Sparkles, CheckCircle2, ArrowRight } from "lucide-react";
import { AppShell } from "@/components/AppShell";
import { Button } from "@/components/ui/button";
import { Card, CardBody, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Spinner } from "@/components/ui/spinner";
import { StepEditorRow, type DraftStep } from "@/components/StepEditorRow";
import { StepList } from "@/components/StepList";
import { ClarificationList } from "@/components/ClarificationList";
import { TriggerBadge } from "@/components/TriggerBadge";
import { useCompileAutomation } from "@/hooks/useAutomations";
import type { AutomationVersion, CompileRequest } from "@/types/automation";

const emptyStep = (): DraftStep => ({ rawInstruction: "", screenshot: null });

export function AutomationBuilderPage() {
  const [name, setName] = useState("");
  const [description, setDescription] = useState("");
  const [triggerHint, setTriggerHint] = useState("");
  const [steps, setSteps] = useState<DraftStep[]>([emptyStep()]);
  const [answers, setAnswers] = useState<Record<string, string>>({});

  const [automationId, setAutomationId] = useState<string | null>(null);
  const [version, setVersion] = useState<AutomationVersion | null>(null);

  const compile = useCompileAutomation();

  const validSteps = steps.filter((s) => s.rawInstruction.trim().length > 0);
  const canCompile = name.trim().length > 0 && validSteps.length > 0 && !compile.isPending;

  const pendingClarifications = useMemo(
    () => version?.definition.steps.filter((s) => s.needsClarification) ?? [],
    [version],
  );

  function buildRequest(): CompileRequest {
    return {
      name: name.trim(),
      description: description.trim() || null,
      triggerHint: triggerHint.trim() || null,
      steps: validSteps.map((s) => ({
        rawInstruction: s.rawInstruction.trim(),
        screenshotBase64: s.screenshot?.base64 ?? null,
        screenshotMediaType: s.screenshot?.mediaType ?? null,
      })),
      answers: Object.entries(answers)
        .filter(([, a]) => a.trim().length > 0)
        .map(([stepId, answer]) => ({ stepId, answer: answer.trim() })),
    };
  }

  async function handleCompile() {
    const result = await compile.mutateAsync({ automationId, request: buildRequest() });
    setAutomationId(result.automationId);
    setVersion(result);
  }

  return (
    <AppShell>
      <div className="mb-6">
        <Link to="/" className="label-mono hover:text-ink-600">
          ← Automations
        </Link>
        <h1 className="mt-2 text-2xl font-semibold tracking-tight">New automation</h1>
      </div>

      <div className="grid gap-6 lg:grid-cols-2">
        {/* Authoring */}
        <div className="space-y-5">
          <Card>
            <CardHeader>
              <CardTitle>Details</CardTitle>
            </CardHeader>
            <CardBody className="space-y-3">
              <div>
                <label className="label-mono">Name</label>
                <Input
                  className="mt-1"
                  placeholder="Access Request Automation"
                  value={name}
                  onChange={(e) => setName(e.target.value)}
                />
              </div>
              <div>
                <label className="label-mono">Description</label>
                <Input
                  className="mt-1"
                  placeholder="What does this automation do?"
                  value={description}
                  onChange={(e) => setDescription(e.target.value)}
                />
              </div>
              <div>
                <label className="label-mono">Trigger (plain language)</label>
                <Input
                  className="mt-1"
                  placeholder="e.g. when a Service Desk access email arrives"
                  value={triggerHint}
                  onChange={(e) => setTriggerHint(e.target.value)}
                />
              </div>
            </CardBody>
          </Card>

          <div className="space-y-3">
            <div className="flex items-center justify-between">
              <h2 className="text-sm font-semibold text-ink-900">Steps</h2>
              <span className="label-mono">{validSteps.length} step(s)</span>
            </div>
            {steps.map((step, i) => (
              <StepEditorRow
                key={i}
                index={i}
                step={step}
                removable={steps.length > 1}
                onChange={(s) => setSteps((prev) => prev.map((p, idx) => (idx === i ? s : p)))}
                onRemove={() => setSteps((prev) => prev.filter((_, idx) => idx !== i))}
              />
            ))}
            <Button
              variant="secondary"
              onClick={() => setSteps((prev) => [...prev, emptyStep()])}
            >
              <Plus className="h-4 w-4" />
              Add step
            </Button>
          </div>

          <Button className="w-full" disabled={!canCompile} onClick={handleCompile}>
            {compile.isPending ? <Spinner className="text-white" /> : <Sparkles className="h-4 w-4" />}
            {version ? "Re-compile" : "Compile with AI"}
          </Button>
          {compile.isError && (
            <p className="rounded-md bg-red-50 p-2 text-sm text-red-700">
              {(compile.error as Error).message}
            </p>
          )}
        </div>

        {/* Result */}
        <div className="space-y-5">
          {!version ? (
            <Card className="flex h-full items-center justify-center p-10 text-center">
              <p className="text-sm text-ink-400">
                Your compiled automation will appear here once you compile.
              </p>
            </Card>
          ) : (
            <>
              <Card>
                <CardHeader className="flex items-center justify-between">
                  <CardTitle>Compiled steps</CardTitle>
                  <TriggerBadge trigger={version.definition.trigger} />
                </CardHeader>
                <CardBody>
                  <StepList ir={version.definition} />
                </CardBody>
              </Card>

              {pendingClarifications.length > 0 ? (
                <Card>
                  <CardHeader>
                    <CardTitle>A few questions</CardTitle>
                  </CardHeader>
                  <CardBody className="space-y-4">
                    <ClarificationList
                      steps={version.definition.steps}
                      answers={answers}
                      onChange={(stepId, answer) =>
                        setAnswers((prev) => ({ ...prev, [stepId]: answer }))
                      }
                    />
                    <Button onClick={handleCompile} disabled={compile.isPending}>
                      {compile.isPending ? <Spinner className="text-white" /> : null}
                      Re-compile with answers
                    </Button>
                  </CardBody>
                </Card>
              ) : (
                <Card>
                  <CardBody className="flex items-center justify-between py-5">
                    <div className="flex items-center gap-2.5">
                      <CheckCircle2 className="h-5 w-5 text-emerald-600" />
                      <p className="text-sm font-medium text-ink-900">
                        Ready — no open questions.
                      </p>
                    </div>
                    {automationId && (
                      <Link to={`/automations/${automationId}`}>
                        <Button>
                          Review &amp; activate
                          <ArrowRight className="h-4 w-4" />
                        </Button>
                      </Link>
                    )}
                  </CardBody>
                </Card>
              )}
            </>
          )}
        </div>
      </div>
    </AppShell>
  );
}
