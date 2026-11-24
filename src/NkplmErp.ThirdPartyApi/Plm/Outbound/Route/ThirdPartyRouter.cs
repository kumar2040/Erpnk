namespace NkplmErp.ThirdPartyApi.Plm.Outbound.Route;

/// <summary>A third-party route plus the route to try when it fails (null = no fallback).</summary>
public readonly record struct ThirdPartyRoute(string Path, string? Fallback = null);

/// <summary>
/// Every third-party (PLM) route, relative to ThirdPartyApi:BaseUrl. Mirrors PLM app/Config/Routes.php.
/// The manager calls Path first and, if that fails, Fallback.
/// </summary>
public static class ThirdPartyRouter
{
    // Main dashboard
    public static readonly ThirdPartyRoute OrderStatus = new("dashboard/order-status");
    public static readonly ThirdPartyRoute ProductionFlow = new("dashboard/production-flow");
    public static readonly ThirdPartyRoute DepartmentStock = new("dashboard/department-stock");

    // Order planning
    public static readonly ThirdPartyRoute Months = new("orderplanning/months");
    public static readonly ThirdPartyRoute Orders = new("orderplanning/orders");
    public static readonly ThirdPartyRoute CollectionTypes = new("orderplanning/collection-types");
    public static readonly ThirdPartyRoute Remaining = new("orderplanning/remaining");
    public static readonly ThirdPartyRoute Analysis = new("orderplanning/analysis");
    public static readonly ThirdPartyRoute OrderTypes = new("orderplanning/order-types");
    public static readonly ThirdPartyRoute YarnNames = new("orderplanning/yarn-names");
    public static readonly ThirdPartyRoute ProductionProgress = new("orderplanning/production-progress");
    public static readonly ThirdPartyRoute UpdateMachine = new("orderplanning/update-machine");
    public static readonly ThirdPartyRoute PlanRows = new("orderplanning/plan-rows");
    public static readonly ThirdPartyRoute SaveKnitterAssignment = new("orderplanning/knitter-assignment");
    public static readonly ThirdPartyRoute MasterProgress = new("orderplanning/master-progress");
    public static readonly ThirdPartyRoute WeaveFactories = new("orderplanning/weave-factories");
    public static readonly ThirdPartyRoute DeptCompletionDate = new("orderplanning/dept-completion-date");
    public static readonly ThirdPartyRoute GaugeDetails = new("orderplanning/gauge-details");
    public static readonly ThirdPartyRoute Yarn = new("orderplanning/yarn");

    // Task board shipment alert
    public static readonly ThirdPartyRoute ShipmentDueOrders = new("shipment/due-orders");

    // BOM page
    public static readonly ThirdPartyRoute BomOrders = new("bom/orders");
    public static readonly ThirdPartyRoute BomYarnRequirement = new("bom/yarn-requirement");
}
