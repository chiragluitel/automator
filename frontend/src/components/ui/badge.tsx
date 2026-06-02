import * as React from "react";
import { cn } from "@/lib/utils";

type Tone = "neutral" | "brand" | "amber" | "green" | "red" | "blue";

const tones: Record<Tone, string> = {
  neutral: "bg-ink-100 text-ink-600",
  brand: "bg-brand-100 text-brand-800",
  amber: "bg-amber-100 text-amber-800",
  green: "bg-emerald-100 text-emerald-800",
  red: "bg-red-100 text-red-700",
  blue: "bg-blue-100 text-blue-800",
};

export function Badge({
  tone = "neutral",
  className,
  ...props
}: React.HTMLAttributes<HTMLSpanElement> & { tone?: Tone }) {
  return (
    <span
      className={cn(
        "inline-flex items-center rounded-full px-2.5 py-0.5 font-mono text-[11px] font-medium uppercase tracking-wide",
        tones[tone],
        className,
      )}
      {...props}
    />
  );
}
