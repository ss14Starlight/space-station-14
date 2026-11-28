using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.BluespaceArtillery;

[Serializable, NetSerializable]
public enum BluespaceArtilleryUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public enum BluespaceArtilleryConsoleVisuals : byte
{
    State,
}

[Serializable, NetSerializable]
public enum BluespaceArtilleryConsoleLayers : byte
{
    Base,
}

[Serializable, NetSerializable]
public enum BluespaceArtilleryConsoleState : byte
{
    Unpowered,
    Idle,
    Charging,
    Ready,
}

[Serializable, NetSerializable]
public enum BluespaceArtilleryVisuals : byte
{
    State,
}

[Serializable, NetSerializable]
public enum BluespaceArtilleryState : byte
{
    Unpowered,
    Idle,
    Charging,
    Ready,
    Cooldown,
}

[Serializable, NetSerializable]
public sealed class BluespaceArtilleryConsoleUiState : BoundUserInterfaceState
{
    /// <summary>
    /// True when the console controls a finished cannon.
    /// </summary>
    public bool Connected;

    /// <summary>
    /// Last message from the console, such as why the cannon could not be built.
    /// </summary>
    public string? Notice;

    public List<BluespaceArtilleryTargetState> Targets = [];
    public NetEntity? Target;

    /// <summary>
    /// Stored charge from 0 to 1.
    /// </summary>
    public float Charge;
    public bool Charging;

    /// <summary>
    /// Power the cannon receives right now, in watts.
    /// </summary>
    public float ReceivedPower;
    public float DrawRate;

    /// <summary>
    /// Game time when the cannon may fire again.
    /// </summary>
    public TimeSpan CooldownEnd;
}

[Serializable, NetSerializable]
public sealed class BluespaceArtilleryTargetState
{
    public NetEntity Entity;
    public string Name = string.Empty;

    /// <summary>
    /// The map or grid the target is on, used to group the list.
    /// </summary>
    public string Location = string.Empty;

    /// <summary>
    /// Test sites are drawn apart from ordinary beacons.
    /// </summary>
    public bool TestSite;
}

[Serializable, NetSerializable]
public sealed class BluespaceArtilleryBuildMessage : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class BluespaceArtillerySelectTargetMessage(NetEntity target) : BoundUserInterfaceMessage
{
    public readonly NetEntity Target = target;
}

[Serializable, NetSerializable]
public sealed class BluespaceArtilleryChargeMessage(bool charge) : BoundUserInterfaceMessage
{
    public readonly bool Charge = charge;
}

[Serializable, NetSerializable]
public sealed class BluespaceArtilleryFireMessage : BoundUserInterfaceMessage;
