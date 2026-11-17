namespace TransConnect.Common.Transport;

/// <summary>Fixed service catalogue (M2 decision Q-M2-SERVICE-001). Changing it is a code/seed change.</summary>
public static class ServiceTypeCodes
{
    public const string FixedRouteCoach = "FIXED_ROUTE_COACH";
    public const string FreightTruck = "FREIGHT_TRUCK";
    public const string Charter = "CHARTER";

    public static readonly IReadOnlyList<string> All = [FixedRouteCoach, FreightTruck, Charter];

    public static bool IsKnown(string? code) => code is not null && All.Contains(code);
}
