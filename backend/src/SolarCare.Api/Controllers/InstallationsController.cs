using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SolarCare.Application.Authentication;
using SolarCare.Application.Operations;

namespace SolarCare.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/installations")]
public sealed class InstallationsController(
    IInstallationRepository repository,
    ITenantContext tenantContext,
    IValidator<InstallationInput> validator) : OperationsControllerBase
{
    [HttpGet]
    [Authorize(Roles = "CompanyAdmin,Manager,Attendant")]
    [ProducesResponseType<IReadOnlyList<InstallationView>>(StatusCodes.Status200OK)]
    public Task<IReadOnlyList<InstallationView>> List(
        [FromQuery] Guid? customerId,
        [FromQuery] int limit = 50,
        [FromQuery] int offset = 0,
        CancellationToken cancellationToken = default) =>
        repository.ListInstallationsAsync(
            tenantContext.TenantId, customerId, PageLimit(limit), PageOffset(offset), cancellationToken);

    [HttpGet("{id:guid}")]
    [Authorize(Roles = "CompanyAdmin,Manager,Attendant")]
    [ProducesResponseType<InstallationView>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken)
    {
        var installation = await repository.GetInstallationAsync(tenantContext.TenantId, id, cancellationToken);
        return installation is null ? NotFound() : Ok(installation);
    }

    [HttpPost]
    [Authorize(Roles = "CompanyAdmin,Manager,Attendant")]
    [ProducesResponseType<InstallationView>(StatusCodes.Status201Created)]
    public async Task<IActionResult> Create(InstallationInput input, CancellationToken cancellationToken)
    {
        var error = await ValidateAsync(validator, input, cancellationToken);
        if (error is not null)
        {
            return error;
        }

        var installation = await repository.CreateInstallationAsync(tenantContext.TenantId, input, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = installation.Id }, installation);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "CompanyAdmin,Manager,Attendant")]
    [ProducesResponseType<InstallationView>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(Guid id, InstallationInput input, CancellationToken cancellationToken)
    {
        var error = await ValidateAsync(validator, input, cancellationToken);
        if (error is not null)
        {
            return error;
        }

        var installation = await repository.UpdateInstallationAsync(
            tenantContext.TenantId, id, input, cancellationToken);
        return installation is null ? NotFound() : Ok(installation);
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "CompanyAdmin,Manager")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken cancellationToken) =>
        await repository.DeactivateInstallationAsync(tenantContext.TenantId, id, cancellationToken)
            ? NoContent()
            : NotFound();
}
