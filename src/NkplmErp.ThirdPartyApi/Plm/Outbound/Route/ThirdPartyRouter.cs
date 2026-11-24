namespace NkplmErp.ThirdPartyApi.Plm.Outbound.Route;

/// <summary>A third-party route plus the route to try when it fails (null = no fallback).</summary>
public readonly record struct ThirdPartyRoute(string Path, string? Fallback = null);

/// <summary>
/// Every third-party (PLM) route, relative to ThirdPartyApi:BaseUrl. Mirrors PLM app/Config/Routes.php.
/// The manager calls Path first and, if that fails, Fallback.
/// Routes live next to their page: Dashboard/, ProductionPlan/ (order planning), Shipment/, Bom/ — each
/// holds a ThirdPartyRouter.&lt;Page&gt;.cs partial of this class.
/// </summary>
public static partial class ThirdPartyRouter
{
}
