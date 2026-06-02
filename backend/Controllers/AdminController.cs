using Bisp.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bisp.Api.Controllers;

[ApiController]
[Route("api/admin")]
[Authorize(Roles = "Admin")]
public sealed class AdminController : ControllerBase
{
    private readonly AggregationService _aggregationService;

    public AdminController(AggregationService aggregationService)
    {
        _aggregationService = aggregationService;
    }

    [HttpPost("aggregate")]
    public async Task<IActionResult> Aggregate(CancellationToken cancellationToken)
    {
        var count = await _aggregationService.RunAsync(cancellationToken);
        return Ok(new { gamesUpserted = count });
    }
}
