namespace SolarCare.Application.Operations;

public sealed record MaintenancePlanInput(
    Guid InstallationId,
    string Name,
    int IntervalDays,
    DateOnly NextDueAt,
    string? Notes);

public sealed record MaintenancePlanView(
    Guid Id,
    Guid InstallationId,
    string Name,
    int IntervalDays,
    DateOnly NextDueAt,
    bool IsActive,
    string? Notes,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record MaintenanceExecutionInput(
    DateTimeOffset PerformedAt,
    Guid? ServiceOrderId,
    string? Notes);

public sealed record MaintenanceRecordView(
    Guid Id,
    Guid MaintenancePlanId,
    Guid InstallationId,
    Guid? ServiceOrderId,
    Guid PerformedByUserId,
    DateTimeOffset PerformedAt,
    string? Notes,
    DateTimeOffset CreatedAt);
