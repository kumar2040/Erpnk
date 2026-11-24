using NkplmErp.Shared.Wrapper;
using NkplmErp.ThirdPartyApi.Plm.Outbound.Route;

namespace NkplmErp.ThirdPartyApi.Plm.Outbound.Manager.Interface;

public interface IThirdPartyManager
{
    /// <summary>
    /// Calls a third-party route with the configured username/password and the given parameters,
    /// falling back to the route's fallback if it fails. Data is the response's "data" payload as T.
    /// Any failure comes back as one generic fail response.
    /// </summary>
    Task<IResponse<T>> CallAsync<T>(ThirdPartyRoute route, object? parameters = null, CancellationToken ct = default);
}
