using Content.Shared._Starlight.StationGridMemory;
using Content.Shared.Station.Components;

namespace Content.Server._Starlight.StationGridMemory;

public sealed class StationGridMemorySystem : EntitySystem
{
    [Dependency] private readonly MetaDataSystem _meta = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<StationGridMemoryComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<StationGridMemoryComponent, GridUidChangedEvent>(OnGridUidChanged);
        SubscribeLocalEvent<StationGridMemoryComponent, EntParentChangedMessage>(OnParentChanged);
    }

    private void OnInit(EntityUid uid, StationGridMemoryComponent comp, ComponentInit args)
    {
        _meta.AddFlag(uid, MetaDataFlags.ExtraTransformEvents);
        if (!TryComp<StationMemberComponent>(Transform(uid).GridUid, out var stationMember)) return;
        comp.LastStation = stationMember.Station;
    }

    private void OnGridUidChanged(EntityUid uid, StationGridMemoryComponent comp, GridUidChangedEvent ev)
    {
        if (!TryComp<StationMemberComponent>(ev.NewGrid, out var stationMember)) return;
        comp.LastStation = stationMember.Station;
    }

    private void OnParentChanged(EntityUid uid, StationGridMemoryComponent comp, EntParentChangedMessage ev)
    {
        if (!TryComp<StationMemberComponent>(ev.Transform.GridUid, out var stationMember)) return;
        comp.LastStation = stationMember.Station;
    }
}
