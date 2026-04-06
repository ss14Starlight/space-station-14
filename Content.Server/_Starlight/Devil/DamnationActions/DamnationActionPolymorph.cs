using Content.Server.Polymorph.Systems;
using Content.Shared._Starlight.Devil.DamnationActions;
using Content.Shared.Polymorph;
using Robust.Shared.Prototypes;
using Content.Shared._Starlight.Devil;

namespace Content.Server._Starlight.Devil.DamnationActions;

public sealed partial class DamnationActionPolymorph : DamnationAction
{
    [DataField]
    public ProtoId<PolymorphPrototype> Polymorph = "IrreversibleMonkey";

    private PolymorphSystem _polymorph = default!;

    public override bool Action(Entity<DamnedComponent> victim) => _polymorph.PolymorphEntity(victim, Polymorph) is not null;

    public override void ResolveIoC()
    {
        base.ResolveIoC();
        _polymorph = _entityManager.System<PolymorphSystem>();
    }
}
