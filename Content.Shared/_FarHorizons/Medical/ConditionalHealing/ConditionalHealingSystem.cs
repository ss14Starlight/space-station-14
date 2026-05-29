using System.Linq;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Medical.Healing;
using Content.Shared.Starlight.Medical.Surgery;
using Content.Shared.Tag;

namespace Content.Shared._FarHorizons.Medical.ConditionalHealing;

public sealed class ConditionalHealingSystem : EntitySystem
{
    [Dependency] private readonly TagSystem _tag = default!;
    [Dependency] private readonly SharedInteractionSystem _interactionSystem = default!;
    [Dependency] private readonly HealingSystem _healing = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ConditionalHealingComponent, UseInHandEvent>(OnUse, before: [typeof(HealingSystem), typeof(SharedSurgerySystem)]);
        SubscribeLocalEvent<ConditionalHealingComponent, AfterInteractEvent>(OnAfterInteract, before: [typeof(HealingSystem), typeof(SharedSurgerySystem)]);
    }

    private void OnUse(Entity<ConditionalHealingComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled ||
            SelectBestMatch((ent, ent.Comp), args.User) is not ConditionalHealingData healing)
            return;

        args.Handled = _healing.TryHeal((ent, healing.MakeComponent()), args.User, args.User);
    }

    private void OnAfterInteract(Entity<ConditionalHealingComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled ||
            !args.CanReach ||
            args.Target == null ||
            !_interactionSystem.InRangeUnobstructed(args.User, args.Target.Value, popup: true) ||
            SelectBestMatch((ent, ent.Comp), args.Target.Value) is not ConditionalHealingData healing)
            return;

        args.Handled = _healing.TryHeal((ent, healing.MakeComponent()), args.Target.Value, args.User);
    }
    public ConditionalHealingData? SelectBestMatch(Entity<ConditionalHealingComponent?> item, EntityUid target) =>
        !Resolve(item, ref item.Comp, false)
            ? null
            : item.Comp.HealingDefinitions
                .Where(p => _tag.HasAnyTag(target, p.AllowedTags))
                .Select(p => (ConditionalHealingData?)p.Healing)
                .FirstOrDefault((ConditionalHealingData?)null);
}
