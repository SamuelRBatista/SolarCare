using SolarCare.Domain.Operations;

namespace SolarCare.Domain.Operations.Tickets;

public sealed class Ticket
{
    private Ticket(Guid id, TicketStatus status)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Ticket ID cannot be empty.", nameof(id));
        }

        Id = id;
        Status = status;
    }

    public Guid Id { get; }
    public TicketStatus Status { get; private set; }

    public static Ticket Create(Guid id) => new(id, TicketStatus.Open);

    public static Ticket Restore(Guid id, string status)
    {
        if (!TicketStatuses.TryParse(status, out var parsedStatus))
        {
            throw new InvalidOperationException($"Unknown persisted ticket status '{status}'.");
        }

        return new Ticket(id, parsedStatus);
    }

    public void EnsureCanUpdate()
    {
        if (Status is TicketStatus.Closed or TicketStatus.Cancelled)
        {
            throw new DomainRuleViolationException(
                $"A ticket in status '{Status}' cannot be updated.");
        }
    }

    public void ChangeStatus(string nextStatus)
    {
        if (!TicketStatuses.TryParse(nextStatus, out var next))
        {
            throw new DomainRuleViolationException($"Unknown ticket status '{nextStatus}'.");
        }

        var allowed = Status switch
        {
            TicketStatus.Open => next is TicketStatus.InProgress or TicketStatus.WaitingCustomer
                or TicketStatus.WaitingParts or TicketStatus.Cancelled,
            TicketStatus.InProgress => next is TicketStatus.WaitingCustomer or TicketStatus.WaitingParts
                or TicketStatus.Resolved or TicketStatus.Cancelled,
            TicketStatus.WaitingCustomer or TicketStatus.WaitingParts =>
                next is TicketStatus.InProgress or TicketStatus.Resolved or TicketStatus.Cancelled,
            TicketStatus.Resolved => next == TicketStatus.Closed,
            _ => false
        };

        if (!allowed)
        {
            throw new DomainRuleViolationException(
                $"A ticket cannot transition from '{Status}' to '{next}'.");
        }

        Status = next;
    }
}
