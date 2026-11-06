namespace SolarCare.Application.Operations;

public sealed record TicketInput(
    Guid CustomerId,
    Guid? InstallationId,
    Guid? EquipmentId,
    string Description,
    string Category,
    string Priority,
    Guid? AssignedUserId);

public sealed record TicketView(
    Guid Id,
    string Protocol,
    Guid CustomerId,
    Guid? InstallationId,
    Guid? EquipmentId,
    string Description,
    string Category,
    string Priority,
    string Status,
    Guid? AssignedUserId,
    Guid OpenedByUserId,
    DateTimeOffset OpenedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? ClosedAt);

public sealed record TicketStatusInput(string Status, string? Note);

public sealed record TicketHistoryView(
    Guid Id,
    Guid ActorUserId,
    string Action,
    string? OldValue,
    string? NewValue,
    string? Note,
    DateTimeOffset CreatedAt);
