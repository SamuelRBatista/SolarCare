namespace SolarCare.Application.Operations;

public sealed record ServiceOrderInput(
    Guid? TicketId,
    Guid InstallationId,
    Guid? TechnicianUserId,
    DateTimeOffset? ScheduledAt,
    string? Address,
    string Description,
    IReadOnlyList<ChecklistItem> Checklist,
    IReadOnlyList<MaterialItem> Materials);

public sealed record ServiceOrderUpdateInput(
    Guid? TechnicianUserId,
    DateTimeOffset? ScheduledAt,
    string? Address,
    string Description);

public sealed record ChecklistItem(string Description, bool IsComplete);

public sealed record MaterialItem(string Description, decimal Quantity, string? Unit);

public sealed record ServiceOrderCompletionInput(
    decimal? HoursWorked,
    string? TechnicalReport,
    IReadOnlyList<ChecklistItem> Checklist,
    IReadOnlyList<MaterialItem> Materials);

public sealed record ServiceOrderStatusInput(string Status, string? Note);

public sealed record ServiceOrderView(
    Guid Id,
    Guid? TicketId,
    Guid InstallationId,
    Guid? TechnicianUserId,
    DateTimeOffset? ScheduledAt,
    string? Address,
    string Description,
    string Status,
    IReadOnlyList<ChecklistItem> Checklist,
    IReadOnlyList<MaterialItem> Materials,
    decimal? HoursWorked,
    string? TechnicalReport,
    DateTimeOffset? CompletedAt,
    Guid CreatedByUserId,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record ServiceOrderHistoryView(
    Guid Id,
    Guid ActorUserId,
    string Action,
    string? OldStatus,
    string? NewStatus,
    string? Note,
    DateTimeOffset CreatedAt);
