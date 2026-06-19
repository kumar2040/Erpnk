using System.Data;
using NkplmErp.API.Controllers.TaskManagement.Model;
using NkplmErp.API.Controllers.TaskManagement.Service.Interface;
using NkplmErp.Shared.Repositories.Interface;

namespace NkplmErp.API.Controllers.TaskManagement.Service.Implementation
{
    public class TaskManagementService : ITaskManagementService
    {
        private readonly IDapperRepository _dapperRepository;

        public TaskManagementService(IDapperRepository dapperRepository)
        {
            _dapperRepository = dapperRepository;
        }

        public async Task<IEnumerable<TaskManagementResponseModel>> GetTasksAsync(
            string flag, DateTime? startDate, DateTime? endDate, string? orderNo, string? factoryType, string? subCategories, string? userId)
        {
            // Only S / P / C / O / H are valid; default to Scheduled for anything else.
            //   S = Scheduled, P = In Progress, C = Completed, O = Overdue, H = On Hold.
            var safeFlag = flag?.Trim().ToUpperInvariant() switch
            {
                "P" => "P",
                "C" => "C",
                "O" => "O",
                "H" => "H",
                _ => "S"
            };

            // Blank order number / factory type / sub-categories / user id means "no value".
            var orderNoParam = string.IsNullOrWhiteSpace(orderNo) ? null : orderNo.Trim();
            var factoryTypeParam = string.IsNullOrWhiteSpace(factoryType) ? null : factoryType.Trim();
            var subCategoriesParam = string.IsNullOrWhiteSpace(subCategories) ? null : subCategories.Trim();
            var userIdParam = string.IsNullOrWhiteSpace(userId) ? null : userId.Trim();

            // The SP resolves the user's AssignedGauge from @UserId and enforces the factory
            // scope itself; @FactoryType only matters for a super-admin (null gauge). The SP
            // also applies the cascading @SubCategories gauge-method filter.
            var rows = await _dapperRepository.GetQueryResultAsync<TaskManagementResponseModel>(
                "spTaskManagement",
                new { Flag = safeFlag, StartDate = startDate, EndDate = endDate, OrderNo = orderNoParam, FactoryType = factoryTypeParam, UserId = userIdParam, SubCategories = subCategoriesParam },
                CommandType.StoredProcedure);

            return rows;
        }

        public async Task<IEnumerable<string>> GetFactoryTypesAsync()
        {
            var rows = await _dapperRepository.GetQueryResultAsync<string?>(
                "spTaskManagement",
                new { Flag = "FT" },
                CommandType.StoredProcedure);

            return rows
                .Where(r => !string.IsNullOrWhiteSpace(r))
                .Select(r => r!.Trim())
                .ToList();
        }

        public async Task<IEnumerable<string>> GetSubCategoriesAsync(string? factoryType, DateTime? startDate, DateTime? endDate, string userId)
        {
            var factoryTypeParam = string.IsNullOrWhiteSpace(factoryType) ? null : factoryType.Trim();
            var userIdParam = string.IsNullOrWhiteSpace(userId) ? null : userId.Trim();

            // Flag 'SUB' returns the distinct gauge sub-methods for the active factory within
            // the date window (numeric -> 'general', tailor code -> name); the SP scopes it to
            // a restricted user's gauge.
            var rows = await _dapperRepository.GetQueryResultAsync<string?>(
                "spTaskManagement",
                new { Flag = "SUB", FactoryType = factoryTypeParam, StartDate = startDate, EndDate = endDate, UserId = userIdParam },
                CommandType.StoredProcedure);

            return rows
                .Where(r => !string.IsNullOrWhiteSpace(r))
                .Select(r => r!.Trim())
                .ToList();
        }

        public async Task<string?> GetUserAssignedGaugeAsync(string userId)
        {
            if (string.IsNullOrWhiteSpace(userId)) return null;

            // Flag 'GAUGE' returns the user's resolved AssignedGauge (NULL = super admin).
            var gauge = await _dapperRepository.GetQueryFirstOrDefaultResultAsync<string?>(
                "spTaskManagement",
                new { Flag = "GAUGE", UserId = userId.Trim() },
                CommandType.StoredProcedure);

            return string.IsNullOrWhiteSpace(gauge) ? null : gauge.Trim();
        }
    }
}
