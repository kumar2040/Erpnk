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
    [HttpGet("departmentstock/{Department}")]
    public async Task<IActionResult> GetdepartmentStock([FromQuery] string? OrderNo, string Department)
    {
        var result = await _buyerOrderSummaryService.GetdepartmentStockAsync(OrderNo,Department);
        return Ok(result);
    }
    [HttpGet("order-view/{orderNo}")]
    public async Task<IActionResult> GetOrderViewDataAsync(string orderNo)
    {
        var result = await _buyerOrderSummaryService.GetOrderViewDataAsync(orderNo);
        return Ok(result);
    }

    [HttpGet("style-details/{styleNo}")]
    public async Task<IActionResult> GetStyleDetails(string styleNo)
    {
        var result = await _buyerOrderSummaryService.GetStyleDetailsAsync(styleNo);
        return Ok(result);
    }

    [HttpGet("buyers-orders/{buyerId}")]
    public async Task<IActionResult> GetBuyersOrders(int buyerId, [FromQuery] int flag)
    {
        var result = await _buyerOrderSummaryService.GetBuyersOrdersAsync(buyerId, flag);
        return Ok(result);
    }
}
