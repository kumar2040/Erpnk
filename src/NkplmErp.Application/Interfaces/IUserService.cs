using NkplmErp.Shared.DTOs;

namespace NkplmErp.Application.Interfaces;

public interface IUserService
{
    Task<IEnumerable<UserListItemDto>> GetAllUsersAsync();
    Task<UserResponseDto?> GetUserByIdAsync(string userId);
    Task<(bool Success, string Message, string? UserId)> CreateUserAsync(CreateUserDto dto);
    Task<(bool Success, string Message)> UpdateUserAsync(string userId, UpdateUserDto dto);
    Task<(bool Success, string Message)> DeleteUserAsync(string userId);
    Task<(bool Success, string Message)> ResetPasswordAsync(string userId, string newPassword);
    Task<IEnumerable<string>> GetUserRolesAsync(string userId);
    Task<(bool Success, string Message)> UpdateUserRolesAsync(string userId, IEnumerable<string> roles);
}
