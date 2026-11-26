using Content.Shared.Damage.Components;
using Content.Shared.StatusEffectNew;

namespace Content.Shared.Damage.Systems;

public sealed partial class DamageModifierStatusEffectSystem : EntitySystem
{
    [SubscribeLocalEvent]
    private void OnDamageModifyStatus(Entity<DamageModifierStatusEffectComponent> status, ref StatusEffectRelayedEvent<DamageModifyEvent> args)
        => args.Args.Damage = DamageSpecifier.ApplyModifierSet(args.Args.Damage, status.Comp.Modifiers); // Starlight-edit: lambda
}
