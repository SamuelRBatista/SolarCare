using System.Text.Json;
using Dapper;
using SolarCare.Application.Operations;
using SolarCare.Domain.Operations;
using SolarCare.Domain.Operations.ServiceOrders;

namespace SolarCare.Infrastructure.Operations;

public sealed partial class DapperOperationsRepository
{
    public async Task<IReadOnlyList<ServiceOrderView>> ListServiceOrdersAsync(
        Guid tenantId,
        string? status,
        Guid? assignedTechnicianId,
        int limit,
        int offset,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT id AS "Id", ticket_id AS "TicketId", installation_id AS "InstallationId",
                   technician_user_id AS "TechnicianUserId", scheduled_at AS "ScheduledAt",
                   address AS "Address", description AS "Description", status AS "Status",
                   checklist::text AS "ChecklistJson", materials::text AS "MaterialsJson",
                   hours_worked AS "HoursWorked", technical_report AS "TechnicalReport",
                   completed_at AS "CompletedAt", created_by_user_id AS "CreatedByUserId",
                   created_at AS "CreatedAt", updated_at AS "UpdatedAt"
            FROM service_orders
            WHERE tenant_id = @TenantId AND (@Status IS NULL OR status = @Status)
              AND (@AssignedTechnicianId IS NULL OR technician_user_id = @AssignedTechnicianId)
            ORDER BY scheduled_at NULLS LAST, created_at DESC, id
            LIMIT @Limit OFFSET @Offset;
            """;
        var rows = await QueryListAsync<ServiceOrderRow>(
            sql,
            new
            {
                TenantId = tenantId,
                Status = string.IsNullOrWhiteSpace(status) ? null : status,
                AssignedTechnicianId = assignedTechnicianId,
                Limit = limit,
                Offset = offset
            },
            cancellationToken);
        return rows.Select(ToServiceOrderView).ToArray();
    }

    public async Task<ServiceOrderView?> GetServiceOrderAsync(
        Guid tenantId,
        Guid id,
        Guid? assignedTechnicianId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT id AS "Id", ticket_id AS "TicketId", installation_id AS "InstallationId",
                   technician_user_id AS "TechnicianUserId", scheduled_at AS "ScheduledAt",
                   address AS "Address", description AS "Description", status AS "Status",
                   checklist::text AS "ChecklistJson", materials::text AS "MaterialsJson",
                   hours_worked AS "HoursWorked", technical_report AS "TechnicalReport",
                   completed_at AS "CompletedAt", created_by_user_id AS "CreatedByUserId",
                   created_at AS "CreatedAt", updated_at AS "UpdatedAt"
            FROM service_orders
            WHERE tenant_id = @TenantId AND id = @Id
              AND (@AssignedTechnicianId IS NULL OR technician_user_id = @AssignedTechnicianId);
            """;
        var row = await QuerySingleAsync<ServiceOrderRow>(
            sql, new { TenantId = tenantId, Id = id, AssignedTechnicianId = assignedTechnicianId }, cancellationToken);
        return row is null ? null : ToServiceOrderView(row);
    }

