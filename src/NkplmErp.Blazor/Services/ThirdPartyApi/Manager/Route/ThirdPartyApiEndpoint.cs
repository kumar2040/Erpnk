namespace NkplmErp.Blazor.Services.ThirdPartyApi.Manager.Route
{
    // Central place for the ThirdPartyApi controller routes (UI -> NkplmErp.API).
    public static class ThirdPartyApiEndpoint
    {
        public const string Base = "api/v1/ThirdPartyApi";

        // Main dashboard
        public static string OrderStatus(int year, string status) =>
            $"{Base}/order-status?year={year}&status={Uri.EscapeDataString(status)}";

        public static string ProductionFlow(int buyerId, string? orderNo) =>
            $"{Base}/production-flow?buyerId={buyerId}&orderNo={Uri.EscapeDataString(orderNo ?? string.Empty)}";

        // Order planning
        public static string Months(DateTime inputDate) => $"{Base}/months?inputDate={inputDate:yyyy-MM-dd}";
        // knitType: count only that product type's lines (e.g. "Knit"); empty = every line.
        public static string Orders(DateTime month, string? knitType) =>
            $"{Base}/orders?month={month:yyyy-MM-dd}&knitType={Uri.EscapeDataString(knitType ?? string.Empty)}";
        public const string CollectionTypes = Base + "/collection-types";
        public static string Remaining(string orderNo) => $"{Base}/remaining?orderNo={Uri.EscapeDataString(orderNo)}";

        public static string OrderAnalysis(string orderNo, string? knitType, int mode) =>
            $"{Base}/order-analysis?orderNo={Uri.EscapeDataString(orderNo)}&mode={mode}&knitType={Uri.EscapeDataString(knitType ?? string.Empty)}";

        public static string WeaveFactories(string orderNo) =>
            $"{Base}/weave-factories?orderNo={Uri.EscapeDataString(orderNo)}";

        public static string ProductionProgress(string orderNo, string knitType) =>
            $"{Base}/production-progress?orderNo={Uri.EscapeDataString(orderNo)}&knitType={Uri.EscapeDataString(knitType)}";

        public static string MasterProgress(string orderNo, string knitType) =>
            $"{Base}/master-progress?orderNo={Uri.EscapeDataString(orderNo)}&knitType={Uri.EscapeDataString(knitType)}";

        public static string PlanRows(string? orderNo = null) => $"{Base}/plan-rows?orderNo={Uri.EscapeDataString(orderNo ?? string.Empty)}";

        public const string PlanRowMachine = Base + "/plan-row-machine";

        public static string YarnNames(IEnumerable<string> productIds) =>
            $"{Base}/yarn-names?productIds={Uri.EscapeDataString(string.Join(",", productIds))}";

        public static string OrderTypes(DateTime month) => $"{Base}/order-types?month={month:yyyy-MM-dd}";

        public static string DeptCompletionDate(string orderNo, string dept) =>
            $"{Base}/dept-completion-date?orderNo={Uri.EscapeDataString(orderNo)}&dept={Uri.EscapeDataString(dept)}";

        public static string GaugeDetails(string orderNo, string gauge, string flag, string? knitType) =>
            $"{Base}/gauge-details?orderNo={Uri.EscapeDataString(orderNo)}&gauge={Uri.EscapeDataString(gauge)}&flag={Uri.EscapeDataString(flag)}&knitType={Uri.EscapeDataString(knitType ?? string.Empty)}";

        public static string Yarn(string orderNo, string? gauge = null, string? ply = null) =>
            $"{Base}/yarn?orderNo={Uri.EscapeDataString(orderNo)}"
            + (gauge is null ? "" : $"&byColor=true&gauge={Uri.EscapeDataString(gauge)}&ply={Uri.EscapeDataString(ply ?? "")}");
    }
}
