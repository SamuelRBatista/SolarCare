namespace SolarCare.Application.Operations;

public interface ICustomerRepository
{
    Task<IReadOnlyList<CustomerView>> ListCustomersAsync(
        Guid tenantId,
        string? search,
        int limit,
        int offset,
        CancellationToken cancellationToken);

    Task<CustomerView?> GetCustomerAsync(Guid tenantId, Guid id, CancellationToken cancellationToken);
    Task<CustomerView> CreateCustomerAsync(Guid tenantId, CustomerInput input, CancellationToken cancellationToken);
    Task<CustomerView?> UpdateCustomerAsync(Guid tenantId, Guid id, CustomerInput input, CancellationToken cancellationToken);
    Task<bool> DeactivateCustomerAsync(Guid tenantId, Guid id, CancellationToken cancellationToken);
}
