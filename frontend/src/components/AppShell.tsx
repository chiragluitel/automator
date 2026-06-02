import { Link, useLocation } from "react-router-dom";
import type { ReactNode } from "react";
import { Workflow, Circle } from "lucide-react";
import { useAgentStatus } from "@/hooks/useRuns";
import { cn } from "@/lib/utils";

export function AppShell({ children }: { children: ReactNode }) {
  const { pathname } = useLocation();
  const { data: agent } = useAgentStatus();
  const online = agent?.online ?? false;

  return (
    <div className="min-h-full">
      <header className="border-b border-ink-200 bg-white/80 backdrop-blur">
        <div className="mx-auto flex h-16 max-w-5xl items-center justify-between px-6">
          <Link to="/" className="flex items-center gap-2.5">
            <span className="flex h-8 w-8 items-center justify-center rounded-lg bg-brand-700 text-white">
              <Workflow className="h-4 w-4" />
            </span>
            <span className="text-[15px] font-semibold tracking-tight">
              Amcor <span className="text-brand-700">AutoFlow</span>
            </span>
          </Link>

          <div className="flex items-center gap-2">
            <span className="label-mono">Agent</span>
            <span
              className={cn(
                "inline-flex items-center gap-1.5 rounded-full px-2.5 py-1 text-xs font-medium",
                online ? "bg-emerald-50 text-emerald-700" : "bg-ink-100 text-ink-600",
              )}
            >
              <Circle
                className={cn("h-2 w-2", online ? "fill-emerald-500 text-emerald-500" : "fill-ink-400 text-ink-400")}
              />
              {online ? "Connected" : "Offline"}
            </span>
          </div>
        </div>
      </header>

      <main className="mx-auto max-w-5xl px-6 py-8">{children}</main>

      <footer className="mx-auto max-w-5xl px-6 pb-10">
        <p className="label-mono">
          {pathname === "/" ? "Internal automation builder" : "AutoFlow"} · MVP
        </p>
      </footer>
    </div>
  );
}
