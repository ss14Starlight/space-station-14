using Content.Shared.DoAfter;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Content.Shared.Interaction;
using Content.Shared.Weapons.Melee.Events;
using Content.Shared.Tag;
using Content.Shared.Popups;
using Content.Shared.ActionBlocker;
using Content.Shared._Starlight.Antags.Abductor.EntitySystems;
using Content.Shared._Starlight.Antags.Abductor.Components;
using Content.Shared._Starlight.Medical.Surgery.Components;

namespace Content.Server._Starlight.Antags.Abductor.EntitySystems;

public sealed partial class AbductorSystem : SharedAbductorSystem
{
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private ActionBlockerSystem _actionBlockerSystem = default!;

    private static readonly ProtoId<TagPrototype> _abductor = "Abductor";
    public void InitializeGizmo()
    {
        SubscribeLocalEvent<AbductorGizmoComponent, AfterInteractEvent>(OnGizmoInteract);
        SubscribeLocalEvent<AbductorGizmoComponent, MeleeHitEvent>(OnGizmoHitInteract);

        SubscribeLocalEvent<AbductorGizmoComponent, AbductorGizmoMarkDoAfterEvent>(OnGizmoDoAfter);
    }

    private void OnGizmoHitInteract(Entity<AbductorGizmoComponent> ent, ref MeleeHitEvent args)
    {
        if (args.HitEntities.Count != 1)
            return;

        var target = args.HitEntities[0];

        if (!HasComp<SurgeryTargetComponent>(target))
            return;

        GizmoUse(ent, target, args.User);
    }

    private void OnGizmoInteract(Entity<AbductorGizmoComponent> ent, ref AfterInteractEvent args)
    {
        if (!_actionBlockerSystem.CanInstrumentInteract(args.User, args.Used, args.Target)
            || !args.Target.HasValue)
            return;

        if (HasComp<SurgeryTargetComponent>(args.Target))
            GizmoUse(ent, args.Target.Value, args.User);

        if (!TryComp<AbductorConsoleComponent>(args.Target, out var console))
            return;

        console.Target = ent.Comp.Target;

        _popup.PopupEntity(Loc.GetString("abductors-ui-gizmo-transferred"), args.User);
        _color.RaiseEffect(Color.FromHex("#00BA00"), new List<EntityUid>(2) { ent.Owner, args.Target.Value }, Filter.Pvs(args.User, entityManager: EntityManager));

        UpdateGui(console.Target, (args.Target.Value, console));
    }

    private void GizmoUse(Entity<AbductorGizmoComponent> ent, EntityUid target, EntityUid user)
    {
        var time = TimeSpan.FromSeconds(6);

        if (_tags.HasTag(target, _abductor))
            time = TimeSpan.FromSeconds(0.5);

        var doAfter = new DoAfterArgs(EntityManager, user, time, new AbductorGizmoMarkDoAfterEvent(), ent, target, ent.Owner)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            DistanceThreshold = 1f
        };
        _doAfter.TryStartDoAfter(doAfter);
    }

    private void OnGizmoDoAfter(Entity<AbductorGizmoComponent> ent, ref AbductorGizmoMarkDoAfterEvent args)
    {
        if (!args.DoAfter.Completed) return;
        if (args.Target is null) return;
        ent.Comp.Target = GetNetEntity(args.Target);

        EnsureComp<AbductorVictimComponent>(args.Target.Value, out var victimComponent);
        victimComponent.LastActivation = _time.CurTime + TimeSpan.FromMinutes(5);

        // Turns out this just works?? Thought it would just convert to the same entitycoords but fuckin apparently not
        var coords =
            _xformSys.ToCoordinates(
                _xformSys.ToMapCoordinates(EnsureComp<TransformComponent>(args.Target.Value).Coordinates));

        victimComponent.Position ??= coords;
    }
}
