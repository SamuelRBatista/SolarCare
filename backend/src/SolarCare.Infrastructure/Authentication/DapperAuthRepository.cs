using Dapper;
using Npgsql;
using SolarCare.Application.Authentication;
using SolarCare.Domain;

namespace SolarCare.Infrastructure.Authentication;

public sealed class DapperAuthRepository(NpgsqlDataSource dataSource) : IAuthRepository
{
    public async Task<AuthenticatedUser> RegisterAsync(
        RegisterCommand command,
        string passwordHash,
        byte[] refreshTokenHash,
        DateTimeOffset refreshTokenExpiresAt,
        CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        try
        {
            var tenantId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            await connection.ExecuteAsync(new CommandDefinition(
                """
                INSERT INTO tenants (id, name)
                VALUES (@TenantId, @CompanyName);
                INSERT INTO users (id, tenant_id, full_name, email, password_hash, role)
                VALUES (@UserId, @TenantId, @FullName, @Email, @PasswordHash, @Role);
                INSERT INTO refresh_tokens (token_hash, user_id, expires_at)
                VALUES (@RefreshTokenHash, @UserId, @RefreshTokenExpiresAt);
                """,
                new
                {
                    TenantId = tenantId,
                    UserId = userId,
                    command.CompanyName,
                    command.FullName,
                    command.Email,
                    PasswordHash = passwordHash,
                    Role = AccessRole.CompanyAdmin.ToString(),
                    RefreshTokenHash = refreshTokenHash,
                    RefreshTokenExpiresAt = refreshTokenExpiresAt
                },
                transaction,
                cancellationToken: cancellationToken));

            await transaction.CommitAsync(cancellationToken);
            return new AuthenticatedUser(
                userId,
                tenantId,
                command.CompanyName,
                command.FullName,
                command.Email,
                AccessRole.CompanyAdmin);
        }
        catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            await transaction.RollbackAsync(cancellationToken);
            throw new AccountAlreadyExistsException();
        }
    }

    public async Task<(AuthenticatedUser User, string PasswordHash)?> FindByEmailAsync(
        string normalizedEmail,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT u.id AS "Id",
                   u.tenant_id AS "TenantId",
                   t.name AS "CompanyName",
                   u.full_name AS "FullName",
                   u.email AS "Email",
                   u.role AS "Role",
                   u.password_hash AS "PasswordHash"
            FROM users u
            JOIN tenants t ON t.id = u.tenant_id
            WHERE u.email = @Email
              AND u.is_active = TRUE
              AND t.is_active = TRUE;
            """;

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var row = await connection.QuerySingleOrDefaultAsync<AuthUserRow>(
            new CommandDefinition(sql, new { Email = normalizedEmail }, cancellationToken: cancellationToken));
        return row is null ? null : (row.ToUser(), row.PasswordHash);
    }

    public async Task SaveRefreshTokenAsync(
        Guid userId,
        byte[] tokenHash,
        DateTimeOffset expiresAt,
        CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO refresh_tokens (token_hash, user_id, expires_at)
            VALUES (@TokenHash, @UserId, @ExpiresAt);
            """;
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            sql,
            new { TokenHash = tokenHash, UserId = userId, ExpiresAt = expiresAt },
            cancellationToken: cancellationToken));
    }

    public async Task<AuthenticatedUser?> RotateRefreshTokenAsync(
        byte[] currentTokenHash,
        byte[] replacementTokenHash,
        DateTimeOffset replacementExpiresAt,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        const string consumeSql = """
            UPDATE refresh_tokens
            SET revoked_at = @Now
            WHERE token_hash = @TokenHash
              AND revoked_at IS NULL
              AND expires_at > @Now
            RETURNING user_id;
            """;
        const string userSql = """
            SELECT u.id AS "Id",
                   u.tenant_id AS "TenantId",
                   t.name AS "CompanyName",
                   u.full_name AS "FullName",
                   u.email AS "Email",
                   u.role AS "Role"
            FROM users u
            JOIN tenants t ON t.id = u.tenant_id
            WHERE u.id = @UserId
              AND u.is_active = TRUE
              AND t.is_active = TRUE;
            """;
        const string insertSql = """
            INSERT INTO refresh_tokens (token_hash, user_id, expires_at)
            VALUES (@TokenHash, @UserId, @ExpiresAt);
            """;

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var userId = await connection.QuerySingleOrDefaultAsync<Guid?>(
            new CommandDefinition(
                consumeSql,
                new { TokenHash = currentTokenHash, Now = now },
                transaction,
                cancellationToken: cancellationToken));
        if (userId is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return null;
        }

        var user = await connection.QuerySingleOrDefaultAsync<AuthUserRow>(
            new CommandDefinition(userSql, new { UserId = userId }, transaction, cancellationToken: cancellationToken));
        if (user is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return null;
        }

        await connection.ExecuteAsync(new CommandDefinition(
            insertSql,
            new { TokenHash = replacementTokenHash, UserId = userId, ExpiresAt = replacementExpiresAt },
            transaction,
            cancellationToken: cancellationToken));
        await transaction.CommitAsync(cancellationToken);
        return user.ToUser();
    }

    public async Task RevokeRefreshTokenAsync(
        byte[] tokenHash,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE refresh_tokens
            SET revoked_at = @Now
            WHERE token_hash = @TokenHash
              AND revoked_at IS NULL;
            """;
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            sql,
            new { TokenHash = tokenHash, Now = now },
            cancellationToken: cancellationToken));
    }

    private sealed class AuthUserRow
    {
        public Guid Id { get; init; }
        public Guid TenantId { get; init; }
        public string CompanyName { get; init; } = string.Empty;
        public string FullName { get; init; } = string.Empty;
        public string Email { get; init; } = string.Empty;
        public string Role { get; init; } = string.Empty;
        public string PasswordHash { get; init; } = string.Empty;

        public AuthenticatedUser ToUser() =>
            new(Id, TenantId, CompanyName, FullName, Email, Enum.Parse<AccessRole>(Role));
    }
}
