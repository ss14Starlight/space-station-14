using Content.Shared._Starlight.Drone.Components;
using Content.Shared.Interaction.Events;
using Content.Shared.Item;
using Content.Shared.Popups;
using Content.Shared.Tag;
using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.Drone;

/// <summary>
/// Handles drone interaction restrictions based on tags.
/// </summary>
public abstract partial class SharedDroneSystem : EntitySystem
{
    [Dependency] private TagSystem _tagSystem = default!;
    [Dependency] private SharedItemSystem _item = default!;
    [Dependency] private SharedPopupSystem _popup = default!;

    [SubscribeLocalEvent]
    private void OnInteractionAttempt(EntityUid uid, DroneComponent component, ref InteractionAttemptEvent args)
    {
        if (args.Target == null)
            return;

        var target = args.Target.Value;

        // Check blacklist first - if any blacklisted tag is present, deny
        foreach (var blacklistedTag in component.InteractionBlacklist)
        {
            if (_tagSystem.HasTag(target, blacklistedTag))
            {
                args.Cancelled = true;
                return;
            }
        }

        // If whitelist is empty, allow everything not blacklisted
        if (component.InteractionWhitelist.Count == 0)
            return;

        // Check whitelist - if any whitelisted tag is present, allow
        foreach (var whitelistedTag in component.InteractionWhitelist)
        {
            if (_tagSystem.HasTag(target, whitelistedTag))
                return; // Allowed!
        }

        // No whitelisted tags found, deny interaction
        args.Cancelled = true;
    }

    // Stops drones from picking up items that are too large for them to carry.
    [SubscribeLocalEvent]
    private void OnPickupAttempt(Entity<DroneComponent> ent, ref PickupAttemptEvent args)
    {
        if (!TryComp<ItemComponent>(args.Item, out var item))
            return;

        var max = _item.GetSizePrototype(ent.Comp.MaxItemSize);
        var size = _item.GetSizePrototype(item.Size);

        if (size.Weight <= max.Weight)
            return;

        args.Cancel();
        _popup.PopupClient(Loc.GetString("drone-item-too-large"), ent, ent);
    }

    [Serializable, NetSerializable]
    public enum DroneVisuals : byte
    {
        Status
    }

    [Serializable, NetSerializable]
    public enum DroneStatus : byte
    {
        Off,
        On
    }
}
