using Dapper;
using SolarCare.Application.Operations;

namespace SolarCare.Infrastructure.Operations;

public sealed partial class DapperOperationsRepository
{
    public async Task<IReadOnlyList<InstallationView>> ListInstallationsAsync(
        Guid tenantId,
        Guid? customerId,
        int limit,
        int offset,
        CancellationToken cancellationToken)
    {
        var rows = await QueryListAsync<InstallationRow>(
            """
            SELECT id AS "Id", customer_id AS "CustomerId", name AS "Name",
                   address AS "Address", city AS "City", state AS "State",
                   installed_at AS "InstalledAt", capacity_kwp AS "CapacityKwp",
                   module_count AS "ModuleCount", module_manufacturer AS "ModuleManufacturer",
                   module_model AS "ModuleModel", inverter_manufacturer AS "InverterManufacturer",
                   inverter_model AS "InverterModel", integrator_name AS "IntegratorName",
                   technical_responsible AS "TechnicalResponsible", notes AS "Notes",
                   is_active AS "IsActive", created_at AS "CreatedAt", updated_at AS "UpdatedAt"
            FROM installations
            WHERE tenant_id = @TenantId AND is_active = TRUE
              AND (@CustomerId IS NULL OR customer_id = @CustomerId)
            ORDER BY lower(name), id
            LIMIT @Limit OFFSET @Offset;
            """,
            new { TenantId = tenantId, CustomerId = customerId, Limit = limit, Offset = offset },
            cancellationToken);
        return rows.Select(ToInstallationView).ToArray();
    }

    public async Task<InstallationView?> GetInstallationAsync(
        Guid tenantId,
        Guid id,
        CancellationToken cancellationToken)
    {
        var row = await QuerySingleAsync<InstallationRow>(
            """
            SELECT id AS "Id", customer_id AS "CustomerId", name AS "Name",
                   address AS "Address", city AS "City", state AS "State",
                   installed_at AS "InstalledAt", capacity_kwp AS "CapacityKwp",
                   module_count AS "ModuleCount", module_manufacturer AS "ModuleManufacturer",
                   module_model AS "ModuleModel", inverter_manufacturer AS "InverterManufacturer",
                   inverter_model AS "InverterModel", integrator_name AS "IntegratorName",
                   technical_responsible AS "TechnicalResponsible", notes AS "Notes",
                   is_active AS "IsActive", created_at AS "CreatedAt", updated_at AS "UpdatedAt"
            FROM installations
            WHERE tenant_id = @TenantId AND id = @Id AND is_active = TRUE;
            """,
            new { TenantId = tenantId, Id = id },
            cancellationToken);
        return row is null ? null : ToInstallationView(row);
    }

