using System.Security.Claims;
using FinanceManager.Api.Http;
using FinanceManager.Application.Authentication;
using FinanceManager.Infrastructure.Authentication;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Filters;

namespace FinanceManager.Api.Authentication;

internal sealed class SessionAdmissionFilter(IAntiforgery antiforgery, OwnerAccessService access) : IAsyncResourceFilter
{
    public async Task OnResourceExecutionAsync(ResourceExecutingContext context, ResourceExecutionDelegate next)
    {
        var http = context.HttpContext;
        if (HttpMethods.IsPost(http.Request.Method) || HttpMethods.IsPut(http.Request.Method) ||
            HttpMethods.IsPatch(http.Request.Method) || HttpMethods.IsDelete(http.Request.Method))
        {
            try
            {
                await antiforgery.ValidateRequestAsync(http);
            }
            catch (AntiforgeryValidationException)
            {
                context.Result = ApiProblems.Result(403, "INVALID_CSRF_TOKEN", "Atualize a página e tente novamente.");
                return;
            }
        }

        var endpoint = http.GetEndpoint();
        var needsAdmission = endpoint?.Metadata.GetMetadata<IAllowAnonymous>() is null &&
            endpoint?.Metadata.GetMetadata<SkipSessionAdmissionAttribute>() is null;
        if (needsAdmission && (!Guid.TryParse(http.User.FindFirstValue(OwnerTicketStore.SessionClaim), out var id) ||
            !await access.AdmitAsync(id, http.RequestAborted)))
        {
            context.Result = ApiProblems.Result(401, "SESSION_REQUIRED", "Entre para continuar.");
            return;
        }

        await next();
    }
}

[AttributeUsage(AttributeTargets.Method)]
internal sealed class SkipSessionAdmissionAttribute : Attribute;
