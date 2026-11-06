namespace SolarCare.Application.Operations;

public interface IEquipmentRepository
{
    Task<IReadOnlyList<EquipmentView>> ListEquipmentAsync(
        Guid tenantId,
        Guid? installationId,
        bool warrantiesOnly,
        int limit,
        int offset,
        CancellationToken cancellationToken);

    Task<EquipmentView?> GetEquipmentAsync(Guid tenantId, Guid id, CancellationToken cancellationToken);
    Task<EquipmentView> CreateEquipmentAsync(Guid tenantId, EquipmentInput input, CancellationToken cancellationToken);
    Task<EquipmentView?> UpdateEquipmentAsync(Guid tenantId, Guid id, EquipmentInput input, CancellationToken cancellationToken);
    Task<bool> DeactivateEquipmentAsync(Guid tenantId, Guid id, CancellationToken cancellationToken);
}
