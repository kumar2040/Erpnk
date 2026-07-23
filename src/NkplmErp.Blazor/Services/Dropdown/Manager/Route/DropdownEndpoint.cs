namespace NkplmErp.Blazor.Services.Dropdown.Manager.Route
{
    // Central place for the Dropdown API routes.
    public static class DropdownEndpoint
    {
        public const string Base = "api/v1/Dropdown";

        // Options for one named list (GET). Query: type, all, filter1, filter2.
        public const string List = Base + "/list";
    }
}
