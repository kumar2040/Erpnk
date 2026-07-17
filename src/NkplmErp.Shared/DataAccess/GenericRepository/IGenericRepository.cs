using NkplmErp.Shared.Models.ResponseModel;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NkplmErp.Shared.DataAccess.GenericRepository
{
    public interface IGenericRepository
    {
        Task<SystemResponse> ExecuteAsync(string spName, object obj, CommandType queryType = CommandType.StoredProcedure);
        Task<List<T>> GetQueryResultAsync<T>(string spName, object obj, CommandType queryType = CommandType.StoredProcedure);
        Task<T> GetQueryFirstOrDefaultResultAsync<T>(string spName, object obj, CommandType queryType = CommandType.StoredProcedure);
        Task<List<object>> GetFromMultipleQuery<T0, T1>(string spName, object obj, CommandType queryType = CommandType.StoredProcedure);
        Task<List<object>> GetFromMultipleQuery<T0, T1, T2>(string spName, object obj, CommandType queryType = CommandType.StoredProcedure);
        Task<List<object>> GetFromMultipleQuery<T0, T1, T2, T3>(string spName, object obj, CommandType queryType = CommandType.StoredProcedure);
    }
}
