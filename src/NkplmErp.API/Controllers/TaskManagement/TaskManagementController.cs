using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NkplmErp.API.Controllers.TaskManagement.Service.Interface;

namespace NkplmErp.API.Controllers.TaskManagement
{
    [ApiController]
    [ApiVersion("1.0")]
    [Route("api/v{version:apiVersion}/[controller]")]
    [Authorize]
    public class TaskManagementController(ITaskManagementService taskManagementService) : ControllerBase
    {
        private readonly ITaskManagementService _taskManagementService = taskManagementService;

        // GET api/v1/TaskManagement?flag=S|P|C&startDate=2026-06-16&endDate=2026-06-16&orderNo=Nksh26
        [HttpGet]
        public async Task<IActionResult> GetTasks(
            [FromQuery] string flag = "S",
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null,
            [FromQuery] string? orderNo = null)
        {
            var data = await _taskManagementService.GetTasksAsync(flag, startDate, endDate, orderNo);
            return Ok(data);
        }
    }
}
