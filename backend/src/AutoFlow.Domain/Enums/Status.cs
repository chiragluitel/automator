namespace AutoFlow.Domain.Enums;

public enum VersionStatus
{
    Draft,
    NeedsClarification,
    Active,
    Archived
}

public enum RunStatus
{
    Pending,
    Dispatched,
    Running,
    Succeeded,
    Failed,
    Cancelled
}
