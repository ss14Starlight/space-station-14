using System.Linq;
using Content.Shared.FixedPoint;
using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.Plumbing;

[Serializable, NetSerializable]
public enum PlumbingSmartDispenserUiKey : byte
{
    Key,
}

/// <summary>
/// A single reagent entry for the plumbing smart dispenser UI.
/// </summary>
[Serializable, NetSerializable]
public sealed class PlumbingSmartDispenserReagentEntry
{
    public string ReagentId;
    public string LocalizedName;
    public FixedPoint2 Quantity;
    public Color Color;

    public PlumbingSmartDispenserReagentEntry(string reagentId, string localizedName, FixedPoint2 quantity, Color color)
    {
        ReagentId = reagentId;
        LocalizedName = localizedName;
        Quantity = quantity;
        Color = color;
    }
}

/// <summary>
/// BUI state sent to the client containing the current reagent inventory.
/// </summary>
[Serializable, NetSerializable]
public sealed class PlumbingSmartDispenserBuiState : BoundUserInterfaceState
{
    public List<PlumbingSmartDispenserReagentEntry> Entries;
    public float MaxPerReagent;

    public PlumbingSmartDispenserBuiState(List<PlumbingSmartDispenserReagentEntry> entries, float maxPerReagent)
    {
        Entries = entries;
        MaxPerReagent = maxPerReagent;
    }
}
