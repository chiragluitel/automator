namespace AutoFlow.Application.Abstractions;

/// <summary>
/// Identity port. MVP implementation returns the seeded demo user.
/// Replace with a claims-based implementation when SSO lands.
/// </summary>
public interface ICurrentUser
{
    Guid UserId { get; }
    string Email { get; }
}
