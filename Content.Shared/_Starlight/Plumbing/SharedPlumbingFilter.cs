using Content.Shared.FixedPoint;
using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.Plumbing;

/// <summary>
///     UI key for the plumbing filter interface.
/// </summary>
[Serializable, NetSerializable]
public enum PlumbingFilterUiKey : byte
{
    Key,
}

/// <summary>
///     State sent to the client to update the filter UI.
/// </summary>
[Serializable, NetSerializable]
public sealed class PlumbingFilterBoundUserInterfaceState : BoundUserInterfaceState
{
    /// <summary>
    ///     The reagent IDs currently being filtered.
    /// </summary>
    public HashSet<string> FilteredReagents { get; }

    /// <summary>
    ///     Whether the filter is enabled.
    /// </summary>
    public bool Enabled { get; }

    public PlumbingFilterBoundUserInterfaceState(HashSet<string> filteredReagents, bool enabled)
    {
        FilteredReagents = filteredReagents;
        Enabled = enabled;
    }
}

/// <summary>
///     Message to toggle the filter on/off.
/// </summary>
[Serializable, NetSerializable]
public sealed class PlumbingFilterToggleMessage : BoundUserInterfaceMessage
{
    public bool Enabled { get; }

    public PlumbingFilterToggleMessage(bool enabled)
    {
        Enabled = enabled;
    }
}

/// <summary>
///     Message to add a reagent to the filter list.
/// </summary>
[Serializable, NetSerializable]
public sealed class PlumbingFilterAddReagentMessage : BoundUserInterfaceMessage
{
    public string ReagentId { get; }

    public PlumbingFilterAddReagentMessage(string reagentId)
    {
        ReagentId = reagentId;
    }
}

/// <summary>
///     Message to remove a reagent from the filter list.
/// </summary>
[Serializable, NetSerializable]
public sealed class PlumbingFilterRemoveReagentMessage : BoundUserInterfaceMessage
{
    public string ReagentId { get; }

    public PlumbingFilterRemoveReagentMessage(string reagentId)
    {
        ReagentId = reagentId;
    }
}

/// <summary>
///     Message to clear all filtered reagents.
/// </summary>
[Serializable, NetSerializable]
public sealed class PlumbingFilterClearMessage : BoundUserInterfaceMessage;
