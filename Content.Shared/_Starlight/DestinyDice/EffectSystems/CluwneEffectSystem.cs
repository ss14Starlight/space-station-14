using System.Linq;
using Content.Shared._Starlight.DestinyDice.Effects;
using Content.Shared._Starlight.EntityEffects.EntitySystems;
using Content.Shared.Cluwne;
using Content.Shared.EntityEffects;
using Content.Shared.Inventory;

namespace Content.Shared._Starlight.DestinyDice.EffectSystems;

public sealed partial class CluwneEffectSystem : EntityEffectSystem<DestinyDiceComponent, CluwneEffect>
{
    [Dependency] private DestinyDiceSystem _dd = default!;

    protected override void Effect(Entity<DestinyDiceComponent> entity, ref EntityEffectEvent<CluwneEffect> args)
    {
        if (!_dd.GetEffectTargets(entity, out var targets)) return;
        targets = targets.ToList();
        foreach (var target in targets.Where(HasComp<InventoryComponent>))
            EnsureComp<CluwneComponent>(target);
    }
}
