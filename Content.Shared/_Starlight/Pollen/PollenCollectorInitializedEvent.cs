namespace Content.Shared._Starlight.Pollen;

/// <summary>
/// Raised on an entity once PollenCollectorSystem has finished rolling its
/// pollen list on MapInit. Other systems (e.g. PollenShopSystem) that need
/// to react to a Diona spawning should subscribe to this instead of
/// MapInitEvent directly - only one system may subscribe to
/// (PollenCollectorComponent, MapInitEvent) at a time.
/// </summary>
public sealed class PollenCollectorInitializedEvent : EntityEventArgs
{
}
