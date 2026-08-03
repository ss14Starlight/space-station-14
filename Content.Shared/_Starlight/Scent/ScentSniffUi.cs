using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.Scent;

[Serializable, NetSerializable]
public enum ScentSniffUiKey : byte
{
    Key
}

// Qualitative freshness tier, computed server-side from age vs ScentTraceComponent.TraceLifetime.
[Serializable, NetSerializable]
public enum ScentFreshness : byte
{
    VeryFresh,
    Fresh,
    SomewhatFresh,
    NotVeryFresh,
}

/// <summary>
/// One row in the sniff-results window: a scent found on the sniffed object, and roughly how
/// fresh it is.
/// </summary>
[Serializable, NetSerializable]
public sealed class ScentTraceEntry
{
    public readonly string ScentId;

    public readonly ScentFreshness Freshness;

    // Resolved display name, always populated. Falls back to "non-humanoid" server-side.
    public readonly string Species;

    public ScentTraceEntry(string scentId, ScentFreshness freshness, string species)
    {
        ScentId = scentId;
        Freshness = freshness;
        Species = species;
    }
}

[Serializable, NetSerializable]
public sealed class ScentSniffBoundUserInterfaceState : BoundUserInterfaceState
{
    public readonly List<ScentTraceEntry> Entries;

    public ScentSniffBoundUserInterfaceState(List<ScentTraceEntry> entries)
    {
        Entries = entries;
    }
}

/// <summary>
/// Sent client -> server when the user clicks "Track" next to a scent entry in the window.
/// </summary>
[Serializable, NetSerializable]
public sealed class ScentSniffTrackMessage : BoundUserInterfaceMessage
{
    public readonly string ScentId;

    public ScentSniffTrackMessage(string scentId)
    {
        ScentId = scentId;
    }
}
