using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using SolarCare.Application.Authentication;

namespace SolarCare.Api.Authentication;

public sealed class JwtAccessTokenIssuer : IAccessTokenIssuer
{
    private static readonly TimeSpan TokenLifetime = TimeSpan.FromMinutes(15);
    private readonly SigningCredentials _credentials;
    private readonly string _issuer;
    private readonly string _audience;
    private readonly TimeProvider _timeProvider;

    public JwtAccessTokenIssuer(
        IConfiguration configuration,
        TimeProvider timeProvider)
    {
        var configuredKey = configuration["Jwt:SigningKey"]
            ?? throw new InvalidOperationException("Configuration 'Jwt:SigningKey' is required.");
        byte[] keyBytes;
        try
        {
            keyBytes = Convert.FromBase64String(configuredKey);
        }
        catch (FormatException exception)
        {
            throw new InvalidOperationException("'Jwt:SigningKey' must be a base64-encoded key.", exception);
        }

        if (keyBytes.Length < 32)
        {
            throw new InvalidOperationException("'Jwt:SigningKey' must contain at least 256 bits.");
        }

        _issuer = configuration["Jwt:Issuer"] ?? "SolarCare.Api";
        _audience = configuration["Jwt:Audience"] ?? "SolarCare.Web";
        _credentials = new SigningCredentials(
            new SymmetricSecurityKey(keyBytes),
            SecurityAlgorithms.HmacSha256);
        _timeProvider = timeProvider;
    }

    public (string Token, DateTimeOffset ExpiresAt) Issue(AuthenticatedUser user)
    {
        var now = _timeProvider.GetUtcNow();
        var expiresAt = now.Add(TokenLifetime);
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim("tenant_id", user.TenantId.ToString()),
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.FullName),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Role, user.Role.ToString())
        };
        var token = new JwtSecurityToken(
            _issuer,
            _audience,
            claims,
            now.UtcDateTime,
            expiresAt.UtcDateTime,
            _credentials);

        return (new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
    }

    public static TokenValidationParameters CreateValidationParameters(IConfiguration configuration)
    {
        var key = configuration["Jwt:SigningKey"]
            ?? throw new InvalidOperationException("Configuration 'Jwt:SigningKey' is required.");
        var keyBytes = Convert.FromBase64String(key);
        if (keyBytes.Length < 32)
        {
            throw new InvalidOperationException("'Jwt:SigningKey' must contain at least 256 bits.");
        }

        return new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = configuration["Jwt:Issuer"] ?? "SolarCare.Api",
            ValidateAudience = true,
            ValidAudience = configuration["Jwt:Audience"] ?? "SolarCare.Web",
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(keyBytes),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            NameClaimType = ClaimTypes.Name,
            RoleClaimType = ClaimTypes.Role
        };
    }
}
