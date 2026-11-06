using SolarCare.Domain.Operations;
using SolarCare.Domain.Operations.ServiceOrders;
using SolarCare.Domain.Operations.Tickets;

namespace SolarCare.Tests;

public sealed class OperationDomainTests
{
    [Theory]
    [InlineData(TicketStatus.Open, TicketStatus.InProgress)]
    [InlineData(TicketStatus.Resolved, TicketStatus.Closed)]
    public void Ticket_ChangesStatusOnlyThroughAllowedTransitions(TicketStatus current, TicketStatus next)
    {
        var ticket = Ticket.Restore(Guid.NewGuid(), current.ToString());

        ticket.ChangeStatus(next.ToString());

        Assert.Equal(next, ticket.Status);
    }

    [Theory]
    [InlineData(TicketStatus.Open, TicketStatus.Closed)]
    [InlineData(TicketStatus.Closed, TicketStatus.Open)]
    [InlineData(TicketStatus.Cancelled, TicketStatus.InProgress)]
    public void Ticket_RejectsInvalidStatusTransitions(TicketStatus current, TicketStatus next)
    {
        var ticket = Ticket.Restore(Guid.NewGuid(), current.ToString());

        Assert.Throws<DomainRuleViolationException>(() => ticket.ChangeStatus(next.ToString()));
    }

    [Theory]
    [InlineData(TicketStatus.Closed)]
    [InlineData(TicketStatus.Cancelled)]
    public void Ticket_CannotBeUpdatedAfterTerminalStatus(TicketStatus status)
    {
        var ticket = Ticket.Restore(Guid.NewGuid(), status.ToString());

        Assert.Throws<DomainRuleViolationException>(ticket.EnsureCanUpdate);
    }

    [Fact]
    public void ServiceOrder_ScheduleUpdatesPendingAndScheduledStates()
    {
        var pending = ServiceOrder.Create(Guid.NewGuid(), scheduledAt: null);
        var scheduledAt = DateTimeOffset.UtcNow;

        Assert.Equal(ServiceOrderStatus.Scheduled, pending.UpdateSchedule(scheduledAt));
        Assert.Equal(ServiceOrderStatus.Pending, pending.UpdateSchedule(scheduledAt: null));
    }

    [Theory]
    [InlineData(ServiceOrderStatus.Pending, ServiceOrderStatus.Scheduled)]
    [InlineData(ServiceOrderStatus.Scheduled, ServiceOrderStatus.InProgress)]
    [InlineData(ServiceOrderStatus.InProgress, ServiceOrderStatus.Cancelled)]
    public void ServiceOrder_ChangesStatusOnlyThroughAllowedTransitions(
        ServiceOrderStatus current,
        ServiceOrderStatus next)
    {
        var order = ServiceOrder.Restore(Guid.NewGuid(), current.ToString());

        order.ChangeStatus(next.ToString());

        Assert.Equal(next, order.Status);
    }

    [Theory]
    [InlineData(ServiceOrderStatus.Pending, ServiceOrderStatus.Completed)]
    [InlineData(ServiceOrderStatus.Completed, ServiceOrderStatus.Cancelled)]
    [InlineData(ServiceOrderStatus.Cancelled, ServiceOrderStatus.Scheduled)]
    public void ServiceOrder_RejectsInvalidStatusTransitions(
        ServiceOrderStatus current,
        ServiceOrderStatus next)
    {
        var order = ServiceOrder.Restore(Guid.NewGuid(), current.ToString());

        Assert.Throws<DomainRuleViolationException>(() => order.ChangeStatus(next.ToString()));
    }

    [Fact]
    public void ServiceOrder_CanOnlyCompleteFromActiveStates()
    {
        var order = ServiceOrder.Restore(Guid.NewGuid(), ServiceOrderStatus.InProgress.ToString());

        order.Complete();

        Assert.Equal(ServiceOrderStatus.Completed, order.Status);
        Assert.Throws<DomainRuleViolationException>(order.Complete);
    }
}
