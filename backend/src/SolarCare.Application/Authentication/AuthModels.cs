using SolarCare.Domain;

namespace SolarCare.Application.Authentication;

public sealed record RegisterCommand(
    string CompanyName,
    string FullName,
    string Email,
    string Password);

public sealed record LoginCommand(string Email, string Password);

public sealed record AuthenticatedUser(
    Guid Id,
    Guid TenantId,
    string CompanyName,
    string FullName,
    string Email,
    AccessRole Role);

public sealed record IssuedAuthSession(
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAt,
    string RefreshToken,
    AuthenticatedUser User);
