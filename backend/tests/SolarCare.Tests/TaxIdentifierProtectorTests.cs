using System.Security.Cryptography;
using Microsoft.Extensions.Configuration;
using SolarCare.Infrastructure.Operations;

namespace SolarCare.Tests;

public sealed class TaxIdentifierProtectorTests
{
    private static AesTaxIdentifierProtector CreateProtector() =>
        new(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["PersonalData:EncryptionKey"] = Convert.ToBase64String(new byte[32])
            })
            .Build());

    [Fact]
    public void ProtectAndReveal_NormalizesIdentifierAndCreatesStableLookup()
    {
        var protector = CreateProtector();

        var first = protector.Protect("123.456.789-01");
        var second = protector.Protect("12345678901");

        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.Equal("12345678901", protector.Reveal(first.Ciphertext));
        Assert.Equal(first.LookupHash, second.LookupHash);
        Assert.NotEqual(first.Ciphertext, second.Ciphertext);
    }

    [Fact]
    public void Protect_RejectsIdentifiersWithUnsupportedLength()
    {
        var protector = CreateProtector();

        Assert.Throws<ArgumentException>(() => protector.Protect("12345"));
    }

    [Fact]
    public void Reveal_RejectsTamperedCiphertext()
    {
        var protector = CreateProtector();
        var protectedValue = protector.Protect("12345678901");
        Assert.NotNull(protectedValue);
        var storedBytes = Convert.FromBase64String(protectedValue.Ciphertext);
        storedBytes[^1] ^= 0x01;

        Assert.ThrowsAny<CryptographicException>(
            () => protector.Reveal(Convert.ToBase64String(storedBytes)));
    }
}
