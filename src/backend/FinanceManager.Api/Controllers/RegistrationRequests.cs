namespace FinanceManager.Api.Controllers;

public sealed record RegistrationRequest(string? FullName, string? Nickname, DateOnly? BirthDate,
    string? Email, string? PhoneNumber, string? Password, string? ConfirmPassword);

public sealed record RegistrationProofRequest(string Username, string Password);
