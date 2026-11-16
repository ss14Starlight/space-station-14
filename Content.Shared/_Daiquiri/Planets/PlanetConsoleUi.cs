using Robust.Shared.Serialization;

namespace Content.Shared._Daiquiri.Planets;

[Serializable, NetSerializable]
public enum PlanetConsoleUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public sealed class PlanetConsoleState : BoundUserInterfaceState
{
    public List<string> Planets;

    public string Printing;

    public float PrintDuration;

    public float PrintRemaining;

    public float CooldownRemaining;

    public float CooldownDuration;

    public string BlockReason;

    public PlanetConsoleState(List<string> planets, string printing, float printDuration, float printRemaining,
        float cooldownRemaining, float cooldownDuration, string blockReason)
    {
        Planets = planets;
        Printing = printing;
        PrintDuration = printDuration;
        PrintRemaining = printRemaining;
        CooldownRemaining = cooldownRemaining;
        CooldownDuration = cooldownDuration;
        BlockReason = blockReason;
    }
}

[Serializable, NetSerializable]
public sealed class PlanetConsolePrintMessage : BoundUserInterfaceMessage
{
    public string Planet = string.Empty;
}