    public async Task<InstallationView> CreateInstallationAsync(
        Guid tenantId,
        InstallationInput input,
        CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO installations (
                id, tenant_id, customer_id, name, address, city, state, installed_at,
                capacity_kwp, module_count, module_manufacturer, module_model,
                inverter_manufacturer, inverter_model, integrator_name,
                technical_responsible, notes
            )
            SELECT @Id, @TenantId, c.id, @Name, @Address, @City, @State,
                   CAST(@InstalledAt AS date), @CapacityKwp, @ModuleCount,
                   @ModuleManufacturer, @ModuleModel, @InverterManufacturer,
                   @InverterModel, @IntegratorName, @TechnicalResponsible, @Notes
            FROM customers c
            WHERE c.tenant_id = @TenantId AND c.id = @CustomerId AND c.is_active = TRUE
            RETURNING id AS "Id", customer_id AS "CustomerId", name AS "Name",
                      address AS "Address", city AS "City", state AS "State",
                      installed_at AS "InstalledAt", capacity_kwp AS "CapacityKwp",
                      module_count AS "ModuleCount", module_manufacturer AS "ModuleManufacturer",
                      module_model AS "ModuleModel", inverter_manufacturer AS "InverterManufacturer",
                      inverter_model AS "InverterModel", integrator_name AS "IntegratorName",
                      technical_responsible AS "TechnicalResponsible", notes AS "Notes",
                      is_active AS "IsActive", created_at AS "CreatedAt", updated_at AS "UpdatedAt";
            """;
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var result = await connection.QuerySingleOrDefaultAsync<InstallationRow>(
            new CommandDefinition(
                sql,
                InstallationParameters(tenantId, Guid.NewGuid(), input),
                cancellationToken: cancellationToken));
        return result is null
            ? throw new RelatedResourceNotFoundException("Customer")
            : ToInstallationView(result);
    }

    public async Task<InstallationView?> UpdateInstallationAsync(
        Guid tenantId,
        Guid id,
        InstallationInput input,
        CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE installations i
            SET customer_id = c.id, name = @Name, address = @Address, city = @City,
                state = @State, installed_at = CAST(@InstalledAt AS date),
                capacity_kwp = @CapacityKwp, module_count = @ModuleCount,
                module_manufacturer = @ModuleManufacturer, module_model = @ModuleModel,
                inverter_manufacturer = @InverterManufacturer, inverter_model = @InverterModel,
                integrator_name = @IntegratorName, technical_responsible = @TechnicalResponsible,
                notes = @Notes, updated_at = now()
            FROM customers c
            WHERE i.tenant_id = @TenantId AND i.id = @Id AND i.is_active = TRUE
              AND c.tenant_id = @TenantId AND c.id = @CustomerId AND c.is_active = TRUE
            RETURNING i.id AS "Id", i.customer_id AS "CustomerId", i.name AS "Name",
                      i.address AS "Address", i.city AS "City", i.state AS "State",
                      i.installed_at AS "InstalledAt", i.capacity_kwp AS "CapacityKwp",
                      i.module_count AS "ModuleCount", i.module_manufacturer AS "ModuleManufacturer",
                      i.module_model AS "ModuleModel", i.inverter_manufacturer AS "InverterManufacturer",
                      i.inverter_model AS "InverterModel", i.integrator_name AS "IntegratorName",
                      i.technical_responsible AS "TechnicalResponsible", i.notes AS "Notes",
                      i.is_active AS "IsActive", i.created_at AS "CreatedAt", i.updated_at AS "UpdatedAt";
            """;
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var result = await connection.QuerySingleOrDefaultAsync<InstallationRow>(
            new CommandDefinition(
                sql,
                InstallationParameters(tenantId, id, input),
                cancellationToken: cancellationToken));
        if (result is null &&
            await GetInstallationAsync(tenantId, id, cancellationToken) is not null)
        {
            throw new RelatedResourceNotFoundException("Customer");
        }

        return result is null ? null : ToInstallationView(result);
    }

