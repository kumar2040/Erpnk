using System.Security.Claims;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NkplmErp.Application.Interfaces;
using NkplmErp.Shared.DTOs;

namespace NkplmErp.API.Controllers;

/// <summary>
/// Knitter CRUD — list, create, edit (incl. gauge assignment), delete,
/// and activate/deactivate. Zero Trust: every endpoint re-checks the
/// caller's KnitterManagement permissions on the server.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
[Authorize]
public class KnitterManagementController(
    IKnitterManagementService knitterService,
    IRoleManagementService roleService) : ControllerBase
{
    private const string PageKey = "KnitterManagement";
    private readonly IKnitterManagementService _knitterService = knitterService;
    private readonly IRoleManagementService _roleService = roleService;

    private string GetCurrentUserId() =>
        User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? User.FindFirstValue("sub")
        ?? throw new UnauthorizedAccessException("User identity not found in token.");

    private async Task<bool> CanViewAsync()
    {
        var perms = await _roleService.GetUserPermissionsAsync(GetCurrentUserId());
        return perms.CanView(PageKey);
    }

    private async Task<bool> CanEditAsync()
    {
        var perms = await _roleService.GetUserPermissionsAsync(GetCurrentUserId());
        return perms.CanEdit(PageKey);
    }

    private async Task<bool> CanDeleteAsync()
    {
        var perms = await _roleService.GetUserPermissionsAsync(GetCurrentUserId());
        return perms.CanDelete(PageKey);
    }

    [HttpGet("knitters")]
    public async Task<IActionResult> GetAllKnitters()
    {
        if (!await CanViewAsync()) return Forbid();
        return Ok(await _knitterService.GetAllKnittersAsync());
    }

    [HttpGet("gauges")]
    public async Task<IActionResult> GetGaugeOptions()
    {
        if (!await CanViewAsync()) return Forbid();
        return Ok(await _knitterService.GetGaugeOptionsAsync());
    }

    [HttpPost("knitters")]
    public async Task<IActionResult> SaveKnitter([FromBody] SaveKnitterRequest request)
    {
        if (!await CanEditAsync()) return Forbid();
        var result = await _knitterService.SaveKnitterAsync(request);
        return result.IsSuccess ? Ok(result) : BadRequest(result);
    }

    [HttpPost("knitters/{cardNo:int}/active")]
    public async Task<IActionResult> SetActive(int cardNo, [FromQuery] bool isActive)
    {
        if (!await CanEditAsync()) return Forbid();
        var result = await _knitterService.SetActiveAsync(cardNo, isActive);
        return result.IsSuccess ? Ok(result) : BadRequest(result);
    }

    [HttpDelete("knitters/{cardNo:int}")]
    public async Task<IActionResult> DeleteKnitter(int cardNo)
    {
        if (!await CanDeleteAsync()) return Forbid();
        var result = await _knitterService.DeleteKnitterAsync(cardNo);
        return result.IsSuccess ? Ok(result) : BadRequest(result);
    }
}
