using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NkplmErp.Application.Interfaces;
using NkplmErp.Shared.DTOs;

namespace NkplmErp.API.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
[Authorize]
public class BuyerOrderSummaryController(IBuyerOrderSummaryService buyerOrderSummaryService) : ControllerBase
{
    private readonly IBuyerOrderSummaryService _buyerOrderSummaryService = buyerOrderSummaryService;

    [HttpGet]
    public async Task<IActionResult> GetBuyerOrderSummary([FromQuery] int year, [FromQuery] string type)
    {
        var result = await _buyerOrderSummaryService.GetBuyerOrderSummaryAsync(year, type);
        return Ok(result);
    }
}
