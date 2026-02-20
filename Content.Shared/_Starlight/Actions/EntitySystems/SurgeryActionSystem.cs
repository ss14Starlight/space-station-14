
using System.Linq;
using Content.Shared._Starlight.Actions.Events;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.DoAfter;
using Content.Shared.Starlight.Medical.Surgery;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.Actions.EntitySystems;
public sealed class SurgeryActionSystem : EntitySystem
{
        [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
        [Dependency] private readonly SharedBodySystem _body = default!;
        [Dependency] private readonly IPrototypeManager _protoManager = default!;
        [Dependency] private readonly IEntityManager _entityManager = default!;
        [Dependency] private readonly SharedSurgerySystem _surgery = default!;
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SurgeryActionEvent>(OnAction);
    }

    private void OnAction(SurgeryActionEvent args)
    {
        if (args.Handled) return;

        var duration = args.InitialDuration;

        foreach (var surgery in args.Surgeries)
        {
            if(!_protoManager.TryIndex<EntityPrototype>(surgery, out var surgeryProto) || !surgeryProto.TryGetComponent<SurgeryComponent>(out var surgeryComp, _entityManager.ComponentFactory))
                continue;
            foreach (var bodypart in _body.GetBodyChildren(args.Performer).Where(part => args.BodyPartTypes.Contains(part.Component.PartType) && (args.BodyPartSymmetries.Contains(part.Component.Symmetry) || part.Component.Symmetry == BodyPartSymmetry.None)))
                foreach (var step in surgeryComp.Steps)
                {
                    if(!_surgery.IsStepComplete(bodypart.Id, surgery, step))
                    {
                        Surgerise(args.Performer, surgery, step, args.Performer, bodypart.Id, duration);
                        break;
                    }
                }
        }
        args.Handled = true;

    }

    private void Surgerise(EntityUid ent, EntProtoId surgery, EntProtoId step, EntityUid body, EntityUid part, TimeSpan delay)
    {
        var ev = new SurgeryDoAfterEvent(surgery, step, 1);
        var doAfter = new DoAfterArgs(EntityManager, ent, delay, ev, body, part)
        {
            BreakOnMove = true,
            CancelDuplicate = false,
            DuplicateCondition = DuplicateConditions.All,
            ForceNet = true,
            DistanceTarget = ent,
        };
        _doAfter.TryStartDoAfter(doAfter);
    }
}