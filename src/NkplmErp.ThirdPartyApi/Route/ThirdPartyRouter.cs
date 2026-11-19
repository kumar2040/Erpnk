namespace NkplmErp.ThirdPartyApi.Route;

/// <summary>A third-party route plus the route to try when it fails (null = no fallback).</summary>
public readonly record struct ThirdPartyRoute(string Path, string? Fallback = null);

/// <summary>
/// Every third-party (PLM) route, relative to ThirdPartyApi:BaseUrl. Mirrors PLM app/Config/Routes.php.
/// The manager calls Path first and, if that fails, Fallback.
/// </summary>
public static class ThirdPartyRouter
{
    public static readonly ThirdPartyRoute Ping = new("ping");

    // Order planning
    public static readonly ThirdPartyRoute Months = new("orderplanning/months");
    public static readonly ThirdPartyRoute Orders = new("orderplanning/orders");
    public static readonly ThirdPartyRoute CollectionTypes = new("orderplanning/collection-types");
    public static readonly ThirdPartyRoute Remaining = new("orderplanning/remaining");
    public static readonly ThirdPartyRoute Yarn = new("orderplanning/yarn");
}
