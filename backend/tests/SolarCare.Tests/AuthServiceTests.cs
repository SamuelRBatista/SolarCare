using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;
using SolarCare.Api.Authentication;
using SolarCare.Application.Authentication;
using SolarCare.Domain;

namespace SolarCare.Tests;

public sealed class AuthServiceTests
{
    [Fact]
    public async Task Register_NormalizesEmailAndStoresOnlyRefreshTokenHash()
    {
        var repository = new CapturingAuthRepository();
        var service = new AuthService(
            repository,
            new StubPasswordService(),
            new StubAccessTokenIssuer(),
            TimeProvider.System);

        var session = await service.RegisterAsync(
            new RegisterCommand(
                "  Empresa Solar  ",
                "  Pessoa Responsável ",
                "  Teste@Example.com ",
                "A-long-test-password"),
            CancellationToken.None);

        Assert.Equal("Empresa Solar", repository.RegisteredCommand?.CompanyName);
        Assert.Equal("Pessoa Responsável", repository.RegisteredCommand?.FullName);
        Assert.Equal("teste@example.com", repository.RegisteredCommand?.Email);
        Assert.Equal("hashed:A-long-test-password", repository.PasswordHash);
        Assert.Equal(nameof(AccessRole.CompanyAdmin), session.User.Role.ToString());
        Assert.NotNull(repository.RefreshTokenHash);
        Assert.Equal(
            SHA256.HashData(Encoding.UTF8.GetBytes(session.RefreshToken)),
            repository.RefreshTokenHash);
        Assert.Equal("access-token", session.AccessToken);
    }

    [Fact]
    public async Task Refresh_RotatesTokenAndPersistsOnlyTokenHashes()
    {
        var repository = new CapturingAuthRepository();
        var service = new AuthService(
            repository,
            new StubPasswordService(),
            new StubAccessTokenIssuer(),
            TimeProvider.System);
        var initialSession = await service.RegisterAsync(
            new RegisterCommand("Solar", "Admin", "admin@example.com", "A-long-test-password"),
            CancellationToken.None);

        var refreshedSession = await service.RefreshAsync(initialSession.RefreshToken, CancellationToken.None);

        Assert.NotNull(refreshedSession);
        Assert.NotEqual(initialSession.RefreshToken, refreshedSession.RefreshToken);
        Assert.Equal(
            SHA256.HashData(Encoding.UTF8.GetBytes(initialSession.RefreshToken)),
            repository.RotatedFromTokenHash);
        Assert.Equal(
            SHA256.HashData(Encoding.UTF8.GetBytes(refreshedSession.RefreshToken)),
            repository.RotatedToTokenHash);
        Assert.NotEqual(initialSession.RefreshToken, Encoding.UTF8.GetString(repository.RotatedFromTokenHash!));
    }

    [Fact]
    public void PasswordService_HashesAndVerifiesWithoutStoringPlaintext()
    {
        var passwordService = new SolarCare.Infrastructure.Authentication.PasswordService();
        const string password = "a-long-test-password";

        var hash = passwordService.Hash(password);

        Assert.NotEqual(password, hash);
        Assert.True(passwordService.Verify(hash, password));
        Assert.False(passwordService.Verify(hash, "incorrect-password"));
    }

    [Fact]
    public void TenantContext_RejectsUnauthenticatedRequest()
    {
        var context = new HttpTenantContext(new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext()
        });

        Assert.Throws<UnauthorizedAccessException>(() => context.TenantId);
    }

    private sealed class CapturingAuthRepository : IAuthRepository
    {
        public RegisterCommand? RegisteredCommand { get; private set; }
        public string? PasswordHash { get; private set; }
        public byte[]? RefreshTokenHash { get; private set; }
        public byte[]? RotatedFromTokenHash { get; private set; }
        public byte[]? RotatedToTokenHash { get; private set; }
        private AuthenticatedUser? RegisteredUser { get; set; }

        public Task<AuthenticatedUser> RegisterAsync(
            RegisterCommand command,
            string passwordHash,
            byte[] refreshTokenHash,
            DateTimeOffset refreshTokenExpiresAt,
            CancellationToken cancellationToken)
        {
            RegisteredCommand = command;
            PasswordHash = passwordHash;
            RefreshTokenHash = refreshTokenHash;
            RegisteredUser = new AuthenticatedUser(
                Guid.NewGuid(),
                Guid.NewGuid(),
                command.CompanyName,
                command.FullName,
                command.Email,
                AccessRole.CompanyAdmin);
            return Task.FromResult(RegisteredUser);
        }

        public Task<(AuthenticatedUser User, string PasswordHash)?> FindByEmailAsync(
            string normalizedEmail,
            CancellationToken cancellationToken) =>
            Task.FromResult<(AuthenticatedUser User, string PasswordHash)?>(null);

        public Task SaveRefreshTokenAsync(
            Guid userId,
            byte[] tokenHash,
            DateTimeOffset expiresAt,
            CancellationToken cancellationToken)
        {
            RefreshTokenHash = tokenHash;
            return Task.CompletedTask;
        }

        public Task<AuthenticatedUser?> RotateRefreshTokenAsync(
            byte[] currentTokenHash,
            byte[] replacementTokenHash,
            DateTimeOffset replacementExpiresAt,
            DateTimeOffset now,
            CancellationToken cancellationToken)
        {
            RotatedFromTokenHash = currentTokenHash;
            RotatedToTokenHash = replacementTokenHash;
            return Task.FromResult(RegisteredUser);
        }

        public Task RevokeRefreshTokenAsync(
            byte[] tokenHash,
            DateTimeOffset now,
            CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class StubPasswordService : IPasswordService
    {
        public string Hash(string password) => $"hashed:{password}";

        public bool Verify(string passwordHash, string password) =>
            passwordHash == $"hashed:{password}";
    }

    private sealed class StubAccessTokenIssuer : IAccessTokenIssuer
    {
        public (string Token, DateTimeOffset ExpiresAt) Issue(AuthenticatedUser user) =>
            ("access-token", DateTimeOffset.UtcNow.AddMinutes(15));
    }
}
