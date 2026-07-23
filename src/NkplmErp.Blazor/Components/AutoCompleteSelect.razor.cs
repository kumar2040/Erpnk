using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using NkplmErp.Blazor.Services.Dropdown.Manager.Interface;
using NkplmErp.Shared.DTOs.Dropdown;

namespace NkplmErp.Blazor.Components
{
    // Standard dropdown for the app. Options come from spDropdown by name, so a
    // list can gain an entry without touching this component or the page using it.
    //
    // Two ways to supply options:
    //   Type="YarnOrderStatus"          -> the component loads them from spDropdown
    //   DropDownModels="@myList"        -> the caller supplies them, no call made
    public partial class AutoCompleteSelect : ComponentBase
    {
        // The spDropdown flag naming which list to load. Ignored when
        // DropDownModels is supplied non-empty.
        [Parameter] public string Type { get; set; } = string.Empty;

        // Optional cascade keys, meaning depends on the flag.
        [Parameter] public string Filter { get; set; } = string.Empty;
        [Parameter] public string Filter2 { get; set; } = string.Empty;

        // Which leading row the list carries:
        //   1 -> "All"    (id DropdownValues.All,    "-1") -- a filter, unfiltered
        //   0 -> "Select" (id DropdownValues.Select, "0")  -- nothing picked yet
        // Added here rather than by spDropdown so the proc only ever returns real
        // data, and so the sentinel ids stay defined in one place.
        [Parameter] public int All { get; set; }

        [Parameter] public int TabIndex { get; set; }
        [Parameter] public bool Disabled { get; set; }

        // Shown on the closed control when nothing is selected.
        [Parameter] public string Placeholder { get; set; } = string.Empty;

        // Extra classes for the caller. Empty by default: the component styles
        // itself in AutoCompleteSelect.razor.css, and a framework class like
        // form-control would override its height and border states.
        [Parameter] public string CssClass { get; set; } = string.Empty;

        // Pre-supplied options. Non-empty means no spDropdown call is made.
        [Parameter] public List<DropDownListModel> DropDownModels { get; set; } = new();

        // The selected id. Two-way bindable: <AutoCompleteSelect @bind-Value="x" />
        [Parameter] public string Value { get; set; } = string.Empty;
        [Parameter] public EventCallback<string> ValueChanged { get; set; }

        [Inject] private IDropdownManager Dropdowns { get; set; } = default!;

        // Unique per instance so several dropdowns can share a page.
        public string Id { get; } = Guid.NewGuid().ToString("N");

        private string _value = string.Empty;
        private string BindingValue => _value;

        // The rendered list: the leading row plus whatever spDropdown returned.
        // Held as a field rather than recomputed per render so the indices the
        // keyboard handler walks stay stable.
        private List<DropDownListModel> Options { get; set; } = new();

        private bool IsOpen { get; set; }

        // Index of the keyboard-highlighted row; -1 = none.
        private int Highlight { get; set; } = -1;

        private DropDownListModel? SelectedItem =>
            Options.FirstOrDefault(x => x.Id == _value);

        // The leading row is a real choice, so matching it counts as a selection.
        private bool HasSelection => SelectedItem is not null;

        private string DisplayText => SelectedItem?.Value ?? Placeholder;

        protected override async Task OnInitializedAsync()
        {
            // Built before the await too, so the closed control reads its leading
            // row while the options are still loading rather than flashing blank.
            BuildOptions();
            _value = Value ?? string.Empty;

            if (DropDownModels.Count == 0 && !string.IsNullOrWhiteSpace(Type))
                DropDownModels = await Dropdowns.GetDropDownListAsync(Type, Filter, Filter2);

            BuildOptions();
            await EnsureValidSelectionAsync();
        }

        // Follow the parent when it changes the selection from outside (a reset
        // button, a cascade). Assigning what we already hold is a no-op.
        protected override async Task OnParametersSetAsync()
        {
            BuildOptions();

            var incoming = Value ?? string.Empty;
            if (!string.IsNullOrEmpty(incoming) && incoming != _value)
                _value = incoming;

            await EnsureValidSelectionAsync();
        }

        // Default the selection to the leading row -- "-1" when All=1, "0" when
        // All=0 -- whenever the held value names no option we can show. Covers an
        // unset value, a value seeded for the other All setting (a page holding
        // "-1" against an All="0" control), and an option a cascade has removed.
        //
        // The parent is TOLD, not just corrected locally: leaving it holding a
        // value the control isn't showing is what made All="0" look broken, since
        // the page kept "-1" while the box had fallen back to "0".
        //
        // This settles in one pass. The parent writes the new id back, the guard
        // above sees incoming == _value, and the id now matches an option, so
        // nothing fires again.
        private async Task EnsureValidSelectionAsync()
        {
            if (Options.Any(o => o.Id == _value)) return;

            _value = LeadingId;
            await ValueChanged.InvokeAsync(_value);
        }

        // The id of the leading row for the current All setting.
        private string LeadingId => All == 1 ? DropdownValues.All : DropdownValues.Select;

        private void BuildOptions()
        {
            // Placeholder doubles as the "Select" label when the caller gave one,
            // so a dropdown can read "Choose vendor" rather than a bare "Select".
            var leadingText = All == 1
                ? "All"
                : (string.IsNullOrWhiteSpace(Placeholder) ? "Select" : Placeholder);

            var options = new List<DropDownListModel>(DropDownModels.Count + 1)
            {
                new() { Id = LeadingId, Value = leadingText }
            };
            options.AddRange(DropDownModels);
            Options = options;
        }

        private void ToggleList()
        {
            if (Disabled) return;

            IsOpen = !IsOpen;
            // Open onto the current selection so arrow keys continue from there
            // rather than jumping to the top of the list.
            Highlight = IsOpen ? Options.FindIndex(x => x.Id == _value) : -1;
        }

        // Fires when focus leaves the wrapper entirely. Selecting an option
        // prevents the mousedown default, so focus stays on the button and this
        // does not race the pick.
        private void CloseList()
        {
            IsOpen = false;
            Highlight = -1;
        }

        private async Task SelectItemAsync(DropDownListModel item)
        {
            IsOpen = false;
            Highlight = -1;

            if (item.Id == _value) return;
            _value = item.Id;
            await ValueChanged.InvokeAsync(item.Id);
        }

        private async Task OnKeyDown(KeyboardEventArgs e)
        {
            if (Disabled) return;

            if (!IsOpen)
            {
                // Any of the usual "open me" keys, matching how a native select behaves.
                if (e.Key is "ArrowDown" or "ArrowUp" or "Enter" or " ")
                    ToggleList();
                return;
            }

            if (Options.Count == 0) return;

            switch (e.Key)
            {
                case "ArrowDown":
                    Highlight = (Highlight + 1) % Options.Count;
                    break;
                case "ArrowUp":
                    Highlight = (Highlight - 1 + Options.Count) % Options.Count;
                    break;
                case "Enter":
                case " ":
                    if (Highlight >= 0 && Highlight < Options.Count)
                        await SelectItemAsync(Options[Highlight]);
                    break;
                case "Escape":
                    CloseList();
                    break;
            }
        }
    }
}
