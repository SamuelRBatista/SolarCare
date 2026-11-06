namespace SolarCare.Application.Operations;

public sealed record ProtectedTaxIdentifier(string Ciphertext, byte[] LookupHash);

public interface ITaxIdentifierProtector
{
    ProtectedTaxIdentifier? Protect(string? value);
    string? Reveal(string? ciphertext);
}
