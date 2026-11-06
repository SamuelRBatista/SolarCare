namespace SolarCare.Application.Operations;

public interface IDashboardRepository
{
    Task<DashboardSummary> GetDashboardSummaryAsync(Guid tenantId, CancellationToken cancellationToken);
}
