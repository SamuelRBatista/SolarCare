using FluentValidation;
using System.Text.RegularExpressions;
using SolarCare.Domain.Operations.ServiceOrders;
using SolarCare.Domain.Operations.Tickets;

namespace SolarCare.Application.Operations;

public sealed class CustomerInputValidator : AbstractValidator<CustomerInput>
{
    public CustomerInputValidator()
    {
        RuleFor(input => input.Name).NotEmpty().Must(value => !string.IsNullOrWhiteSpace(value)).MaximumLength(160);
        RuleFor(input => input.Kind).Must(value => value is "Person" or "Company");
        RuleFor(input => input.TaxIdentifier)
            .Must(value => string.IsNullOrWhiteSpace(value) || value.Count(char.IsDigit) is 11 or 14)
            .WithMessage("O CPF/CNPJ deve conter 11 ou 14 dígitos.");
        RuleFor(input => input.Phone).MaximumLength(40);
        RuleFor(input => input.Email).EmailAddress().When(input => !string.IsNullOrWhiteSpace(input.Email));
        RuleFor(input => input.Email).MaximumLength(254);
        RuleFor(input => input.Address).MaximumLength(240);
        RuleFor(input => input.City).MaximumLength(120);
        RuleFor(input => input.State)
            .Must(value => string.IsNullOrWhiteSpace(value) || Regex.IsMatch(value.Trim(), "^[A-Za-z]{2}$"))
            .WithMessage("Informe a sigla de estado com duas letras.");
        RuleFor(input => input.Notes).MaximumLength(4000);
    }
}

public sealed class InstallationInputValidator : AbstractValidator<InstallationInput>
{
    public InstallationInputValidator()
    {
        RuleFor(input => input.CustomerId).NotEmpty();
        RuleFor(input => input.Name).NotEmpty().Must(value => !string.IsNullOrWhiteSpace(value)).MaximumLength(160);
        RuleFor(input => input.Address).MaximumLength(240);
        RuleFor(input => input.City).MaximumLength(120);
        RuleFor(input => input.State)
            .Must(value => string.IsNullOrWhiteSpace(value) || Regex.IsMatch(value.Trim(), "^[A-Za-z]{2}$"));
        RuleFor(input => input.CapacityKwp).GreaterThan(0).When(input => input.CapacityKwp.HasValue);
        RuleFor(input => input.ModuleCount).GreaterThan(0).When(input => input.ModuleCount.HasValue);
        RuleFor(input => input.Notes).MaximumLength(4000);
    }
}

public sealed class EquipmentInputValidator : AbstractValidator<EquipmentInput>
{
    public EquipmentInputValidator()
    {
        RuleFor(input => input.InstallationId).NotEmpty();
        RuleFor(input => input.Category).NotEmpty().Must(value => !string.IsNullOrWhiteSpace(value)).MaximumLength(40);
        RuleFor(input => input.Manufacturer).NotEmpty().Must(value => !string.IsNullOrWhiteSpace(value)).MaximumLength(120);
        RuleFor(input => input.Model).MaximumLength(120);
        RuleFor(input => input.SerialNumber).MaximumLength(160);
        RuleFor(input => input.Status).Must(value => value is "Active" or "UnderMaintenance" or "Retired");
        RuleFor(input => input.Notes).MaximumLength(4000);
    }
}

public sealed class TicketInputValidator : AbstractValidator<TicketInput>
{
    public TicketInputValidator()
    {
        RuleFor(input => input.CustomerId).NotEmpty();
        RuleFor(input => input.Description).NotEmpty().Must(value => !string.IsNullOrWhiteSpace(value)).MaximumLength(8000);
        RuleFor(input => input.Category).NotEmpty().Must(value => !string.IsNullOrWhiteSpace(value)).MaximumLength(80);
        RuleFor(input => input.Priority).Must(value => value is "Low" or "Normal" or "High" or "Urgent");
    }
}

