// Mirrors contracts/automation-ir.schema.json and the backend DTOs.

export type TriggerType =
  | "manual"
  | "schedule"
  | "email_received"
  | "file_created"
  | "webhook";

export type StepAction =
  | "open_application"
  | "navigate"
  | "click"
  | "type_text"
  | "select_option"
  | "read_email"
  | "extract"
  | "wait"
  | "condition"
  | "loop"
  | "api_call";

export interface IrTarget {
  app?: string | null;
  url?: string | null;
  selector?: string | null;
  label?: string | null;
}

export interface IrStep {
  id: string;
  order: number;
  action: StepAction;
  target?: IrTarget;
  params?: Record<string, unknown>;
  rawInstruction: string;
  assetRefs?: string[];
  needsClarification?: boolean;
  clarificationQuestion?: string | null;
}

export interface IrCondition {
  field: string;
  op: string;
  value: unknown;
}

export interface IrTrigger {
  type: TriggerType;
  source?: string | null;
  schedule?: string | null;
  conditions?: IrCondition[];
}

export interface IrVariable {
  name: string;
  type: string;
  from?: string | null;
}

export interface AutomationIr {
  name: string;
  schemaVersion: number;
  description?: string | null;
  trigger: IrTrigger;
  variables?: IrVariable[];
  steps: IrStep[];
}

export type VersionStatus = "Draft" | "NeedsClarification" | "Active" | "Archived";
export type RunStatus =
  | "Pending"
  | "Dispatched"
  | "Running"
  | "Succeeded"
  | "Failed"
  | "Cancelled";

export interface AutomationSummary {
  id: string;
  name: string;
  description?: string | null;
  status: VersionStatus;
  currentVersionNumber?: number | null;
  updatedAt: string;
}

export interface AutomationVersion {
  id: string;
  automationId: string;
  versionNumber: number;
  status: VersionStatus;
  definition: AutomationIr;
  createdAt: string;
}

export interface AutomationDetail {
  id: string;
  name: string;
  description?: string | null;
  currentVersionId?: string | null;
  versions: AutomationVersion[];
}

export interface RunStepLog {
  stepId: string;
  stepOrder: number;
  status: RunStatus;
  message?: string | null;
  startedAt?: string | null;
  finishedAt?: string | null;
}

export interface Run {
  id: string;
  automationVersionId: string;
  triggerType: string;
  status: RunStatus;
  error?: string | null;
  createdAt: string;
  startedAt?: string | null;
  finishedAt?: string | null;
  stepLogs: RunStepLog[];
}

// ---- Authoring input ----

export interface AuthoredStep {
  rawInstruction: string;
  screenshotBase64?: string | null;
  screenshotMediaType?: string | null;
}

export interface ClarificationAnswer {
  stepId: string;
  answer: string;
}

export interface CompileRequest {
  name: string;
  description?: string | null;
  triggerHint?: string | null;
  steps: AuthoredStep[];
  answers?: ClarificationAnswer[] | null;
}
