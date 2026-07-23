namespace NkplmErp.Shared.DTOs.Dropdown
{
    // The two leading rows a dropdown can carry. spDropdown never returns these --
    // AutoCompleteSelect prepends one of them from its All parameter -- but a page
    // reading the selection has to recognise them, so the ids live here rather
    // than as a literal "-1" scattered across pages.
    public static class DropdownValues
    {
        // "All" -- the list is unfiltered. Chosen as -1 so it can never collide
        // with a real key, including a genuine 0.
        public const string All = "-1";

        // "Select" -- nothing picked yet, shown when a dropdown is All="0".
        public const string Select = "0";

        // True when the selection is a leading row rather than a real option.
        // Callers use this to decide whether to send a filter at all: passing
        // "-1" down to a proc would be read as a real (and wrong) value.
        public static bool IsPlaceholder(string? id) =>
            string.IsNullOrWhiteSpace(id) || id == All || id == Select;
    }
}
