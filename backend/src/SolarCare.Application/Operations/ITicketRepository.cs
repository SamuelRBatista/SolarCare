namespace SolarCare.Application.Operations;

public interface ITicketRepository
{
    Task<IReadOnlyList<TicketView>> ListTicketsAsync(
        Guid tenantId,
        string? status,
        Guid? customerId,
        int limit,
        int offset,
        CancellationToken cancellationToken);

    Task<TicketView?> GetTicketAsync(Guid tenantId, Guid id, CancellationToken cancellationToken);
    Task<TicketView> CreateTicketAsync(Guid tenantId, Guid actorUserId, TicketInput input, CancellationToken cancellationToken);
    Task<TicketView?> UpdateTicketAsync(Guid tenantId, Guid actorUserId, Guid id, TicketInput input, CancellationToken cancellationToken);
    Task<TicketView?> ChangeTicketStatusAsync(Guid tenantId, Guid actorUserId, Guid id, TicketStatusInput input, CancellationToken cancellationToken);
    Task<IReadOnlyList<TicketHistoryView>?> ListTicketHistoryAsync(Guid tenantId, Guid id, CancellationToken cancellationToken);
}
