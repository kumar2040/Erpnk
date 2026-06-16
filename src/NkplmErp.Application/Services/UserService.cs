using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using NkplmErp.Application.Interfaces;
using NkplmErp.Domain.Entities;
using NkplmErp.Shared.DTOs;

namespace NkplmErp.Application.Services;

public class UserService : IUserService
{
    private readonly UserManager<User> _userManager;
    private readonly RoleManager<Role> _roleManager;
    private readonly IAuditService _auditService;
    private readonly ICurrentUserService _currentUserService;

    public UserService(
        UserManager<User> userManager,
        RoleManager<Role> roleManager,
        IAuditService auditService,
        ICurrentUserService currentUserService)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _auditService = auditService;
        _currentUserService = currentUserService;
    }

    public async Task<IEnumerable<UserListItemDto>> GetAllUsersAsync()
    {
        var users = await _userManager.Users
            .Where(u => u.IsActive)
            .OrderByDescending(u => u.CreatedAt)
            .ToListAsync();

        var userDtos = new List<UserListItemDto>();

        foreach (var user in users)
        {
            var roles = await _userManager.GetRolesAsync(user);
            userDtos.Add(new UserListItemDto
            {
                Id = user.Id,
                FullName = $"{user.FirstName} {user.LastName}",
                Email = user.Email ?? string.Empty,
                IsActive = user.IsActive,
                CreatedAt = user.CreatedAt,
                LastLoginAt = user.LastLoginAt,
                AssignedGauge = user.AssignedGauge,
                Roles = roles
            });
        }

        return userDtos;
    }

    public async Task<UserResponseDto?> GetUserByIdAsync(string userId)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null) return null;

        var roles = await _userManager.GetRolesAsync(user);

        return new UserResponseDto
        {
            Id = user.Id,
            Email = user.Email ?? string.Empty,
            FirstName = user.FirstName,
            LastName = user.LastName,
            BranchId = user.BranchId,
            IsActive = user.IsActive,
            MfaEnabled = user.MfaEnabled,
            CreatedAt = user.CreatedAt,
            LastLoginAt = user.LastLoginAt,
            AssignedGauge = user.AssignedGauge,
            Roles = roles
        };
    }

    public async Task<(bool Success, string Message, string? UserId)> CreateUserAsync(CreateUserDto dto)
    {
        // Check if email already exists
        var existingUser = await _userManager.FindByEmailAsync(dto.Email);
        if (existingUser != null)
        {
            return (false, "A user with this email already exists.", null);
        }

        // Validate roles exist
        foreach (var roleName in dto.Roles)
        {
            var roleExists = await _roleManager.RoleExistsAsync(roleName);
            if (!roleExists)
            {
                return (false, $"Role '{roleName}' does not exist.", null);
            }
        }

        var user = new User
        {
            UserName = dto.Email,
            Email = dto.Email,
            FirstName = dto.FirstName,
            LastName = dto.LastName,
            BranchId = dto.BranchId,
            IsActive = dto.IsActive,
            AssignedGauge = dto.AssignedGauge,
            CreatedAt = DateTime.UtcNow
        };

        var result = await _userManager.CreateAsync(user, dto.Password);
        if (!result.Succeeded)
        {
            var errors = string.Join(", ", result.Errors.Select(e => e.Description));
            return (false, errors, null);
        }

        // Assign roles
        if (dto.Roles.Any())
        {
            var roleResult = await _userManager.AddToRolesAsync(user, dto.Roles);
            if (!roleResult.Succeeded)
            {
                // Rollback user creation if role assignment fails
                await _userManager.DeleteAsync(user);
                var errors = string.Join(", ", roleResult.Errors.Select(e => e.Description));
                return (false, $"User created but role assignment failed: {errors}", null);
            }
        }

        var currentUserId = _currentUserService.UserId ?? "system";
        await _auditService.LogAsync(currentUserId, "UserCreated", "User", user.Id, "", 
            $"User created: {user.Email}, Roles: {string.Join(", ", dto.Roles)}");

        return (true, "User created successfully.", user.Id);
    }

    public async Task<(bool Success, string Message)> UpdateUserAsync(string userId, UpdateUserDto dto)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
        {
            return (false, "User not found.");
        }

        // Check if email is being changed and if it's already taken
        if (user.Email != dto.Email)
        {
            var existingUser = await _userManager.FindByEmailAsync(dto.Email);
            if (existingUser != null && existingUser.Id != userId)
            {
                return (false, "Email is already in use by another user.");
            }
        }

        // Validate roles exist
        foreach (var roleName in dto.Roles)
        {
            var roleExists = await _roleManager.RoleExistsAsync(roleName);
            if (!roleExists)
            {
                return (false, $"Role '{roleName}' does not exist.");
            }
        }

        var oldValues = $"Email: {user.Email}, Name: {user.FirstName} {user.LastName}, Active: {user.IsActive}";

        // Update user properties
        user.FirstName = dto.FirstName;
        user.LastName = dto.LastName;
        user.Email = dto.Email;
        user.UserName = dto.Email;
        user.BranchId = dto.BranchId;
        user.IsActive = dto.IsActive;
        user.AssignedGauge = dto.AssignedGauge;

        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            var errors = string.Join(", ", result.Errors.Select(e => e.Description));
            return (false, errors);
        }

        // Update roles
        var currentRoles = await _userManager.GetRolesAsync(user);
        var rolesToRemove = currentRoles.Except(dto.Roles).ToList();
        var rolesToAdd = dto.Roles.Except(currentRoles).ToList();

        if (rolesToRemove.Any())
        {
            await _userManager.RemoveFromRolesAsync(user, rolesToRemove);
        }

        if (rolesToAdd.Any())
        {
            await _userManager.AddToRolesAsync(user, rolesToAdd);
        }

        var newValues = $"Email: {user.Email}, Name: {user.FirstName} {user.LastName}, Active: {user.IsActive}";
        var currentUserId = _currentUserService.UserId ?? "system";
        await _auditService.LogAsync(currentUserId, "UserUpdated", "User", user.Id, oldValues, newValues);

        return (true, "User updated successfully.");
    }

    public async Task<(bool Success, string Message)> DeleteUserAsync(string userId)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
        {
            return (false, "User not found.");
        }

        // Soft delete - set IsActive to false
        user.IsActive = false;
        var result = await _userManager.UpdateAsync(user);

        if (!result.Succeeded)
        {
            var errors = string.Join(", ", result.Errors.Select(e => e.Description));
            return (false, errors);
        }

        var currentUserId = _currentUserService.UserId ?? "system";
        await _auditService.LogAsync(currentUserId, "UserDeleted", "User", user.Id, 
            $"Active: true", $"Active: false (Soft Delete)");

        return (true, "User deleted successfully.");
    }

    public async Task<(bool Success, string Message)> ResetPasswordAsync(string userId, string newPassword)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
        {
            return (false, "User not found.");
        }

        // Remove existing password and set new one
        var removeResult = await _userManager.RemovePasswordAsync(user);
        if (!removeResult.Succeeded)
        {
            var errors = string.Join(", ", removeResult.Errors.Select(e => e.Description));
            return (false, $"Failed to remove old password: {errors}");
        }

        var addResult = await _userManager.AddPasswordAsync(user, newPassword);
        if (!addResult.Succeeded)
        {
            var errors = string.Join(", ", addResult.Errors.Select(e => e.Description));
            return (false, $"Failed to set new password: {errors}");
        }

        var currentUserId = _currentUserService.UserId ?? "system";
        await _auditService.LogAsync(currentUserId, "PasswordReset", "User", user.Id, "", 
            "Admin password reset");

        return (true, "Password reset successfully.");
    }

    public async Task<IEnumerable<string>> GetUserRolesAsync(string userId)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null) return Enumerable.Empty<string>();

        return await _userManager.GetRolesAsync(user);
    }

    public async Task<(bool Success, string Message)> UpdateUserRolesAsync(string userId, IEnumerable<string> roles)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
        {
            return (false, "User not found.");
        }

        // Validate all roles exist
        foreach (var roleName in roles)
        {
            var roleExists = await _roleManager.RoleExistsAsync(roleName);
            if (!roleExists)
            {
                return (false, $"Role '{roleName}' does not exist.");
            }
        }

        var currentRoles = await _userManager.GetRolesAsync(user);
        var rolesToRemove = currentRoles.Except(roles).ToList();
        var rolesToAdd = roles.Except(currentRoles).ToList();

        if (rolesToRemove.Any())
        {
            var removeResult = await _userManager.RemoveFromRolesAsync(user, rolesToRemove);
            if (!removeResult.Succeeded)
            {
                var errors = string.Join(", ", removeResult.Errors.Select(e => e.Description));
                return (false, $"Failed to remove roles: {errors}");
            }
        }

        if (rolesToAdd.Any())
        {
            var addResult = await _userManager.AddToRolesAsync(user, rolesToAdd);
            if (!addResult.Succeeded)
            {
                var errors = string.Join(", ", addResult.Errors.Select(e => e.Description));
                return (false, $"Failed to add roles: {errors}");
            }
        }

        var currentUserId = _currentUserService.UserId ?? "system";
        await _auditService.LogAsync(currentUserId, "UserRolesUpdated", "User", user.Id, 
            $"Old: {string.Join(", ", currentRoles)}", 
            $"New: {string.Join(", ", roles)}");

        return (true, "User roles updated successfully.");
    }
}
