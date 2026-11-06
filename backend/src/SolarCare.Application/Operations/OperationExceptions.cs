namespace SolarCare.Application.Operations;

public sealed class DuplicateCustomerTaxIdentifierException : Exception
{
    public DuplicateCustomerTaxIdentifierException()
        : base("A customer with this tax identifier already exists for the tenant.")
    {
    }
}

public sealed class RelatedResourceNotFoundException : Exception
{
    public RelatedResourceNotFoundException(string resourceName)
        : base($"{resourceName} was not found or is not accessible.")
    {
    }
}
