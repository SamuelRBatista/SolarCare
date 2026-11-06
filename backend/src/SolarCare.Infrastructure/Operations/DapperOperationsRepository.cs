using Dapper;
using Npgsql;
using SolarCare.Application.Operations;

namespace SolarCare.Infrastructure.Operations;

public sealed partial class DapperOperationsRepository(
    NpgsqlDataSource dataSource,
    ITaxIdentifierProtector taxIdentifierProtector,
    TimeProvider timeProvider) : IOperationsRepository
{
    private async Task<IReadOnlyList<T>> QueryListAsync<T>(
        string sql,
        object parameters,
        CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var result = await connection.QueryAsync<T>(
            new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
        return result.AsList();
    }

    private async Task<T?> QuerySingleAsync<T>(
        string sql,
        object parameters,
        CancellationToken cancellationToken)
        where T : class
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<T>(
            new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
    }

    private static string? NormalizeState(string? state) =>
        string.IsNullOrWhiteSpace(state) ? null : state.Trim().ToUpperInvariant();

    private static string? NormalizeEmail(string? email) =>
        string.IsNullOrWhiteSpace(email) ? null : email.Trim().ToLowerInvariant();
}
