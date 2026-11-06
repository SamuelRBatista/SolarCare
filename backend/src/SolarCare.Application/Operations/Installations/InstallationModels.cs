namespace SolarCare.Application.Operations;

public sealed record InstallationInput(
    Guid CustomerId,
    string Name,
    string? Address,
    string? City,
    string? State,
    DateOnly? InstalledAt,
    decimal? CapacityKwp,
    int? ModuleCount,
    string? ModuleManufacturer,
    string? ModuleModel,
    string? InverterManufacturer,
    string? InverterModel,
    string? IntegratorName,
    string? TechnicalResponsible,
    string? Notes);

public sealed record InstallationView(
    Guid Id,
    Guid CustomerId,
    string Name,
    string? Address,
    string? City,
    string? State,
    DateOnly? InstalledAt,
    decimal? CapacityKwp,
    int? ModuleCount,
    string? ModuleManufacturer,
    string? ModuleModel,
    string? InverterManufacturer,
    string? InverterModel,
    string? IntegratorName,
    string? TechnicalResponsible,
    string? Notes,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
