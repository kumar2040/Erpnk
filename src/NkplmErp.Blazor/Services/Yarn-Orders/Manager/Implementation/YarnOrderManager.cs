using System.Net.Http.Json;
using NkplmErp.Blazor.Model.Yarn_Orders;
using NkplmErp.Blazor.Services.Yarn_Orders.Manager.Interface;
using NkplmErp.Blazor.Services.Yarn_Orders.Manager.Route;
using NkplmErp.Shared.Wrapper;

namespace NkplmErp.Blazor.Services.Yarn_Orders.Manager.Implementation
{
    public class YarnOrderManager : IYarnOrderManager
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<YarnOrderManager> _logger;

        public YarnOrderManager(HttpClient httpClient, ILogger<YarnOrderManager> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
        }

        public async Task<IResponse<YarnOrdersResponseModel>> UpdateYarnOrderAsync(YarnOrdersRequestModel request)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync(YarnOrderEndpoint.Update, request);

                // The controller returns the IResponse envelope on BOTH 200 and 400, so the
                // envelope is what comes off the wire — deserializing the bare model here
                // would silently yield an all-default instance.
                var payload = await response.Content
                    .ReadFromJsonAsync<Response<YarnOrdersResponseModel>>();

                if (payload is not null) return payload;

                var error = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("UpdateYarnOrderAsync returned {Status}: {Error}", response.StatusCode, error);
                return Response<YarnOrdersResponseModel>.Fail(error);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "UpdateYarnOrderAsync failed");
                return Response<YarnOrdersResponseModel>.Fail(ex.Message);
            }
        }
    }
}
