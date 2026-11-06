namespace SolarCare.Application.Operations;

public sealed record DashboardSummary(
    long CustomerCount,
    long InstallationCount,
    long EquipmentCount,
    long WarrantiesExpiringSoon,
    long OpenTicketCount,
    long PendingServiceOrderCount,
    long UpcomingMaintenanceCount,
    long OverdueMaintenanceCount);
