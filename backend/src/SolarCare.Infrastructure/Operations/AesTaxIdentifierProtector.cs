using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using SolarCare.Application.Operations;

namespace SolarCare.Infrastructure.Operations;

public sealed class AesTaxIdentifierProtector : ITaxIdentifierProtector
{
    private const int NonceLength = 12;
    private const int TagLength = 16;
    private readonly byte[] _encryptionKey;
    private readonly byte[] _lookupKey;

    public AesTaxIdentifierProtector(IConfiguration configuration)
    {
        var configuredKey = configuration["PersonalData:EncryptionKey"]
            ?? throw new InvalidOperationException(
                "Configuration 'PersonalData:EncryptionKey' is required.");
        try
        {
            _encryptionKey = Convert.FromBase64String(configuredKey);
        }
        catch (FormatException exception)
        {
            throw new InvalidOperationException(
                "'PersonalData:EncryptionKey' must be a base64-encoded key.",
                exception);
        }

        if (_encryptionKey.Length != 32)
        {
            throw new InvalidOperationException(
                "'PersonalData:EncryptionKey' must contain exactly 256 bits.");
        }

        _lookupKey = HMACSHA256.HashData(
            _encryptionKey,
            Encoding.UTF8.GetBytes("SolarCare.customer-tax-id.lookup.v1"));
    }

    public ProtectedTaxIdentifier? Protect(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = new string(value.Where(char.IsDigit).ToArray());
        if (normalized.Length is not (11 or 14))
        {
            throw new ArgumentException("CPF/CNPJ must contain 11 or 14 digits.", nameof(value));
        }

        var plaintext = Encoding.UTF8.GetBytes(normalized);
        var nonce = RandomNumberGenerator.GetBytes(NonceLength);
        var ciphertext = new byte[plaintext.Length];
        var tag = new byte[TagLength];
        using (var aes = new AesGcm(_encryptionKey, TagLength))
        {
            aes.Encrypt(nonce, plaintext, ciphertext, tag);
        }

        var storedValue = new byte[NonceLength + TagLength + ciphertext.Length];
        nonce.CopyTo(storedValue, 0);
        tag.CopyTo(storedValue, NonceLength);
        ciphertext.CopyTo(storedValue, NonceLength + TagLength);
        return new ProtectedTaxIdentifier(
            Convert.ToBase64String(storedValue),
            HMACSHA256.HashData(_lookupKey, plaintext));
    }

    public string? Reveal(string? ciphertext)
    {
        if (string.IsNullOrEmpty(ciphertext))
        {
            return null;
        }

        var storedValue = Convert.FromBase64String(ciphertext);
        if (storedValue.Length <= NonceLength + TagLength)
        {
            throw new CryptographicException("Stored tax identifier is malformed.");
        }

        var plaintext = new byte[storedValue.Length - NonceLength - TagLength];
        using (var aes = new AesGcm(_encryptionKey, TagLength))
        {
            aes.Decrypt(
                storedValue.AsSpan(0, NonceLength),
                storedValue.AsSpan(NonceLength + TagLength),
                storedValue.AsSpan(NonceLength, TagLength),
                plaintext);
        }

        return Encoding.UTF8.GetString(plaintext);
    }
}
