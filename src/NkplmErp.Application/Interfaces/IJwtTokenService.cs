using System.Security.Claims;
using NkplmErp.Domain.Entities;

namespace NkplmErp.Application.Interfaces;

public interface IJwtTokenService
{
    Task<string> GenerateTokenAsync(User user, IList<string> roles);
    string GenerateRefreshToken();
    ClaimsPrincipal GetClaimsPrincipalFromExpiredToken(string token);
}
