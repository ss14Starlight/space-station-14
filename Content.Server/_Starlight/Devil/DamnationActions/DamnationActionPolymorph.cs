using Content.Server.Polymorph.Systems;
using Content.Shared._Starlight.Devil.DamnationActions;
using Content.Shared.Polymorph;
using Robust.Shared.Prototypes;

namespace Content.Server._Starlight.Devil.DamnationActions;

public sealed partial class DamnationActionPolymorph : DamnationAction
{
    [DataField]
    public ProtoId<PolymorphPrototype> Polymorph = "IrreversibleMonkey";

    private PolymorphSystem _polymorph = default!;
    private IEntityManager _entityManager = default!;

    public override bool Action(Entity<DamnedComponent> victim)
    {
        _polymorph.PolymorphEntity(victim, Polymorph);
        return true;
    }

    public override void ResolveIoC()
    {
        base.ResolveIoC();

        _entityManager = IoCManager.Resolve<IEntityManager>();
        _polymorph = _entityManager.System<PolymorphSystem>();
    }
}