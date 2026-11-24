using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NkplmErp.ThirdPartyApi.Tts.Outbound.Manager.Interface;
using NkplmErp.Shared.DTOs.Tts;

namespace NkplmErp.ThirdPartyApi.Tts.Outbound.Controller
{
    [ApiController]
    [ApiVersion("1.0")]
    [Route("api/v{version:apiVersion}/[controller]")]
    [Authorize]
    public class TtsController(ITtsService ttsService) : ControllerBase
    {
        // No per-page permission check on purpose: read-aloud is a utility for any signed-in user
        // (notifications, etc.), not a page with its own PageKey. [Authorize] keeps it off the public surface.

        // GET api/v1/Tts/voices
        [HttpGet("voices")]
        public async Task<IActionResult> GetVoicesAsync(CancellationToken ct)
        {
            var result = await ttsService.GetVoicesAsync(ct);
            return result.Succeeded ? Ok(result) : BadRequest(result);
        }

        // GET api/v1/Tts/health
        [HttpGet("health")]
        public async Task<IActionResult> GetHealthAsync(CancellationToken ct)
        {
            var result = await ttsService.GetHealthAsync(ct);
            return result.Succeeded ? Ok(result) : BadRequest(result);
        }

        // POST api/v1/Tts/speak   { "text": "...", "lang": "auto", "speed": 1, "voices": { "en": "...", "ne": "..." } }
        // Success returns the audio itself (audio/wav); failure returns the usual IResponse with the message.
        [HttpPost("speak")]
        public async Task<IActionResult> SpeakAsync([FromBody] TtsRequestModel request, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(request.Text))
                return BadRequest("text is required.");

            var result = await ttsService.SpeakAsync(request, ct);
            return result.Succeeded
                ? File(result.Data.Audio, result.Data.ContentType)
                : BadRequest(result);
        }
    }
}
