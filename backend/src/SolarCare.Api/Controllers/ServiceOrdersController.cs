using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SolarCare.Application.Authentication;
using SolarCare.Application.Operations;
using SolarCare.Domain;

namespace SolarCare.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/service-orders")]
public sealed class ServiceOrdersController(
    IServiceOrderRepository repository,
    ITenantContext tenantContext,
    IValidator<ServiceOrderInput> serviceOrderValidator,
    IValidator<ServiceOrderUpdateInput> updateValidator,
    IValidator<ServiceOrderStatusInput> statusValidator,
    IValidator<ServiceOrderCompletionInput> completionValidator) : OperationsControllerBase
{
    [HttpGet]
    [Authorize(Roles = "CompanyAdmin,Manager,Attendant,Technician")]
    [ProducesResponseType<IReadOnlyList<ServiceOrderView>>(StatusCodes.Status200OK)]
    public Task<IReadOnlyList<ServiceOrderView>> List(
        [FromQuery] string? status,
        [FromQuery] int limit = 50,
        [FromQuery] int offset = 0,
        CancellationToken cancellationToken = default) =>
        repository.ListServiceOrdersAsync(
            tenantContext.TenantId,
            status,
            TechnicianId(),
            PageLimit(limit),
            PageOffset(offset),
            cancellationToken);

    [HttpGet("{id:guid}")]
    [Authorize(Roles = "CompanyAdmin,Manager,Attendant,Technician")]
    [ProducesResponseType<ServiceOrderView>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken)
    {
        var order = await repository.GetServiceOrderAsync(
            tenantContext.TenantId, id, TechnicianId(), cancellationToken);
        return order is null ? NotFound() : Ok(order);
    }

    [HttpPost]
    [Authorize(Roles = "CompanyAdmin,Manager,Attendant")]
    [ProducesResponseType<ServiceOrderView>(StatusCodes.Status201Created)]
    public async Task<IActionResult> Create(ServiceOrderInput input, CancellationToken cancellationToken)
    {
        var error = await ValidateAsync(serviceOrderValidator, input, cancellationToken);
        if (error is not null)
        {
            return error;
        }

        var order = await repository.CreateServiceOrderAsync(
            tenantContext.TenantId, tenantContext.UserId, input, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = order.Id }, order);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "CompanyAdmin,Manager")]
    public async Task<IActionResult> Update(
        Guid id,
        ServiceOrderUpdateInput input,
        CancellationToken cancellationToken)
    {
        var error = await ValidateAsync(updateValidator, input, cancellationToken);
        if (error is not null)
        {
            return error;
        }

        var order = await repository.UpdateServiceOrderAsync(
            tenantContext.TenantId, tenantContext.UserId, id, input, cancellationToken);
        return order is null ? NotFound() : Ok(order);
    }

    [HttpPatch("{id:guid}/status")]
    [Authorize(Roles = "CompanyAdmin,Manager,Technician")]
    public async Task<IActionResult> ChangeStatus(
        Guid id,
        ServiceOrderStatusInput input,
        CancellationToken cancellationToken)
    {
        var error = await ValidateAsync(statusValidator, input, cancellationToken);
        if (error is not null)
        {
            return error;
        }

        var order = await repository.ChangeServiceOrderStatusAsync(
            tenantContext.TenantId, tenantContext.UserId, id, TechnicianId(), input, cancellationToken);
        return order is null ? NotFound() : Ok(order);
    }

    [HttpGet("{id:guid}/history")]
    [Authorize(Roles = "CompanyAdmin,Manager,Attendant,Technician")]
    [ProducesResponseType<IReadOnlyList<ServiceOrderHistoryView>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> History(Guid id, CancellationToken cancellationToken)
    {
        var history = await repository.ListServiceOrderHistoryAsync(
            tenantContext.TenantId, id, TechnicianId(), cancellationToken);
        return history is null ? NotFound() : Ok(history);
    }

    [HttpPost("{id:guid}/complete")]
    [Authorize(Roles = "CompanyAdmin,Manager,Technician")]
    public async Task<IActionResult> Complete(
        Guid id,
        ServiceOrderCompletionInput input,
        CancellationToken cancellationToken)
    {
        var error = await ValidateAsync(completionValidator, input, cancellationToken);
        if (error is not null)
        {
            return error;
        }

        var order = await repository.CompleteServiceOrderAsync(
            tenantContext.TenantId, tenantContext.UserId, id, TechnicianId(), input, cancellationToken);
        return order is null ? NotFound() : Ok(order);
    }

    private Guid? TechnicianId() =>
        tenantContext.Role == AccessRole.Technician ? tenantContext.UserId : null;
}
