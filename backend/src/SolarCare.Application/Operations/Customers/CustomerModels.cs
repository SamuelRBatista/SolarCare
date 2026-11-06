namespace SolarCare.Application.Operations;

public sealed record CustomerInput(
    string Name,
    string Kind,
    string? TaxIdentifier,
    string? Phone,
    string? Email,
    string? Address,
    string? City,
    string? State,
    string? Notes);

public sealed record CustomerView(
    Guid Id,
    string Name,
    string Kind,
    string? TaxIdentifier,
    string? Phone,
    string? Email,
    string? Address,
    string? City,
    string? State,
    string? Notes,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
