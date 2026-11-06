namespace SolarCare.Application.Operations;

public interface IInstallationRepository
{
    Task<IReadOnlyList<InstallationView>> ListInstallationsAsync(
        Guid tenantId,
        Guid? customerId,
        int limit,
        int offset,
        CancellationToken cancellationToken);

    Task<InstallationView?> GetInstallationAsync(Guid tenantId, Guid id, CancellationToken cancellationToken);
    Task<InstallationView> CreateInstallationAsync(Guid tenantId, InstallationInput input, CancellationToken cancellationToken);
    Task<InstallationView?> UpdateInstallationAsync(Guid tenantId, Guid id, InstallationInput input, CancellationToken cancellationToken);
    Task<bool> DeactivateInstallationAsync(Guid tenantId, Guid id, CancellationToken cancellationToken);
}
