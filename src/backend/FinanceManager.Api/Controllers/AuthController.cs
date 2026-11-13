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
public sealed class AuthController(OwnerAccessService access, OwnerRegistrationService registration, OwnerRegistrationTransitionService transition, IAntiforgery antiforgery) : ControllerBase
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
    [HttpPost("registration/proof")]
    public async Task<IActionResult> Proof(RegistrationProofRequest request, CancellationToken cancellationToken)
    {
        var result = await transition.ProveAsync(request.Username, request.Password, cancellationToken);
        if (result.Outcome == CredentialOutcome.Locked)
            return ApiProblems.Result(429, "LOGIN_THROTTLED", "Muitas tentativas. Tente novamente mais tarde.");
        if (result.Failure == RegistrationOutcome.AuthorizationExpired)
            return AuthorizationError(RegistrationOutcome.AuthorizationExpired);
        if (result.Outcome != CredentialOutcome.Accepted || result.Token is null || result.ExpiresAtUtc is null)
            return ApiProblems.Result(401, "INVALID_CREDENTIALS", "Credenciais inválidas.");
        RegistrationAuthorizationCookie.Write(Response, result.Token, result.ExpiresAtUtc.Value);
        return Ok(new { expiresAtUtc = result.ExpiresAtUtc });
    }

    [AllowAnonymous]
    [HttpGet("registration/authorization")]
    public async Task<IActionResult> Authorization(CancellationToken cancellationToken)
    {
        var result = await transition.GetAuthorizationAsync(Request.Cookies[RegistrationAuthorizationCookie.Name], cancellationToken);
        return result.Outcome == RegistrationOutcome.Succeeded
            ? Ok(new { expiresAtUtc = result.ExpiresAtUtc }) : AuthorizationError(result.Outcome);
    }

    [AllowAnonymous]
    [HttpPost("registration/complete")]
    public async Task<IActionResult> Complete(RegistrationRequest request, CancellationToken cancellationToken)
    {
        var result = await transition.CompleteAsync(Request.Cookies[RegistrationAuthorizationCookie.Name], request.FullName,
            request.Nickname, request.BirthDate, request.Email, request.PhoneNumber, request.Password, cancellationToken);
        if (result.Outcome == RegistrationOutcome.InvalidProfile)
            return ApiProblems.Validation(result.Errors?.ToDictionary() ?? new Dictionary<string, string[]>());
        if (result.Outcome != RegistrationOutcome.Succeeded) return AuthorizationError(result.Outcome);
        RegistrationAuthorizationCookie.Clear(Response);
        await HttpContext.SignOutAsync(OwnerTicketStore.Scheme);
        return Ok(new { message = "Cadastro concluído. Entre para continuar." });
    }

    private static IActionResult AuthorizationError(RegistrationOutcome outcome) => outcome switch
    {
        RegistrationOutcome.AuthorizationExpired => ApiProblems.Result(401, "REGISTRATION_AUTHORIZATION_EXPIRED", "A autorização expirou. Comprove o acesso novamente."),
        RegistrationOutcome.AlreadyCompleted => ApiProblems.Result(409, "REGISTRATION_ALREADY_COMPLETED", "Cadastro já concluído. Entre para continuar."),
        RegistrationOutcome.AuthorizationRequired => ApiProblems.Result(401, "REGISTRATION_AUTHORIZATION_REQUIRED", "Comprove o acesso anterior para continuar."),
        _ => ApiProblems.Result(500, "INTERNAL_ERROR", "Não foi possível concluir o cadastro.")
    };

    [AllowAnonymous]
    [EnableRateLimiting("login")]
    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        var result = await access.LoginAsync(request.Email, request.Password,
            Guid.TryParse(User.FindFirstValue(OwnerTicketStore.SessionClaim), out var current) ? current : null,
            cancellationToken);
        if (result.Outcome == CredentialOutcome.Locked)
        {
            return ApiProblems.Result(429, "LOGIN_THROTTLED", "Muitas tentativas. Tente novamente mais tarde.");
        }
        if (result.Session is not { } session)
        {
            return ApiProblems.Result(401, "INVALID_CREDENTIALS", "Credenciais inválidas.");
        }

        await HttpContext.SignInAsync(OwnerTicketStore.Scheme,
            OwnerTicketStore.CreatePrincipal(session.Id, session.OwnerId, result.Email!));
        return NoContent();
    }

    [HttpGet("session")]
    public async Task<IActionResult> Session(CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(User.FindFirstValue(OwnerTicketStore.SessionClaim), out var id))
            return ApiProblems.Result(401, "SESSION_REQUIRED", "Entre para continuar.");
        var email = await access.GetSessionEmailAsync(id, cancellationToken);
        return email is null ? ApiProblems.Result(401, "SESSION_REQUIRED", "Entre para continuar.") : Ok(new { email });
    }

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

public sealed record LoginRequest(string Email, string Password);
