using System.Security.Cryptography;
using System.Text;
using SolarCare.Domain;

namespace SolarCare.Application.Authentication;

public sealed class AuthService(
    IAuthRepository repository,
    IPasswordService passwordService,
    IAccessTokenIssuer accessTokenIssuer,
    TimeProvider timeProvider) : IAuthService
{
    private static readonly TimeSpan RefreshTokenLifetime = TimeSpan.FromDays(7);

    public async Task<IssuedAuthSession> RegisterAsync(
        RegisterCommand command,
        CancellationToken cancellationToken)
    {
        var normalizedEmail = NormalizeEmail(command.Email);
        var refreshToken = CreateRefreshToken();
        var now = timeProvider.GetUtcNow();
        var user = await repository.RegisterAsync(
            command with
            {
                CompanyName = command.CompanyName.Trim(),
                FullName = command.FullName.Trim(),
                Email = normalizedEmail
            },
            passwordService.Hash(command.Password),
            HashRefreshToken(refreshToken),
            now.Add(RefreshTokenLifetime),
            cancellationToken);

        return CreateSession(user, refreshToken);
    }

    public async Task<IssuedAuthSession?> LoginAsync(
        LoginCommand command,
        CancellationToken cancellationToken)
    {
        var credentials = await repository.FindByEmailAsync(
            NormalizeEmail(command.Email),
            cancellationToken);

        if (credentials is null ||
            !passwordService.Verify(credentials.Value.PasswordHash, command.Password))
        {
            return null;
        }

        var refreshToken = CreateRefreshToken();
        await repository.SaveRefreshTokenAsync(
            credentials.Value.User.Id,
            HashRefreshToken(refreshToken),
            timeProvider.GetUtcNow().Add(RefreshTokenLifetime),
            cancellationToken);

        return CreateSession(credentials.Value.User, refreshToken);
    }

    public async Task<IssuedAuthSession?> RefreshAsync(
        string refreshToken,
        CancellationToken cancellationToken)
    {
        var replacement = CreateRefreshToken();
        var now = timeProvider.GetUtcNow();
        var user = await repository.RotateRefreshTokenAsync(
            HashRefreshToken(refreshToken),
            HashRefreshToken(replacement),
            now.Add(RefreshTokenLifetime),
            now,
            cancellationToken);

        return user is null ? null : CreateSession(user, replacement);
    }

    public Task RevokeAsync(string refreshToken, CancellationToken cancellationToken) =>
        repository.RevokeRefreshTokenAsync(
            HashRefreshToken(refreshToken),
            timeProvider.GetUtcNow(),
            cancellationToken);

    private IssuedAuthSession CreateSession(AuthenticatedUser user, string refreshToken)
    {
        var (accessToken, expiresAt) = accessTokenIssuer.Issue(user);
        return new IssuedAuthSession(accessToken, expiresAt, refreshToken, user);
    }

    private static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

    private static string CreateRefreshToken() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(64))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

    private static byte[] HashRefreshToken(string token) =>
        SHA256.HashData(Encoding.UTF8.GetBytes(token));
}
