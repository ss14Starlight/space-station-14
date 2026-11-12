using Content.Shared._Starlight.Actions.Events;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;

namespace Content.Shared._Starlight.Actions.EntitySystems;

public sealed partial class SharedDamageActionSystem : EntitySystem
{
    [Dependency] private DamageableSystem _damage = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DamageActionEvent>(OnDamageAction);
    }

    private void OnDamageAction(DamageActionEvent ev)
    {
        if(ev.Handled)
            return;

        _damage.ChangeDamage(ev.Target, ev.Damage, ev.IgnoreResistances, origin: ev.Performer);
        ev.Handled = true;
    }
}
