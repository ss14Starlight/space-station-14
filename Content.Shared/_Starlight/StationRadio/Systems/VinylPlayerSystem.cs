using Content.Shared._Starlight.StationRadio.Components;
using Content.Shared.Destructible;
using Content.Shared.Power;
using Robust.Shared.Containers;
using Content.Shared.Examine;

namespace Content.Shared._Starlight.StationRadio.Systems;

public abstract partial class SharedVinylPlayerSystem : EntitySystem
{
    [Dependency] private SharedContainerSystem _container = default!;

    [SubscribeLocalEvent]
    protected virtual void OnPowerChanged(EntityUid uid, VinylPlayerComponent comp, PowerChangedEvent args)
    {
    }

    [SubscribeLocalEvent]
    protected virtual void OnDestruction(EntityUid uid, VinylPlayerComponent comp, DestructionEventArgs args)
    {
    }

    [SubscribeLocalEvent]
    protected virtual void OnVinylInserted(EntityUid uid, VinylPlayerComponent comp, EntInsertedIntoContainerMessage args)
    {
    }

    [SubscribeLocalEvent]
    protected virtual void OnVinylRemove(EntityUid uid, VinylPlayerComponent comp, EntRemovedFromContainerMessage args)
    {
    }

    /// <summary>
    /// Show what vinyl is currently inserted when examined.
    /// </summary>
    [SubscribeLocalEvent]
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
