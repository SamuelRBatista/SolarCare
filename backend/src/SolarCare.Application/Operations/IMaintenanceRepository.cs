namespace SolarCare.Application.Operations;

public interface IMaintenanceRepository
{
    Task<IReadOnlyList<MaintenancePlanView>> ListMaintenancePlansAsync(
        Guid tenantId,
        bool includeInactive,
        int limit,
        int offset,
        CancellationToken cancellationToken);

    Task<MaintenancePlanView?> GetMaintenancePlanAsync(Guid tenantId, Guid id, CancellationToken cancellationToken);
    Task<MaintenancePlanView> CreateMaintenancePlanAsync(Guid tenantId, MaintenancePlanInput input, CancellationToken cancellationToken);
    Task<MaintenancePlanView?> UpdateMaintenancePlanAsync(Guid tenantId, Guid id, MaintenancePlanInput input, CancellationToken cancellationToken);
    Task<bool> DeactivateMaintenancePlanAsync(Guid tenantId, Guid id, CancellationToken cancellationToken);
    Task<MaintenanceRecordView?> ExecuteMaintenanceAsync(Guid tenantId, Guid actorUserId, Guid id, MaintenanceExecutionInput input, CancellationToken cancellationToken);
    Task<IReadOnlyList<MaintenanceRecordView>?> ListMaintenanceHistoryAsync(Guid tenantId, Guid id, CancellationToken cancellationToken);
}
