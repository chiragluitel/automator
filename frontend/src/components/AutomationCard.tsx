import { Link } from "react-router-dom";
import { ChevronRight } from "lucide-react";
import { Card } from "@/components/ui/card";
import { VersionStatusBadge } from "@/components/StatusBadge";
import { formatDate } from "@/lib/utils";
import type { AutomationSummary } from "@/types/automation";

export function AutomationCard({ automation }: { automation: AutomationSummary }) {
  return (
    <Link to={`/automations/${automation.id}`}>
      <Card className="flex items-center justify-between p-5 transition-shadow hover:shadow-md">
        <div className="min-w-0">
          <div className="flex items-center gap-2.5">
            <h3 className="truncate text-[15px] font-semibold text-ink-900">
              {automation.name}
            </h3>
            <VersionStatusBadge status={automation.status} />
          </div>
          {automation.description && (
            <p className="mt-1 line-clamp-1 text-sm text-ink-600">{automation.description}</p>
          )}
          <p className="label-mono mt-2">
            {automation.currentVersionNumber
              ? `v${automation.currentVersionNumber}`
              : "no version"}{" "}
            · updated {formatDate(automation.updatedAt)}
          </p>
        </div>
        <ChevronRight className="h-5 w-5 shrink-0 text-ink-400" />
      </Card>
    </Link>
  );
}
