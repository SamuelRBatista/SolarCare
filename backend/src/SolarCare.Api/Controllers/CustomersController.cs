using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SolarCare.Application.Authentication;
using SolarCare.Application.Operations;

namespace SolarCare.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/customers")]
public sealed class CustomersController(
    ICustomerRepository repository,
    ITenantContext tenantContext,
    IValidator<CustomerInput> validator) : OperationsControllerBase
{
    [HttpGet]
    [Authorize(Roles = "CompanyAdmin,Manager,Attendant")]
    [ProducesResponseType<IReadOnlyList<CustomerView>>(StatusCodes.Status200OK)]
    public Task<IReadOnlyList<CustomerView>> List(
        [FromQuery] string? search,
        [FromQuery] int limit = 50,
        [FromQuery] int offset = 0,
        CancellationToken cancellationToken = default) =>
        repository.ListCustomersAsync(
            tenantContext.TenantId,
            search,
            PageLimit(limit),
            PageOffset(offset),
            cancellationToken);

    [HttpGet("{id:guid}")]
    [Authorize(Roles = "CompanyAdmin,Manager,Attendant")]
    [ProducesResponseType<CustomerView>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken)
    {
        var customer = await repository.GetCustomerAsync(tenantContext.TenantId, id, cancellationToken);
        return customer is null ? NotFound() : Ok(customer);
    }

    [HttpPost]
    [Authorize(Roles = "CompanyAdmin,Manager,Attendant")]
    [ProducesResponseType<CustomerView>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create(CustomerInput input, CancellationToken cancellationToken)
    {
        var error = await ValidateAsync(validator, input, cancellationToken);
        if (error is not null)
        {
            return error;
        }

        var customer = await repository.CreateCustomerAsync(tenantContext.TenantId, input, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = customer.Id }, customer);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "CompanyAdmin,Manager,Attendant")]
    [ProducesResponseType<CustomerView>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(Guid id, CustomerInput input, CancellationToken cancellationToken)
    {
        var error = await ValidateAsync(validator, input, cancellationToken);
        if (error is not null)
        {
            return error;
        }

        var customer = await repository.UpdateCustomerAsync(tenantContext.TenantId, id, input, cancellationToken);
        return customer is null ? NotFound() : Ok(customer);
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "CompanyAdmin,Manager")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken cancellationToken) =>
        await repository.DeactivateCustomerAsync(tenantContext.TenantId, id, cancellationToken)
            ? NoContent()
            : NotFound();
}
