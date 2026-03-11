using Microsoft.AspNetCore.Mvc;
using NkplmErp.Application.Interfaces;
using NkplmErp.Shared.DTOs;

namespace NkplmErp.API.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class LookupController(ILookupService lookupService) : ControllerBase
{
    private readonly ILookupService _lookupService = lookupService;

    [HttpGet("distinct")]
    public async Task<IActionResult> GetDistinctValues([FromQuery] string tableName, [FromQuery] string columnName)
    {
        try
        {
            var result = await _lookupService.GetDistinctValuesAsync(tableName, columnName);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpGet("items")]
    public async Task<IActionResult> GetLookupItems([FromQuery] string tableName, [FromQuery] string keyColumn, [FromQuery] string valueColumn)
    {
        try
        {
            var result = await _lookupService.GetLookupItemsAsync(tableName, keyColumn, valueColumn);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception)
        {
            return StatusCode(500, "An error occurred while fetching lookup items.");
        }
    }
}
