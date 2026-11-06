using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SolarCare.Application.Authentication;
using SolarCare.Application.Operations;

namespace SolarCare.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/maintenance-plans")]
public sealed class MaintenancePlansController(
    IMaintenanceRepository repository,
    ITenantContext tenantContext,
    IValidator<MaintenancePlanInput> planValidator,
    IValidator<MaintenanceExecutionInput> executionValidator) : OperationsControllerBase
{
    [HttpGet]
    [Authorize(Roles = "CompanyAdmin,Manager,Attendant")]
    [ProducesResponseType<IReadOnlyList<MaintenancePlanView>>(StatusCodes.Status200OK)]
    public Task<IReadOnlyList<MaintenancePlanView>> List(
        [FromQuery] bool includeInactive,
        [FromQuery] int limit = 50,
        [FromQuery] int offset = 0,
        CancellationToken cancellationToken = default) =>
        repository.ListMaintenancePlansAsync(
            tenantContext.TenantId, includeInactive, PageLimit(limit), PageOffset(offset), cancellationToken);

    [HttpGet("{id:guid}")]
    [Authorize(Roles = "CompanyAdmin,Manager,Attendant")]
    [ProducesResponseType<MaintenancePlanView>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken)
    {
        var plan = await repository.GetMaintenancePlanAsync(tenantContext.TenantId, id, cancellationToken);
        return plan is null ? NotFound() : Ok(plan);
    }

    [HttpPost]
    [Authorize(Roles = "CompanyAdmin,Manager,Attendant")]
    [ProducesResponseType<MaintenancePlanView>(StatusCodes.Status201Created)]
    public async Task<IActionResult> Create(MaintenancePlanInput input, CancellationToken cancellationToken)
    {
        var error = await ValidateAsync(planValidator, input, cancellationToken);
        if (error is not null)
        {
            return error;
        }

        var plan = await repository.CreateMaintenancePlanAsync(tenantContext.TenantId, input, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = plan.Id }, plan);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "CompanyAdmin,Manager,Attendant")]
    [ProducesResponseType<MaintenancePlanView>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(Guid id, MaintenancePlanInput input, CancellationToken cancellationToken)
    {
        var error = await ValidateAsync(planValidator, input, cancellationToken);
        if (error is not null)
        {
            return error;
        }

        var plan = await repository.UpdateMaintenancePlanAsync(
            tenantContext.TenantId, id, input, cancellationToken);
        return plan is null ? NotFound() : Ok(plan);
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "CompanyAdmin,Manager")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken cancellationToken) =>
        await repository.DeactivateMaintenancePlanAsync(tenantContext.TenantId, id, cancellationToken)
            ? NoContent()
            : NotFound();

    [HttpPost("{id:guid}/execute")]
    [Authorize(Roles = "CompanyAdmin,Manager,Attendant")]
    public async Task<IActionResult> Execute(
        Guid id,
        MaintenanceExecutionInput input,
        CancellationToken cancellationToken)
    {
        var error = await ValidateAsync(executionValidator, input, cancellationToken);
        if (error is not null)
        {
            return error;
        }

        var record = await repository.ExecuteMaintenanceAsync(
            tenantContext.TenantId, tenantContext.UserId, id, input, cancellationToken);
        return record is null ? NotFound() : CreatedAtAction(nameof(History), new { id }, record);
    }

    [HttpGet("{id:guid}/history")]
    [Authorize(Roles = "CompanyAdmin,Manager,Attendant")]
    [ProducesResponseType<IReadOnlyList<MaintenanceRecordView>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> History(Guid id, CancellationToken cancellationToken)
    {
        var history = await repository.ListMaintenanceHistoryAsync(
            tenantContext.TenantId, id, cancellationToken);
        return history is null ? NotFound() : Ok(history);
    }
}
