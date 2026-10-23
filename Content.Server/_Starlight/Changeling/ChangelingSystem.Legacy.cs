using Content.Server.Ensnaring;
using Content.Shared._Starlight.Actions.Components;
using Content.Shared._Starlight.Actions.EntitySystems;
using Content.Shared._Starlight.Changeling;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Cuffs;
using Content.Shared.Cuffs.Components;
using Content.Shared.Ensnaring.Components;
using Content.Shared.Eye.Blinding.Components;
using Content.Shared.IdentityManagement;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Popups;
using Content.Shared.Tag;
using Robust.Shared.Prototypes;

namespace Content.Server._Starlight.Changeling;

public sealed partial class ChangelingSystem
{
    [Dependency] private SharedCuffableSystem _cuffs = default!;
    [Dependency] private SharedLatchSystem _latch = default!;
    [Dependency] private EnsnareableSystem _ensnareable = default!;

    private static readonly ProtoId<ReagentPrototype> PolytrinicAcidPrototype = "PolytrinicAcid";
    private static readonly ProtoId<TagPrototype> BolaTag = "Bola";

    // Registered for existing/admin-granted actions, without granting them or adding them to the shop.
    private void SubscribeLegacyAbilities()
    {
        SubscribeLocalEvent<ChangelingComponent, StingBlindEvent>(OnStingBlind);
        SubscribeLocalEvent<ChangelingComponent, StingChemEvent>(OnStingChem);
        SubscribeLocalEvent<ChangelingComponent, StingTransformEvent>(OnStingTransform);
        SubscribeLocalEvent<ChangelingComponent, ActionBiodegradeEvent>(OnBiodegrade);
        SubscribeLocalEvent<ChangelingComponent, ActionLastResortEvent>(OnLastResort);
        SubscribeLocalEvent<ChangelingComponent, ActionLesserFormEvent>(OnLesserForm);
    }

    private void OnStingBlind(EntityUid uid, ChangelingComponent comp, ref StingBlindEvent args)
    {
        if (args.Handled || !TrySting(uid, args)
            || !TryComp<BlindableComponent>(args.Target, out var blindable) || blindable.IsBlind)
            return;

        _hiveBlindness.AdjustEyeDamage((args.Target, blindable), 2);
        _statusEffect.TryAddStatusEffectDuration(args.Target, "StatusEffectTemporaryBlindness", TimeSpan.FromSeconds(5));
        args.Handled = true;
    }

    private void OnStingChem(EntityUid uid, ChangelingComponent comp, ref StingChemEvent args)
    {
        if (!args.Handled && TrySting(uid, args))
            args.Handled = TryInjectReagents(args.Target, args.Chems);
    }

    private void OnStingTransform(EntityUid uid, ChangelingComponent comp, ref StingTransformEvent args)
    {
        if (!args.Handled && TrySting(uid, args, true))
            args.Handled = TryTransform(args.Target, comp, true, true);
    }

    private void OnBiodegrade(EntityUid uid, ChangelingComponent comp, ref ActionBiodegradeEvent args)
    {
        if (args.Handled)
            return;

        if (TryComp<CuffableComponent>(uid, out var cuffs) && _cuffs.TryGetLastCuff((uid, cuffs), out var cuff))
        {
            _cuffs.Uncuff(uid, uid, cuff.Value);
            QueueDel(cuff.Value);
            args.Handled = true;
        }

        if (TryComp<EnsnareableComponent>(uid, out var ensnareable))
        {
            foreach (var ensnaring in ensnareable.Container.ContainedEntities)
            {
                if (!TryComp<EnsnaringComponent>(ensnaring, out var ensnaringComponent) || !_tag.HasTag(ensnaring, BolaTag))
                    continue;

                _ensnareable.ForceFree(ensnaring, ensnaringComponent);
                QueueDel(ensnaring);
                args.Handled = true;
                break;
            }
        }

        var solution = new Solution();
        solution.AddReagent(PolytrinicAcidPrototype, 10f);
        var target = uid;
        if (TryComp<LatchedComponent>(uid, out var latched) && Exists(latched.Latcher))
        {
            target = latched.Latcher;
            args.Handled |= _latch.TryBreakLatch((uid, latched));
        }
        else if (TryComp<PullableComponent>(uid, out var pullable) && pullable.Puller is { } puller)
        {
            target = puller;
        }
        args.Handled |= _puddle.TrySplashSpillAt(target, Transform(target).Coordinates, solution, out _);
    }

    /// <summary>
    /// Retains the unimplemented legacy last-resort event for compatibility.
    /// </summary>
    public void OnLastResort(EntityUid uid, ChangelingComponent comp, ref ActionLastResortEvent args)
    {
        // TODO: Implement. This legacy event had no implementation or action prototype.
    }

    /// <summary>
    /// Transforms the carrier into the legacy monkey form while preserving its mind and resources.
    /// </summary>
    public void OnLesserForm(EntityUid uid, ChangelingComponent comp, ref ActionLesserFormEvent args)
    {
        if (args.Handled || TransformEntity(uid, protoId: "MobLingMonkey", comp: comp) is not { } monkey)
            return;

        var transformed = Comp<ChangelingComponent>(monkey);
        // This replaces a carrier, rather than creating an independent one. The successful action's cost follows it.
        transformed.Resources = comp.Resources;
        transformed.IsInLesserForm = true;
        if (TryGetColony(uid, out var member, out var colony))
        {
            AddColonyMember(monkey, member.Mind, colony);
            colony.Members.Remove(uid);
            if (colony.Active == uid)
                colony.Active = monkey;
            foreach (var anchor in colony.Minds.Values)
            {
                if (!TerminatingOrDeleted(anchor) && Comp<ChangelingMindspaceComponent>(anchor).Viewing == uid)
                    ObserveHiveBody(anchor, Comp<ChangelingMindspaceComponent>(anchor), monkey);
            }
        }
        Dirty(monkey, transformed);
        PlayMeatySound(monkey, transformed);
        var message = Loc.GetString("changeling-transform-others", ("user", Identity.Entity(monkey, EntityManager)));
        _popup.PopupEntity(message, monkey, PopupType.LargeCaution);
        args.Handled = true;
    }
}
