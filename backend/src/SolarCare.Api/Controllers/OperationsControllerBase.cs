using FluentValidation;
using Microsoft.AspNetCore.Mvc;

namespace SolarCare.Api.Controllers;

public abstract class OperationsControllerBase : ControllerBase
{
    protected async Task<IActionResult?> ValidateAsync<T>(
        IValidator<T> validator,
        T input,
        CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(input, cancellationToken);
        return validation.IsValid
            ? null
            : BadRequest(new ValidationProblemDetails(validation.ToDictionary()));
    }

    protected static int PageLimit(int value) => Math.Clamp(value, 1, 100);

    protected static int PageOffset(int value) => Math.Clamp(value, 0, 1_000_000);
}
