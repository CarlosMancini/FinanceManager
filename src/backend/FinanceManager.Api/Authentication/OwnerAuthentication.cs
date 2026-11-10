using System.Threading.RateLimiting;
using FinanceManager.Api.Http;
using FinanceManager.Infrastructure.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;

namespace FinanceManager.Api.Authentication;

internal static class OwnerAuthentication
{
    public static void AddOwnerAuthentication(this WebApplicationBuilder builder)
    {
        var services = builder.Services;
        services.AddAuthentication(OwnerTicketStore.Scheme).AddCookie(OwnerTicketStore.Scheme, options =>
        {
            options.Cookie.Name = "__Host-FinanceManager";
            options.Cookie.Path = "/";
            options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.Lax;
            options.SlidingExpiration = false;
            options.Events.OnRedirectToLogin = context =>
                ApiProblems.WriteAsync(context.HttpContext, 401, "SESSION_REQUIRED", "Entre para continuar.");
            options.Events.OnRedirectToAccessDenied = context =>
                ApiProblems.WriteAsync(context.HttpContext, 403, "ACCESS_DENIED", "Acesso negado.");
        });
        services.AddOptions<CookieAuthenticationOptions>(OwnerTicketStore.Scheme)
            .Configure<OwnerTicketStore, TimeProvider>((options, tickets, clock) =>
            {
                options.SessionStore = tickets;
                options.TimeProvider = clock;
            });
        services.AddAuthorization();
        services.AddScoped<SessionAdmissionFilter>();
        services.AddAntiforgery(options =>
        {
            options.HeaderName = "X-XSRF-TOKEN";
            options.Cookie.Name = "__Host-FinanceManager-Antiforgery";
            options.Cookie.Path = "/";
            options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            options.Cookie.HttpOnly = true;
        });
        var directory = builder.Configuration["Security:KeyDirectory"] ??
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FinanceManager", "keys");
        var protection = services.AddDataProtection().SetApplicationName("FinanceManager")
            .PersistKeysToFileSystem(new DirectoryInfo(directory));
        if (OperatingSystem.IsWindows())
        {
            protection.ProtectKeysWithDpapi();
        }
        services.AddRateLimiter(options =>
        {
            options.AddPolicy("login", context => RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "local",
                _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1) }));
            options.OnRejected = (context, _) => new ValueTask(ApiProblems.WriteAsync(context.HttpContext,
                429, "LOGIN_THROTTLED", "Muitas tentativas. Tente novamente mais tarde."));
        });
    }
}
