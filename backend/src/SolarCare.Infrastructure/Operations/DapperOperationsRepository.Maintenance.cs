using Dapper;
using SolarCare.Application.Operations;

namespace SolarCare.Infrastructure.Operations;

public sealed partial class DapperOperationsRepository
{
    public Task<IReadOnlyList<MaintenancePlanView>> ListMaintenancePlansAsync(
        Guid tenantId,
        bool includeInactive,
        int limit,
        int offset,
        CancellationToken cancellationToken) =>
        QueryListAsync<MaintenancePlanView>(
            """
            SELECT id AS "Id", installation_id AS "InstallationId", name AS "Name",
                   interval_days AS "IntervalDays", next_due_at AS "NextDueAt",
                   is_active AS "IsActive", notes AS "Notes",
                   created_at AS "CreatedAt", updated_at AS "UpdatedAt"
            FROM maintenance_plans
            WHERE tenant_id = @TenantId AND (@IncludeInactive OR is_active = TRUE)
            ORDER BY next_due_at, name, id
            LIMIT @Limit OFFSET @Offset;
            """,
            new { TenantId = tenantId, IncludeInactive = includeInactive, Limit = limit, Offset = offset },
            cancellationToken);

    public Task<MaintenancePlanView?> GetMaintenancePlanAsync(
        Guid tenantId,
        Guid id,
        CancellationToken cancellationToken) =>
        QuerySingleAsync<MaintenancePlanView>(
            """
            SELECT id AS "Id", installation_id AS "InstallationId", name AS "Name",
                   interval_days AS "IntervalDays", next_due_at AS "NextDueAt",
                   is_active AS "IsActive", notes AS "Notes",
                   created_at AS "CreatedAt", updated_at AS "UpdatedAt"
            FROM maintenance_plans
            WHERE tenant_id = @TenantId AND id = @Id;
            """,
            new { TenantId = tenantId, Id = id },
            cancellationToken);

    public async Task<MaintenancePlanView> CreateMaintenancePlanAsync(
        Guid tenantId,
        MaintenancePlanInput input,
        CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO maintenance_plans (
                id, tenant_id, installation_id, name, interval_days, next_due_at, notes
            )
            SELECT @Id, @TenantId, i.id, @Name, @IntervalDays,
                   CAST(@NextDueAt AS date), @Notes
            FROM installations i
            WHERE i.tenant_id = @TenantId AND i.id = @InstallationId AND i.is_active = TRUE
            RETURNING id AS "Id", installation_id AS "InstallationId", name AS "Name",
                      interval_days AS "IntervalDays", next_due_at AS "NextDueAt",
                      is_active AS "IsActive", notes AS "Notes",
                      created_at AS "CreatedAt", updated_at AS "UpdatedAt";
            """;
        var result = await QuerySingleAsync<MaintenancePlanView>(
            sql, MaintenanceParameters(tenantId, Guid.NewGuid(), input), cancellationToken);
        return result ?? throw new RelatedResourceNotFoundException("Installation");
    }

    public async Task<MaintenancePlanView?> UpdateMaintenancePlanAsync(
        Guid tenantId,
        Guid id,
        MaintenancePlanInput input,
        CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE maintenance_plans p
            SET installation_id = i.id, name = @Name, interval_days = @IntervalDays,
                next_due_at = CAST(@NextDueAt AS date), notes = @Notes, updated_at = now()
            FROM installations i
            WHERE p.tenant_id = @TenantId AND p.id = @Id AND p.is_active = TRUE
              AND i.tenant_id = @TenantId AND i.id = @InstallationId AND i.is_active = TRUE
            RETURNING p.id AS "Id", p.installation_id AS "InstallationId", p.name AS "Name",
                      p.interval_days AS "IntervalDays", p.next_due_at AS "NextDueAt",
                      p.is_active AS "IsActive", p.notes AS "Notes",
                      p.created_at AS "CreatedAt", p.updated_at AS "UpdatedAt";
            """;
        var result = await QuerySingleAsync<MaintenancePlanView>(
            sql, MaintenanceParameters(tenantId, id, input), cancellationToken);
        if (result is null &&
            await GetMaintenancePlanAsync(tenantId, id, cancellationToken) is not null)
        {
            throw new RelatedResourceNotFoundException("Installation");
        }

        return result;
    }

