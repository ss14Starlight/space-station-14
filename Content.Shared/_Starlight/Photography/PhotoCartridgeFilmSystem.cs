using Content.Shared._Starlight.Snapshots;
using Content.Shared.CartridgeLoader;
using Content.Shared.Charges.Components;
using Content.Shared.Charges.Systems;
using Content.Shared.Flash.Components;
using Content.Shared.Timing;
using Robust.Shared.Containers;
using Robust.Shared.Timing;

namespace Content.Shared._Starlight.Photography;

/// <summary>
/// Grants photo capture to a PDA while the photo cartridge is installed.
/// Film loading lives in <see cref="SnapshotCaptureSystem"/>.
/// </summary>
public sealed partial class PhotoCartridgeFilmSystem : EntitySystem
{
    [Dependency] private SharedContainerSystem _containers = default!;
    [Dependency] private SharedChargesSystem _charges = default!;
    [Dependency] private UseDelaySystem _useDelay = default!;
    [Dependency] private IGameTiming _timing = default!;

    private readonly Dictionary<EntityUid, CartridgePhotoState> _preservedState = [];

    private sealed record CartridgePhotoState(int Charges, Dictionary<string, TimeSpan> RemainingDelays);

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

        var delays = new Dictionary<string, TimeSpan>();
        foreach (var (key, info) in new List<KeyValuePair<string, UseDelayInfo>>(
                TryComp<UseDelayComponent>(loader, out var useDelayComponent) ? useDelayComponent.Delays : []))
        {
            delays[key] = info.EndTime > _timing.CurTime ? info.EndTime - _timing.CurTime : TimeSpan.Zero;
        }

        _preservedState[loader] = new CartridgePhotoState(_charges.GetCurrentCharges((loader, charges, null)), delays);
    }

    private void RestoreState(EntityUid loader)
    {
        if (!_preservedState.TryGetValue(loader, out var state))
            return;

        _preservedState.Remove(loader);

        if (TryComp<LimitedChargesComponent>(loader, out var charges))
            _charges.SetCharges((loader, charges), state.Charges);

        if (TryComp<UseDelayComponent>(loader, out var useDelay))
        {
            foreach (var (key, remaining) in state.RemainingDelays)
            {
                if (remaining <= TimeSpan.Zero)
                {
                    _useDelay.CancelDelay((loader, useDelay), key);
                    continue;
                }

                _useDelay.SetLength((loader, useDelay), remaining, key);
                _useDelay.TryResetDelay((loader, useDelay), false, key);
            }
        }
    }
}
