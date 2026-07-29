using NkplmErp.Shared.DTOs.Yarn_Orders;
using NkplmErp.Blazor.Services.Yarn_Orders.Manager.Interface;
using NkplmErp.Blazor.Services.Yarn_Orders.Manager.Route;
using NkplmErp.Blazor.Shared.Http;
using NkplmErp.Shared.Wrapper;

namespace NkplmErp.Blazor.Services.Yarn_Orders.Manager.Implementation
{
    public class YarnOrderManager : IYarnOrderManager
    {
        private readonly IHttpServices _http;

        public YarnOrderManager(IHttpServices http)
        {
            _http = http;
        }

        public async Task<IResponse<YarnOrderResponseModel>> UpdateYarnOrderAsync(YarnOrderRequestModel request)
        {
            var response = await _http.PostAsJsonAsync<YarnOrderResponseModel>(YarnOrderEndpoint.Update, request);
            return response;
        }

        public async Task<IResponse<YarnOrderResponseModel>> SaveInvoiceAsync(YarnOrderRequestModel request)
        {
            var response = await _http.PostAsJsonAsync<YarnOrderResponseModel>(YarnOrderEndpoint.Invoice, request);
            return response;
        }
    }
}
