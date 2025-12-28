using Content.Server.Body.Systems;
using Content.Shared._Starlight.Devil.DamnationActions;
using Robust.Shared.Random;
using System.Linq;
using Content.Shared.Starlight.Medical.Surgery.Events;
using Content.Shared.Starlight.Medical.Surgery.Steps.Parts;
using Robust.Server.Containers;
using Robust.Shared.Containers;

namespace Content.Server._Starlight.Devil.DamnationActions;

public sealed partial class DamnationActionRemoveOrgan : DamnationAction
{
    private BodySystem _body = default!;
    private IRobustRandom _random = default!;
    private ContainerSystem _container = default!;

    public override bool Action(Entity<DamnedComponent> victim)
    {
        var completed = false;
        var organs = _body.GetBodyOrgans(victim).ToList();
        while (!completed)
        {
            var organ = _random.Pick(organs);

             _container.TryGetContainingContainer((organ.Id, null, null), out var container);
            if(_body.RemoveOrgan(organ.Id))
            {
                completed = true;

                // our current surgery doesn't provide useful methods for other systems to indicate that an organ has been removed,
                // so the event is manually triggered to cause run on effects (blindness, muteness, et cetera)
                if(container is BaseContainer part)
                {
                    var ev = new SurgeryOrganExtracted(victim, part.Owner, organ.Id);
                    _entityManager.EventBus.RaiseLocalEvent(organ.Id, ref ev);
                }
            }
        }

        return true;
    }

    public override void ResolveIoC()
    {
        base.ResolveIoC();

        _body = _entityManager.System<BodySystem>();
        _random = IoCManager.Resolve<IRobustRandom>();
        _container = _entityManager.System<ContainerSystem>();
    }
}