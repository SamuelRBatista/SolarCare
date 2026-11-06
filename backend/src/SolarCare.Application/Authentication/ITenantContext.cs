using SolarCare.Domain;

namespace SolarCare.Application.Authentication;

public interface ITenantContext
{
    Guid TenantId { get; }
    Guid UserId { get; }
    AccessRole Role { get; }
}
