using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SolarCare.Application.Authentication;
using SolarCare.Application.Operations;

namespace SolarCare.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/dashboard")]
public sealed class DashboardController(
    IDashboardRepository repository,
    ITenantContext tenantContext) : ControllerBase
{
    [HttpGet("summary")]
    [Authorize(Roles = "CompanyAdmin,Manager,Attendant")]
    [ProducesResponseType<DashboardSummary>(StatusCodes.Status200OK)]
    public Task<DashboardSummary> Summary(CancellationToken cancellationToken) =>
        repository.GetDashboardSummaryAsync(tenantContext.TenantId, cancellationToken);
}
