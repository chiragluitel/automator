import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import {
  activateVersion,
  compileAutomation,
  getAutomation,
  getAutomationTrigger,
  listAutomations,
  setTriggerActive,
} from "@/api/endpoints";
import type { CompileRequest } from "@/types/automation";

const keys = {
  all: ["automations"] as const,
  detail: (id: string) => ["automation", id] as const,
  trigger: (id: string) => ["automation-trigger", id] as const,
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
      qc.invalidateQueries({ queryKey: keys.trigger(version.automationId) });
    },
  });
}

export function useAutomationTrigger(automationId: string | undefined) {
  return useQuery({
    queryKey: keys.trigger(automationId ?? ""),
    queryFn: () => getAutomationTrigger(automationId!),
    enabled: Boolean(automationId),
    staleTime: 10_000,
  });
}

export function useSetTriggerActive() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (args: { automationId: string; triggerId: string; isActive: boolean }) =>
      setTriggerActive(args.automationId, args.triggerId, args.isActive),
    onSuccess: (_data, args) => {
      qc.invalidateQueries({ queryKey: keys.trigger(args.automationId) });
    },
  });
}
