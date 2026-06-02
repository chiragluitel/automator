import { apiClient } from "@/lib/apiClient";
import type {
  AutomationDetail,
  AutomationSummary,
  AutomationVersion,
  CompileRequest,
  Run,
} from "@/types/automation";

export async function listAutomations(): Promise<AutomationSummary[]> {
  const { data } = await apiClient.get<AutomationSummary[]>("/api/automations");
  return data;
}

export async function getAutomation(id: string): Promise<AutomationDetail> {
  const { data } = await apiClient.get<AutomationDetail>(`/api/automations/${id}`);
  return data;
}

export async function compileAutomation(
  automationId: string | null,
  request: CompileRequest,
): Promise<AutomationVersion> {
  const url = automationId
    ? `/api/automations/${automationId}/compile`
    : "/api/automations/compile";
  const { data } = await apiClient.post<AutomationVersion>(url, request);
  return data;
}

export async function activateVersion(
  automationId: string,
  versionId: string,
): Promise<AutomationVersion> {
  const { data } = await apiClient.post<AutomationVersion>(
    `/api/automations/${automationId}/versions/${versionId}/activate`,
  );
  return data;
}

export async function triggerRun(automationId: string): Promise<Run> {
  const { data } = await apiClient.post<Run>(`/api/automations/${automationId}/runs`);
  return data;
}

export async function getRun(runId: string): Promise<Run> {
  const { data } = await apiClient.get<Run>(`/api/runs/${runId}`);
  return data;
}

export async function getAgentStatus(): Promise<{ online: boolean }> {
  const { data } = await apiClient.get<{ online: boolean }>("/api/agents/status");
  return data;
}
