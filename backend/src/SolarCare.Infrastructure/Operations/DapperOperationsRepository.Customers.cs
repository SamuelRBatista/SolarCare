using Dapper;
using Npgsql;
using SolarCare.Application.Operations;
using System.Security.Cryptography;

namespace SolarCare.Infrastructure.Operations;

public sealed partial class DapperOperationsRepository
{
    public async Task<IReadOnlyList<CustomerView>> ListCustomersAsync(
        Guid tenantId,
        string? search,
        int limit,
        int offset,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT id AS "Id", name AS "Name", kind AS "Kind",
                   tax_id_ciphertext AS "TaxIdCiphertext", phone AS "Phone",
                   email AS "Email", address AS "Address", city AS "City",
                   state AS "State", notes AS "Notes", is_active AS "IsActive",
                   created_at AS "CreatedAt", updated_at AS "UpdatedAt"
            FROM customers
            WHERE tenant_id = @TenantId
              AND is_active = TRUE
              AND (@Search IS NULL OR name ILIKE '%' || @Search || '%' OR email ILIKE '%' || @Search || '%')
            ORDER BY lower(name), id
            LIMIT @Limit OFFSET @Offset;
            """;
        var rows = await QueryListAsync<CustomerRow>(
            sql,
            new { TenantId = tenantId, Search = CleanSearch(search), Limit = limit, Offset = offset },
            cancellationToken);
        return rows.Select(ToCustomerView).ToArray();
    }

    public async Task<CustomerView?> GetCustomerAsync(
        Guid tenantId,
        Guid id,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT id AS "Id", name AS "Name", kind AS "Kind",
                   tax_id_ciphertext AS "TaxIdCiphertext", phone AS "Phone",
                   email AS "Email", address AS "Address", city AS "City",
                   state AS "State", notes AS "Notes", is_active AS "IsActive",
                   created_at AS "CreatedAt", updated_at AS "UpdatedAt"
            FROM customers
            WHERE tenant_id = @TenantId AND id = @Id AND is_active = TRUE;
            """;
        var row = await QuerySingleAsync<CustomerRow>(
            sql, new { TenantId = tenantId, Id = id }, cancellationToken);
        return row is null ? null : ToCustomerView(row);
    }

    public async Task<CustomerView> CreateCustomerAsync(
        Guid tenantId,
        CustomerInput input,
        CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO customers (
                id, tenant_id, name, kind, tax_id_ciphertext, tax_id_lookup,
                phone, email, address, city, state, notes
            )
            VALUES (
                @Id, @TenantId, @Name, @Kind, @TaxIdCiphertext, @TaxIdLookup,
                @Phone, @Email, @Address, @City, @State, @Notes
            )
            RETURNING id AS "Id", name AS "Name", kind AS "Kind",
                      tax_id_ciphertext AS "TaxIdCiphertext", phone AS "Phone",
                      email AS "Email", address AS "Address", city AS "City",
                      state AS "State", notes AS "Notes", is_active AS "IsActive",
                      created_at AS "CreatedAt", updated_at AS "UpdatedAt";
            """;
        var protectedTaxId = taxIdentifierProtector.Protect(input.TaxIdentifier);
        try
        {
            await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
            var row = await connection.QuerySingleAsync<CustomerRow>(
                new CommandDefinition(
                    sql,
                    CustomerParameters(tenantId, Guid.NewGuid(), input, protectedTaxId),
                    cancellationToken: cancellationToken));
            return ToCustomerView(row);
        }
        catch (PostgresException exception) when (
            exception.SqlState == PostgresErrorCodes.UniqueViolation &&
            exception.ConstraintName == "ux_customers_tenant_tax_id")
        {
            throw new DuplicateCustomerTaxIdentifierException();
        }
        catch (CryptographicException exception)
        {
            throw new InvalidOperationException("The customer's tax identifier could not be protected.", exception);
        }
    }

    public async Task<CustomerView?> UpdateCustomerAsync(
        Guid tenantId,
        Guid id,
        CustomerInput input,
        CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE customers
            SET name = @Name, kind = @Kind,
                tax_id_ciphertext = @TaxIdCiphertext, tax_id_lookup = @TaxIdLookup,
                phone = @Phone, email = @Email, address = @Address, city = @City,
                state = @State, notes = @Notes, updated_at = now()
            WHERE tenant_id = @TenantId AND id = @Id AND is_active = TRUE
            RETURNING id AS "Id", name AS "Name", kind AS "Kind",
                      tax_id_ciphertext AS "TaxIdCiphertext", phone AS "Phone",
                      email AS "Email", address AS "Address", city AS "City",
                      state AS "State", notes AS "Notes", is_active AS "IsActive",
                      created_at AS "CreatedAt", updated_at AS "UpdatedAt";
            """;
        var protectedTaxId = taxIdentifierProtector.Protect(input.TaxIdentifier);
        try
        {
            await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
            var row = await connection.QuerySingleOrDefaultAsync<CustomerRow>(
                new CommandDefinition(
                    sql,
                    CustomerParameters(tenantId, id, input, protectedTaxId),
                    cancellationToken: cancellationToken));
            return row is null ? null : ToCustomerView(row);
        }
        catch (PostgresException exception) when (
            exception.SqlState == PostgresErrorCodes.UniqueViolation &&
            exception.ConstraintName == "ux_customers_tenant_tax_id")
        {
            throw new DuplicateCustomerTaxIdentifierException();
        }
        catch (CryptographicException exception)
        {
            throw new InvalidOperationException("The customer's tax identifier could not be protected.", exception);
        }
    }