    public async Task<bool> DeactivateMaintenancePlanAsync(
        Guid tenantId,
        Guid id,
        CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE maintenance_plans SET is_active = FALSE, updated_at = now()
            WHERE tenant_id = @TenantId AND id = @Id AND is_active = TRUE;
            """;
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        return await connection.ExecuteAsync(new CommandDefinition(
            sql, new { TenantId = tenantId, Id = id }, cancellationToken: cancellationToken)) == 1;
    }

    public async Task<MaintenanceRecordView?> ExecuteMaintenanceAsync(
        Guid tenantId,
        Guid actorUserId,
        Guid id,
        MaintenanceExecutionInput input,
        CancellationToken cancellationToken)
    {
        const string sql = """
            WITH updated_plan AS (
                UPDATE maintenance_plans
                SET next_due_at = CAST(@PerformedAt AS date) + interval_days,
                    updated_at = @PerformedAt
                WHERE tenant_id = @TenantId AND id = @Id AND is_active = TRUE
                RETURNING id, installation_id
            )
            INSERT INTO maintenance_records (
                id, tenant_id, maintenance_plan_id, installation_id, service_order_id,
                performed_by_user_id, performed_at, notes
            )
            SELECT @RecordId, @TenantId, p.id, p.installation_id, @ServiceOrderId,
                   @ActorUserId, @PerformedAt, @Notes
            FROM updated_plan p
            WHERE @ServiceOrderId IS NULL OR EXISTS (
                SELECT 1 FROM service_orders o
                WHERE o.tenant_id = @TenantId AND o.id = @ServiceOrderId
                  AND o.installation_id = p.installation_id
            )
            RETURNING id AS "Id", maintenance_plan_id AS "MaintenancePlanId",
                      installation_id AS "InstallationId", service_order_id AS "ServiceOrderId",
                      performed_by_user_id AS "PerformedByUserId", performed_at AS "PerformedAt",
                      notes AS "Notes", created_at AS "CreatedAt";
            """;
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var result = await connection.QuerySingleOrDefaultAsync<MaintenanceRecordView>(
            new CommandDefinition(
                sql,
                new
                {
                    TenantId = tenantId,
                    Id = id,
                    RecordId = Guid.NewGuid(),
                    ServiceOrderId = input.ServiceOrderId,
                    ActorUserId = actorUserId,
                    input.PerformedAt,
                    Notes = CleanOptional(input.Notes)
                },
                transaction,
                cancellationToken: cancellationToken));
        if (result is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return null;
        }

        await transaction.CommitAsync(cancellationToken);
        return result;
    }

    public async Task<IReadOnlyList<MaintenanceRecordView>?> ListMaintenanceHistoryAsync(
        Guid tenantId,
        Guid id,
        CancellationToken cancellationToken)
    {
        if (await GetMaintenancePlanAsync(tenantId, id, cancellationToken) is null)
        {
            return null;
        }

        return await QueryListAsync<MaintenanceRecordView>(
            """
            SELECT id AS "Id", maintenance_plan_id AS "MaintenancePlanId",
                   installation_id AS "InstallationId", service_order_id AS "ServiceOrderId",
                   performed_by_user_id AS "PerformedByUserId", performed_at AS "PerformedAt",
                   notes AS "Notes", created_at AS "CreatedAt"
            FROM maintenance_records
            WHERE tenant_id = @TenantId AND maintenance_plan_id = @Id
            ORDER BY performed_at DESC, id;
            """,
            new { TenantId = tenantId, Id = id },
            cancellationToken);
    }

    public async Task<DashboardSummary> GetDashboardSummaryAsync(
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                (SELECT count(*) FROM customers
                 WHERE tenant_id = @TenantId AND is_active = TRUE) AS "CustomerCount",
                (SELECT count(*) FROM installations
                 WHERE tenant_id = @TenantId AND is_active = TRUE) AS "InstallationCount",
                (SELECT count(*) FROM equipment
                 WHERE tenant_id = @TenantId AND is_active = TRUE) AS "EquipmentCount",
                (SELECT count(*) FROM equipment
                 WHERE tenant_id = @TenantId AND is_active = TRUE
                   AND warranty_expires_at BETWEEN CURRENT_DATE AND CURRENT_DATE + 90) AS "WarrantiesExpiringSoon",
                (SELECT count(*) FROM service_tickets
                 WHERE tenant_id = @TenantId
                   AND status IN ('Open', 'InProgress', 'WaitingCustomer', 'WaitingParts')) AS "OpenTicketCount",
                (SELECT count(*) FROM service_orders
                 WHERE tenant_id = @TenantId
                   AND status IN ('Pending', 'Scheduled', 'InProgress')) AS "PendingServiceOrderCount",
                (SELECT count(*) FROM maintenance_plans
                 WHERE tenant_id = @TenantId AND is_active = TRUE
                   AND next_due_at BETWEEN CURRENT_DATE AND CURRENT_DATE + 30) AS "UpcomingMaintenanceCount",
                (SELECT count(*) FROM maintenance_plans
                 WHERE tenant_id = @TenantId AND is_active = TRUE
                   AND next_due_at < CURRENT_DATE) AS "OverdueMaintenanceCount";
            """;
        return await QuerySingleAsync<DashboardSummary>(
            sql, new { TenantId = tenantId }, cancellationToken)
            ?? throw new InvalidOperationException("The dashboard summary query returned no result.");
    }

    private static object MaintenanceParameters(Guid tenantId, Guid id, MaintenancePlanInput input) =>
        new
        {
            TenantId = tenantId,
            Id = id,
            input.InstallationId,
            Name = input.Name.Trim(),
            input.IntervalDays,
            input.NextDueAt,
            Notes = CleanOptional(input.Notes)
        };
}
