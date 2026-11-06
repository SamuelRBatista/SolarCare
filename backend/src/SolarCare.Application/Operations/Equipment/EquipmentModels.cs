namespace SolarCare.Application.Operations;

public sealed record EquipmentInput(
    Guid InstallationId,
    string Category,
    string Manufacturer,
    string? Model,
    string? SerialNumber,
    DateOnly? AcquiredAt,
    DateOnly? InstalledAt,
    DateOnly? WarrantyExpiresAt,
    string Status,
    string? Notes);

public sealed record EquipmentView(
    Guid Id,
    Guid InstallationId,
    string Category,
    string Manufacturer,
    string? Model,
    string? SerialNumber,
    DateOnly? AcquiredAt,
    DateOnly? InstalledAt,
    DateOnly? WarrantyExpiresAt,
    string Status,
    string? Notes,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
