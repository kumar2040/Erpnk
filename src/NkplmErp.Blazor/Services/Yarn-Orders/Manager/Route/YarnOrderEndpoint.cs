namespace NkplmErp.Blazor.Services.Yarn_Orders.Manager.Route
{
    // Central place for the YarnOrder API routes.
    public static class YarnOrderEndpoint
    {
        public const string Base = "api/v1/YarnOrder";

        // Set a vendor sub-order's departure and/or arrival date (POST).
        public const string Update = Base + "/update";
    }
}
