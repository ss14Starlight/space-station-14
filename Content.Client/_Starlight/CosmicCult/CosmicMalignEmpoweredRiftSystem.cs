using Content.Shared._Starlight.CosmicCult.Components;
using Robust.Client.GameObjects;
using Robust.Shared.GameStates;

namespace Content.Client._Starlight.CosmicCult;

public sealed partial class CosmicMalignEmpoweredRiftSystem : EntitySystem
{
    [Dependency] private SpriteSystem _sprite = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CosmicMalignEmpoweredRiftComponent, ComponentHandleState>(OnHandleState);
    }

    private void OnHandleState(EntityUid uid, CosmicMalignEmpoweredRiftComponent component, ref ComponentHandleState args)
    {
        if (args.Current is not CosmicMalignEmpoweredRiftComponent.State state)
            return;

        component.IsOccupied = state.IsOccupied;

        if (!state.IsOccupied)
            return;

        if (!TryComp<SpriteComponent>(uid, out var sprite))
            return;

        _sprite.LayerSetRsiState((uid, sprite), 0, "empowered-rift-occupied");
    }
}
