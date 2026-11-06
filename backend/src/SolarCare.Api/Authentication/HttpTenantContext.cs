using System.Security.Claims;
using SolarCare.Application.Authentication;
using SolarCare.Domain;

namespace SolarCare.Api.Authentication;

public sealed class HttpTenantContext(IHttpContextAccessor httpContextAccessor) : ITenantContext
{
    public Guid TenantId => ReadGuidClaim("tenant_id");

    public Guid UserId => ReadGuidClaim(ClaimTypes.NameIdentifier);

    public AccessRole Role
    {
        get
        {
            var value = Principal.FindFirstValue(ClaimTypes.Role);
            return Enum.TryParse<AccessRole>(value, out var role) && Enum.IsDefined(role)
                ? role
                : throw new UnauthorizedAccessException("The authenticated user has no valid role.");
        }
    }

    private ClaimsPrincipal Principal =>
        httpContextAccessor.HttpContext?.User is { Identity.IsAuthenticated: true } principal
            ? principal
            : throw new UnauthorizedAccessException("An authenticated user is required.");

    private Guid ReadGuidClaim(string claimType) =>
        Guid.TryParse(Principal.FindFirstValue(claimType), out var value)
            ? value
            : throw new UnauthorizedAccessException($"The authenticated user has no valid {claimType} claim.");
}
