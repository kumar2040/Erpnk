using NkplmErp.API.Model.TaskManagement;
using NkplmErp.Shared.Wrapper;

namespace NkplmErp.API.Services.Interface.TaskManagement
{
    public interface ITaskManagementService
    {
        Task<IResponse<List<TaskManagementResponseModel>>> GetTasksAsync(string flag, DateTime? startDate, DateTime? endDate, string? orderNo, string? factoryType, string? subCategories, string? userId);
        Task<IResponse<List<string>>> GetFactoryTypesAsync();
        Task<IResponse<List<string>>> GetSubCategoriesAsync(string? factoryType, DateTime? startDate, DateTime? endDate, string userId);
        Task<IResponse<string?>> GetUserAssignedGaugeAsync(string userId);
        Task<IResponse<SyncResultModel>> SyncKnitterRecordsAsync();
        Task<IResponse<KnitterSummaryResponseModel?>> GetKnitterSummaryAsync(int taskId, string userId);
        Task<IResponse<List<KnitterReturnPointResponseModel>>> GetKnitterReturnSeriesAsync(string? rId, string userId);
        Task<IResponse<List<OrderStyleResponseModel>>> GetOrderStylesAsync(int taskId, string userId);
    }
}
