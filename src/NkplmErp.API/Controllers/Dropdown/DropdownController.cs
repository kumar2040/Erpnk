using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NkplmErp.Application.Interfaces.Dropdown;

namespace NkplmErp.API.Controllers.Dropdown
{
    [ApiController]
    [ApiVersion("1.0")]
    [Route("api/v{version:apiVersion}/[controller]")]
    [Authorize]
    public class DropdownController(IDropdownService dropdownService) : ControllerBase
    {
        private readonly IDropdownService _dropdownService = dropdownService;

        // GET api/v1/Dropdown/list?type=YarnOrderStatus&filter1=&filter2=
        //
        // Returns the real options only. The leading "All" / "Select" row is the
        // control's, so there is no all flag here.
        //
        // No per-page permission check on purpose: this serves every page's option
        // lists, so it has no single PageKey to gate on, and the payload is labels
        // rather than records. [Authorize] still keeps it off the public surface.
        [HttpGet("list")]
        public async Task<IActionResult> GetList(
            [FromQuery] string type,
            [FromQuery] string? filter1 = null,
            [FromQuery] string? filter2 = null)
        {
            if (string.IsNullOrWhiteSpace(type))
                return BadRequest("type is required.");

            var result = await _dropdownService.GetDropDownListAsync(type, filter1, filter2);
            return result.Succeeded ? Ok(result) : BadRequest(result);
        }
    }
}
