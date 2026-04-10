using System.Linq;
using Content.Server._Starlight.Medical.Body.Systems;
using Content.Server._Starlight.Medical.Limbs;
using Content.Server.Administration.Systems;
using Content.Shared._Starlight.Trigger.Components.Effects;
using Content.Shared.Body.Components;
using Content.Shared.Body.Part;
using Content.Shared.Humanoid;
using Content.Shared.Trigger;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;

namespace Content.Server._Starlight.Trigger.Systems;

public sealed class AmputatateOnTriggerSystem : XOnTriggerSystem<AmputatateOnTriggerComponent>
{
    [Dependency] private readonly LimbSystem _limbSystem = default!;
    [Dependency] private readonly BodySystem _bodySystem = default!;
    [Dependency] private readonly StarlightEntitySystem _entitySystem = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    protected override void OnTrigger(Entity<AmputatateOnTriggerComponent> ent, EntityUid target, ref TriggerEvent args)
    {
        // Override the normal target if we target the container
        if (ent.Comp.TargetContainer)
        {
            // BOOM whoever is wearing this clothing item
            if (!_container.TryGetContainingContainer(ent.Owner, out var container))
                return;

            target = container.Owner;
        }

        if (_entitySystem.TryEntity<TransformComponent, HumanoidAppearanceComponent, BodyComponent>(target, out var body))
        {
            foreach (var part in ent.Comp.Parts)
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

        args.Handled = true;
    }
}
