using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using NkplmErp.Application.Interfaces;

namespace NkplmErp.ThirdPartyApi.Controller
{
    /// <summary>Shared permission checks for the page controllers (ProductionPlan, Dashboard, ...). Not routable itself.</summary>
    public abstract class PermissionController(IRoleManagementService roleService) : ControllerBase
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