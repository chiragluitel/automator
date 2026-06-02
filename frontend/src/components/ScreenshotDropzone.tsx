import { useRef, useState } from "react";
import { ImagePlus, X } from "lucide-react";
import { cn } from "@/lib/utils";

interface Props {
  value?: { base64: string; mediaType: string } | null;
  onChange: (value: { base64: string; mediaType: string } | null) => void;
}

/** Reads a dropped/selected image into a base64 data URL for the compile request. */
export function ScreenshotDropzone({ value, onChange }: Props) {
  const inputRef = useRef<HTMLInputElement>(null);
  const [dragging, setDragging] = useState(false);

  function handleFile(file?: File) {
    if (!file) return;
    const reader = new FileReader();
    reader.onload = () =>
      onChange({ base64: String(reader.result), mediaType: file.type || "image/png" });
    reader.readAsDataURL(file);
  }

  if (value) {
    return (
      <div className="relative w-fit">
        <img
          src={value.base64}
          alt="step screenshot"
          className="max-h-40 rounded-lg border border-ink-200"
        />
        <button
          type="button"
          onClick={() => onChange(null)}
          className="absolute -right-2 -top-2 rounded-full bg-white p-1 shadow-card ring-1 ring-ink-200"
          aria-label="Remove screenshot"
        >
          <X className="h-3.5 w-3.5 text-ink-600" />
        </button>
      </div>
    );
  }

  return (
    <button
      type="button"
      onClick={() => inputRef.current?.click()}
      onDragOver={(e) => {
        e.preventDefault();
        setDragging(true);
      }}
      onDragLeave={() => setDragging(false)}
      onDrop={(e) => {
        e.preventDefault();
        setDragging(false);
        handleFile(e.dataTransfer.files?.[0]);
      }}
      className={cn(
        "flex w-full items-center justify-center gap-2 rounded-lg border border-dashed px-3 py-4 text-sm",
        dragging ? "border-brand-400 bg-brand-50 text-brand-700" : "border-ink-200 text-ink-400 hover:border-ink-400",
      )}
    >
      <ImagePlus className="h-4 w-4" />
      Attach a screenshot (optional)
      <input
        ref={inputRef}
        type="file"
        accept="image/*"
        className="hidden"
        onChange={(e) => handleFile(e.target.files?.[0])}
      />
    </button>
  );
}
