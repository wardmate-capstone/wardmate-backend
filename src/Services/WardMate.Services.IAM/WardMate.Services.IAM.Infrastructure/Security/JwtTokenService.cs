using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using WardMate.Services.IAM.Application.DTOs;
using WardMate.Services.IAM.Application.Interfaces;
using WardMate.Services.IAM.Domain.Entities;

namespace WardMate.Services.IAM.Infrastructure.Security;

public sealed class JwtTokenService(IOptions<JwtOptions> options, TimeProvider clock) : ITokenService
{
    public AccessToken CreateAccessToken(User user)
    {
        var settings = options.Value;
        var now = clock.GetUtcNow().UtcDateTime;
        var expires = now.AddMinutes(settings.AccessTokenMinutes);
        var dto = CurrentUserDto.From(user);
        var payload = new JwtPayload(settings.Issuer, settings.Audience, null, now, expires, now)
        {
            ["sub"] = user.Id.ToString(),
            ["email"] = user.Email,
            ["jti"] = Guid.NewGuid().ToString(),
            ["role"] = dto.Roles,
            ["permissions"] = dto.Permissions
        };
        var credentials = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.Key)), SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(new JwtHeader(credentials), payload);
        return new(new JwtSecurityTokenHandler().WriteToken(token), expires);
    }
    public NewRefreshToken CreateRefreshToken()
    {
        var value = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        return new(value, HashRefreshToken(value), clock.GetUtcNow().UtcDateTime.AddDays(options.Value.RefreshTokenDays));
    }
    public string HashRefreshToken(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    public Guid? ValidateAccessTokenForRefresh(string token)
    {
        try
        {
            var handler = new JwtSecurityTokenHandler { MapInboundClaims = false };
            var principal = handler.ValidateToken(token, options.Value.ValidationParameters(validateLifetime: false), out var validated);
            // Expired access tokens are accepted ONLY here, alongside an unexpired stored refresh token.
            if (validated is not JwtSecurityToken { Payload.Expiration: not null } jwt || jwt.ValidFrom > clock.GetUtcNow().UtcDateTime) return null;
            return Guid.TryParse(principal.FindFirst("sub")?.Value, out var id) ? id : null;
        }
        catch (SecurityTokenException) { return null; }
        catch (ArgumentException) { return null; }
    }
}