    public async Task<bool> DeactivateInstallationAsync(
        Guid tenantId,
        Guid id,
        CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE installations SET is_active = FALSE, updated_at = now()
            WHERE tenant_id = @TenantId AND id = @Id AND is_active = TRUE;
            """;
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        return await connection.ExecuteAsync(new CommandDefinition(
            sql, new { TenantId = tenantId, Id = id }, cancellationToken: cancellationToken)) == 1;
    }

    public async Task<IReadOnlyList<EquipmentView>> ListEquipmentAsync(
        Guid tenantId,
        Guid? installationId,
        bool warrantiesOnly,
        int limit,
        int offset,
        CancellationToken cancellationToken)
    {
        var rows = await QueryListAsync<EquipmentRow>(
            """
            SELECT id AS "Id", installation_id AS "InstallationId", category AS "Category",
                   manufacturer AS "Manufacturer", model AS "Model", serial_number AS "SerialNumber",
                   acquired_at AS "AcquiredAt", installed_at AS "InstalledAt",
                   warranty_expires_at AS "WarrantyExpiresAt", status AS "Status", notes AS "Notes",
                   is_active AS "IsActive", created_at AS "CreatedAt", updated_at AS "UpdatedAt"
            FROM equipment
            WHERE tenant_id = @TenantId AND is_active = TRUE
              AND (@InstallationId IS NULL OR installation_id = @InstallationId)
              AND (NOT @WarrantiesOnly OR warranty_expires_at BETWEEN CURRENT_DATE AND CURRENT_DATE + 90)
            ORDER BY warranty_expires_at NULLS LAST, manufacturer, model, id
            LIMIT @Limit OFFSET @Offset;
            """,
            new { TenantId = tenantId, InstallationId = installationId, WarrantiesOnly = warrantiesOnly, Limit = limit, Offset = offset },
            cancellationToken);
        return rows.Select(ToEquipmentView).ToArray();
    }

    public async Task<EquipmentView?> GetEquipmentAsync(
        Guid tenantId,
        Guid id,
        CancellationToken cancellationToken)
    {
        var row = await QuerySingleAsync<EquipmentRow>(
            """
            SELECT id AS "Id", installation_id AS "InstallationId", category AS "Category",
                   manufacturer AS "Manufacturer", model AS "Model", serial_number AS "SerialNumber",
                   acquired_at AS "AcquiredAt", installed_at AS "InstalledAt",
                   warranty_expires_at AS "WarrantyExpiresAt", status AS "Status", notes AS "Notes",
                   is_active AS "IsActive", created_at AS "CreatedAt", updated_at AS "UpdatedAt"
            FROM equipment
            WHERE tenant_id = @TenantId AND id = @Id AND is_active = TRUE;
            """,
            new { TenantId = tenantId, Id = id },
            cancellationToken);
        return row is null ? null : ToEquipmentView(row);
    }

    public async Task<EquipmentView> CreateEquipmentAsync(
        Guid tenantId,
        EquipmentInput input,
        CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO equipment (
                id, tenant_id, installation_id, category, manufacturer, model, serial_number,
                acquired_at, installed_at, warranty_expires_at, status, notes
            )
            SELECT @Id, @TenantId, i.id, @Category, @Manufacturer, @Model, @SerialNumber,
                   CAST(@AcquiredAt AS date), CAST(@InstalledAt AS date),
                   CAST(@WarrantyExpiresAt AS date), @Status, @Notes
            FROM installations i
            WHERE i.tenant_id = @TenantId AND i.id = @InstallationId AND i.is_active = TRUE
            RETURNING id AS "Id", installation_id AS "InstallationId", category AS "Category",
                      manufacturer AS "Manufacturer", model AS "Model", serial_number AS "SerialNumber",
                      acquired_at AS "AcquiredAt", installed_at AS "InstalledAt",
                      warranty_expires_at AS "WarrantyExpiresAt", status AS "Status", notes AS "Notes",
                      is_active AS "IsActive", created_at AS "CreatedAt", updated_at AS "UpdatedAt";
            """;
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var result = await connection.QuerySingleOrDefaultAsync<EquipmentRow>(
            new CommandDefinition(sql, EquipmentParameters(tenantId, Guid.NewGuid(), input), cancellationToken: cancellationToken));
        return result is null
            ? throw new RelatedResourceNotFoundException("Installation")
            : ToEquipmentView(result);
    }

    public async Task<EquipmentView?> UpdateEquipmentAsync(
        Guid tenantId,
        Guid id,
        EquipmentInput input,
        CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE equipment e
            SET installation_id = i.id, category = @Category, manufacturer = @Manufacturer,
                model = @Model, serial_number = @SerialNumber,
                acquired_at = CAST(@AcquiredAt AS date), installed_at = CAST(@InstalledAt AS date),
                warranty_expires_at = CAST(@WarrantyExpiresAt AS date), status = @Status,
                notes = @Notes, updated_at = now()
            FROM installations i
            WHERE e.tenant_id = @TenantId AND e.id = @Id AND e.is_active = TRUE
              AND i.tenant_id = @TenantId AND i.id = @InstallationId AND i.is_active = TRUE
            RETURNING e.id AS "Id", e.installation_id AS "InstallationId", e.category AS "Category",
                      e.manufacturer AS "Manufacturer", e.model AS "Model", e.serial_number AS "SerialNumber",
                      e.acquired_at AS "AcquiredAt", e.installed_at AS "InstalledAt",
                      e.warranty_expires_at AS "WarrantyExpiresAt", e.status AS "Status", e.notes AS "Notes",
                      e.is_active AS "IsActive", e.created_at AS "CreatedAt", e.updated_at AS "UpdatedAt";
            """;
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var result = await connection.QuerySingleOrDefaultAsync<EquipmentRow>(
            new CommandDefinition(sql, EquipmentParameters(tenantId, id, input), cancellationToken: cancellationToken));
        if (result is null &&
            await GetEquipmentAsync(tenantId, id, cancellationToken) is not null)
        {
            throw new RelatedResourceNotFoundException("Installation");
        }

        return result is null ? null : ToEquipmentView(result);
    }

    public async Task<bool> DeactivateEquipmentAsync(
        Guid tenantId,
        Guid id,
        CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE equipment SET is_active = FALSE, updated_at = now()
            WHERE tenant_id = @TenantId AND id = @Id AND is_active = TRUE;
            """;
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        return await connection.ExecuteAsync(new CommandDefinition(
            sql, new { TenantId = tenantId, Id = id }, cancellationToken: cancellationToken)) == 1;
    }

