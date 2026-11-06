using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SolarCare.Application.Authentication;
using SolarCare.Application.Operations;

namespace SolarCare.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/equipment")]
public sealed class EquipmentController(
    IEquipmentRepository repository,
    ITenantContext tenantContext,
    IValidator<EquipmentInput> validator) : OperationsControllerBase
{
    [HttpGet]
    [Authorize(Roles = "CompanyAdmin,Manager,Attendant")]
    [ProducesResponseType<IReadOnlyList<EquipmentView>>(StatusCodes.Status200OK)]
    public Task<IReadOnlyList<EquipmentView>> List(
        [FromQuery] Guid? installationId,
        [FromQuery] bool warrantiesOnly,
        [FromQuery] int limit = 50,
        [FromQuery] int offset = 0,
        CancellationToken cancellationToken = default) =>
        repository.ListEquipmentAsync(
            tenantContext.TenantId,
            installationId,
            warrantiesOnly,
            PageLimit(limit),
            PageOffset(offset),
            cancellationToken);

    [HttpGet("{id:guid}")]
    [Authorize(Roles = "CompanyAdmin,Manager,Attendant")]
    [ProducesResponseType<EquipmentView>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken)
    {
        var equipment = await repository.GetEquipmentAsync(tenantContext.TenantId, id, cancellationToken);
        return equipment is null ? NotFound() : Ok(equipment);
    }

    [HttpPost]
    [Authorize(Roles = "CompanyAdmin,Manager,Attendant")]
    [ProducesResponseType<EquipmentView>(StatusCodes.Status201Created)]
    public async Task<IActionResult> Create(EquipmentInput input, CancellationToken cancellationToken)
    {
        var error = await ValidateAsync(validator, input, cancellationToken);
        if (error is not null)
        {
            return error;
        }

        var equipment = await repository.CreateEquipmentAsync(tenantContext.TenantId, input, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = equipment.Id }, equipment);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "CompanyAdmin,Manager,Attendant")]
    [ProducesResponseType<EquipmentView>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(Guid id, EquipmentInput input, CancellationToken cancellationToken)
    {
        var error = await ValidateAsync(validator, input, cancellationToken);
        if (error is not null)
        {
            return error;
        }

        var equipment = await repository.UpdateEquipmentAsync(tenantContext.TenantId, id, input, cancellationToken);
        return equipment is null ? NotFound() : Ok(equipment);
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "CompanyAdmin,Manager")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken cancellationToken) =>
        await repository.DeactivateEquipmentAsync(tenantContext.TenantId, id, cancellationToken)
            ? NoContent()
            : NotFound();
}