    public async Task<ServiceOrderView> CreateServiceOrderAsync(
        Guid tenantId,
        Guid actorUserId,
        ServiceOrderInput input,
        CancellationToken cancellationToken)
    {
        var id = Guid.NewGuid();
        var orderState = ServiceOrder.Create(id, input.ScheduledAt);
        const string sql = """
            INSERT INTO service_orders (
                id, tenant_id, ticket_id, installation_id, technician_user_id,
                scheduled_at, address, description, status, checklist, materials,
                created_by_user_id
            )
            SELECT @Id, @TenantId, @TicketId, i.id, @TechnicianUserId,
                   @ScheduledAt, @Address, @Description, @Status,
                   CAST(@Checklist AS jsonb), CAST(@Materials AS jsonb), @ActorUserId
            FROM installations i
            WHERE i.tenant_id = @TenantId AND i.id = @InstallationId AND i.is_active = TRUE
              AND (
                  @TicketId IS NULL OR EXISTS (
                      SELECT 1 FROM service_tickets t
                      WHERE t.tenant_id = @TenantId AND t.id = @TicketId
                        AND t.status NOT IN ('Closed', 'Cancelled')
                        AND (t.installation_id IS NULL OR t.installation_id = i.id)
                        AND EXISTS (
                            SELECT 1 FROM customers c
                            WHERE c.tenant_id = @TenantId AND c.id = t.customer_id
                              AND c.id = i.customer_id AND c.is_active = TRUE
                        )
                  )
              )
              AND (
                  @TechnicianUserId IS NULL OR EXISTS (
                      SELECT 1 FROM users u
                      WHERE u.tenant_id = @TenantId AND u.id = @TechnicianUserId AND u.is_active = TRUE
                  )
              )
            RETURNING id AS "Id", ticket_id AS "TicketId", installation_id AS "InstallationId",
                      technician_user_id AS "TechnicianUserId", scheduled_at AS "ScheduledAt",
                      address AS "Address", description AS "Description", status AS "Status",
                      checklist::text AS "ChecklistJson", materials::text AS "MaterialsJson",
                      hours_worked AS "HoursWorked", technical_report AS "TechnicalReport",
                      completed_at AS "CompletedAt", created_by_user_id AS "CreatedByUserId",
                      created_at AS "CreatedAt", updated_at AS "UpdatedAt";
            """;
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var row = await connection.QuerySingleOrDefaultAsync<ServiceOrderRow>(
            new CommandDefinition(
                sql,
                new
                {
                    Id = id,
                    TenantId = tenantId,
                    input.TicketId,
                    input.InstallationId,
                    input.TechnicianUserId,
                    input.ScheduledAt,
                    Status = orderState.Status.ToString(),
                    Address = CleanOptional(input.Address),
                    Description = input.Description.Trim(),
                    Checklist = JsonSerializer.Serialize(input.Checklist),
                    Materials = JsonSerializer.Serialize(input.Materials),
                    ActorUserId = actorUserId
                },
                transaction,
                cancellationToken: cancellationToken));
        if (row is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            throw new RelatedResourceNotFoundException("Installation, ticket, or technician");
        }

        await InsertHistoryAsync(
            connection,
            transaction,
            tenantId,
            id,
            actorUserId,
            "Created",
            null,
            orderState.Status.ToString(),
            null,
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ToServiceOrderView(row);
    }

    public async Task<ServiceOrderView?> UpdateServiceOrderAsync(
        Guid tenantId,
        Guid actorUserId,
        Guid id,
        ServiceOrderUpdateInput input,
        CancellationToken cancellationToken)
    {
        const string selectSql = """
            SELECT status AS "Status", technician_user_id AS "TechnicianUserId",
                   scheduled_at AS "ScheduledAt", address AS "Address", description AS "Description"
            FROM service_orders
            WHERE tenant_id = @TenantId AND id = @Id
            FOR UPDATE;
            """;
        const string sql = """
            UPDATE service_orders o
            SET technician_user_id = @TechnicianUserId,
                scheduled_at = @ScheduledAt,
                address = @Address,
                description = @Description,
                status = @Status,
                updated_at = @UpdatedAt
            WHERE o.tenant_id = @TenantId AND o.id = @Id
              AND o.status IN ('Pending', 'Scheduled', 'InProgress')
              AND (
                  @TechnicianUserId IS NULL OR EXISTS (
                      SELECT 1 FROM users u
                      WHERE u.tenant_id = @TenantId AND u.id = @TechnicianUserId AND u.is_active = TRUE
                  )
              )
            RETURNING o.id AS "Id", o.ticket_id AS "TicketId", o.installation_id AS "InstallationId",
                      o.technician_user_id AS "TechnicianUserId", o.scheduled_at AS "ScheduledAt",
                      o.address AS "Address", o.description AS "Description", o.status AS "Status",
                      o.checklist::text AS "ChecklistJson", o.materials::text AS "MaterialsJson",
                      o.hours_worked AS "HoursWorked", o.technical_report AS "TechnicalReport",
                      o.completed_at AS "CompletedAt", o.created_by_user_id AS "CreatedByUserId",
                      o.created_at AS "CreatedAt", o.updated_at AS "UpdatedAt";
            """;
        var address = CleanOptional(input.Address);
        var description = input.Description.Trim();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var existing = await connection.QuerySingleOrDefaultAsync<ServiceOrderUpdateState>(
            new CommandDefinition(
                selectSql,
                new { TenantId = tenantId, Id = id },
                transaction,
                cancellationToken: cancellationToken));
        if (existing is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return null;
        }

        var orderState = ServiceOrder.Restore(id, existing.Status);
        var nextStatus = orderState.UpdateSchedule(input.ScheduledAt);

        var row = await connection.QuerySingleOrDefaultAsync<ServiceOrderRow>(
            new CommandDefinition(
                sql,
                new
                {
                    TenantId = tenantId,
                    Id = id,
                    input.TechnicianUserId,
                    input.ScheduledAt,
                    Status = nextStatus.ToString(),
                    Address = address,
                    Description = description,
                    UpdatedAt = timeProvider.GetUtcNow()
                },
                transaction,
                cancellationToken: cancellationToken));
        if (row is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            throw new DomainRuleViolationException(
                $"A service order in status '{existing.Status}' cannot be updated or has an invalid assignee.");
        }

        var changedFields = new List<string>();
        if (existing.TechnicianUserId != input.TechnicianUserId)
        {
            changedFields.Add(nameof(input.TechnicianUserId));
        }

        if (existing.ScheduledAt != input.ScheduledAt)
        {
            changedFields.Add(nameof(input.ScheduledAt));
        }

        if (!string.Equals(existing.Address, address, StringComparison.Ordinal))
        {
            changedFields.Add(nameof(input.Address));
        }

        if (!string.Equals(existing.Description, description, StringComparison.Ordinal))
        {
            changedFields.Add(nameof(input.Description));
        }

        await InsertHistoryAsync(
            connection,
            transaction,
            tenantId,
            id,
            actorUserId,
            "Updated",
            existing.Status,
            row.Status,
            $"Changed fields: {string.Join(", ", changedFields)}",
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ToServiceOrderView(row);
    }

    public async Task<ServiceOrderView?> ChangeServiceOrderStatusAsync(
        Guid tenantId,
        Guid actorUserId,
        Guid id,
        Guid? assignedTechnicianId,
        ServiceOrderStatusInput input,
        CancellationToken cancellationToken)
    {
        const string selectSql = """
            SELECT status
            FROM service_orders
            WHERE tenant_id = @TenantId AND id = @Id
              AND (@AssignedTechnicianId IS NULL OR technician_user_id = @AssignedTechnicianId)
            FOR UPDATE;
            """;
        const string updateSql = """
            UPDATE service_orders
            SET status = @Status,
                scheduled_at = CASE WHEN @Status = 'Scheduled' AND scheduled_at IS NULL
                                    THEN @Now ELSE scheduled_at END,
                updated_at = @Now
            WHERE tenant_id = @TenantId AND id = @Id
              AND (@AssignedTechnicianId IS NULL OR technician_user_id = @AssignedTechnicianId)
            RETURNING id AS "Id", ticket_id AS "TicketId", installation_id AS "InstallationId",
                      technician_user_id AS "TechnicianUserId", scheduled_at AS "ScheduledAt",
                      address AS "Address", description AS "Description", status AS "Status",
                      checklist::text AS "ChecklistJson", materials::text AS "MaterialsJson",
                      hours_worked AS "HoursWorked", technical_report AS "TechnicalReport",
                      completed_at AS "CompletedAt", created_by_user_id AS "CreatedByUserId",
                      created_at AS "CreatedAt", updated_at AS "UpdatedAt";
            """;
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var currentStatus = await connection.QuerySingleOrDefaultAsync<string>(
            new CommandDefinition(
                selectSql,
                new { TenantId = tenantId, Id = id, AssignedTechnicianId = assignedTechnicianId },
                transaction,
                cancellationToken: cancellationToken));
        if (currentStatus is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return null;
        }

        var orderState = ServiceOrder.Restore(id, currentStatus);
        try
        {
            orderState.ChangeStatus(input.Status);
        }
        catch (DomainRuleViolationException)
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        var now = timeProvider.GetUtcNow();
        var row = await connection.QuerySingleAsync<ServiceOrderRow>(
            new CommandDefinition(
                updateSql,
                new
                {
                    TenantId = tenantId,
                    Id = id,
                    AssignedTechnicianId = assignedTechnicianId,
                    Status = orderState.Status.ToString(),
                    Now = now
                },
                transaction,
                cancellationToken: cancellationToken));
        await InsertHistoryAsync(
            connection,
            transaction,
            tenantId,
            id,
            actorUserId,
            "StatusChanged",
            currentStatus,
            orderState.Status.ToString(),
            CleanOptional(input.Note),
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ToServiceOrderView(row);
    }

    public async Task<ServiceOrderView?> CompleteServiceOrderAsync(
        Guid tenantId,
        Guid actorUserId,
        Guid id,
        Guid? assignedTechnicianId,
        ServiceOrderCompletionInput input,
        CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE service_orders
            SET status = @Status, hours_worked = @HoursWorked,
                technical_report = @TechnicalReport,
                checklist = CAST(@Checklist AS jsonb),
                materials = CAST(@Materials AS jsonb),
                completed_at = @CompletedAt, updated_at = @CompletedAt
            WHERE tenant_id = @TenantId AND id = @Id
              AND status IN ('Pending', 'Scheduled', 'InProgress')
              AND (@AssignedTechnicianId IS NULL OR technician_user_id = @AssignedTechnicianId)
            RETURNING id AS "Id", ticket_id AS "TicketId", installation_id AS "InstallationId",
                      technician_user_id AS "TechnicianUserId", scheduled_at AS "ScheduledAt",
                      address AS "Address", description AS "Description", status AS "Status",
                      checklist::text AS "ChecklistJson", materials::text AS "MaterialsJson",
                      hours_worked AS "HoursWorked", technical_report AS "TechnicalReport",
                      completed_at AS "CompletedAt", created_by_user_id AS "CreatedByUserId",
                      created_at AS "CreatedAt", updated_at AS "UpdatedAt";
            """;
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var currentStatus = await connection.QuerySingleOrDefaultAsync<string>(
            new CommandDefinition(
                """
                SELECT status FROM service_orders
                WHERE tenant_id = @TenantId AND id = @Id
                  AND (@AssignedTechnicianId IS NULL OR technician_user_id = @AssignedTechnicianId)
                FOR UPDATE;
                """,
                new { TenantId = tenantId, Id = id, AssignedTechnicianId = assignedTechnicianId },
                transaction,
                cancellationToken: cancellationToken));
        if (currentStatus is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return null;
        }

        var orderState = ServiceOrder.Restore(id, currentStatus);
        try
        {
            orderState.Complete();
        }
        catch (DomainRuleViolationException)
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        var completedAt = timeProvider.GetUtcNow();
        var row = await connection.QuerySingleAsync<ServiceOrderRow>(
            new CommandDefinition(
                sql,
                new
                {
                    TenantId = tenantId,
                    Id = id,
                    AssignedTechnicianId = assignedTechnicianId,
                    Status = orderState.Status.ToString(),
                    input.HoursWorked,
                    TechnicalReport = CleanOptional(input.TechnicalReport),
                    Checklist = JsonSerializer.Serialize(input.Checklist),
                    Materials = JsonSerializer.Serialize(input.Materials),
                    CompletedAt = completedAt
                },
                transaction,
                cancellationToken: cancellationToken));
        await InsertHistoryAsync(
            connection,
            transaction,
            tenantId,
            id,
            actorUserId,
            "Completed",
            currentStatus,
            orderState.Status.ToString(),
            null,
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ToServiceOrderView(row);
    }

    public async Task<IReadOnlyList<ServiceOrderHistoryView>?> ListServiceOrderHistoryAsync(
        Guid tenantId,
        Guid id,
        Guid? assignedTechnicianId,
        CancellationToken cancellationToken)
    {
        if (await GetServiceOrderAsync(tenantId, id, assignedTechnicianId, cancellationToken) is null)
        {
            return null;
        }

        return await QueryListAsync<ServiceOrderHistoryView>(
            """
            SELECT id AS "Id", actor_user_id AS "ActorUserId", action AS "Action",
                   old_status AS "OldStatus", new_status AS "NewStatus", note AS "Note",
                   created_at AS "CreatedAt"
            FROM service_order_history
            WHERE tenant_id = @TenantId AND service_order_id = @Id
            ORDER BY created_at, id;
            """,
            new { TenantId = tenantId, Id = id },
            cancellationToken);
    }

    private static async Task InsertHistoryAsync(
        Npgsql.NpgsqlConnection connection,
        Npgsql.NpgsqlTransaction transaction,
        Guid tenantId,
        Guid orderId,
        Guid actorUserId,
        string action,
        string? oldStatus,
        string? newStatus,
        string? note,
        CancellationToken cancellationToken)
    {
        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO service_order_history (
                id, tenant_id, service_order_id, actor_user_id, action,
                old_status, new_status, note
            )
            VALUES (@Id, @TenantId, @OrderId, @ActorUserId, @Action, @OldStatus, @NewStatus, @Note);
            """,
            new
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                OrderId = orderId,
                ActorUserId = actorUserId,
                Action = action,
                OldStatus = oldStatus,
                NewStatus = newStatus,
                Note = note
            },
            transaction,
            cancellationToken: cancellationToken));
    }

