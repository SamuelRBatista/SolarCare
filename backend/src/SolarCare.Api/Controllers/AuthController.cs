using System.Security.Claims;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SolarCare.Application.Authentication;

namespace SolarCare.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(
    IAuthService authService,
    IValidator<RegisterCommand> registerValidator,
    IValidator<LoginCommand> loginValidator,
    IWebHostEnvironment environment) : ControllerBase
{
    private static readonly TimeSpan RefreshTokenLifetime = TimeSpan.FromDays(7);

    [HttpPost("register")]
    [EnableRateLimiting("auth")]
    [AllowAnonymous]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Register(
        RegisterRequest request,
        CancellationToken cancellationToken)
    {
        var command = new RegisterCommand(
            request.CompanyName,
            request.FullName,
            request.Email,
            request.Password);
        var validation = await registerValidator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
        {
            return BadRequest(new ValidationProblemDetails(validation.ToDictionary()));
        }

        try
        {
            var session = await authService.RegisterAsync(command, cancellationToken);
            WriteRefreshCookie(session.RefreshToken);
            return StatusCode(StatusCodes.Status201Created, ToResponse(session));
        }
        catch (AccountAlreadyExistsException)
        {
            return Conflict(new ProblemDetails
            {
                Title = "Conta não disponível",
                Detail = "Não foi possível criar uma conta com os dados informados.",
                Status = StatusCodes.Status409Conflict
            });
        }
    }

    [HttpPost("login")]
    [EnableRateLimiting("auth")]
    [AllowAnonymous]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login(
        LoginRequest request,
        CancellationToken cancellationToken)
    {
        var command = new LoginCommand(request.Email, request.Password);
        var validation = await loginValidator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
        {
            return BadRequest(new ValidationProblemDetails(validation.ToDictionary()));
        }

        var session = await authService.LoginAsync(command, cancellationToken);
        if (session is null)
        {
            return Unauthorized(new ProblemDetails
            {
                Title = "Credenciais inválidas",
                Detail = "E-mail ou senha inválidos.",
                Status = StatusCodes.Status401Unauthorized
            });
        }

        WriteRefreshCookie(session.RefreshToken);
        return Ok(ToResponse(session));
    }

    [HttpPost("refresh")]
    [EnableRateLimiting("auth")]
    [AllowAnonymous]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Refresh(CancellationToken cancellationToken)
    {
        if (!Request.Cookies.TryGetValue("solarcare_refresh", out var refreshToken) ||
            string.IsNullOrWhiteSpace(refreshToken))
        {
            return Unauthorized();
        }

        var session = await authService.RefreshAsync(refreshToken, cancellationToken);
        if (session is null)
        {
            ClearRefreshCookie();
            return Unauthorized();
        }

        WriteRefreshCookie(session.RefreshToken);
        return Ok(ToResponse(session));
    }

    [HttpPost("logout")]
    [EnableRateLimiting("auth")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        if (Request.Cookies.TryGetValue("solarcare_refresh", out var refreshToken) &&
            !string.IsNullOrWhiteSpace(refreshToken))
        {
            await authService.RevokeAsync(refreshToken, cancellationToken);
        }

        ClearRefreshCookie();
        return NoContent();
    }

    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType<CurrentUserResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public IActionResult Me()
    {
        if (!Guid.TryParse(User.FindFirstValue("tenant_id"), out var tenantId) ||
            !Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
        {
            return Unauthorized();
        }

        return Ok(new CurrentUserResponse(
            userId,
            tenantId,
            User.FindFirstValue(ClaimTypes.Name) ?? string.Empty,
            User.FindFirstValue(ClaimTypes.Email) ?? string.Empty,
            User.FindFirstValue(ClaimTypes.Role) ?? string.Empty));
    }

    private void WriteRefreshCookie(string token)
    {
        Response.Cookies.Append(
            "solarcare_refresh",
            token,
            new CookieOptions
            {
                HttpOnly = true,
                Secure = Request.IsHttps || !environment.IsDevelopment(),
                SameSite = SameSiteMode.Strict,
                Path = "/api/auth",
                MaxAge = RefreshTokenLifetime,
                IsEssential = true
            });
    }

    private void ClearRefreshCookie()
    {
        Response.Cookies.Delete(
            "solarcare_refresh",
            new CookieOptions
            {
                HttpOnly = true,
                Secure = Request.IsHttps || !environment.IsDevelopment(),
                SameSite = SameSiteMode.Strict,
                Path = "/api/auth"
            });
    }

    private static AuthResponse ToResponse(IssuedAuthSession session) =>
        new(
            session.AccessToken,
            session.AccessTokenExpiresAt,
            new UserResponse(
                session.User.Id,
                session.User.TenantId,
                session.User.CompanyName,
                session.User.FullName,
                session.User.Email,
                session.User.Role.ToString()));

    public sealed record RegisterRequest(
        string CompanyName,
        string FullName,
        string Email,
        string Password);

    public sealed record LoginRequest(string Email, string Password);

    public sealed record AuthResponse(
        string AccessToken,
        DateTimeOffset AccessTokenExpiresAt,
        UserResponse User);

    public sealed record UserResponse(
        Guid Id,
        Guid TenantId,
        string CompanyName,
        string FullName,
        string Email,
        string Role);

    public sealed record CurrentUserResponse(
        Guid Id,
        Guid TenantId,
        string FullName,
        string Email,
        string Role);
}
