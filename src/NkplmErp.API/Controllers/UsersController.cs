using System.Security.Claims;
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
public class UsersController : ControllerBase
{
    private readonly IUserService _userService;
    private readonly IRoleManagementService _roleService;
    private readonly IIdentityService _identityService;

    public UsersController(IUserService userService, IRoleManagementService roleService, IIdentityService identityService)
    {
        _userService = userService;
        _roleService = roleService;
        _identityService = identityService;
    }

    private string GetCurrentUserId() =>
        User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? User.FindFirstValue("sub")
        ?? throw new UnauthorizedAccessException("User identity not found in token.");

    /// <summary>
    /// Get all users
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAllUsers()
    {
        var userId = GetCurrentUserId();
        var callerPerms = await _roleService.GetUserPermissionsAsync(userId);
        if (!callerPerms.CanView("Users"))
            return Forbid();

        var users = await _userService.GetAllUsersAsync();
        return Ok(users);
    }

    /// <summary>
    /// Get user by ID
    /// </summary>
    [HttpGet("{id}")]
    public async Task<IActionResult> GetUserById(string id)
    {
        var userId = GetCurrentUserId();
        var callerPerms = await _roleService.GetUserPermissionsAsync(userId);
        if (!callerPerms.CanView("Users"))
            return Forbid();

        var user = await _userService.GetUserByIdAsync(id);
        if (user == null)
        {
            return NotFound(new { message = "User not found." });
        }

        return Ok(user);
    }

    /// <summary>
    /// Create a new user
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> CreateUser([FromBody] CreateUserDto dto)
    {
        var userId = GetCurrentUserId();
        var callerPerms = await _roleService.GetUserPermissionsAsync(userId);
        if (!callerPerms.CanEdit("Users"))
            return Forbid();

        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var (success, message, newUserId) = await _userService.CreateUserAsync(dto);
        
        if (!success)
        {
            return BadRequest(new { message });
        }

        var user = await _userService.GetUserByIdAsync(newUserId!);
        return CreatedAtAction(nameof(GetUserById), new { id = newUserId }, user);
    }

    /// <summary>
    /// Update an existing user
    /// </summary>
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateUser(string id, [FromBody] UpdateUserDto dto)
    {
        var userId = GetCurrentUserId();
        var callerPerms = await _roleService.GetUserPermissionsAsync(userId);
        if (!callerPerms.CanEdit("Users"))
            return Forbid();

        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var (success, message) = await _userService.UpdateUserAsync(id, dto);
        
        if (!success)
        {
            return BadRequest(new { message });
        }

        var user = await _userService.GetUserByIdAsync(id);
        return Ok(user);
    }

    /// <summary>
    /// Delete a user (soft delete)
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteUser(string id)
    {
        var userId = GetCurrentUserId();
        var callerPerms = await _roleService.GetUserPermissionsAsync(userId);
        if (!callerPerms.CanDelete("Users"))
            return Forbid();

        var (success, message) = await _userService.DeleteUserAsync(id);
        
        if (!success)
        {
            return BadRequest(new { message });
        }

        return Ok(new { message });
    }

    /// <summary>
    /// Reset user password (admin only)
    /// </summary>
    [HttpPost("{id}/reset-password")]
    public async Task<IActionResult> ResetPassword(string id, [FromBody] string newPassword)
    {
        var userId = GetCurrentUserId();
        var callerPerms = await _roleService.GetUserPermissionsAsync(userId);
        if (!callerPerms.CanEdit("Users"))
            return Forbid();

        if (string.IsNullOrWhiteSpace(newPassword))
        {
            return BadRequest(new { message = "Password cannot be empty." });
        }

        var (success, message) = await _userService.ResetPasswordAsync(id, newPassword);
        
        if (!success)
        {
            return BadRequest(new { message });
        }

        return Ok(new { message });
    }

    /// <summary>
    /// Force-logout a user: ends all their active sessions immediately
    /// (rotates security stamp + revokes refresh tokens).
    /// </summary>
    [HttpPost("{id}/force-logout")]
    public async Task<IActionResult> ForceLogout(string id)
    {
        var callerId = GetCurrentUserId();
        var callerPerms = await _roleService.GetUserPermissionsAsync(callerId);
        if (!callerPerms.CanEdit("Users"))
            return Forbid();

        var result = await _identityService.ForceLogoutAsync(id, callerId);
        return result.IsSuccess
            ? Ok(new { message = result.Message })
            : BadRequest(new { message = result.Message });
    }

    /// <summary>
    /// Get user's roles
    /// </summary>
    [HttpGet("{id}/roles")]
    public async Task<IActionResult> GetUserRoles(string id)
    {
        var userId = GetCurrentUserId();
        var callerPerms = await _roleService.GetUserPermissionsAsync(userId);
        if (!callerPerms.CanView("Users"))
            return Forbid();

        var roles = await _userService.GetUserRolesAsync(id);
        return Ok(roles);
    }

    /// <summary>
    /// Update user's roles
    /// </summary>
    [HttpPut("{id}/roles")]
    public async Task<IActionResult> UpdateUserRoles(string id, [FromBody] IEnumerable<string> roles)
    {
        var userId = GetCurrentUserId();
        var callerPerms = await _roleService.GetUserPermissionsAsync(userId);
        if (!callerPerms.CanEdit("Users"))
            return Forbid();

        var (success, message) = await _userService.UpdateUserRolesAsync(id, roles);
        
        if (!success)
        {
            return BadRequest(new { message });
        }

        return Ok(new { message });
    }
}
