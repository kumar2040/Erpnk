using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NkplmErp.ThirdPartyApi.Inbound.Base;
using NkplmErp.ThirdPartyApi.Inbound.Filter;
using NkplmErp.ThirdPartyApi.Common.Model;

namespace NkplmErp.ThirdPartyApi.Inbound.Controller
{
    /// <summary>
    /// Inbound API: PLM (PHP) calls NkplmErp here — the reverse of ThirdPartyManager. Not JWT-protected;
    /// every route is guarded by ThirdPartyInboundAuthFilter (rate limit, allowed base URL, credentials).
    /// Body: { username, password, parameters: { ... } }. Reply: { success, route, data } or 400 Bad Request.
    /// </summary>
    [ApiController]
    [ApiVersion("1.0")]
    [Route("api/v{version:apiVersion}/[controller]")]
    [AllowAnonymous]
    [TypeFilter(typeof(ThirdPartyInboundAuthFilter))]
    public class ThirdPartyInboundController : ControllerBase
    {
        // POST api/v1/ThirdPartyInbound/ping   { username, password, parameters: {} }
        // Connectivity check for the PHP side: passes only when every gateway check passes.
        [HttpPost("ping")]
        public Task<IActionResult> PingAsync([FromBody] ThirdPartyRequestModel request) =>
            ThirdPartyInboundResponse.RespondAsync(HttpContext, "ping", request.Parameters,
                _ => Task.FromResult<object?>(new { message = "pong", serverTime = DateTime.Now }));
    }
}
