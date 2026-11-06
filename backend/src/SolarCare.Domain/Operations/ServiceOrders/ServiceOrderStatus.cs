namespace SolarCare.Domain.Operations.ServiceOrders;

public enum ServiceOrderStatus
{
    Pending,
    Scheduled,
    InProgress,
    Completed,
    Cancelled
}

public static class ServiceOrderStatuses
{
    public static bool TryParse(string? value, out ServiceOrderStatus status)
    {
        if (Enum.TryParse(value, ignoreCase: false, out status) && Enum.IsDefined(status))
        {
            return true;
        }

        status = default;
        return false;
    }
}