public sealed class TicketStatusInputValidator : AbstractValidator<TicketStatusInput>
{
    public TicketStatusInputValidator()
    {
        RuleFor(input => input.Status)
            .Must(value => TicketStatuses.TryParse(value, out _));
        RuleFor(input => input.Note).MaximumLength(2000);
    }
}

public sealed class ServiceOrderInputValidator : AbstractValidator<ServiceOrderInput>
{
    public ServiceOrderInputValidator()
    {
        RuleFor(input => input.InstallationId).NotEmpty();
        RuleFor(input => input.Description).NotEmpty().MaximumLength(8000);
        RuleFor(input => input.Address).MaximumLength(240);
        RuleFor(input => input.Checklist).NotNull().Must(items => items is { Count: <= 100 });
        RuleFor(input => input.Materials).NotNull().Must(items => items is { Count: <= 100 });
        RuleForEach(input => input.Checklist)
            .ChildRules(item => item.RuleFor(value => value.Description).NotEmpty().MaximumLength(240));
        RuleForEach(input => input.Materials).ChildRules(item =>
        {
            item.RuleFor(value => value.Description).NotEmpty().MaximumLength(240);
            item.RuleFor(value => value.Quantity).GreaterThan(0);
            item.RuleFor(value => value.Unit).MaximumLength(32);
        });
    }
}

public sealed class ServiceOrderUpdateInputValidator : AbstractValidator<ServiceOrderUpdateInput>
{
    public ServiceOrderUpdateInputValidator()
    {
        RuleFor(input => input.Description).NotEmpty().Must(value => !string.IsNullOrWhiteSpace(value)).MaximumLength(8000);
        RuleFor(input => input.Address).MaximumLength(240);
    }
}

public sealed class ServiceOrderCompletionInputValidator : AbstractValidator<ServiceOrderCompletionInput>
{
    public ServiceOrderCompletionInputValidator()
    {
        RuleFor(input => input.HoursWorked).GreaterThanOrEqualTo(0).When(input => input.HoursWorked.HasValue);
        RuleFor(input => input.TechnicalReport).MaximumLength(8000);
        RuleFor(input => input.Checklist).NotNull().Must(items => items is { Count: <= 100 });
        RuleFor(input => input.Materials).NotNull().Must(items => items is { Count: <= 100 });
        RuleForEach(input => input.Checklist)
            .ChildRules(item => item.RuleFor(value => value.Description).NotEmpty().MaximumLength(240));
        RuleForEach(input => input.Materials).ChildRules(item =>
        {
            item.RuleFor(value => value.Description).NotEmpty().MaximumLength(240);
            item.RuleFor(value => value.Quantity).GreaterThan(0);
            item.RuleFor(value => value.Unit).MaximumLength(32);
        });
    }
}

public sealed class ServiceOrderStatusInputValidator : AbstractValidator<ServiceOrderStatusInput>
{
    public ServiceOrderStatusInputValidator()
    {
        RuleFor(input => input.Status)
            .Must(value => ServiceOrderStatuses.TryParse(value, out var status) &&
                status is ServiceOrderStatus.Scheduled or ServiceOrderStatus.InProgress or ServiceOrderStatus.Cancelled);
        RuleFor(input => input.Note).MaximumLength(2000);
    }
}

public sealed class MaintenancePlanInputValidator : AbstractValidator<MaintenancePlanInput>
{
    public MaintenancePlanInputValidator()
    {
        RuleFor(input => input.InstallationId).NotEmpty();
        RuleFor(input => input.Name).NotEmpty().Must(value => !string.IsNullOrWhiteSpace(value)).MaximumLength(160);
        RuleFor(input => input.IntervalDays).InclusiveBetween(1, 3650);
        RuleFor(input => input.Notes).MaximumLength(4000);
    }
}

public sealed class MaintenanceExecutionInputValidator : AbstractValidator<MaintenanceExecutionInput>
{
    public MaintenanceExecutionInputValidator()
    {
        RuleFor(input => input.PerformedAt).NotEmpty();
        RuleFor(input => input.Notes).MaximumLength(4000);
    }
}
