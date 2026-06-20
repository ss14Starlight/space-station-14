using Content.Server.Administration.Systems;
using Content.Shared._Starlight;
using Content.Shared._Starlight.Abstract;
using Content.Shared._Starlight.Railroading;
using Content.Shared._Starlight.Railroading.Components;
using Content.Shared._Starlight.Railroading.Components.Handlers;
using Content.Shared._Starlight.Railroading.Events;

namespace Content.Server._Starlight.Railroading.HandlerSystem;

public sealed partial class RailroadingSpawnCardsHandlerSystem : EntitySystem
{
    [Dependency] private StarlightEntitySystem _entitySystem = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<RailroadSpawnCardsOnChosenComponent, RailroadingCardChosenEvent>(OnChosen);
    }

    private void OnChosen(Entity<RailroadSpawnCardsOnChosenComponent> ent, ref RailroadingCardChosenEvent args)
    {
        if (!TryComp<RuleOwnerComponent>(ent, out var ruleOwner)
            || !_entitySystem.TryEntity<RailroadRuleComponent>(ruleOwner.RuleOwner, out var rule))
            return;

        foreach (var card in ent.Comp.Cards)
            rule.Comp.DynamicCards.Enqueue(card);
    }
}
