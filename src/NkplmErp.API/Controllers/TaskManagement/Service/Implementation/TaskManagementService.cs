using System.Data;
using Dapper;
using Microsoft.Data.SqlClient;
using NkplmErp.API.Controllers.TaskManagement.Model;
using NkplmErp.API.Controllers.TaskManagement.Service.Interface;

namespace NkplmErp.API.Controllers.TaskManagement.Service.Implementation
{
    public class TaskManagementService : ITaskManagementService
    {
        private readonly string _connectionString;

        public TaskManagementService(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
        }

        public async Task<IEnumerable<TaskManagementResponseModel>> GetTasksAsync(
            string flag, DateTime? startDate, DateTime? endDate, string? orderNo)
        {
            // Only S / P / C / O are valid; default to Scheduled for anything else.
            //   S = Scheduled, P = In Progress, C = Completed, O = Overdue.
            var safeFlag = flag?.Trim().ToUpperInvariant() switch
            {
                "P" => "P",
                "C" => "C",
                "O" => "O",
                _ => "S"
            };

            // Blank order number means "no filter".
            var orderNoParam = string.IsNullOrWhiteSpace(orderNo) ? null : orderNo.Trim();

            using var connection = new SqlConnection(_connectionString);

            var rows = await connection.QueryAsync<TaskManagementResponseModel>(
                "spTaskManagement",
                new { Flag = safeFlag, StartDate = startDate, EndDate = endDate, OrderNo = orderNoParam },
                commandType: CommandType.StoredProcedure);

            return rows.ToList();
        }
    }
}
