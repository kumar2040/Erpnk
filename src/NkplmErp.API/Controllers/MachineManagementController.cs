using System.Security.Claims;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NkplmErp.Application.Interfaces;
using NkplmErp.Shared.DTOs;

namespace NkplmErp.API.Controllers;

/// <summary>
/// Knit machine CRUD — list, create, edit (incl. gauge assignment), delete,
/// and activate/deactivate. Zero Trust: every endpoint re-checks the caller's
/// MachineManagement permissions on the server.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
[Authorize]
public class MachineManagementController(
    IMachineManagementService machineService,
    IRoleManagementService roleService) : ControllerBase
{
    private const string PageKey = "MachineManagement";
    private readonly IMachineManagementService _machineService = machineService;
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

    [HttpGet("machines")]
    public async Task<IActionResult> GetAllMachines()
    {
        if (!await CanViewAsync()) return Forbid();
        return Ok(await _machineService.GetAllMachinesAsync());
    }

    [HttpGet("gauges")]
    public async Task<IActionResult> GetGaugeOptions()
    {
        if (!await CanViewAsync()) return Forbid();
        return Ok(await _machineService.GetGaugeOptionsAsync());
    }

    [HttpPost("machines")]
    public async Task<IActionResult> SaveMachine([FromBody] SaveMachineRequest request)
    {
        if (!await CanEditAsync()) return Forbid();
        var result = await _machineService.SaveMachineAsync(request);
        return result.IsSuccess ? Ok(result) : BadRequest(result);
    }

    [HttpPost("machines/{machineId:int}/active")]
    public async Task<IActionResult> SetActive(int machineId, [FromQuery] bool isActive)
    {
        if (!await CanEditAsync()) return Forbid();
        var result = await _machineService.SetActiveAsync(machineId, isActive);
        return result.IsSuccess ? Ok(result) : BadRequest(result);
    }

    [HttpDelete("machines/{machineId:int}")]
    public async Task<IActionResult> DeleteMachine(int machineId)
    {
        if (!await CanDeleteAsync()) return Forbid();
        var result = await _machineService.DeleteMachineAsync(machineId);
        return result.IsSuccess ? Ok(result) : BadRequest(result);
    }
}
