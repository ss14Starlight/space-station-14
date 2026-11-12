using Content.Shared._Starlight.Snapshots;
using Content.Shared.CartridgeLoader;
using Content.Shared.Charges.Components;
using Content.Shared.Flash.Components;
using Content.Shared.Timing;
using Robust.Shared.Containers;
using Robust.Shared.GameStates;
using Robust.Shared.Timing;

namespace Content.Shared._Starlight.Photography;

/// <summary>
/// Grants photo capture to a PDA while the photo cartridge is installed.
/// Film loading lives in <see cref="SnapshotCaptureSystem"/>.
/// </summary>
public sealed partial class PhotoCartridgeFilmSystem : EntitySystem
{
    [Dependency] private SharedContainerSystem _containers = default!;

    private readonly Dictionary<EntityUid, CartridgePhotoState> _preservedState = [];

    private sealed record CartridgePhotoState(IComponentState? ChargesState, IComponentState? UseDelayState);

    [SubscribeLocalEvent]
    private void OnLoaderComponentShutdown(Entity<MetaDataComponent> ent, ref ComponentShutdown args)
        => _preservedState.Remove(ent.Owner);

    [SubscribeLocalEvent]
    private void OnCartridgeAdded(Entity<PhotoCartridgeComponent> ent, ref CartridgeAddedEvent args)
    {
        EntityManager.AddComponents(args.Loader, ent.Comp.GrantedComponents, removeExisting: false);
        RestoreState(args.Loader);
    }

    [SubscribeLocalEvent]
    private void OnCartridgeRemoved(Entity<PhotoCartridgeComponent> ent, ref CartridgeRemovedEvent args)
    {
        if (HasAnotherPhotoCartridge(args.Loader, ent.Owner))
            return;

        SaveState(args.Loader);
        RemComp<SnapshotCaptureComponent>(args.Loader);
        RemComp<FlashComponent>(args.Loader);
        RemComp<LimitedChargesComponent>(args.Loader);
        RemComp<AutoRechargeComponent>(args.Loader);
        RemComp<UseDelayComponent>(args.Loader);
    }

    private bool HasAnotherPhotoCartridge(EntityUid loader, EntityUid removedCartridge)
    {
        if (!TryComp<CartridgeLoaderComponent>(loader, out var loaderComponent))
            return false;

        if (loaderComponent.CartridgeSlot.Item is { } slotItem
            && slotItem != removedCartridge
            && HasComp<PhotoCartridgeComponent>(slotItem))
        {
            return true;
        }

        if (!_containers.TryGetContainer(loader, SharedCartridgeLoaderSystem.InstalledContainerId, out var container))
            return false;

        foreach (var program in container.ContainedEntities)
        {
            if (program != removedCartridge && HasComp<PhotoCartridgeComponent>(program))
                return true;
        }

        return false;
    }

    private void SaveState(EntityUid loader)
    {
        if (!TryComp<LimitedChargesComponent>(loader, out var charges))
            return;

        var chargeState = EntityManager.GetComponentState(EntityManager.EventBus, charges, null, GameTick.Zero);
        IComponentState? useDelayState = null;
        if (TryComp<UseDelayComponent>(loader, out var useDelayComponent))
            useDelayState = EntityManager.GetComponentState(EntityManager.EventBus, useDelayComponent, null, GameTick.Zero);

        _preservedState[loader] = new CartridgePhotoState(chargeState, useDelayState);
    }

    private void RestoreState(EntityUid loader)
    {
        if (!_preservedState.TryGetValue(loader, out var state))
            return;

        _preservedState.Remove(loader);

        if (state.ChargesState != null && TryComp<LimitedChargesComponent>(loader, out var charges))
        {
            var handleState = new ComponentHandleState(state.ChargesState, null);
            EntityManager.EventBus.RaiseComponentEvent(loader, charges, ref handleState);
            Dirty(loader, charges);
        }

        if (state.UseDelayState != null && TryComp<UseDelayComponent>(loader, out var useDelay))
        {
            var handleState = new ComponentHandleState(state.UseDelayState, null);
            EntityManager.EventBus.RaiseComponentEvent(loader, useDelay, ref handleState);
            Dirty(loader, useDelay);
        }
    }
}
