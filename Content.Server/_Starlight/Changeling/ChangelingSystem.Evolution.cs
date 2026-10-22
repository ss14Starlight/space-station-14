using System.Linq;
using Content.Shared._Starlight.Changeling;
using Content.Shared.Actions;
using Content.Shared.Body.Part;
using Content.Shared._Starlight.Medical.Body.Part;
using Content.Shared.Changeling.Components;
using Content.Shared.Hands;
using Content.Shared.Hands.Components;
using Content.Shared.Mind;
using Content.Shared.RetractableItemAction;
using Content.Shared.Stealth.Components;

namespace Content.Server._Starlight.Changeling;

public sealed partial class ChangelingSystem
{
    [Dependency] private RetractableItemActionSystem _retractable = default!;

    private void InitializeEvolution()
    {
        SubscribeLocalEvent<MindComponent, ActionAddedEvent>(OnEvolutionMindActionAdded);
        SubscribeLocalEvent<ChangelingComponent, ActionAddedEvent>(OnEvolutionBodyActionAdded);
    }

    private float ChemicalCost(ChangelingComponent ling, ChangelingActionComponent action)
        => ling.Evolved ? action.EvolvedChemicalCost ?? action.ChemicalCost : action.ChemicalCost;

    private void OnEvolutionMindActionAdded(EntityUid uid, MindComponent mind, ref ActionAddedEvent args)
    {
        if (mind.OwnedEntity is { } body && TryComp<ChangelingComponent>(body, out var ling))
            ApplyActionEvolution(ling, args.Action);
    }

    private void OnEvolutionBodyActionAdded(EntityUid uid, ChangelingComponent ling, ref ActionAddedEvent args)
        => ApplyActionEvolution(ling, args.Action);

    private void ApplyActionEvolution(ChangelingComponent ling, EntityUid action)
    {
        if (!ling.Evolved || !TryComp<RetractableItemActionComponent>(action, out var retractable))
            return;
        if (MetaData(action).EntityPrototype?.ID is "ActionRetractableItemArmBlade" or "ActionChangelingFragmentArmblade")
            _retractable.SetPrototype((action, retractable), "ArmBladePlus");
    }

    private void ApplyBodyEvolution(EntityUid uid, ChangelingComponent ling)
    {
        if (!ling.Evolved)
            return;
        if (TryComp<ChangelingDevourComponent>(uid, out var devour))
            _devour.SetEvolved((uid, devour));
        foreach (var action in _actions.GetActions(uid).ToArray())
            ApplyActionEvolution(ling, action.Owner);
        if (ling.HasChemicalSting)
            InitializeStingChemistry(uid);
        if (ling.EyesAction != null)
            UpdateEnhancedEyes(uid, ling);
        if (ling.StealthEnabled)
            ConfigureChameleon(uid, ling);
        if (TryComp<ChangelingColonyMemberComponent>(uid, out var member) && member.IsFragment && !member.IsBlood
            && TryComp<BodyPartComponent>(uid, out var part))
        {
            member.EvolvedAction = part.PartType switch
            {
                BodyPartType.Arm or BodyPartType.Hand => "ActionChangelingFragmentArmblade",
                BodyPartType.Head => "ActionChangelingFragmentShriek",
                BodyPartType.Leg or BodyPartType.Foot => "ActionChameleonSkin",
                _ => null,
            };
            if (part.PartType is BodyPartType.Arm or BodyPartType.Hand && !HasComp<HandsComponent>(uid))
            {
                var hands = EnsureComp<HandsComponent>(uid);
                _hands.AddHand((uid, hands), "armblade", HandLocation.Middle);
            }
        }
        _speed.RefreshMovementSpeedModifiers(uid);
        Dirty(uid, ling);
    }

    private void ConfigureChameleon(EntityUid uid, ChangelingComponent ling)
    {
        var movement = EnsureComp<StealthOnMoveComponent>(uid);
        movement.PassiveVisibilityRate = -0.37f;
        movement.MovementVisibilityRate = ling.Evolved ? 0 : 0.2f;
        Dirty(uid, movement);
    }
}
