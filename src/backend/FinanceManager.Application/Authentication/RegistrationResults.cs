namespace FinanceManager.Application.Authentication;

public enum RegistrationState { Empty, LegacyPending, Completed }
public enum RegistrationOutcome { Succeeded, AlreadyRegistered, RegistrationRequired, AuthorizationRequired, AuthorizationExpired, AlreadyCompleted, InvalidProfile, Failed }

public sealed record RegistrationResult(RegistrationOutcome Outcome, IReadOnlyDictionary<string, string[]>? Errors = null);
