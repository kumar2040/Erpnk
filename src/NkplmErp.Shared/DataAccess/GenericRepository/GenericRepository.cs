using NkplmErp.Shared.Models.ResponseModel;
using NkplmErp.Shared.Repositories.Interface;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NkplmErp.Shared.DataAccess.GenericRepository
{
    public class GenericRepository : IGenericRepository
    {
        private IDapperRepository _dapperRepository { get; set; } = default!;

        public GenericRepository(IDapperRepository dapperRepository)
        {
            _dapperRepository = dapperRepository;
        }

        public async Task<SystemResponse> ExecuteAsync(string spName, object obj, CommandType queryType = CommandType.StoredProcedure)
            => await _dapperRepository.ExecuteAsync(spName, obj, queryType);
        public async Task<List<T>> GetQueryResultAsync<T>(string spName, object obj, CommandType queryType = CommandType.StoredProcedure)
            => await _dapperRepository.GetQueryResultAsync<T>(spName, obj, queryType);
        public async Task<T> GetQueryFirstOrDefaultResultAsync<T>(string spName, object obj, CommandType queryType = CommandType.StoredProcedure)
            => await _dapperRepository.GetQueryFirstOrDefaultResultAsync<T>(spName, obj, queryType);
        public async Task<List<object>> GetFromMultipleQuery<T0, T1>(string spName, object obj, CommandType queryType = CommandType.StoredProcedure)
            => await _dapperRepository.GetFromMultipleQuery<T0, T1>(spName, obj, queryType);
        public async Task<List<object>> GetFromMultipleQuery<T0, T1, T2>(string spName, object obj, CommandType queryType = CommandType.StoredProcedure)
            => await _dapperRepository.GetFromMultipleQuery<T0, T1, T2>(spName, obj, queryType);
        public async Task<List<object>> GetFromMultipleQuery<T0, T1, T2, T3>(string spName, object obj, CommandType queryType = CommandType.StoredProcedure)
            => await _dapperRepository.GetFromMultipleQuery<T0, T1, T2, T3>(spName, obj, queryType);
    }
}
