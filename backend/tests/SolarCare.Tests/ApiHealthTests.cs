using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using SolarCare.Api.Authentication;
using SolarCare.Api.Controllers;
using SolarCare.Application.Authentication;
using SolarCare.Domain;

namespace SolarCare.Tests;

public sealed class ApiHealthTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public ApiHealthTests(WebApplicationFactory<Program> factory)
    {
        var oldConnectionString = Environment.GetEnvironmentVariable("ConnectionStrings__SolarCare");
        var oldSigningKey = Environment.GetEnvironmentVariable("Jwt__SigningKey");
        var oldEncryptionKey = Environment.GetEnvironmentVariable("PersonalData__EncryptionKey");
        Environment.SetEnvironmentVariable(
            "ConnectionStrings__SolarCare",
            "Host=localhost;Database=solarcare;Username=solarcare;Password=test");
        Environment.SetEnvironmentVariable("Jwt__SigningKey", Convert.ToBase64String(new byte[32]));
        Environment.SetEnvironmentVariable("PersonalData__EncryptionKey", Convert.ToBase64String(new byte[32]));
        try
        {
            _client = factory.CreateClient();
        }
        finally
        {
            Environment.SetEnvironmentVariable("ConnectionStrings__SolarCare", oldConnectionString);
            Environment.SetEnvironmentVariable("Jwt__SigningKey", oldSigningKey);
            Environment.SetEnvironmentVariable("PersonalData__EncryptionKey", oldEncryptionKey);
        }
    }

    [Fact]
    public async Task LiveEndpoint_ReturnsHealthyStatus()
    {
        using var response = await _client.GetAsync("/api/health/live");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(
            """{"status":"healthy"}""",
            await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task CurrentUser_RejectsUnauthenticatedRequest()
    {
        using var response = await _client.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CurrentUser_ReturnsTenantFromSignedAccessToken()
    {
        var tenantId = Guid.NewGuid();
        var issuer = new JwtAccessTokenIssuer(
            new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Jwt:SigningKey"] = Convert.ToBase64String(new byte[32])
                })
                .Build(),
            TimeProvider.System);
        var (token, _) = issuer.Issue(new AuthenticatedUser(
            Guid.NewGuid(),
            tenantId,
            "Solar Teste",
            "Pessoa Teste",
            "teste@example.com",
            AccessRole.CompanyAdmin));
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        using var response = await _client.GetAsync("/api/auth/me");
        var currentUser = await response.Content.ReadFromJsonAsync<AuthController.CurrentUserResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(tenantId, currentUser?.TenantId);
        Assert.Equal(nameof(AccessRole.CompanyAdmin), currentUser?.Role);
    }

    [Fact]
    public async Task Registration_RejectsWeakPasswordBeforeDatabaseAccess()
    {
        using var response = await _client.PostAsJsonAsync(
            "/api/auth/register",
            new
            {
                companyName = "Empresa de teste",
                fullName = "Pessoa de teste",
                email = "test@example.com",
                password = "short"
            });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