    private static ServiceOrderView ToServiceOrderView(ServiceOrderRow row) =>
        new(
            row.Id,
            row.TicketId,
            row.InstallationId,
            row.TechnicianUserId,
            row.ScheduledAt,
            row.Address,
            row.Description,
            row.Status,
            JsonSerializer.Deserialize<IReadOnlyList<ChecklistItem>>(row.ChecklistJson) ?? [],
            JsonSerializer.Deserialize<IReadOnlyList<MaterialItem>>(row.MaterialsJson) ?? [],
            row.HoursWorked,
            row.TechnicalReport,
            row.CompletedAt,
            row.CreatedByUserId,
            row.CreatedAt,
            row.UpdatedAt);

    private sealed class ServiceOrderRow
    {
        public Guid Id { get; init; }
        public Guid? TicketId { get; init; }
        public Guid InstallationId { get; init; }
        public Guid? TechnicianUserId { get; init; }
        public DateTimeOffset? ScheduledAt { get; init; }
        public string? Address { get; init; }
        public string Description { get; init; } = string.Empty;
        public string Status { get; init; } = string.Empty;
        public string ChecklistJson { get; init; } = "[]";
        public string MaterialsJson { get; init; } = "[]";
        public decimal? HoursWorked { get; init; }
        public string? TechnicalReport { get; init; }
        public DateTimeOffset? CompletedAt { get; init; }
        public Guid CreatedByUserId { get; init; }
        public DateTimeOffset CreatedAt { get; init; }
        public DateTimeOffset UpdatedAt { get; init; }
    }

    private sealed record ServiceOrderUpdateState(
        string Status,
        Guid? TechnicianUserId,
        DateTimeOffset? ScheduledAt,
        string? Address,
        string Description);
}
