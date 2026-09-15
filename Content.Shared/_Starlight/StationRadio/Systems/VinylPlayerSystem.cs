using Content.Shared._Starlight.StationRadio.Components;
using Content.Shared.Destructible;
using Content.Shared.Power;
using Robust.Shared.Containers;
using Content.Shared.Examine;

namespace Content.Shared._Starlight.StationRadio.Systems;

public abstract partial class SharedVinylPlayerSystem : EntitySystem
{
    [Dependency] private SharedContainerSystem _container = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<VinylPlayerComponent, EntInsertedIntoContainerMessage>(OnVinylInserted);
        SubscribeLocalEvent<VinylPlayerComponent, EntRemovedFromContainerMessage>(OnVinylRemove);
        SubscribeLocalEvent<VinylPlayerComponent, DestructionEventArgs>(OnDestruction);
        SubscribeLocalEvent<VinylPlayerComponent, PowerChangedEvent>(OnPowerChanged);

        SubscribeLocalEvent<VinylPlayerComponent, ExaminedEvent>(OnExamined); // Starlight - Shift Click to view what Vinyl is inserted.
    }

    protected virtual void OnPowerChanged(EntityUid uid, VinylPlayerComponent comp, PowerChangedEvent args)
    {
        // Starlight -> Moved to Content.Server/_Starlight/StationRadio/Systems/VinylPlayerSystem.cs
    }

    protected virtual void OnDestruction(EntityUid uid, VinylPlayerComponent comp, DestructionEventArgs args)
    {
        // Starlight -> Moved to Content.Server/_Starlight/StationRadio/Systems/VinylPlayerSystem.cs

    }

    protected virtual void OnVinylInserted(EntityUid uid, VinylPlayerComponent comp, EntInsertedIntoContainerMessage args)
    {
        // Starlight -> Moved to Content.Server/_Starlight/StationRadio/Systems/VinylPlayerSystem.cs
    }

    protected virtual void OnVinylRemove(EntityUid uid, VinylPlayerComponent comp, EntRemovedFromContainerMessage args)
    {
        // Starlight -> Moved to Content.Server/_Starlight/StationRadio/Systems/VinylPlayerSystem.cs
    }


    /// <summary>
    /// Show what vinyl is currently inserted when examined.
    /// </summary>
    private void OnExamined(EntityUid uid, VinylPlayerComponent comp, ref ExaminedEvent args)
    {
        if (!_container.TryGetContainer(uid, "vinyl", out var container) || container.ContainedEntities.Count == 0) // confirm actual container ID
        {
            args.PushMarkup(Loc.GetString("vinyl-player-examine-empty"));
            return;
        }

        var vinyl = container.ContainedEntities[0];
        args.PushMarkup(Loc.GetString("vinyl-player-examine-loaded", ("vinyl", Name(vinyl))));
    }
}
