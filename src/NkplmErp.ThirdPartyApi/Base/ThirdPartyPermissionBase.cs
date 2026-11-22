using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using NkplmErp.Application.Interfaces;

namespace NkplmErp.ThirdPartyApi.Base
{
    /// <summary>Base class (not a controller) with the CanEdit/CanView checks the page controllers inherit.</summary>
    public abstract class ThirdPartyPermissionBase(IRoleManagementService roleService) : ControllerBase
    {
        protected async Task<bool> CanEditAsync(string module)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return false;
            var perms = await roleService.GetUserPermissionsAsync(userId);
            return perms.CanEdit(module);
        }

        protected async Task<bool> CanViewAsync(string module)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return false;
            var perms = await roleService.GetUserPermissionsAsync(userId);
            return perms.CanView(module);
        }
    }
}