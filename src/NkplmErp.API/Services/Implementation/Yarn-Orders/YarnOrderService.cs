using System.Data;
using NkplmErp.API.Model.Yarn_Orders;
using NkplmErp.API.Services.Interface.Yarn_Orders;
using NkplmErp.Shared.DataAccess.GenericRepository;
using NkplmErp.Shared.Wrapper;

namespace NkplmErp.API.Services.Implementation.Yarn_Orders
{
    public class YarnOrderService : IYarnOrderService
    {
        private readonly IGenericRepository _genericRepository;

        public YarnOrderService(IGenericRepository genericRepository)
        {
            _genericRepository = genericRepository;
        }

        // sp_ManageYarnOrder flag 'T'. The dates travel as raw strings and the SP
        // converts them; it also decides what happened and returns the message, so
        // the only logic here is success vs fail.
        public async Task<IResponse<YarnOrderResponseModel>> UpdateYarnOrderAsync(YarnOrderRequestModel request)
        {
            try
            {
                var row = await _genericRepository.GetQueryFirstOrDefaultResultAsync<YarnOrderResponseModel>(
                    "sp_ManageYarnOrder",
                    new
                    {
                        Flag = "T",
                        request.YarnId,
                        request.DepartureDate,
                        request.ArrivalDate
                    },
                    CommandType.StoredProcedure);

                if (row is null)
                    return Response<YarnOrderResponseModel>.Fail("No response from procedure.");

                return row.UpdatedCount > 0
                    ? Response<YarnOrderResponseModel>.Success(row, row.Message)
                    : Response<YarnOrderResponseModel>.Fail(row.Message);
            }
            catch (Exception ex)
            {
                return Response<YarnOrderResponseModel>.Fail(ex.Message);
            }
        }
    }
}
