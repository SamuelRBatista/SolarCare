using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SolarCare.Application.Authentication;
using SolarCare.Application.Operations;

namespace SolarCare.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/tickets")]
public sealed class TicketsController(
    ITicketRepository repository,
    ITenantContext tenantContext,
    IValidator<TicketInput> ticketValidator,
    IValidator<TicketStatusInput> statusValidator) : OperationsControllerBase
{
    [HttpGet]
    [Authorize(Roles = "CompanyAdmin,Manager,Attendant")]
    [ProducesResponseType<IReadOnlyList<TicketView>>(StatusCodes.Status200OK)]
    public Task<IReadOnlyList<TicketView>> List(
        [FromQuery] string? status,
        [FromQuery] Guid? customerId,
        [FromQuery] int limit = 50,
        [FromQuery] int offset = 0,
        CancellationToken cancellationToken = default) =>
        repository.ListTicketsAsync(
            tenantContext.TenantId, status, customerId, PageLimit(limit), PageOffset(offset), cancellationToken);

    [HttpGet("{id:guid}")]
    [Authorize(Roles = "CompanyAdmin,Manager,Attendant")]
    [ProducesResponseType<TicketView>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken)
    {
        var ticket = await repository.GetTicketAsync(tenantContext.TenantId, id, cancellationToken);
        return ticket is null ? NotFound() : Ok(ticket);
    }

    [HttpPost]
    [Authorize(Roles = "CompanyAdmin,Manager,Attendant")]
    [ProducesResponseType<TicketView>(StatusCodes.Status201Created)]
    public async Task<IActionResult> Create(TicketInput input, CancellationToken cancellationToken)
    {
        var error = await ValidateAsync(ticketValidator, input, cancellationToken);
        if (error is not null)
        {
            return error;
        }

        var ticket = await repository.CreateTicketAsync(
            tenantContext.TenantId, tenantContext.UserId, input, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = ticket.Id }, ticket);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "CompanyAdmin,Manager,Attendant")]
    [ProducesResponseType<TicketView>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Update(Guid id, TicketInput input, CancellationToken cancellationToken)
    {
        var error = await ValidateAsync(ticketValidator, input, cancellationToken);
        if (error is not null)
        {
            return error;
        }

        var ticket = await repository.UpdateTicketAsync(
            tenantContext.TenantId, tenantContext.UserId, id, input, cancellationToken);
        return ticket is null ? NotFound() : Ok(ticket);
    }

    [HttpPatch("{id:guid}/status")]
    [Authorize(Roles = "CompanyAdmin,Manager,Attendant")]
    public async Task<IActionResult> ChangeStatus(
        Guid id,
        TicketStatusInput input,
        CancellationToken cancellationToken)
    {
        var error = await ValidateAsync(statusValidator, input, cancellationToken);
        if (error is not null)
        {
            return error;
        }

        var ticket = await repository.ChangeTicketStatusAsync(
            tenantContext.TenantId, tenantContext.UserId, id, input, cancellationToken);
        return ticket is null ? NotFound() : Ok(ticket);
    }

    [HttpGet("{id:guid}/history")]
    [Authorize(Roles = "CompanyAdmin,Manager,Attendant")]
    [ProducesResponseType<IReadOnlyList<TicketHistoryView>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> History(Guid id, CancellationToken cancellationToken)
    {
        var history = await repository.ListTicketHistoryAsync(tenantContext.TenantId, id, cancellationToken);
        return history is null ? NotFound() : Ok(history);
    }
}
