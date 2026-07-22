using System.Data;
using NkplmErp.Shared.DTOs.TaskManagement;
using NkplmErp.Application.Interfaces.TaskManagement;
using NkplmErp.Shared.DataAccess.GenericRepository;
using NkplmErp.Shared.Wrapper;

namespace NkplmErp.Infrastructure.Services.TaskManagement
{
    public class TaskManagementService : ITaskManagementService
    {
        private readonly IGenericRepository _genericRepository;

        public TaskManagementService(IGenericRepository genericRepository)
        {
            _genericRepository = genericRepository;
        }

        public async Task<IResponse<List<TaskManagementResponseModel>>> GetTasksAsync(
            string flag, DateTime? startDate, DateTime? endDate, string? orderNo, string? factoryType, string? subCategories, string? userId)
        {
            try
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
                var rows = await _genericRepository.GetQueryResultAsync<TaskManagementResponseModel>(
                    "spTaskManagement",
                    new { Flag = safeFlag, StartDate = startDate, EndDate = endDate, OrderNo = orderNoParam, FactoryType = factoryTypeParam, UserId = userIdParam, SubCategories = subCategoriesParam },
                    CommandType.StoredProcedure);

                return Response<List<TaskManagementResponseModel>>.Success(rows);
            }
            catch (Exception ex)
            {
                return Response<List<TaskManagementResponseModel>>.Fail(ex.Message);
            }
        }

        public async Task<IResponse<List<string>>> GetFactoryTypesAsync()
        {
            try
            {
                var rows = await _genericRepository.GetQueryResultAsync<string?>(
                    "spTaskManagement",
                    new { Flag = "FT" },
                    CommandType.StoredProcedure);

                var factoryTypes = rows
                    .Where(r => !string.IsNullOrWhiteSpace(r))
                    .Select(r => r!.Trim())
                    .ToList();

                return Response<List<string>>.Success(factoryTypes);
            }
            catch (Exception ex)
            {
                return Response<List<string>>.Fail(ex.Message);
            }
        }

        public async Task<IResponse<List<string>>> GetSubCategoriesAsync(string? factoryType, DateTime? startDate, DateTime? endDate, string userId)
        {
            try
            {
                var factoryTypeParam = string.IsNullOrWhiteSpace(factoryType) ? null : factoryType.Trim();
                var userIdParam = string.IsNullOrWhiteSpace(userId) ? null : userId.Trim();

                // Flag 'SUB' returns the distinct gauge sub-methods for the active factory within
                // the date window (numeric -> 'general', tailor code -> name); the SP scopes it to
                // a restricted user's gauge.
                var rows = await _genericRepository.GetQueryResultAsync<string?>(
                    "spTaskManagement",
                    new { Flag = "SUB", FactoryType = factoryTypeParam, StartDate = startDate, EndDate = endDate, UserId = userIdParam },
                    CommandType.StoredProcedure);

                var subCategories = rows
                    .Where(r => !string.IsNullOrWhiteSpace(r))
                    .Select(r => r!.Trim())
                    .ToList();

                return Response<List<string>>.Success(subCategories);
            }
            catch (Exception ex)
            {
                return Response<List<string>>.Fail(ex.Message);
            }
        }

        public async Task<IResponse<string?>> GetUserAssignedGaugeAsync(string userId)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(userId)) return Response<string?>.Success(null);

                // Flag 'GAUGE' returns the user's resolved AssignedGauge (NULL = super admin).
                var gauge = await _genericRepository.GetQueryFirstOrDefaultResultAsync<string?>(
                    "spTaskManagement",
                    new { Flag = "GAUGE", UserId = userId.Trim() },
                    CommandType.StoredProcedure);

                return Response<string?>.Success(string.IsNullOrWhiteSpace(gauge) ? null : gauge.Trim());
            }
            catch (Exception ex)
            {
                return Response<string?>.Fail(ex.Message);
            }
        }

        public async Task<IResponse<SyncResultModel>> SyncKnitterRecordsAsync()
        {
            try
            {
                // Watermark-based incremental pull from MySQL via the linked server.
                var result = await _genericRepository.GetQueryFirstOrDefaultResultAsync<SyncResultModel>(
                    "sp_SyncKnitterRecords",
                    new { },
                    CommandType.StoredProcedure);

                return Response<SyncResultModel>.Success(result ?? new SyncResultModel { Message = "No response from sync procedure." });
            }
            catch (Exception ex)
            {
                return Response<SyncResultModel>.Fail(ex.Message);
            }
        }

        // ---- Order return-detail modal (flags KH / KD / KS) ----

        public async Task<IResponse<KnitterSummaryResponseModel?>> GetKnitterSummaryAsync(int taskId, string userId)
        {
            try
            {
                if (taskId <= 0) return Response<KnitterSummaryResponseModel?>.Success((KnitterSummaryResponseModel?)null);

                // Flag 'KH' returns ONE aggregated summary row for the line, scoped to the caller's
                // factory via @UserId.
                var result = await _genericRepository.GetQueryFirstOrDefaultResultAsync<KnitterSummaryResponseModel>(
                    "spTaskManagement",
                    new { Flag = "KH", TaskId = taskId, UserId = userId },
                    CommandType.StoredProcedure);

                return Response<KnitterSummaryResponseModel?>.Success(result);
            }
            catch (Exception ex)
            {
                return Response<KnitterSummaryResponseModel?>.Fail(ex.Message);
            }
        }

        public async Task<IResponse<List<KnitterReturnPointResponseModel>>> GetKnitterReturnSeriesAsync(string? rId, string userId)
        {
            try
            {
                var ridParam = string.IsNullOrWhiteSpace(rId) ? null : rId.Trim();
                if (ridParam is null) return Response<List<KnitterReturnPointResponseModel>>.Success(new List<KnitterReturnPointResponseModel>());

                // Flag 'KD' returns one row per return date/time (count of received item_no),
                // scoped to the caller's factory via @UserId.
                var rows = await _genericRepository.GetQueryResultAsync<KnitterReturnPointResponseModel>(
                    "spTaskManagement",
                    new { Flag = "KD", RId = ridParam, UserId = userId },
                    CommandType.StoredProcedure);

                return Response<List<KnitterReturnPointResponseModel>>.Success(rows);
            }
            catch (Exception ex)
            {
                return Response<List<KnitterReturnPointResponseModel>>.Fail(ex.Message);
            }
        }

        public async Task<IResponse<List<OrderStyleResponseModel>>> GetOrderStylesAsync(int taskId, string userId)
        {
            try
            {
                if (taskId <= 0) return Response<List<OrderStyleResponseModel>>.Success(new List<OrderStyleResponseModel>());

                // Flag 'KS' returns the distinct (style, colour, size) rows for the line,
                // scoped to the caller's factory via @UserId.
                var rows = await _genericRepository.GetQueryResultAsync<OrderStyleResponseModel>(
                    "spTaskManagement",
                    new { Flag = "KS", TaskId = taskId, UserId = userId },
                    CommandType.StoredProcedure);

                return Response<List<OrderStyleResponseModel>>.Success(rows);
            }
            catch (Exception ex)
            {
                return Response<List<OrderStyleResponseModel>>.Fail(ex.Message);
            }
        }
    }
}
