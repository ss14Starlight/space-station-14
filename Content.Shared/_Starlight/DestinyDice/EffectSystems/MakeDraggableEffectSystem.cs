using Content.Shared.EntityEffects;
using Content.Shared.Tabletop.Components;

namespace Content.Shared._Starlight.DestinyDice.EffectSystems;

public sealed class MakeDraggableEffectSystem : EntityEffectSystem<DestinyDiceComponent, MakeDraggableEffect>
{
    protected override void Effect(Entity<DestinyDiceComponent> entity, ref EntityEffectEvent<MakeDraggableEffect> args)
    {
        var uid = entity.Comp.ActiveRoller;
        if (uid is null) return;

        var comp = EnsureComp<TabletopDraggableComponent>(uid.Value);
        comp.DraggableWithoutTabletop = true;
        Dirty(uid.Value, comp);
    }
}

public sealed partial class MakeDraggableEffect : EntityEffectBase<MakeDraggableEffect>;
