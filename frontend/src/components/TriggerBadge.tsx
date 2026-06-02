import { Badge } from "@/components/ui/badge";
import type { IrTrigger } from "@/types/automation";

const labels: Record<string, string> = {
  manual: "Manual",
  schedule: "Scheduled",
  email_received: "On email",
  file_created: "On file",
  webhook: "Webhook",
};

export function TriggerBadge({ trigger }: { trigger: IrTrigger }) {
  return <Badge tone="brand">{labels[trigger.type] ?? trigger.type}</Badge>;
}
