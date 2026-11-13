using System.Text.Json.Serialization;
using FinanceManager.Api.Authentication;
using FinanceManager.Api.Http;
using FinanceManager.Application.Authentication;
using FinanceManager.Infrastructure;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.Extensions.FileProviders;

var provisionOwner = args is ["provision-owner"];
var builder = WebApplication.CreateBuilder(provisionOwner ? [] : args);
builder.Configuration["AllowedHosts"] = "localhost";
var address = builder.Configuration["urls"] ?? "https://localhost:7043";
if (!Uri.TryCreate(address, UriKind.Absolute, out var endpoint) ||
    endpoint.Scheme != "https" || endpoint.Host != "localhost" ||
    endpoint.PathAndQuery != "/" || endpoint.UserInfo.Length != 0 || endpoint.Fragment.Length != 0)
{
    throw new InvalidOperationException("Configure um único endereço HTTPS em localhost.");
}

builder.WebHost.ConfigureKestrel(options =>
{
    var certificate = builder.Configuration.GetSection("Kestrel:Certificates:Default");
    options.Configure(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
    {
        ["Certificates:Default:Path"] = certificate["Path"],
        ["Certificates:Default:Password"] = certificate["Password"]
    }).Build());
    options.ListenLocalhost(endpoint.Port, listen => listen.UseHttps());
});
builder.Logging.AddFilter("Microsoft.EntityFrameworkCore", LogLevel.None);
builder.Logging.AddFilter("Microsoft.AspNetCore.Hosting.Diagnostics", LogLevel.Warning);
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddInfrastructure();
builder.AddOwnerAuthentication();
builder.Services.AddControllers(options => options.Filters.AddService<SessionAdmissionFilter>())
    .AddJsonOptions(options => options.JsonSerializerOptions.UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow)
    .ConfigureApiBehaviorOptions(options =>
        options.InvalidModelStateResponseFactory = context =>
            ApiProblems.Validation(context.ModelState
                .Where(entry => entry.Value?.Errors.Count > 0)
                .ToDictionary(entry => entry.Key, _ => new[] { "Confira o valor informado." })));

await using var app = builder.Build();
if (provisionOwner)
{
    Console.Error.WriteLine("Crie a conta pela interface do aplicativo.");
    Environment.ExitCode = 1;
    return;
}

app.UseExceptionHandler(new ExceptionHandlerOptions
{
    SuppressDiagnosticsCallback = _ => true,
    ExceptionHandler = context =>
        ApiProblems.WriteAsync(context, 500, "INTERNAL_ERROR", "Ocorreu uma falha inesperada.")
});
app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/api"))
    {
        context.Response.Headers.CacheControl = "no-store";
    }
    await next(context);
});
app.UseStatusCodePages(context =>
    ApiProblems.WriteAsync(context.HttpContext, context.HttpContext.Response.StatusCode,
        "HTTP_ERROR", "Não foi possível concluir a solicitação."));

var browserRoot = app.Configuration["Angular:BrowserRoot"];
using var files = string.IsNullOrWhiteSpace(browserRoot) ? null :
    new PhysicalFileProvider(Path.GetFullPath(browserRoot));
if (files is not null)
{
    app.UseStaticFiles(new StaticFileOptions { FileProvider = files });
    app.MapFallback(context =>
    {
        if (context.Request.Path.StartsWithSegments("/api") ||
            (!HttpMethods.IsGet(context.Request.Method) && !HttpMethods.IsHead(context.Request.Method)))
        {
            return ApiProblems.WriteAsync(context, 404, "NOT_FOUND", "Endereço não encontrado.");
        }
        return Results.File(Path.Combine(files.Root, "index.html"), "text/html").ExecuteAsync(context);
    });
}

app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
await app.RunAsync();

public partial class Program;
