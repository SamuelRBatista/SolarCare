namespace SolarCare.Application.Authentication;

public interface IAuthRepository
{
    Task<AuthenticatedUser> RegisterAsync(
        RegisterCommand command,
        string passwordHash,
        byte[] refreshTokenHash,
        DateTimeOffset refreshTokenExpiresAt,
        CancellationToken cancellationToken);

    Task<(AuthenticatedUser User, string PasswordHash)?> FindByEmailAsync(
        string normalizedEmail,
        CancellationToken cancellationToken);

    Task SaveRefreshTokenAsync(
        Guid userId,
        byte[] tokenHash,
        DateTimeOffset expiresAt,
        CancellationToken cancellationToken);

    Task<AuthenticatedUser?> RotateRefreshTokenAsync(
        byte[] currentTokenHash,
        byte[] replacementTokenHash,
        DateTimeOffset replacementExpiresAt,
        DateTimeOffset now,
        CancellationToken cancellationToken);

    Task RevokeRefreshTokenAsync(
        byte[] tokenHash,
        DateTimeOffset now,
        CancellationToken cancellationToken);
}

public interface IPasswordService
{
    string Hash(string password);
    bool Verify(string passwordHash, string password);
}

public interface IAccessTokenIssuer
{
    (string Token, DateTimeOffset ExpiresAt) Issue(AuthenticatedUser user);
}

public interface IAuthService
{
    Task<IssuedAuthSession> RegisterAsync(
        RegisterCommand command,
        CancellationToken cancellationToken);

    Task<IssuedAuthSession?> LoginAsync(
        LoginCommand command,
        CancellationToken cancellationToken);

    Task<IssuedAuthSession?> RefreshAsync(
        string refreshToken,
        CancellationToken cancellationToken);

    Task RevokeAsync(string refreshToken, CancellationToken cancellationToken);
}

public sealed class AccountAlreadyExistsException : Exception
{
    public AccountAlreadyExistsException()
        : base("An account already exists for this email address.")
    {
    }
}
