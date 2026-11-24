namespace NkplmErp.ThirdPartyApi.Plm.Outbound.Route;

public static partial class ThirdPartyRouter
{
    public static readonly ThirdPartyRoute BomOrders = new("bom/orders");
    public static readonly ThirdPartyRoute BomYarnRequirement = new("bom/yarn-requirement");
}
