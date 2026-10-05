using Content.Shared._Starlight.Washing;

namespace Content.Client._Starlight.Washing;

/// <summary>
/// System for using washing fixtures for self-cleaning.
/// </summary>
public sealed partial class WashingFixtureSystem : SharedWashingFixtureSystem
{
    /// <summary>
    /// No-op on client.
    /// </summary>
    protected override void TryStartCleaning(Entity<WashingFixtureComponent> entity, EntityUid user, EntityUid target) { }
}
