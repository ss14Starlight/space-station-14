using Content.Shared._Starlight.Devil.DamnationActions;
using Robust.Shared.Random;
using System.Linq;
using Content.Shared.Starlight.Medical.Surgery.Events;
using Content.Shared.Starlight.Medical.Surgery.Steps.Parts;
using Robust.Server.Containers;
using Robust.Shared.Containers;
using Content.Server.Popups;
using Content.Shared.Popups;
using Content.Server.Hands.Systems;
using Content.Shared.Throwing;
using Content.Shared._Starlight.Devil;
using Content.Server._Starlight.Medical.Body.Systems;

namespace Content.Server._Starlight.Devil.DamnationActions;

public sealed partial class DamnationActionRemoveOrgan : DamnationAction
{
    private BodySystem _body = default!;
    private IRobustRandom _random = default!;
    private ContainerSystem _container = default!;
    private PopupSystem _popup = default!;
    private HandsSystem _hands = default!;
    private ThrowingSystem _throwing = default!;

    public override bool Action(Entity<DamnedComponent> victim)
    {
        var completed = false;
        var organs = _body.GetBodyOrgans(victim)
                    // no brain so we don't RR, would just cause unecessary confusion
                    .Where(organ => !_entityManager.HasComponent<OrganBrainComponent>(organ.Id))
                    .ToList();

        while (!completed && organs.Count > 0)
        {
            var index = _random.Next(organs.Count);
            var organ = organs[index];
            organs.RemoveAt(index);
            _container.TryGetContainingContainer((organ.Id, null, null), out var container);

            if(_body.RemoveOrgan(organ.Id))
            {
                // our current surgery doesn't provide useful methods for other systems to indicate that an organ has been removed,
                // so the event is manually triggered to cause run on effects (blindness, muteness, et cetera)
                if(container is BaseContainer part)
                {
                    var ev = new SurgeryOrganExtracted(victim, part.Owner, organ.Id);
                    _entityManager.EventBus.RaiseLocalEvent(organ.Id, ref ev);
                }

                var victimName = _entityManager.GetComponent<MetaDataComponent>(victim).EntityName;
                var organName = _entityManager.GetComponent<MetaDataComponent>(organ.Id).EntityName;

                _hands.TryPickupAnyHand(victim, organ.Id);
                _popup.PopupEntity(Loc.GetString("damnation-action-remove-organ-popup", ("name", victimName), ("organ", organName)), victim, PopupType.MediumCaution);

                // success!
                return true;
            }
        }

        return false;
    }

    public override void ResolveIoC()
    {
        base.ResolveIoC();

        _body = _entityManager.System<BodySystem>();
        _random = IoCManager.Resolve<IRobustRandom>();
        _container = _entityManager.System<ContainerSystem>();
        _popup = _entityManager.System<PopupSystem>();
        _hands = _entityManager.System<HandsSystem>();
        _throwing = _entityManager.System<ThrowingSystem>();
    }
}
