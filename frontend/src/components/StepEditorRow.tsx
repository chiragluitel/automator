import { GripVertical, Trash2 } from "lucide-react";
import { Textarea } from "@/components/ui/textarea";
import { ScreenshotDropzone } from "@/components/ScreenshotDropzone";

export interface DraftStep {
  rawInstruction: string;
  screenshot?: { base64: string; mediaType: string } | null;
}

interface Props {
  index: number;
  step: DraftStep;
  onChange: (step: DraftStep) => void;
  onRemove: () => void;
  removable: boolean;
}

export function StepEditorRow({ index, step, onChange, onRemove, removable }: Props) {
  return (
    <div className="flex gap-3 rounded-xl border border-ink-200 bg-white p-4">
      <div className="flex flex-col items-center pt-1.5">
        <GripVertical className="h-4 w-4 text-ink-200" />
        <span className="mt-1 font-mono text-xs font-semibold text-ink-400">{index + 1}</span>
      </div>

      <div className="flex-1 space-y-3">
        <Textarea
          rows={2}
          placeholder="Describe this step in plain language, e.g. “Open Chrome and go to Amcor Central”"
          value={step.rawInstruction}
          onChange={(e) => onChange({ ...step, rawInstruction: e.target.value })}
        />
        <ScreenshotDropzone
          value={step.screenshot ?? null}
          onChange={(screenshot) => onChange({ ...step, screenshot })}
        />
      </div>

      {removable && (
        <button
          type="button"
          onClick={onRemove}
          className="self-start rounded-lg p-2 text-ink-400 hover:bg-ink-100 hover:text-red-600"
          aria-label="Remove step"
        >
          <Trash2 className="h-4 w-4" />
        </button>
      )}
    </div>
  );
}
