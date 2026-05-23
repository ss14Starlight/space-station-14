using Robust.Shared.Map;
using Robust.Shared.Serialization;

namespace Content.Shared._PV.Terraforming;

[RegisterComponent]
public sealed partial class TerraformerConsoleComponent : Component
{
}

[Serializable, NetSerializable]
public enum TerraformerConsoleUiKey
{
    Key,
}

[Serializable, NetSerializable]
public enum TerraformerConsoleStatus : byte
{
    Inactive,
    Empty,
    Working,
}

[Serializable, NetSerializable]
public sealed class TerraformerConsoleRefreshMessage : BoundUserInterfaceMessage
{
}

[Serializable, NetSerializable]
public sealed class TerraformerConsoleBoundInterfaceState : BoundUserInterfaceState
{
    public readonly NetEntity? GridEntity;
    public readonly int TerraformerCount;
    public readonly int TotalTilesTerraformed;
    public readonly List<TerraformerConsoleEntry> Terraformers;
    public readonly TerraformerAtmosphereSummary Atmosphere;

    public TerraformerConsoleBoundInterfaceState(
        NetEntity? gridEntity,
        int terraformerCount,
        int totalTilesTerraformed,
        List<TerraformerConsoleEntry> terraformers,
        TerraformerAtmosphereSummary atmosphere)
    {
        GridEntity = gridEntity;
        TerraformerCount = terraformerCount;
        TotalTilesTerraformed = totalTilesTerraformed;
        Terraformers = terraformers;
        Atmosphere = atmosphere;
    }
}

[Serializable, NetSerializable]
public sealed class TerraformerConsoleEntry
{
    public readonly NetEntity Entity;
    public readonly NetCoordinates Coordinates;
    public readonly string Name;
    public readonly TerraformerConsoleStatus Status;
    public readonly float Fuel;
    public readonly float MaxFuel;
    public readonly float Radius;
    public readonly float BarrierRadius;
    public readonly int TilesTerraformed;
    public readonly int GridX;
    public readonly int GridY;

    public TerraformerConsoleEntry(
        NetEntity entity,
        NetCoordinates coordinates,
        string name,
        TerraformerConsoleStatus status,
        float fuel,
        float maxFuel,
        float radius,
        float barrierRadius,
        int tilesTerraformed,
        int gridX,
        int gridY)
    {
        Entity = entity;
        Coordinates = coordinates;
        Name = name;
        Status = status;
        Fuel = fuel;
        MaxFuel = maxFuel;
        Radius = radius;
        BarrierRadius = barrierRadius;
        TilesTerraformed = tilesTerraformed;
        GridX = gridX;
        GridY = gridY;
    }
}

[Serializable, NetSerializable]
public sealed class TerraformerAtmosphereSummary
{
    public readonly int TileCount;
    public readonly float AveragePressure;
    public readonly float AverageTemperature;

    public readonly float OxygenPercent;
    public readonly float NitrogenPercent;
    public readonly float CarbonDioxidePercent;
    public readonly float PlasmaPercent;
    public readonly float TritiumPercent;
    public readonly float NitrousOxidePercent;
    public readonly float OtherPercent;

    public TerraformerAtmosphereSummary(
        int tileCount,
        float averagePressure,
        float averageTemperature,
        float oxygenPercent,
        float nitrogenPercent,
        float carbonDioxidePercent,
        float plasmaPercent,
        float tritiumPercent,
        float nitrousOxidePercent,
        float otherPercent)
    {
        TileCount = tileCount;
        AveragePressure = averagePressure;
        AverageTemperature = averageTemperature;
        OxygenPercent = oxygenPercent;
        NitrogenPercent = nitrogenPercent;
        CarbonDioxidePercent = carbonDioxidePercent;
        PlasmaPercent = plasmaPercent;
        TritiumPercent = tritiumPercent;
        NitrousOxidePercent = nitrousOxidePercent;
        OtherPercent = otherPercent;
    }
}