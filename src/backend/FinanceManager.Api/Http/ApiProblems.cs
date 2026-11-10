using Microsoft.AspNetCore.Mvc;

namespace FinanceManager.Api.Http;

internal static class ApiProblems
{
    private static ProblemDetails Describe(int status, string code, string title)
    {
        var problem = new ProblemDetails { Status = status, Title = title };
        problem.Extensions["code"] = code;
        return problem;
    }

    public static ObjectResult Result(int status, string code, string title) =>
        new(Describe(status, code, title))
        {
            StatusCode = status,
            ContentTypes = { "application/problem+json" }
        };

    public static Task WriteAsync(HttpContext context, int status, string code, string title)
    {
        context.Response.StatusCode = status;
        context.Response.Headers.CacheControl = "no-store";
        return context.Response.WriteAsJsonAsync(
            Describe(status, code, title), options: null, contentType: "application/problem+json");
    }
}
