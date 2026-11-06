using SolarCare.Domain.Operations;

namespace SolarCare.Domain.Operations.ServiceOrders;

public sealed class ServiceOrder
{
    private ServiceOrder(Guid id, ServiceOrderStatus status)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Service order ID cannot be empty.", nameof(id));
        }

        Id = id;
        Status = status;
    }

    public Guid Id { get; }
    public ServiceOrderStatus Status { get; private set; }

    public static ServiceOrder Create(Guid id, DateTimeOffset? scheduledAt) =>
        new(id, scheduledAt.HasValue ? ServiceOrderStatus.Scheduled : ServiceOrderStatus.Pending);

    public static ServiceOrder Restore(Guid id, string status)
    {
        if (!ServiceOrderStatuses.TryParse(status, out var parsedStatus))
        {
            throw new InvalidOperationException($"Unknown persisted service order status '{status}'.");
        }

        return new ServiceOrder(id, parsedStatus);
    }

    public ServiceOrderStatus UpdateSchedule(DateTimeOffset? scheduledAt)
    {
        EnsureCanUpdate();
        if (Status == ServiceOrderStatus.Pending && scheduledAt.HasValue)
        {
            Status = ServiceOrderStatus.Scheduled;
        }
        else if (Status == ServiceOrderStatus.Scheduled && !scheduledAt.HasValue)
        {
            Status = ServiceOrderStatus.Pending;
        }

        return Status;
    }

    public void EnsureCanUpdate()
    {
        if (Status is not (ServiceOrderStatus.Pending or ServiceOrderStatus.Scheduled or ServiceOrderStatus.InProgress))
        {
            throw new DomainRuleViolationException(
                $"A service order in status '{Status}' cannot be updated.");
        }
    }

    public void ChangeStatus(string nextStatus)
    {
        if (!ServiceOrderStatuses.TryParse(nextStatus, out var next))
        {
            throw new DomainRuleViolationException($"Unknown service order status '{nextStatus}'.");
        }

        var allowed = Status switch
        {
            ServiceOrderStatus.Pending => next is ServiceOrderStatus.Scheduled or ServiceOrderStatus.Cancelled,
            ServiceOrderStatus.Scheduled => next is ServiceOrderStatus.InProgress or ServiceOrderStatus.Cancelled,
            ServiceOrderStatus.InProgress => next == ServiceOrderStatus.Cancelled,
            _ => false
        };

        if (!allowed)
        {
            throw new DomainRuleViolationException(
                $"A service order cannot transition from '{Status}' to '{next}'.");
        }

        Status = next;
    }

    public void Complete()
    {
        if (Status is not (ServiceOrderStatus.Pending or ServiceOrderStatus.Scheduled or ServiceOrderStatus.InProgress))
        {
            throw new DomainRuleViolationException(
                "Only a pending, scheduled, or in-progress service order can be completed.");
        }

        Status = ServiceOrderStatus.Completed;
    }
}
