namespace SolarCare.Application.Operations;

public interface IServiceOrderRepository
{
    Task<IReadOnlyList<ServiceOrderView>> ListServiceOrdersAsync(
        Guid tenantId,
        string? status,
        Guid? assignedTechnicianId,
        int limit,
        int offset,
        CancellationToken cancellationToken);

    Task<ServiceOrderView?> GetServiceOrderAsync(
        Guid tenantId,
        Guid id,
        Guid? assignedTechnicianId,
        CancellationToken cancellationToken);

    Task<ServiceOrderView> CreateServiceOrderAsync(
        Guid tenantId,
        Guid actorUserId,
        ServiceOrderInput input,
        CancellationToken cancellationToken);

    Task<ServiceOrderView?> UpdateServiceOrderAsync(
        Guid tenantId,
        Guid actorUserId,
        Guid id,
        ServiceOrderUpdateInput input,
        CancellationToken cancellationToken);

    Task<ServiceOrderView?> ChangeServiceOrderStatusAsync(
        Guid tenantId,
        Guid actorUserId,
        Guid id,
        Guid? assignedTechnicianId,
        ServiceOrderStatusInput input,
        CancellationToken cancellationToken);

    Task<ServiceOrderView?> CompleteServiceOrderAsync(
        Guid tenantId,
        Guid actorUserId,
        Guid id,
        Guid? assignedTechnicianId,
        ServiceOrderCompletionInput input,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<ServiceOrderHistoryView>?> ListServiceOrderHistoryAsync(
        Guid tenantId,
        Guid id,
        Guid? assignedTechnicianId,
        CancellationToken cancellationToken);
}
