using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace WardMate.Services.IAM.Infrastructure.Security;

public sealed class JwtOptions
{
    public string Issuer { get; set; } = "wardmate";
    public string Audience { get; set; } = "wardmate-client";
    public string Key { get; set; } = string.Empty;
    public int AccessTokenMinutes { get; set; } = 15;
    public int RefreshTokenDays { get; set; } = 7;

    public TokenValidationParameters ValidationParameters(bool validateLifetime = true) => new()
    {
        ValidateIssuer = true,
        ValidIssuer = Issuer,
        ValidateAudience = true,
        ValidAudience = Audience,
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Key)),
        RequireSignedTokens = true,
        RequireExpirationTime = true,
        ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
        ValidateLifetime = validateLifetime,
        ClockSkew = TimeSpan.Zero,
        NameClaimType = "sub",
        RoleClaimType = "role"
    };
}
