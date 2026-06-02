import { Input } from "@/components/ui/input";
import type { IrStep } from "@/types/automation";

interface Props {
  steps: IrStep[];
  answers: Record<string, string>;
  onChange: (stepId: string, answer: string) => void;
}

/** Renders the questions Claude flagged and collects the user's answers. */
export function ClarificationList({ steps, answers, onChange }: Props) {
  const pending = steps.filter((s) => s.needsClarification && s.clarificationQuestion);
  if (pending.length === 0) return null;

  return (
    <div className="space-y-4">
      {pending.map((step) => (
        <div key={step.id}>
          <label className="text-sm font-medium text-ink-900">
            <span className="label-mono mr-2">step {step.order}</span>
            {step.clarificationQuestion}
          </label>
          <Input
            className="mt-1.5"
            placeholder="Your answer"
            value={answers[step.id] ?? ""}
            onChange={(e) => onChange(step.id, e.target.value)}
          />
        </div>
      ))}
    </div>
  );
}
