using Microsoft.AspNetCore.Identity;
using SolarCare.Application.Authentication;

namespace SolarCare.Infrastructure.Authentication;

public sealed class PasswordService : IPasswordService
{
    private static readonly PasswordHasher<PasswordSubject> Hasher = new();
    private static readonly PasswordSubject Subject = new();

    public string Hash(string password) => Hasher.HashPassword(Subject, password);

    public bool Verify(string passwordHash, string password) =>
        Hasher.VerifyHashedPassword(Subject, passwordHash, password) is
            PasswordVerificationResult.Success or PasswordVerificationResult.SuccessRehashNeeded;

    private sealed class PasswordSubject { }
}
