import { Bell, BellOff, Mail, RefreshCw } from "lucide-react";
import { Card, CardBody, CardHeader, CardTitle } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { Spinner } from "@/components/ui/spinner";
import { useAutomationTrigger, useSetTriggerActive } from "@/hooks/useAutomations";

interface Props {
  automationId: string;
}

const CONDITION_LABELS: Record<string, string> = {
  from:             "From",
  to:               "To",
  subject_contains: "Subject contains",
  body_contains:    "Body contains",
  folder:           "Folder",
  is_unread:        "Unread only",
  is_flagged:       "Flagged only",
  has_attachment:   "Has attachment",
  importance:       "Importance",
  category:         "Category",
};

export function TriggerStatusPanel({ automationId }: Props) {
  const { data: trigger, isLoading, refetch } = useAutomationTrigger(automationId);
  const toggle = useSetTriggerActive();

  if (isLoading) {
    return (
      <Card>
        <CardBody className="flex items-center gap-2 text-ink-400 py-4">
          <Spinner /> Loading trigger…
        </CardBody>
      </Card>
    );
  }

  if (!trigger) return null;

  const conditions = Object.entries(trigger.conditions).filter(([, v]) => v);

  async function handleToggle() {
    if (!trigger) return;
    await toggle.mutateAsync({
      automationId,
      triggerId: trigger.id,
      isActive: !trigger.isActive,
    });
    refetch();
  }

  return (
    <Card>
      <CardHeader className="flex items-center justify-between">
        <CardTitle className="flex items-center gap-2">
          <Mail className="h-4 w-4 text-ink-500" />
          Email trigger
        </CardTitle>
        <Badge tone={trigger.isActive ? "green" : "neutral"}>
          {trigger.isActive ? "Watching" : "Paused"}
        </Badge>
      </CardHeader>
      <CardBody className="space-y-3">
        {conditions.length > 0 ? (
          <div className="space-y-1.5">
            {conditions.map(([key, value]) => (
              <div key={key} className="flex items-start justify-between gap-2 text-sm">
                <span className="text-ink-500 shrink-0">
                  {CONDITION_LABELS[key] ?? key}
                </span>
                <span className="text-ink-900 text-right font-mono text-xs truncate max-w-[60%]">
                  {value}
                </span>
              </div>
            ))}
          </div>
        ) : (
          <p className="text-sm text-ink-400">Watching all incoming emails.</p>
        )}

        <p className="text-xs text-ink-400">
          The agent polls Outlook every 30 seconds. When a matching email arrives it
          starts a run automatically, with <code className="font-mono">{"{{triggerEmail}}"}</code> pre-filled.
        </p>

        <div className="flex items-center gap-2 pt-1">
          <Button
            variant="secondary"
            onClick={handleToggle}
            disabled={toggle.isPending}
          >
            {toggle.isPending ? (
              <Spinner />
            ) : trigger.isActive ? (
              <BellOff className="h-4 w-4" />
            ) : (
              <Bell className="h-4 w-4" />
            )}
            {trigger.isActive ? "Pause watching" : "Resume watching"}
          </Button>
          <Button variant="ghost" onClick={() => refetch()}>
            <RefreshCw className="h-4 w-4" />
          </Button>
        </div>
      </CardBody>
    </Card>
  );
}
