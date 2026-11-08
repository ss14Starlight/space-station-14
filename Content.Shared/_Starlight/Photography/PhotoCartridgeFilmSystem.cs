using Content.Shared._Starlight.Snapshots;
using Content.Shared.CartridgeLoader;
using Content.Shared.Charges.Components;
using Content.Shared.Flash.Components;
using Content.Shared.Timing;

namespace Content.Shared._Starlight.Photography;

/// <summary>
/// Grants photo capture to a PDA while the photo cartridge is installed.
/// Film loading lives in <see cref="SnapshotCaptureSystem"/>.
/// </summary>
public sealed partial class PhotoCartridgeFilmSystem : EntitySystem
{

    [SubscribeLocalEvent]
    private void OnCartridgeAdded(Entity<PhotoCartridgeComponent> ent, ref CartridgeAddedEvent args)
        => EntityManager.AddComponents(args.Loader, ent.Comp.GrantedComponents);

    [SubscribeLocalEvent]
    private void OnCartridgeRemoved(Entity<PhotoCartridgeComponent> ent, ref CartridgeRemovedEvent args)
    {
        RemComp<SnapshotCaptureComponent>(args.Loader);
        RemComp<FlashComponent>(args.Loader);
        RemComp<LimitedChargesComponent>(args.Loader);
        RemComp<AutoRechargeComponent>(args.Loader);
        RemComp<UseDelayComponent>(args.Loader);
    }
}
