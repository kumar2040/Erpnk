namespace NkplmErp.Blazor.Services.ThirdPartyApi.Manager.Route
{
    // Central place for the ThirdPartyApi controller routes (UI -> NkplmErp.API).
    public static class ThirdPartyApiEndpoint
    {
        public const string Base = "api/v1/ThirdPartyApi";

        public static string Months(DateTime inputDate) => $"{Base}/months?inputDate={inputDate:yyyy-MM-dd}";
        public static string Orders(DateTime month) => $"{Base}/orders?month={month:yyyy-MM-dd}";
        public const string CollectionTypes = Base + "/collection-types";
        public static string Remaining(string orderNo) => $"{Base}/remaining?orderNo={Uri.EscapeDataString(orderNo)}";

        public static string Yarn(string orderNo, string? gauge = null, string? ply = null) =>
            $"{Base}/yarn?orderNo={Uri.EscapeDataString(orderNo)}"
            + (gauge is null ? "" : $"&gauge={Uri.EscapeDataString(gauge)}&ply={Uri.EscapeDataString(ply ?? "")}");
    }
}
