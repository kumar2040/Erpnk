using System.Data;
using NkplmErp.Shared.DTOs.Yarn_Orders;
using NkplmErp.Application.Interfaces.Yarn_Orders;
using NkplmErp.Shared.DataAccess.GenericRepository;
using NkplmErp.Shared.Wrapper;

namespace NkplmErp.Infrastructure.Services.Yarn_Orders
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

        // sp_ManageYarnOrder flag 'I'. Everything that makes this more than a column write
        // -- completing the sub-order, deciding whether the whole yarn order is now received,
        // raising the Planning task and its bell notifications -- happens inside the procedure,
        // in one place next to the data. Here it is success vs fail and the SP's own message.
        //
        // A blank InvoiceNo is passed through as null on purpose: that is the correction path
        // (clear the invoice, reopen the order), not a missing value to reject.
        public async Task<IResponse<YarnOrderResponseModel>> SaveInvoiceAsync(YarnOrderRequestModel request, string userId)
        {
            try
            {
                var row = await _genericRepository.GetQueryFirstOrDefaultResultAsync<YarnOrderResponseModel>(
                    "sp_ManageYarnOrder",
                    new
                    {
                        Flag = "I",
                        request.YarnId,
                        InvoiceNo = string.IsNullOrWhiteSpace(request.InvoiceNo) ? null : request.InvoiceNo.Trim(),
                        InvoiceBy = userId,
                        request.Weight,
                        PragyapanNo = string.IsNullOrWhiteSpace(request.PragyapanNo) ? null : request.PragyapanNo.Trim(),
                        LcTtNo = string.IsNullOrWhiteSpace(request.LcTtNo) ? null : request.LcTtNo.Trim()
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
