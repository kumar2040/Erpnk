using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NkplmErp.ThirdPartyApi.Common.Model;
using NkplmErp.ThirdPartyApi.Plm.Inbound.Base;
using NkplmErp.ThirdPartyApi.Plm.Inbound.Filter;

namespace NkplmErp.ThirdPartyApi.Plm.Inbound.Controller.Ping;

/// <summary>Inbound (PLM -> ERP): connectivity check; passes only when every gateway check passes.</summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/ThirdPartyInbound")]
[AllowAnonymous]
[TypeFilter(typeof(ThirdPartyInboundAuthFilter))]
public class PingController : ControllerBase
{
    // POST api/v1/ThirdPartyInbound/ping   { username, password, parameters: {} }
    [HttpPost("ping")]
    public Task<IActionResult> PingAsync([FromBody] ThirdPartyRequestModel request) =>
        ThirdPartyInboundResponse.RespondAsync(HttpContext, "ping", request.Parameters,
            _ => Task.FromResult<object?>(new { message = "pong", serverTime = DateTime.Now }));
}
