import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import {
  activateVersion,
  compileAutomation,
  getAutomation,
  listAutomations,
} from "@/api/endpoints";
import type { CompileRequest } from "@/types/automation";

const keys = {
  all: ["automations"] as const,
  detail: (id: string) => ["automation", id] as const,
};

export function useAutomations() {
  return useQuery({ queryKey: keys.all, queryFn: listAutomations });
}

export function useAutomation(id: string | undefined) {
  return useQuery({
    queryKey: keys.detail(id ?? ""),
    queryFn: () => getAutomation(id!),
    enabled: Boolean(id),
  });
}

export function useCompileAutomation() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (args: { automationId: string | null; request: CompileRequest }) =>
      compileAutomation(args.automationId, args.request),
    onSuccess: (version) => {
      qc.invalidateQueries({ queryKey: keys.all });
      qc.invalidateQueries({ queryKey: keys.detail(version.automationId) });
    },
  });
}

export function useActivateVersion() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (args: { automationId: string; versionId: string }) =>
      activateVersion(args.automationId, args.versionId),
    onSuccess: (version) => {
      qc.invalidateQueries({ queryKey: keys.all });
      qc.invalidateQueries({ queryKey: keys.detail(version.automationId) });
    },
  });
}
