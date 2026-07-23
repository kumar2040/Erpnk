namespace NkplmErp.Shared.DTOs.Dropdown
{
    // One option in any dropdown. Property names match the columns spDropdown
    // returns exactly -- Dapper's underscore matching is off in this project, so
    // a rename on either side binds silently to null.
    public class DropDownListModel
    {
        // The code stored / sent back to the API. Empty string is the "All" row.
        public string Id { get; set; } = string.Empty;

        // What the user reads.
        public string Value { get; set; } = string.Empty;
    }
}
