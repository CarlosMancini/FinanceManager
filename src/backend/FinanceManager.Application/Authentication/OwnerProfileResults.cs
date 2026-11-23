using FinanceManager.Domain.Authentication;

namespace FinanceManager.Application.Authentication;

public enum OwnerProfileOutcome { Updated, Unchanged, Invalid, EmailAlreadyRegistered, SessionRequired, Locked, Conflict, Failed }

public sealed record OwnerProfileData(string FullName, string? Nickname, DateOnly BirthDate, string Email, string PhoneNumber);

public sealed class OwnerPasswordChange(string currentPassword, string newPassword, string confirmPassword)
{
    public string CurrentPassword { get; } = currentPassword;
    public string NewPassword { get; } = newPassword;
    public string ConfirmPassword { get; } = confirmPassword;
    public override string ToString() => nameof(OwnerPasswordChange);
}

public sealed record OwnerProfileResult(OwnerProfileOutcome Outcome, OwnerProfileData? Profile = null,
    IReadOnlyDictionary<string, string[]>? Errors = null);

public sealed record OwnerProfileCommand(OwnerProfileChanges Changes, OwnerPasswordChange? PasswordChange);
