using System.Linq;
using Content.Server._Starlight.Medical.Body.Systems;
using Content.Server._Starlight.Medical.Limbs;
using Content.Server.Administration.Systems;
using Content.Server.Chat.Systems;
using Content.Shared._Starlight.Damage.Components;
using Content.Shared.Armor;
using Content.Shared.Body.Components;
using Content.Shared.Body.Part;
using Content.Shared.Damage.Events;
using Content.Shared.Humanoid;
using Content.Shared.Timing;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Random;
using Robust.Shared.Utility;

namespace Content.Server._Starlight.Damage.Systems;

public sealed class AmputateCombatSystem : EntitySystem
{
    [Dependency] private readonly UseDelaySystem _delay = default!;
    [Dependency] private readonly StarlightEntitySystem _entitySystem = default!;
    [Dependency] private readonly LimbSystem _limbSystem = default!;
    [Dependency] private readonly BodySystem _bodySystem = default!;
    [Dependency] private readonly ChatSystem _chatSystem = default!;
    [Dependency] private readonly BloodstreamSystem _bloodstreamSystem = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    public override void Initialize()
    {
        SubscribeLocalEvent<AmputateOnHitComponent, MeleeHitEvent>(OnMeleeHit);
        SubscribeLocalEvent<AmputateOnHitComponent, DamageExamineEvent>(OnExamineDamage);
    }

    private void OnMeleeHit(Entity<AmputateOnHitComponent> weapon, ref MeleeHitEvent args)
    {
        if (!args.IsHit || _delay.IsDelayed(weapon.Owner) || args.HitEntities.Count == 0)
            return;

        foreach (var target in args.HitEntities)
        {
            var chance = weapon.Comp.Chance;
            if (TryComp(target, out ArmorComponent? armor))
                chance *= armor.AmputateChanceModifier;

            if (_random.Prob(chance))
            {
                DoAmputate(target, weapon.Comp, args);
            }
        }
    }

    private void DoAmputate(EntityUid target, AmputateOnHitComponent comp, MeleeHitEvent args)
    {
        if (!_entitySystem.TryEntity<TransformComponent, HumanoidAppearanceComponent, BodyComponent>(target,
                out var body, log: false))
            return;

        const float BleedAmount = 100;
        var part = _random.Pick(comp.Parts);
        {
            var basePart = Spawn(part);
            if (TryComp<BodyPartComponent>(basePart, out var bodypart))
            {
                var targetpart = _bodySystem.GetBodyChildrenOfType(target, bodypart.PartType).FirstOrDefault(p => p.Component.Symmetry == bodypart.Symmetry);
                if (TryComp(targetpart.Id, out TransformComponent? targetPartTransform) &&
                    TryComp(targetpart.Id, out MetaDataComponent? targetPartMetadata) &&
                    TryComp(targetpart.Id, out BodyPartComponent? targetPartBodyPart))
                {
                    Entity<TransformComponent, MetaDataComponent, BodyPartComponent> partToDelete = (targetpart.Id, targetPartTransform, targetPartMetadata, targetPartBodyPart);
                    _limbSystem.Amputate(body, partToDelete);
                    _chatSystem.TryEmoteWithChat(target, "Scream");
                    _bloodstreamSystem.TryModifyBleedAmount(target, BleedAmount);
                }
            }
            Del(basePart);
        }
    }
    public void OnExamineDamage(EntityUid uid, AmputateOnHitComponent component, ref DamageExamineEvent args)
    {
        const int ToPercentage = 100;

        if (component.Hidden || component.Chance == 0)
            return;
        var markup = new FormattedMessage();
        if (!args.Message.IsEmpty)
            markup.PushNewline();
        markup.AddMarkupOrThrow(Loc.GetString("damage-examine-amputate", ("chance", component.Chance*ToPercentage)));

        args.Message.AddMessage(markup);
    }
}
