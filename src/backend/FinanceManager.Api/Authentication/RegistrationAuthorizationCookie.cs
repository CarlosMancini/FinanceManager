namespace FinanceManager.Api.Authentication;

internal static class RegistrationAuthorizationCookie
{
    public const string Name = "__Host-FinanceManager-Registration";

    public static void Write(HttpResponse response, string token, DateTimeOffset expiresAtUtc) =>
        response.Cookies.Append(Name, token, Options(expiresAtUtc));

    public static void Clear(HttpResponse response) => response.Cookies.Delete(Name, Options(null));

    private static CookieOptions Options(DateTimeOffset? expires) => new()
    {
        Secure = true, HttpOnly = true, SameSite = SameSiteMode.Strict, Path = "/", Expires = expires
    };
}
