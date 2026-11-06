namespace SolarCare.Application.Operations;

public interface IOperationsRepository :
    ICustomerRepository,
    IAssetRepository,
    ITicketRepository,
    IServiceOrderRepository,
    IMaintenanceRepository,
    IDashboardRepository
{
}
