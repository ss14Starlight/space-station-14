using Content.Shared.Mobs.Systems;

namespace Content.Server._Starlight.Silicons.Borgs;

/// <summary>
/// Gives robotics consoles a borg's position, but only while the borg is in a state someone would have to
/// go out and deal with. A healthy, crewed, powered borg reports nothing, so the console cannot be used to
/// follow one around.
/// </summary>
public sealed partial class BorgEmergencyBeaconSystem : EntitySystem
{
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    /// <summary>
    /// Where the borg is if it needs help, or an empty string if it does not.
    /// <paramref name="brainActive"/> is false for a missing brain and for one with nobody in it.
    /// </summary>
    public string GetBeaconLocation(EntityUid borg, float chargeFraction, bool brainActive, bool lockedDown)
    {
        if (!NeedsHelp(borg, chargeFraction, brainActive, lockedDown))
            return string.Empty;

        var tile = _transform.GetGridOrMapTilePosition(borg);
        return $"({tile.X}, {tile.Y})";
    }

    private bool NeedsHelp(EntityUid borg, float chargeFraction, bool brainActive, bool lockedDown)
        => !brainActive
        || lockedDown
        || chargeFraction <= 0f
        || _mobState.IsCritical(borg)
        || _mobState.IsDead(borg);
}
