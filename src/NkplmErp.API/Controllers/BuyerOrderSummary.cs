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
    public async Task<IActionResult> GetBuyerOrderSummary([FromQuery] int year, [FromQuery] string type, [FromQuery] int maxrec = 0)
    {
        var result = await _buyerOrderSummaryService.GetBuyerOrderSummaryAsync(year, type, maxrec);
        return Ok(result);
    }

    [HttpGet("years")]
    public async Task<IActionResult> GetBuyerYears([FromQuery] int? customerId)
    {
        Console.WriteLine($"DEBUG: API - GetBuyerYears called with customerId: {customerId}");
        var result = await _buyerOrderSummaryService.GetBuyerOrderYearsAsync(customerId);
        Console.WriteLine($"DEBUG: API - GetBuyerYears returning {result?.Count() ?? 0} items");
        return Ok(result);
    }

    [HttpGet("history/{buyerId}")]
    public async Task<IActionResult> GetBuyerHistory(int buyerId, [FromQuery] int? year = null)
    {
        Console.WriteLine($"DEBUG: API - GetBuyerHistory called for buyerId: {buyerId}, year: {(year.HasValue ? year.Value.ToString() : "null")}");

        var result = await _buyerOrderSummaryService.GetBuyerOrderHistoryAsync(buyerId, year);

        Console.WriteLine($"DEBUG: API - GetBuyerHistory returning {result?.Count() ?? 0} items");

        return Ok(result);
    }

    [HttpGet("profile/{buyerId}")]
    public async Task<IActionResult> GetBuyerProfile(int buyerId, [FromQuery] int? year = null)
    {

        var result = await _buyerOrderSummaryService.GetBuyerProfileAsync(buyerId, year);
        return Ok(result);
    }
    [HttpGet("absent-buyers")]
    public async Task<IActionResult> GetAbsentBuyer()
    {
        var result = await _buyerOrderSummaryService.GetAbsentBuyer();
        return Ok(result);

    }
    [HttpGet("order-status-detail")]
    public async Task<IActionResult> GetOrderStatusDetail([FromQuery] int year, [FromQuery] string status)
    {
        var result = await _buyerOrderSummaryService.GetOrderStatusDetailAsync(year, status);
        return Ok(result);
    }
    [HttpGet("productionflow/{buyerId}")]
    public async Task<IActionResult> GetProductionFlow(int buyerId,[FromQuery] string? orderNo = null)
    {
        var result = await _buyerOrderSummaryService.GetProductionFlowAsync(buyerId,orderNo);
        return Ok(result);
    }
}
