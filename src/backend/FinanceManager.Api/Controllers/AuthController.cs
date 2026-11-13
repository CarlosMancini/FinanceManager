using System.Security.Claims;
using FinanceManager.Api.Authentication;
using FinanceManager.Api.Http;
using FinanceManager.Application.Authentication;
using FinanceManager.Infrastructure.Authentication;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace FinanceManager.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/auth")]
public sealed class AuthController(OwnerAccessService access, OwnerRegistrationService registration, IAntiforgery antiforgery) : ControllerBase
{
    [AllowAnonymous]
    [HttpGet("registration-state")]
    public async Task<IActionResult> RegistrationState(CancellationToken cancellationToken)
    {
        var state = await registration.GetStateAsync(cancellationToken);
        return Ok(new { state = state switch { Application.Authentication.RegistrationState.Empty => "empty", Application.Authentication.RegistrationState.LegacyPending => "legacyPending", _ => "completed" } });
    }

    [AllowAnonymous]
    [HttpPost("register")]
    public async Task<IActionResult> Register(RegistrationRequest request, CancellationToken cancellationToken)
    {
        var result = await registration.RegisterAsync(request.FullName, request.Nickname, request.BirthDate, request.Email, request.PhoneNumber, request.Password, cancellationToken);
        return result.Outcome switch
        {
            RegistrationOutcome.Succeeded => StatusCode(201, new { message = "Cadastro concluído. Entre para continuar." }),
            RegistrationOutcome.AlreadyRegistered => ApiProblems.Result(409, "OWNER_ALREADY_REGISTERED", "Já existe um cadastro. Entre para continuar."),
            RegistrationOutcome.RegistrationRequired => ApiProblems.Result(409, "REGISTRATION_REQUIRED", "Complete o cadastro existente."),
            RegistrationOutcome.InvalidProfile => ApiProblems.Validation(result.Errors?.ToDictionary() ?? new Dictionary<string, string[]> { ["profile"] = ["Confira os dados informados."] }),
            _ => ApiProblems.Result(500, "INTERNAL_ERROR", "Não foi possível concluir o cadastro.")
        };
    }

    [AllowAnonymous]
    [HttpGet("csrf")]
    public IActionResult Csrf()
    {
        var tokens = antiforgery.GetAndStoreTokens(HttpContext);
        Response.Cookies.Append("XSRF-TOKEN", tokens.RequestToken!, new CookieOptions
        {
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Path = "/"
        });
        return NoContent();
    }

    [AllowAnonymous]
    [EnableRateLimiting("login")]
    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        var result = await access.LoginAsync(request.Username, request.Password,
            Guid.TryParse(User.FindFirstValue(OwnerTicketStore.SessionClaim), out var current) ? current : null,
            cancellationToken);
        if (result.Outcome == CredentialOutcome.Locked)
        {
            return ApiProblems.Result(429, "LOGIN_THROTTLED", "Muitas tentativas. Tente novamente mais tarde.");
        }
        if (result.Session is not { } session)
        {
            return ApiProblems.Result(401, "INVALID_CREDENTIALS", "Usuário ou senha inválidos.");
        }

        await HttpContext.SignInAsync(OwnerTicketStore.Scheme,
            OwnerTicketStore.CreatePrincipal(session.Id, session.OwnerId, result.Username!));
        return NoContent();
    }

    [HttpGet("session")]
    public IActionResult Session() => Ok(new { username = User.Identity!.Name });

    [SkipSessionAdmission]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(User.FindFirstValue(OwnerTicketStore.SessionClaim), out var id) ||
            !await access.LogoutAsync(id, cancellationToken))
        {
            return ApiProblems.Result(401, "SESSION_REQUIRED", "Entre para continuar.");
        }

        await HttpContext.SignOutAsync(OwnerTicketStore.Scheme);
        return NoContent();
    }
}

public sealed record LoginRequest(string Username, string Password);
