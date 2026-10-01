using Microsoft.Extensions.Options;
using WardMate.Services.IAM.Application.DTOs;

namespace WardMate.Services.IAM.API.Security;

public sealed class RefreshCookieOptions
{
    public SameSiteMode SameSite { get; set; } = SameSiteMode.Strict;
    // Explicit opt-in for HTTP localhost only; never accepted outside Development.
    public bool AllowInsecureLocalhost { get; set; }
}

public sealed class RefreshTokenCookie(IOptions<RefreshCookieOptions> options, IWebHostEnvironment environment)
{
    public const string Name = "refreshToken";
    public const string Path = "/api/v1/auth";

    public string? Read(HttpContext context) => context.Request.Cookies[Name];

    public void Write(HttpContext context, AuthResponseDto tokens) => context.Response.Cookies.Append(
        Name, tokens.RefreshToken, CookieOptions(context, new DateTimeOffset(tokens.RefreshTokenExpiresAt, TimeSpan.Zero)));

    public void Delete(HttpContext context) => context.Response.Cookies.Delete(Name, CookieOptions(context));

    private CookieOptions CookieOptions(HttpContext context, DateTimeOffset? expires = null)
    {
        var host = context.Request.Host.Host;
        var local = host.Equals("localhost", StringComparison.OrdinalIgnoreCase) || host is "127.0.0.1" or "::1" or "[::1]";
        return new CookieOptions
        {
            HttpOnly = true, Secure = !(environment.IsDevelopment() && options.Value.AllowInsecureLocalhost && local),
            SameSite = options.Value.SameSite, Path = Path, Expires = expires, IsEssential = true
        };
    }
}

// Public transport DTO deliberately excludes the refresh-token secret.
public sealed record BrowserAuthResponse(string AccessToken, DateTime AccessTokenExpiresAt,
    DateTime RefreshTokenExpiresAt, string TokenType)
{
    public static BrowserAuthResponse From(AuthResponseDto tokens) =>
        new(tokens.AccessToken, tokens.AccessTokenExpiresAt, tokens.RefreshTokenExpiresAt, tokens.TokenType);
}
