using NkplmErp.Blazor.Model.Yarn_Orders;
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

        public async Task<IResponse<YarnOrdersResponseModel>> UpdateYarnOrderAsync(YarnOrdersRequestModel request)
        {
            var response = await _http.PostAsJsonAsync<YarnOrdersResponseModel>(YarnOrderEndpoint.Update, request);
            return response;
        }
    }
}
