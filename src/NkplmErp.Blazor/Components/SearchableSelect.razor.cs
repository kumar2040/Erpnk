using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace NkplmErp.Blazor.Components
{
    public partial class SearchableSelect<TItem> : ComponentBase
    {
        [Parameter] public IEnumerable<TItem> Items { get; set; } = Enumerable.Empty<TItem>();
        [Parameter] public TItem? Value { get; set; }
        [Parameter] public EventCallback<TItem?> ValueChanged { get; set; }
        [Parameter] public Func<TItem, string> DisplayFunc { get; set; } = item => item?.ToString() ?? "";
        [Parameter] public string Placeholder { get; set; } = "Search and select...";
        [Parameter] public string Class { get; set; } = "";

        private string _searchText = "";
        private bool _isDropdownOpen = false;

        private IEnumerable<TItem> FilteredItems => string.IsNullOrWhiteSpace(_searchText)
            ? Items
            : Items.Where(item => DisplayFunc(item).Contains(_searchText, StringComparison.OrdinalIgnoreCase));

        protected override void OnParametersSet()
        {
            if (Value != null && !_isDropdownOpen)
            {
                _searchText = DisplayFunc(Value);
            }
        }

        private async Task HandleInput(ChangeEventArgs e)
        {
            _searchText = e.Value?.ToString() ?? "";
            _isDropdownOpen = true;
            
            // If the search text is cleared, we might want to clear the value too
            if (string.IsNullOrEmpty(_searchText))
            {
                await SelectItem(default);
            }
        }

        private void ToggleDropdown()
        {
            _isDropdownOpen = !_isDropdownOpen;
            if (_isDropdownOpen && Value != null)
            {
                _searchText = DisplayFunc(Value);
            }
        }

        private async Task SelectItem(TItem? item)
        {
            Value = item;
            _searchText = item != null ? DisplayFunc(item) : "";
            _isDropdownOpen = false;
            await ValueChanged.InvokeAsync(Value);
        }

        private void OnBlur()
        {
            // Delay closing to allow click events on dropdown items to fire
            Task.Delay(200).ContinueWith(_ => 
            {
                _isDropdownOpen = false;
                if (Value != null)
                {
                    _searchText = DisplayFunc(Value);
                }
                InvokeAsync(StateHasChanged);
            });
        }
    }
}
