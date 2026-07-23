using NkplmErp.Blazor.Services.Dropdown.Manager.Interface;
using NkplmErp.Blazor.Services.Dropdown.Manager.Route;
using NkplmErp.Blazor.Shared.Http;
using NkplmErp.Shared.DTOs.Dropdown;

namespace NkplmErp.Blazor.Services.Dropdown.Manager.Implementation
{
    public class DropdownManager : IDropdownManager
    {
        private readonly IHttpServices _http;

        public DropdownManager(IHttpServices http)
        {
            _http = http;
        }

        public async Task<List<DropDownListModel>> GetDropDownListAsync(
            string dropDownType, string? filter1 = null, string? filter2 = null)
        {
            var url = $"{DropdownEndpoint.List}?type={Uri.EscapeDataString(dropDownType)}";

            if (!string.IsNullOrWhiteSpace(filter1))
                url += $"&filter1={Uri.EscapeDataString(filter1)}";
            if (!string.IsNullOrWhiteSpace(filter2))
                url += $"&filter2={Uri.EscapeDataString(filter2)}";

            // The API wraps the rows in Response<T>, so read the envelope and hand
            // back just the payload. A failed call yields an empty list -- an empty
            // dropdown is a better failure than a page that won't render.
            var response = await _http.GetAsync<List<DropDownListModel>>(url);
            return response?.Data ?? new();
        }
    }
}
