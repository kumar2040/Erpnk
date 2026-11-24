namespace NkplmErp.ThirdPartyApi.Plm.Outbound.Route;

public static partial class ThirdPartyRouter
{
    public static readonly ThirdPartyRoute OrderStatus = new("dashboard/order-status");
    public static readonly ThirdPartyRoute ProductionFlow = new("dashboard/production-flow");
    public static readonly ThirdPartyRoute DepartmentStock = new("dashboard/department-stock");
}
