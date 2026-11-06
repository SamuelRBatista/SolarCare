using Dapper;
using SolarCare.Application.Operations;
using SolarCare.Domain.Operations;
using SolarCare.Domain.Operations.Tickets;

namespace SolarCare.Infrastructure.Operations;

public sealed partial class DapperOperationsRepository
{
    public Task<IReadOnlyList<TicketView>> ListTicketsAsync(
        Guid tenantId,
        string? status,
        Guid? customerId,
        int limit,
        int offset,
        CancellationToken cancellationToken) =>
        QueryListAsync<TicketView>(
            """
            SELECT id AS "Id", protocol AS "Protocol", customer_id AS "CustomerId",
                   installation_id AS "InstallationId", equipment_id AS "EquipmentId",
                   description AS "Description", category AS "Category", priority AS "Priority",
                   status AS "Status", assigned_user_id AS "AssignedUserId",
                   opened_by_user_id AS "OpenedByUserId", opened_at AS "OpenedAt",
                   updated_at AS "UpdatedAt", closed_at AS "ClosedAt"
            FROM service_tickets
            WHERE tenant_id = @TenantId
              AND (@Status IS NULL OR status = @Status)
              AND (@CustomerId IS NULL OR customer_id = @CustomerId)
            ORDER BY opened_at DESC, id
            LIMIT @Limit OFFSET @Offset;
            """,
            new
            {
                TenantId = tenantId,
                Status = string.IsNullOrWhiteSpace(status) ? null : status,
                CustomerId = customerId,
                Limit = limit,
                Offset = offset
            },
            cancellationToken);

    public Task<TicketView?> GetTicketAsync(
        Guid tenantId,
        Guid id,
        CancellationToken cancellationToken) =>
        QuerySingleAsync<TicketView>(
            """
            SELECT id AS "Id", protocol AS "Protocol", customer_id AS "CustomerId",
                   installation_id AS "InstallationId", equipment_id AS "EquipmentId",
                   description AS "Description", category AS "Category", priority AS "Priority",
                   status AS "Status", assigned_user_id AS "AssignedUserId",
                   opened_by_user_id AS "OpenedByUserId", opened_at AS "OpenedAt",
                   updated_at AS "UpdatedAt", closed_at AS "ClosedAt"
            FROM service_tickets WHERE tenant_id = @TenantId AND id = @Id;
            """,
            new { TenantId = tenantId, Id = id },
            cancellationToken);

