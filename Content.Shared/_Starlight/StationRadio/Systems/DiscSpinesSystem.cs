using Content.Shared._Starlight.StationRadio.Components;
using Robust.Shared.Containers;
using Robust.Shared.Timing;

namespace Content.Shared._Starlight.StationRadio.Systems;

public sealed partial class DiscSpinesSystem : EntitySystem
{
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private DiscCoverSystem _cover = default!;
    [Dependency] private IGameTiming _timing = default!;

    [SubscribeLocalEvent]
    private void OnInserted(EntityUid uid, DiscSpinesComponent comp, EntInsertedIntoContainerMessage args)
    {
        if (args.Container.ID == comp.Container)
            UpdateSpines((uid, comp));
    }

    [SubscribeLocalEvent]
    private void OnRemoved(EntityUid uid, DiscSpinesComponent comp, EntRemovedFromContainerMessage args)
    {
        if (args.Container.ID == comp.Container)
            UpdateSpines((uid, comp));
    }

    private void UpdateSpines(Entity<DiscSpinesComponent> ent)
    {
        if (_timing.ApplyingState || !_container.TryGetContainer(ent, ent.Comp.Container, out var container))
            return;

        var spines = new Color[container.ContainedEntities.Count];
        for (var i = 0; i < spines.Length; i++)
            spines[i] = SpineColor(container.ContainedEntities[i], ent.Comp.BareColor);

        _appearance.SetData(ent, DiscSpinesVisuals.Spines, new DiscSpinesData { Colors = spines });
    }

    private Color SpineColor(EntityUid item, Color bare)
    {
        if (!TryComp<DiscCoverComponent>(item, out var cover))
            return bare;

        return _cover.GetDisc((item, cover)) is { } disc
            ? _cover.GetCover(disc, cover.Patterns).Main
            : cover.EmptyColor;
    }
}
