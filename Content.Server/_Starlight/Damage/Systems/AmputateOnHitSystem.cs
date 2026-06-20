using System.Linq;
using Content.Server._Starlight.Medical.Body.Systems;
using Content.Server._Starlight.Medical.Limbs;
using Content.Server.Administration.Systems;
using Content.Shared._Starlight;
using Content.Shared._Starlight.Damage.Components;
using Content.Shared.Body.Components;
using Content.Shared.Body.Part;
using Content.Shared.Humanoid;
using Content.Shared.Timing;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Random;

namespace Content.Server._Starlight.Damage.Systems;

public sealed partial class MeleeThrowOnHitSystem : EntitySystem
{
    [Dependency] private UseDelaySystem _delay = default!;
    [Dependency] private StarlightEntitySystem _entitySystem = default!;
    [Dependency] private LimbSystem _limbSystem = default!;
    [Dependency] private BodySystem _bodySystem = default!;
    [Dependency] private IRobustRandom _random = default!;
    public override void Initialize()
        => SubscribeLocalEvent<AmputateOnHitComponent, MeleeHitEvent>(OnMeleeHit);

    private void OnMeleeHit(Entity<AmputateOnHitComponent> weapon, ref MeleeHitEvent args)
    {
        if (!args.IsHit || _delay.IsDelayed(weapon.Owner) || args.HitEntities.Count == 0)
            return;

        if (_random.Prob(weapon.Comp.Chance))
        {
            foreach (var target in args.HitEntities)
            {
                if (_entitySystem.TryEntity<TransformComponent, HumanoidAppearanceComponent, BodyComponent>(target, out var body, log: false))
                {
                    var part = _random.Pick(weapon.Comp.Parts);
                    {
                        var basepart = Spawn(part);
                        if (TryComp<BodyPartComponent>(basepart, out var bodypart))
                        {
                            var targetpart = _bodySystem.GetBodyChildrenOfType(target, bodypart.PartType).FirstOrDefault(p => p.Component.Symmetry == bodypart.Symmetry);
                            if (TryComp(targetpart.Id, out TransformComponent? targetPartTransform) &&
                               TryComp(targetpart.Id, out MetaDataComponent? targetPartMetadata) &&
                               TryComp(targetpart.Id, out BodyPartComponent? targetPartBodyPart))
                            {
                                Entity<TransformComponent, MetaDataComponent, BodyPartComponent> PartToDelete = (targetpart.Id, targetPartTransform, targetPartMetadata, targetPartBodyPart);
                                _limbSystem.Amputatate(body, PartToDelete);
                            }
                        }
                        Del(basepart);
                    }
                }
            }
        }
    }
}
