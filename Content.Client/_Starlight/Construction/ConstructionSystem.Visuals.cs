using Content.Client._Starlight.Computers.RemoteControl;
using Content.Shared._Starlight.Construction;
using Content.Shared.Interaction;
using Robust.Client.GameObjects;
using Robust.Shared.Map;

// ReSharper disable once CheckNamespace
namespace Content.Client.Construction;

/*
 * (Intentionally not a docstring) Updates the ghost visuals based on whether the player can reach them for placement
 * or not.
 */
public sealed partial class ConstructionSystem
{
    [Dependency] private SharedInteractionSystem _interactionSystem = default!;
    [Dependency] private RemoteControlInterface _remoteControl = default!;

    // Ghost tint depending on whether the player can actually reach the spot to build it.
    private static readonly Color _ghostReachableColor = Color.FromHex("#00FF0080");
    private static readonly Color _ghostUnreachableColor = Color.FromHex("#FF000080");
    private static readonly Color _ghostBuildingColor = Color.FromHex("#FFCC0080");

    /// <summary>
    /// On update, recalculate reachability of every ghost.
    /// </summary>
    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        foreach (var ghost in _ghosts.Values)
        {
            if (TryComp<ConstructionGhostComponent>(ghost, out var comp))
                UpdateGhostColor(ghost, comp);
        }
    }

    [SubscribeNetworkEvent]
    private void OnCancel(CancelStructureConstructionMessage message)
    {
        if (!_ghosts.TryGetValue(message.GhostId, out var ghost) ||
            !TryComp<ConstructionGhostComponent>(ghost, out var comp))
            return;

        comp.IsBuilding = false;
    }

    /// <summary>
    /// Checks if the player can reach the ghost and ets the color based on that.
    /// </summary>
    private void UpdateGhostColor(EntityUid ghost, ConstructionGhostComponent comp, SpriteComponent? sprite = null)
    {
        if (comp.Prototype is null || !Resolve(ghost, ref sprite))
            return;

        // Ghosts being built are yellow.
        if (comp.IsBuilding)
        {
            _sprite.SetColor((ghost, sprite), _ghostBuildingColor);
            return;
        }

        var mapCoords = _transformSystem.ToMapCoordinates(Transform(ghost).Coordinates);
        var reachable = CanReach(mapCoords, comp.Prototype.CanBuildInImpassable);

        _sprite.SetColor((ghost, sprite), reachable ? _ghostReachableColor : _ghostUnreachableColor);
    }

    /// <summary>
    /// Whether the controlled entity can reach the position.
    /// </summary>
    private bool CanReach(MapCoordinates mapCoords, bool canBuildInImpassable)
    {
        if ((_remoteControl.ControlledEntity ?? _playerManager.LocalEntity) is not { } player)
            return false;

        var predicate = GetPredicate(canBuildInImpassable, mapCoords);
        return _interactionSystem.InRangeUnobstructed(player, mapCoords, predicate: predicate);
    }
}