    private static object InstallationParameters(Guid tenantId, Guid id, InstallationInput input) =>
        new
        {
            TenantId = tenantId,
            Id = id,
            input.CustomerId,
            Name = input.Name.Trim(),
            Address = CleanOptional(input.Address),
            City = CleanOptional(input.City),
            State = NormalizeState(input.State),
            input.InstalledAt,
            input.CapacityKwp,
            input.ModuleCount,
            ModuleManufacturer = CleanOptional(input.ModuleManufacturer),
            ModuleModel = CleanOptional(input.ModuleModel),
            InverterManufacturer = CleanOptional(input.InverterManufacturer),
            InverterModel = CleanOptional(input.InverterModel),
            IntegratorName = CleanOptional(input.IntegratorName),
            TechnicalResponsible = CleanOptional(input.TechnicalResponsible),
            Notes = CleanOptional(input.Notes)
        };

    private static object EquipmentParameters(Guid tenantId, Guid id, EquipmentInput input) =>
        new
        {
            TenantId = tenantId,
            Id = id,
            input.InstallationId,
            Category = input.Category.Trim(),
            Manufacturer = input.Manufacturer.Trim(),
            Model = CleanOptional(input.Model),
            SerialNumber = CleanOptional(input.SerialNumber),
            input.AcquiredAt,
            input.InstalledAt,
            input.WarrantyExpiresAt,
            input.Status,
            Notes = CleanOptional(input.Notes)
        };

    private static InstallationView ToInstallationView(InstallationRow row) =>
        new(
            row.Id,
            row.CustomerId,
            row.Name,
            row.Address,
            row.City,
            row.State,
            row.InstalledAt,
            row.CapacityKwp,
            row.ModuleCount,
            row.ModuleManufacturer,
            row.ModuleModel,
            row.InverterManufacturer,
            row.InverterModel,
            row.IntegratorName,
            row.TechnicalResponsible,
            row.Notes,
            row.IsActive,
            AsUtcOffset(row.CreatedAt),
            AsUtcOffset(row.UpdatedAt));

    private static EquipmentView ToEquipmentView(EquipmentRow row) =>
        new(
            row.Id,
            row.InstallationId,
            row.Category,
            row.Manufacturer,
            row.Model,
            row.SerialNumber,
            row.AcquiredAt,
            row.InstalledAt,
            row.WarrantyExpiresAt,
            row.Status,
            row.Notes,
            row.IsActive,
            AsUtcOffset(row.CreatedAt),
            AsUtcOffset(row.UpdatedAt));

    private static DateTimeOffset AsUtcOffset(DateTime value) =>
        new(DateTime.SpecifyKind(value, DateTimeKind.Utc));

    private sealed class InstallationRow
    {
        public Guid Id { get; init; }
        public Guid CustomerId { get; init; }
        public string Name { get; init; } = string.Empty;
        public string? Address { get; init; }
        public string? City { get; init; }
        public string? State { get; init; }
        public DateOnly? InstalledAt { get; init; }
        public decimal? CapacityKwp { get; init; }
        public int? ModuleCount { get; init; }
        public string? ModuleManufacturer { get; init; }
        public string? ModuleModel { get; init; }
        public string? InverterManufacturer { get; init; }
        public string? InverterModel { get; init; }
        public string? IntegratorName { get; init; }
        public string? TechnicalResponsible { get; init; }
        public string? Notes { get; init; }
        public bool IsActive { get; init; }
        public DateTime CreatedAt { get; init; }
        public DateTime UpdatedAt { get; init; }
    }

    private sealed class EquipmentRow
    {
        public Guid Id { get; init; }
        public Guid InstallationId { get; init; }
        public string Category { get; init; } = string.Empty;
        public string Manufacturer { get; init; } = string.Empty;
        public string? Model { get; init; }
        public string? SerialNumber { get; init; }
        public DateOnly? AcquiredAt { get; init; }
        public DateOnly? InstalledAt { get; init; }
        public DateOnly? WarrantyExpiresAt { get; init; }
        public string Status { get; init; } = string.Empty;
        public string? Notes { get; init; }
        public bool IsActive { get; init; }
        public DateTime CreatedAt { get; init; }
        public DateTime UpdatedAt { get; init; }
    }
}
