namespace FinanceManager.Api.Controllers;

public sealed record RegistrationRequest(string? FullName, string? Nickname, DateOnly? BirthDate,
    string? Email, string? PhoneNumber, string? Password);
