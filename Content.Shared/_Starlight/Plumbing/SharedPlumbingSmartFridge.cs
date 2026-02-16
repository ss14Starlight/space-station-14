using System.Linq;
using Content.Shared.FixedPoint;
using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.Plumbing;

[Serializable, NetSerializable]
public enum PlumbingSmartFridgeUiKey : byte
{
    Key,
}

/// <summary>
/// A single reagent entry for the plumbing smart fridge UI.
/// </summary>
[Serializable, NetSerializable]
public sealed class PlumbingSmartFridgeReagentEntry
{
    public string ReagentId;
    public string LocalizedName;
    public FixedPoint2 Quantity;
    public Color Color;

    public PlumbingSmartFridgeReagentEntry(string reagentId, string localizedName, FixedPoint2 quantity, Color color)
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
public sealed class PlumbingSmartFridgeBoundUserInterfaceState : BoundUserInterfaceState
{
    public List<PlumbingSmartFridgeReagentEntry> Entries;
    public float MaxPerReagent;

    public PlumbingSmartFridgeBoundUserInterfaceState(List<PlumbingSmartFridgeReagentEntry> entries, float maxPerReagent)
    {
        Entries = entries;
        MaxPerReagent = maxPerReagent;
    }
}
