using Content.Server.Administration.Systems;
using Content.Shared._Starlight.Railroading;
using Content.Shared._Starlight.Railroading.Events;

namespace Content.Server._Starlight.Railroading;

public sealed partial class RailroadingSpawnCardsHandlerSystem : EntitySystem
{
    [Dependency] private readonly RailroadRuleSystem _railroadRule = default!;
    [Dependency] private readonly StarlightEntitySystem _entitySystem = default!;

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
