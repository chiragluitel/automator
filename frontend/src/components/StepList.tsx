import { AlertCircle } from "lucide-react";
import { Badge } from "@/components/ui/badge";
import type { AutomationIr, IrStep } from "@/types/automation";

function targetSummary(step: IrStep): string | null {
  const t = step.target;
  if (!t) return null;
  return t.url || t.selector || t.label || t.app || null;
}

export function StepList({ ir }: { ir: AutomationIr }) {
  return (
    <ol className="space-y-2">
      {ir.steps.map((step) => (
        <li
          key={step.id}
          className="flex gap-3 rounded-lg border border-ink-200 bg-white p-3.5"
        >
          <span className="mt-0.5 flex h-6 w-6 shrink-0 items-center justify-center rounded-md bg-ink-100 font-mono text-xs font-semibold text-ink-600">
            {step.order}
          </span>
          <div className="min-w-0 flex-1">
            <div className="flex flex-wrap items-center gap-2">
              <Badge tone="blue">{step.action}</Badge>
              {targetSummary(step) && (
                <span className="truncate font-mono text-xs text-ink-600">
                  {targetSummary(step)}
                </span>
              )}
            </div>
            <p className="mt-1.5 text-sm text-ink-800">{step.rawInstruction}</p>
            {step.needsClarification && step.clarificationQuestion && (
              <p className="mt-2 flex items-start gap-1.5 rounded-md bg-amber-50 p-2 text-xs text-amber-800">
                <AlertCircle className="mt-0.5 h-3.5 w-3.5 shrink-0" />
                {step.clarificationQuestion}
              </p>
            )}
          </div>
        </li>
      ))}
    </ol>
  );
}
