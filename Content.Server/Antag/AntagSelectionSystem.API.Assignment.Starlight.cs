using Content.Server.Antag.Components;
using Content.Shared.Antag;
using Content.Shared.Humanoid;
using Content.Shared.Preferences;
using JetBrains.Annotations;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

using static Content.Server.Antag.Components.AntagSelectionTime;

namespace Content.Server.Antag;

public sealed partial class AntagSelectionSystem
{
    /// <summary>
    /// Checks if a player has already been pre-selected for a different antag within the same game rule.
    /// </summary>
    private bool HasConflictingPreSelection(
        Entity<AntagSelectionComponent> gameRule,
        ProtoId<AntagSpecifierPrototype> definition,
        ICommonSession player)
    {
        foreach (var (proto, sessions) in gameRule.Comp.PreSelectedSessions)
        {
            if (proto != definition && sessions.Contains(player))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Checks if a given player is valid for a given antag definition, checking the player's selected profile if it exists.
    /// </summary>
    /// <param name="player">The player session to check.</param>
    /// <param name="antagEntity">The entity representing the antag.</param>
    /// <param name="selectedProfile">The player's selected character profile, if any.</param>
    /// <param name="definition">The antag definition to check against.</param>
    /// <returns>True if the player is valid for the antag, false otherwise.</returns>
    private bool IsSelectedProfileValidForAntag(
        ICommonSession player,
        EntityUid antagEntity,
        HumanoidCharacterProfile? selectedProfile,
        AntagSpecifierPrototype definition)
    {
        if (selectedProfile != null)
            return IsProfileValidForAntag(player, selectedProfile, definition);

        // Bodies without HumanoidAppearanceComponent have no character profile to
        // validate and are allowed through here. Humanoid bodies must have a recoverable
        // profile so profile-specific antag requirements, including species restrictions
        // and preferences, cannot be bypassed.
        if (!TryComp<HumanoidAppearanceComponent>(antagEntity, out var humanoid))
            return true;

        var profile = _humanoidAppearance.GetBaseProfile((antagEntity, humanoid));
        return profile != null && IsProfileValidForAntag(player, profile, definition);
    }

    /// <summary>
    /// Returns whether a player may claim an antagonist ghost role.
    /// This intentionally does not require the antag preference to be enabled.
    /// </summary>
    [PublicAPI]
    public bool CanTakeAntagGhostRole(ICommonSession session, ProtoId<AntagSpecifierPrototype> definition)
    {
        return Proto.Resolve(definition, out var antag) && CanTakeAntagGhostRole(session, antag);
    }

    /// <summary>
    /// Returns whether a player may claim an antagonist ghost role.
    /// </summary>
    [PublicAPI]
    public bool CanTakeAntagGhostRole(ICommonSession session, AntagSpecifierPrototype definition)
    {
        return !IsAntagBanned(session, definition) && _playTime.IsAllowedNonSpawning(session, definition.PrefRoles);
    }
}
