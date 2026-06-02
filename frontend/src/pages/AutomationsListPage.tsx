import { Link } from "react-router-dom";
import { Plus, Inbox } from "lucide-react";
import { AppShell } from "@/components/AppShell";
import { AutomationCard } from "@/components/AutomationCard";
import { Button } from "@/components/ui/button";
import { Spinner } from "@/components/ui/spinner";
import { useAutomations } from "@/hooks/useAutomations";

export function AutomationsListPage() {
  const { data, isLoading } = useAutomations();

  return (
    <AppShell>
      <div className="mb-6 flex items-end justify-between">
        <div>
          <h1 className="text-2xl font-semibold tracking-tight">Automations</h1>
          <p className="mt-1 text-sm text-ink-600">
            Describe a task in plain language; AutoFlow compiles and runs it.
          </p>
        </div>
        <Link to="/new">
          <Button>
            <Plus className="h-4 w-4" />
            New automation
          </Button>
        </Link>
      </div>

      {isLoading ? (
        <div className="flex items-center gap-2 text-ink-400">
          <Spinner /> Loading…
        </div>
      ) : data && data.length > 0 ? (
        <div className="space-y-3">
          {data.map((a) => (
            <AutomationCard key={a.id} automation={a} />
          ))}
        </div>
      ) : (
        <div className="flex flex-col items-center rounded-xl border border-dashed border-ink-200 py-16 text-center">
          <Inbox className="h-8 w-8 text-ink-200" />
          <p className="mt-3 text-sm font-medium text-ink-900">No automations yet</p>
          <p className="mt-1 text-sm text-ink-600">Create your first one to get started.</p>
          <Link to="/new" className="mt-4">
            <Button>
              <Plus className="h-4 w-4" />
              New automation
            </Button>
          </Link>
        </div>
      )}
    </AppShell>
  );
}
