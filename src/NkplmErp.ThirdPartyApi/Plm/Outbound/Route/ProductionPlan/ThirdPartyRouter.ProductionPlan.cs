namespace NkplmErp.ThirdPartyApi.Plm.Outbound.Route;

public static partial class ThirdPartyRouter
{
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
}