    public async Task<TicketView> CreateTicketAsync(
        Guid tenantId,
        Guid actorUserId,
        TicketInput input,
        CancellationToken cancellationToken)
    {
        var id = Guid.NewGuid();
        var ticketState = Ticket.Create(id);
        var protocol = $"SC-{timeProvider.GetUtcNow():yyyyMMdd}-{id.ToString("N")[..8].ToUpperInvariant()}";
        const string insertSql = """
            INSERT INTO service_tickets (
                id, tenant_id, protocol, customer_id, installation_id, equipment_id,
                description, category, priority, status, assigned_user_id, opened_by_user_id
            )
            SELECT @Id, @TenantId, @Protocol, c.id, @InstallationId, @EquipmentId,
                   @Description, @Category, @Priority, @Status, @AssignedUserId, @ActorUserId
            FROM customers c
            WHERE c.tenant_id = @TenantId AND c.id = @CustomerId AND c.is_active = TRUE
              AND (
                  @InstallationId IS NULL OR EXISTS (
                      SELECT 1 FROM installations i
                      WHERE i.tenant_id = @TenantId AND i.id = @InstallationId
                        AND i.customer_id = c.id AND i.is_active = TRUE
                  )
              )
              AND (
                  @EquipmentId IS NULL OR EXISTS (
                      SELECT 1
                      FROM equipment e
                      JOIN installations i
                        ON i.tenant_id = e.tenant_id AND i.id = e.installation_id
                      WHERE e.tenant_id = @TenantId AND e.id = @EquipmentId
                        AND e.is_active = TRUE
                        AND (@InstallationId IS NULL OR i.id = @InstallationId)
                        AND i.customer_id = c.id
                  )
              )
              AND (
                  @AssignedUserId IS NULL OR EXISTS (
                      SELECT 1 FROM users u
                      WHERE u.tenant_id = @TenantId AND u.id = @AssignedUserId AND u.is_active = TRUE
                  )
              )
            RETURNING id AS "Id", protocol AS "Protocol", customer_id AS "CustomerId",
                      installation_id AS "InstallationId", equipment_id AS "EquipmentId",
                      description AS "Description", category AS "Category", priority AS "Priority",
                      status AS "Status", assigned_user_id AS "AssignedUserId",
                      opened_by_user_id AS "OpenedByUserId", opened_at AS "OpenedAt",
                      updated_at AS "UpdatedAt", closed_at AS "ClosedAt";
            """;

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var ticket = await connection.QuerySingleOrDefaultAsync<TicketView>(
            new CommandDefinition(
                insertSql,
                new
                {
                    Id = id,
                    TenantId = tenantId,
                    Protocol = protocol,
                    Status = ticketState.Status.ToString(),
                    input.CustomerId,
                    input.InstallationId,
                    input.EquipmentId,
                    Description = input.Description.Trim(),
                    Category = input.Category.Trim(),
                    input.Priority,
                    input.AssignedUserId,
                    ActorUserId = actorUserId
                },
                transaction,
                cancellationToken: cancellationToken));

        if (ticket is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            throw new RelatedResourceNotFoundException("Customer, installation, equipment, or assignee");
        }

        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO service_ticket_history (id, tenant_id, ticket_id, actor_user_id, action, new_value)
            VALUES (@Id, @TenantId, @TicketId, @ActorUserId, 'Created', @NewValue);
            """,
            new
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                TicketId = id,
                ActorUserId = actorUserId,
                NewValue = ticketState.Status.ToString()
            },
            transaction,
            cancellationToken: cancellationToken));
        await transaction.CommitAsync(cancellationToken);
        return ticket;
    }

    public async Task<TicketView?> UpdateTicketAsync(
        Guid tenantId,
        Guid actorUserId,
        Guid id,
        TicketInput input,
        CancellationToken cancellationToken)
    {
        const string selectSql = """
            SELECT status
            FROM service_tickets
            WHERE tenant_id = @TenantId AND id = @Id
            FOR UPDATE;
            """;
        const string updateSql = """
            UPDATE service_tickets t
            SET customer_id = c.id,
                installation_id = @InstallationId,
                equipment_id = @EquipmentId,
                description = @Description,
                category = @Category,
                priority = @Priority,
                assigned_user_id = @AssignedUserId,
                updated_at = @Now
            FROM customers c
            WHERE t.tenant_id = @TenantId AND t.id = @Id
              AND c.tenant_id = @TenantId AND c.id = @CustomerId AND c.is_active = TRUE
              AND (
                  @InstallationId IS NULL OR EXISTS (
                      SELECT 1 FROM installations i
                      WHERE i.tenant_id = @TenantId AND i.id = @InstallationId
                        AND i.customer_id = c.id AND i.is_active = TRUE
                  )
              )
              AND (
                  @EquipmentId IS NULL OR EXISTS (
                      SELECT 1
                      FROM equipment e
                      JOIN installations i
                        ON i.tenant_id = e.tenant_id AND i.id = e.installation_id
                      WHERE e.tenant_id = @TenantId AND e.id = @EquipmentId
                        AND e.is_active = TRUE
                        AND (@InstallationId IS NULL OR i.id = @InstallationId)
                        AND i.customer_id = c.id
                  )
              )
              AND (
                  @AssignedUserId IS NULL OR EXISTS (
                      SELECT 1 FROM users u
                      WHERE u.tenant_id = @TenantId AND u.id = @AssignedUserId AND u.is_active = TRUE
                  )
              )
            RETURNING t.id AS "Id", t.protocol AS "Protocol", t.customer_id AS "CustomerId",
                      t.installation_id AS "InstallationId", t.equipment_id AS "EquipmentId",
                      t.description AS "Description", t.category AS "Category", t.priority AS "Priority",
                      t.status AS "Status", t.assigned_user_id AS "AssignedUserId",
                      t.opened_by_user_id AS "OpenedByUserId", t.opened_at AS "OpenedAt",
                      t.updated_at AS "UpdatedAt", t.closed_at AS "ClosedAt";
            """;

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var currentStatus = await connection.QuerySingleOrDefaultAsync<string>(
            new CommandDefinition(
                selectSql,
                new { TenantId = tenantId, Id = id },
                transaction,
                cancellationToken: cancellationToken));
        if (currentStatus is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return null;
        }

        try
        {
            Ticket.Restore(id, currentStatus).EnsureCanUpdate();
        }
        catch (DomainRuleViolationException)
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        var now = timeProvider.GetUtcNow();
        var ticket = await connection.QuerySingleOrDefaultAsync<TicketView>(
            new CommandDefinition(
                updateSql,
                new
                {
                    TenantId = tenantId,
                    Id = id,
                    input.CustomerId,
                    input.InstallationId,
                    input.EquipmentId,
                    Description = input.Description.Trim(),
                    Category = input.Category.Trim(),
                    input.Priority,
                    input.AssignedUserId,
                    Now = now
                },
                transaction,
                cancellationToken: cancellationToken));
        if (ticket is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            throw new RelatedResourceNotFoundException("Customer, installation, equipment, or assignee");
        }

        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO service_ticket_history (
                id, tenant_id, ticket_id, actor_user_id, action, note
            )
            VALUES (@Id, @TenantId, @TicketId, @ActorUserId, 'Updated', 'Ticket details updated.');
            """,
            new
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                TicketId = id,
                ActorUserId = actorUserId
            },
            transaction,
            cancellationToken: cancellationToken));
        await transaction.CommitAsync(cancellationToken);
        return ticket;
    }

    public async Task<TicketView?> ChangeTicketStatusAsync(
        Guid tenantId,
        Guid actorUserId,
        Guid id,
        TicketStatusInput input,
        CancellationToken cancellationToken)
    {
        const string selectSql = """
            SELECT status
            FROM service_tickets
            WHERE tenant_id = @TenantId AND id = @Id
            FOR UPDATE;
            """;
        const string updateSql = """
            UPDATE service_tickets
            SET status = @Status,
                closed_at = CASE WHEN @Status IN ('Closed', 'Cancelled') THEN @Now ELSE NULL END,
                updated_at = @Now
            WHERE tenant_id = @TenantId AND id = @Id
            RETURNING id AS "Id", protocol AS "Protocol", customer_id AS "CustomerId",
                      installation_id AS "InstallationId", equipment_id AS "EquipmentId",
                      description AS "Description", category AS "Category", priority AS "Priority",
                      status AS "Status", assigned_user_id AS "AssignedUserId",
                      opened_by_user_id AS "OpenedByUserId", opened_at AS "OpenedAt",
                      updated_at AS "UpdatedAt", closed_at AS "ClosedAt";
            """;

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var currentStatus = await connection.QuerySingleOrDefaultAsync<string>(
            new CommandDefinition(selectSql, new { TenantId = tenantId, Id = id }, transaction, cancellationToken: cancellationToken));
        if (currentStatus is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return null;
        }

        var ticketState = Ticket.Restore(id, currentStatus);
        try
        {
            ticketState.ChangeStatus(input.Status);
        }
        catch (DomainRuleViolationException)
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        var now = timeProvider.GetUtcNow();
        var updatedTicket = await connection.QuerySingleAsync<TicketView>(
            new CommandDefinition(
                updateSql,
                new { TenantId = tenantId, Id = id, Status = ticketState.Status.ToString(), Now = now },
                transaction,
                cancellationToken: cancellationToken));
        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO service_ticket_history (
                id, tenant_id, ticket_id, actor_user_id, action, old_value, new_value, note
            )
            VALUES (@HistoryId, @TenantId, @TicketId, @ActorUserId, 'StatusChanged', @OldValue, @NewValue, @Note);
            """,
            new
            {
                HistoryId = Guid.NewGuid(),
                TenantId = tenantId,
                TicketId = id,
                ActorUserId = actorUserId,
                OldValue = currentStatus,
                NewValue = ticketState.Status.ToString(),
                Note = CleanOptional(input.Note)
            },
            transaction,
            cancellationToken: cancellationToken));
        await transaction.CommitAsync(cancellationToken);
        return updatedTicket;
    }

    public async Task<IReadOnlyList<TicketHistoryView>?> ListTicketHistoryAsync(
        Guid tenantId,
        Guid id,
        CancellationToken cancellationToken)
    {
        if (await GetTicketAsync(tenantId, id, cancellationToken) is null)
        {
            return null;
        }

        return await QueryListAsync<TicketHistoryView>(
            """
            SELECT id AS "Id", actor_user_id AS "ActorUserId", action AS "Action",
                   old_value AS "OldValue", new_value AS "NewValue", note AS "Note",
                   created_at AS "CreatedAt"
            FROM service_ticket_history
            WHERE tenant_id = @TenantId AND ticket_id = @Id
            ORDER BY created_at, id;
            """,
            new { TenantId = tenantId, Id = id },
            cancellationToken);
    }

}
