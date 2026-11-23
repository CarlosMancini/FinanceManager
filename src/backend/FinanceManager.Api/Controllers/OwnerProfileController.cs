using System.Security.Claims;
using System.Text.Json;
using FinanceManager.Api.Http;
using FinanceManager.Application.Authentication;
using FinanceManager.Infrastructure.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace FinanceManager.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/auth/profile")]
public sealed class OwnerProfileController(OwnerProfileService service) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        if (Request.Query.Count != 0) return Invalid();
        if (!Identity(out var ownerId, out var sessionId)) return SessionRequired();
        var result = await service.GetAsync(ownerId, sessionId, cancellationToken);
        return result.Profile is null ? SessionRequired() : Ok(result.Profile);
    }

    [HttpPatch]
    [EnableRateLimiting("login")]
    public async Task<IActionResult> Update([FromBody] JsonElement body, CancellationToken cancellationToken)
    {
        if (Request.Query.Count != 0 || !OwnerProfileRequests.TryRead(body, out var command)) return Invalid();
        if (!Identity(out var ownerId, out var sessionId)) return SessionRequired();
        OwnerProfileResult result;
        try { result = await service.UpdateAsync(ownerId, sessionId, command!, cancellationToken); }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch { return ApiProblems.Result(500, "INTERNAL_ERROR", "Não foi possível atualizar o cadastro. Tente novamente."); }
        return result.Outcome switch
        {
            OwnerProfileOutcome.Updated => Ok(new { outcome = "updated", message = "Cadastro atualizado com sucesso.", profile = result.Profile }),
            OwnerProfileOutcome.Unchanged => Ok(new { outcome = "unchanged", message = "Não há alterações para atualizar.", profile = result.Profile }),
            OwnerProfileOutcome.Invalid => ApiProblems.Validation(result.Errors?.ToDictionary() ?? new Dictionary<string, string[]>()),
            OwnerProfileOutcome.EmailAlreadyRegistered => ProblemWithErrors(409, "EMAIL_ALREADY_REGISTERED", "Este e-mail já está registrado. Informe outro e-mail.", new Dictionary<string, string[]> { ["email"] = ["Este e-mail já está registrado. Informe outro e-mail."] }),
            OwnerProfileOutcome.Conflict => ProblemWithErrors(409, "PROFILE_UPDATE_CONFLICT", "O cadastro mudou durante a atualização. Revise os dados atuais e tente novamente.", result.Errors ?? new Dictionary<string, string[]> { ["profile"] = ["Revise os dados atuais."] }),
            OwnerProfileOutcome.Locked => ApiProblems.Result(429, "LOGIN_THROTTLED", "Muitas tentativas. Tente novamente mais tarde."),
            OwnerProfileOutcome.SessionRequired => SessionRequired(),
            _ => ApiProblems.Result(500, "INTERNAL_ERROR", "Não foi possível atualizar o cadastro. Tente novamente.")
        };
    }

    private bool Identity(out Guid ownerId, out Guid sessionId)
    {
        ownerId = Guid.Empty;
        sessionId = Guid.Empty;
        return Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out ownerId) &&
            Guid.TryParse(User.FindFirstValue(OwnerTicketStore.SessionClaim), out sessionId);
    }

    private static IActionResult Invalid() => ApiProblems.Validation(new Dictionary<string, string[]> { ["profile"] = ["Confira os dados informados."] });
    private static IActionResult SessionRequired() => ApiProblems.Result(401, "SESSION_REQUIRED", "Entre para continuar.");
    private static IActionResult ProblemWithErrors(int status, string code, string message, IReadOnlyDictionary<string, string[]> errors)
    {
        var problem = new ProblemDetails { Status = status, Title = message };
        problem.Extensions["code"] = code;
        problem.Extensions["errors"] = errors;
        return new ObjectResult(problem) { StatusCode = status, ContentTypes = { "application/problem+json" } };
    }
}
