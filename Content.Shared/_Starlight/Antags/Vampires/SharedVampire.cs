using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.Antags.Vampires;

/// <summary>
/// Visual layers for DrunkBlood alert digits
/// </summary>
public enum VampireVisualLayers : byte
{
    Digit1,
    Digit2,
    Digit3,
    Digit4,
}

[Serializable, NetSerializable]
public sealed partial class VampireClassClosedBuiMsg : BoundUserInterfaceMessage { }

[Serializable, NetSerializable]
public enum VampireClassUiKey : byte
{
    Key
}

[Serializable, NetSerializable]
public enum VampireLocateUiKey : byte
{
    Key
}

[Serializable, NetSerializable]
public sealed class VampireClassChosenBuiMsg : BoundUserInterfaceMessage
{
    public string Choice { get; init; } = string.Empty;
}

[Serializable, NetSerializable]
public readonly record struct VampireLocateTarget(NetEntity Target, string DisplayName);

[Serializable, NetSerializable]
public sealed partial class VampireLocateBuiState : BoundUserInterfaceState
{
    public List<VampireLocateTarget> Targets { get; init; } = new();
}

[Serializable, NetSerializable]
public sealed partial class VampireLocateSelectedBuiMsg : BoundUserInterfaceMessage
{
    public NetEntity Target { get; init; }
}
