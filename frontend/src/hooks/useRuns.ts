import { useMutation, useQuery } from "@tanstack/react-query";
import { getAgentStatus, getRun, triggerRun } from "@/api/endpoints";
import type { Run, RunStatus } from "@/types/automation";

const TERMINAL: RunStatus[] = ["Succeeded", "Failed", "Cancelled"];

export function useTriggerRun() {
  return useMutation({ mutationFn: (automationId: string) => triggerRun(automationId) });
}

/** Polls a run until it reaches a terminal state. */
export function useRun(runId: string | undefined) {
  return useQuery({
    queryKey: ["run", runId ?? ""],
    queryFn: () => getRun(runId!),
    enabled: Boolean(runId),
    refetchInterval: (query) => {
      const data = query.state.data as Run | undefined;
      if (!data) return 1500;
      return TERMINAL.includes(data.status) ? false : 1500;
    },
  });
}

export function useAgentStatus() {
  return useQuery({
    queryKey: ["agentStatus"],
    queryFn: getAgentStatus,
    refetchInterval: 5000,
  });
}
