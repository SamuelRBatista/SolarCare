namespace SolarCare.Domain.Operations.Tickets;

public enum TicketStatus
{
    Open,
    InProgress,
    WaitingCustomer,
    WaitingParts,
    Resolved,
    Closed,
    Cancelled
}

public static class TicketStatuses
{
    public static bool TryParse(string? value, out TicketStatus status)
    {
        if (Enum.TryParse(value, ignoreCase: false, out status) && Enum.IsDefined(status))
        {
            return true;
        }

        status = default;
        return false;
    }
}
