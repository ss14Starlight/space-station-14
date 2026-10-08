using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using Content.Client._Starlight.Computers.RemoteControl;
using Content.Client.Gameplay;
using Content.Shared.Examine;
using Content.Shared.Verbs;
using Robust.Client.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Utility;

// ReSharper disable CheckNamespace
namespace Content.Client.Verbs;

public sealed partial class VerbSystem
{
    [Dependency] private RemoteControlInterface _remoteControl = default!;

    /// <summary>
    /// Gets context-menu entities using an optional user and field-of-view override.
    /// </summary>
    /// <param name="targetPos">The location around which to find entities.</param>
    /// <param name="userOverride">The entity used for visibility checks, or null to use the local player.</param>
    /// <param name="drawFovOverride">Whether to apply field-of-view checks, or null to use the current eye.</param>
    /// <param name="entities">The found entities, if any.</param>
    /// <returns>True if any entities were found.</returns>
    public bool TryGetEntityMenuEntities(MapCoordinates targetPos, EntityUid? userOverride,
        bool? drawFovOverride, [NotNullWhen(true)] out List<EntityUid>? entities)
    {
        entities = null;

        if (_stateManager.CurrentState is not GameplayStateBase)
            return false;

        var player = userOverride ?? _playerManager.LocalEntity;
        if (player is not { } playerEntity)
            return false;

        // If FOV drawing is disabled, we will modify the visibility option to ignore visiblity checks.
        var visibility = (drawFovOverride ?? _eyeManager.CurrentEye.DrawFov)
            ? Visibility
            : Visibility | MenuVisibility.NoFov;

        var ev = new MenuVisibilityEvent
        {
            TargetPos = targetPos,
            Visibility = visibility,
        };

        RaiseLocalEvent(playerEntity, ref ev);
        visibility = ev.Visibility;

        // Initially, we include all entities returned by a sprite area lookup
        var box = Box2.CenteredAround(targetPos.Position, new Vector2(_lookupSize, _lookupSize));
        var queryResult = _tree.QueryAabb(targetPos.MapId, box);
        entities = new List<EntityUid>(queryResult.Count);
        foreach (var ent in queryResult)
        {
            entities.Add(ent.Uid);
        }

        // If we're in a container list all other entities in it.
        // E.g., allow players in lockers to examine / interact with other entities in the same locker
        if (_containers.TryGetContainingContainer((playerEntity, null), out var container))
        {
            // Only include the container contents when clicking near it.
            if (entities.Contains(container.Owner)
                || _containers.TryGetOuterContainer(container.Owner, Transform(container.Owner), out var outer)
                && entities.Contains(outer.Owner))
            {
                // The container itself might be in some other container, so it might not have been added by the
                // sprite tree lookup.
                if (!entities.Contains(container.Owner))
                    entities.Add(container.Owner);

                // TODO Context Menu
                // This might miss entities in some situations. E.g., one of the contained entities entity in it, that
                // itself has another entity attached to it, then we should be able to "see" that entity.
                // E.g., if a security guard is on a segway and gets thrown in a locker, this wouldn't let you see the guard.
                foreach (var ent in container.ContainedEntities)
                {
                    if (!entities.Contains(ent))
                        entities.Add(ent);
                }
            }
        }

        if ((visibility & MenuVisibility.InContainer) != 0)
        {
            // This is inefficient, but I'm lazy and CBF implementing my own recursive container method. Note that
            // this might actually fail to add the contained children of some entities in the menu. E.g., an entity
            // with a large sprite aabb, but small broadphase might appear in the menu, but have its children added
            // by this.
            var flags = LookupFlags.All & ~LookupFlags.Sensors;
            foreach (var e in _lookup.GetEntitiesInRange(targetPos, _lookupSize, flags: flags))
            {
                if (!entities.Contains(e))
                    entities.Add(e);
            }
        }

        // Do we have to do FoV checks?
        if ((visibility & MenuVisibility.NoFov) == 0)
        {
            TryComp(playerEntity, out ExaminerComponent? examiner);
            for (var i = entities.Count - 1; i >= 0; i--)
            {
                if (!_examine.CanExamine(playerEntity, targetPos, e => e == playerEntity, entities[i], examiner))
                    entities.RemoveSwap(i);
            }
        }

        if ((visibility & MenuVisibility.Invisible) != 0)
            return entities.Count != 0;

        for (var i = entities.Count - 1; i >= 0; i--)
        {
            if (_tagSystem.HasTag(entities[i], HideContextMenuTag))
                entities.RemoveSwap(i);
        }

        // Unless we added entities in containers, every entity should already have a visible sprite due to
        // the fact that we used the sprite tree query.
        if (container == null && (visibility & MenuVisibility.InContainer) == 0)
            return entities.Count != 0;

        var spriteQuery = GetEntityQuery<SpriteComponent>();
        for (var i = entities.Count - 1; i >= 0; i--)
        {
            if (!spriteQuery.TryGetComponent(entities[i], out var spriteComponent) || !spriteComponent.Visible)
                entities.RemoveSwap(i);
        }

        return entities.Count != 0;
    }
}
