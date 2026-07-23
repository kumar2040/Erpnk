using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using NkplmErp.Blazor.Services.Dropdown.Manager.Interface;
using NkplmErp.Shared.DTOs.Dropdown;

namespace NkplmErp.Blazor.Components
{
    // Type-to-filter picker backed by spDropdown. Same option contract as
    // AutoCompleteSelect; use this one only when the list is long enough that
    // scanning a <select> is worse than typing.
    public partial class AutoCompleteComponent : ComponentBase
    {
        [Parameter] public string Type { get; set; } = string.Empty;
        [Parameter] public string Filter { get; set; } = string.Empty;
        [Parameter] public string Filter2 { get; set; } = string.Empty;
        // Kept for signature parity with AutoCompleteSelect. A typeahead has no
        // leading row -- an empty box already means "nothing picked" -- so this
        // does not add one; see DropdownValues for the sentinel ids.
        [Parameter] public int All { get; set; }

        [Parameter] public int TabIndex { get; set; }
        [Parameter] public bool Disabled { get; set; }
        [Parameter] public string Placeholder { get; set; } = string.Empty;
        // Extra classes for the caller. Empty by default: the component styles
        // itself in AutoCompleteComponent.razor.css, and a framework class like
        // form-control would override its height and border states.
        [Parameter] public string CssClass { get; set; } = string.Empty;

        [Parameter] public List<DropDownListModel> DropDownModels { get; set; } = new();

        // The selected id. Two-way bindable.
        [Parameter] public string Value { get; set; } = string.Empty;
        [Parameter] public EventCallback<string> ValueChanged { get; set; }

        [Inject] private IDropdownManager Dropdowns { get; set; } = default!;

        private string SearchText { get; set; } = string.Empty;
        private List<DropDownListModel> FilteredItems { get; set; } = new();
        private bool ShowItemList { get; set; }
        private int Highlight { get; set; } = -1;

        private string _value = string.Empty;

        protected override async Task OnInitializedAsync()
        {
            if (DropDownModels.Count == 0 && !string.IsNullOrWhiteSpace(Type))
                DropDownModels = await Dropdowns.GetDropDownListAsync(Type, Filter, Filter2);

            // Seeded here as well as after a load. The original only filled this
            // inside its load path, so passing DropDownModels in left the list
            // empty until the user typed.
            FilteredItems = DropDownModels;
            _value = Value ?? string.Empty;
            SyncSearchTextToValue();
        }

        protected override void OnParametersSet()
        {
            var incoming = Value ?? string.Empty;
            if (incoming != _value)
            {
                _value = incoming;
                SyncSearchTextToValue();
            }
        }

        // Keep the visible text showing the selected option's label.
        private void SyncSearchTextToValue()
        {
            if (string.IsNullOrEmpty(_value))
            {
                SearchText = string.Empty;
                return;
            }

            var match = DropDownModels.FirstOrDefault(x => x.Id == _value);
            if (match is not null) SearchText = match.Value;
        }

        private void OnSearchInput(ChangeEventArgs e)
        {
            SearchText = e.Value?.ToString() ?? string.Empty;
            ShowItemList = true;
            Highlight = -1;

            FilteredItems = string.IsNullOrWhiteSpace(SearchText)
                ? DropDownModels
                : DropDownModels
                    .Where(x => x.Value.Contains(SearchText, StringComparison.OrdinalIgnoreCase))
                    .ToList();
        }

        private async Task SelectItem(DropDownListModel item)
        {
            SearchText = item.Value;
            ShowItemList = false;
            Highlight = -1;

            if (item.Id == _value) return;
            _value = item.Id;
            await ValueChanged.InvokeAsync(item.Id);
        }

        private void ShowList()
        {
            FilteredItems = DropDownModels;
            ShowItemList = true;
        }

        // mousedown on the options already committed the pick, so closing here
        // is safe without the timing delay the original relied on.
        private void HideList()
        {
            ShowItemList = false;
            Highlight = -1;
            SyncSearchTextToValue();
        }

        // Keyboard nav in Blazor rather than a jQuery document handler. The
        // original queried '.searchitem' globally, so a second instance on the
        // same page drove the first one's list.
        private async Task OnKeyDown(KeyboardEventArgs e)
        {
            if (!ShowItemList || FilteredItems.Count == 0)
            {
                if (e.Key == "ArrowDown") ShowList();
                return;
            }

            switch (e.Key)
            {
                case "ArrowDown":
                    Highlight = (Highlight + 1) % FilteredItems.Count;
                    break;
                case "ArrowUp":
                    Highlight = (Highlight - 1 + FilteredItems.Count) % FilteredItems.Count;
                    break;
                case "Enter":
                    if (Highlight >= 0 && Highlight < FilteredItems.Count)
                        await SelectItem(FilteredItems[Highlight]);
                    break;
                case "Escape":
                    HideList();
                    break;
            }
        }
    }
}