    public async Task<bool> DeactivateCustomerAsync(
        Guid tenantId,
        Guid id,
        CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE customers SET is_active = FALSE, updated_at = now()
            WHERE tenant_id = @TenantId AND id = @Id AND is_active = TRUE;
            """;
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        return await connection.ExecuteAsync(new CommandDefinition(
            sql, new { TenantId = tenantId, Id = id }, cancellationToken: cancellationToken)) == 1;
    }

    private static object CustomerParameters(
        Guid tenantId,
        Guid id,
        CustomerInput input,
        ProtectedTaxIdentifier? taxIdentifier) =>
        new
        {
            TenantId = tenantId,
            Id = id,
            Name = input.Name.Trim(),
            Kind = input.Kind,
            TaxIdCiphertext = taxIdentifier?.Ciphertext,
            TaxIdLookup = taxIdentifier?.LookupHash,
            Phone = CleanOptional(input.Phone),
            Email = NormalizeEmail(input.Email),
            Address = CleanOptional(input.Address),
            City = CleanOptional(input.City),
            State = NormalizeState(input.State),
            Notes = CleanOptional(input.Notes)
        };

    private CustomerView ToCustomerView(CustomerRow row) =>
        new(
            row.Id,
            row.Name,
            row.Kind,
            taxIdentifierProtector.Reveal(row.TaxIdCiphertext),
            row.Phone,
            row.Email,
            row.Address,
            row.City,
            row.State,
            row.Notes,
            row.IsActive,
            row.CreatedAt,
            row.UpdatedAt);

    private static string? CleanSearch(string? search) =>
        string.IsNullOrWhiteSpace(search) ? null : search.Trim();

    private static string? CleanOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private sealed class CustomerRow
    {
        public Guid Id { get; init; }
        public string Name { get; init; } = string.Empty;
        public string Kind { get; init; } = string.Empty;
        public string? TaxIdCiphertext { get; init; }
        public string? Phone { get; init; }
        public string? Email { get; init; }
        public string? Address { get; init; }
        public string? City { get; init; }
        public string? State { get; init; }
        public string? Notes { get; init; }
        public bool IsActive { get; init; }
        public DateTimeOffset CreatedAt { get; init; }
        public DateTimeOffset UpdatedAt { get; init; }
    }
}
