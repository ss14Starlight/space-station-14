using System.Linq;
using Content.Shared._Starlight.Changeling;
using Content.Shared.Actions;
using Content.Shared.Changeling.Components;
using Content.Shared.Changeling.Systems;
using Content.Shared.Mind;
using Content.Shared.RetractableItemAction;
using Content.Shared.Stealth.Components;
using Robust.Shared.Utility;

namespace Content.Server._Starlight.Changeling;

public sealed partial class ChangelingSystem
{
    [Dependency] private RetractableItemActionSystem _retractable = default!;
    [Dependency] private ChangelingDevourSystem _devour = default!;

    [SubscribeLocalEvent]
    private void OnEvolveAdaptations(EntityUid uid, ChangelingComponent ling, EvolveChangelingEvent args)
    {
        if (ling.Evolved)
            return;
        ling.Evolved = true;
        ApplyBodyEvolution(uid, ling);
    }

    private float ChemicalCost(ChangelingComponent ling, ChangelingActionComponent action)
        => ling.Evolved ? action.EvolvedChemicalCost ?? action.ChemicalCost : action.ChemicalCost;

    [SubscribeLocalEvent]
    private void OnEvolutionMindActionAdded(EntityUid uid, MindComponent mind, ref ActionAddedEvent args)
    {
        if (mind.OwnedEntity is { } body && TryComp<ChangelingComponent>(body, out var ling))
            ApplyActionEvolution(ling, args.Action);
    }

    [SubscribeLocalEvent]
    private void OnEvolutionBodyActionAdded(EntityUid uid, ChangelingComponent ling, ref ActionAddedEvent args)
        => ApplyActionEvolution(ling, args.Action);

    private void ApplyActionEvolution(ChangelingComponent ling, EntityUid action)
    {
        if (!ling.Evolved)
            return;
        var iconState = MetaData(action).EntityPrototype?.ID switch
        {
            "ActionRetractableItemArmBlade" => "armblade-plus",
            "ActionShriekDissonant" => "shriek_resonant",
            "ActionToggleSpacesuit" => "chitinous_armor",
            _ => null,
        };
        if (iconState != null)
            _actions.SetIcon(action, new SpriteSpecifier.Rsi(
                new ResPath("/Textures/_Starlight/Interface/Actions/actions_changeling.rsi"), iconState));
        if (MetaData(action).EntityPrototype?.ID == "ActionChemicalSting")
            _actions.SetCanTargetSelf(action, true);
        if (!TryComp<RetractableItemActionComponent>(action, out var retractable))
            return;
        if (MetaData(action).EntityPrototype?.ID == "ActionRetractableItemArmBlade")
            _retractable.SetPrototype((action, retractable), "ArmBladePlus");
    }

    private void ApplyBodyEvolution(EntityUid uid, ChangelingComponent ling)
    {
        if (!ling.Evolved)
            return;
        if (TryComp<ChangelingDevourComponent>(uid, out var devour))
            _devour.SetEvolved((uid, devour));
        EvolveSpacesuit(uid, ling);
        foreach (var action in _actions.GetActions(uid).ToArray())
            ApplyActionEvolution(ling, action.Owner);
        if (ling.HasChemicalSting)
            InitializeStingChemistry(uid);
        if (ling.EyesAction != null)
            UpdateEnhancedEyes(uid, ling);
        if (ling.StealthEnabled)
            ConfigureChameleon(uid, ling);
        _speed.RefreshMovementSpeedModifiers(uid);
        Dirty(uid, ling);
    }

    private void EvolveSpacesuit(EntityUid uid, ChangelingComponent ling)
    {
        foreach (var (original, replacement, slot) in new[]
                 {
                     (SpacesuitPrototype, ArmorPrototype, "outerClothing"),
                     (SpacesuitHelmetPrototype, ArmorHelmetPrototype, "head"),
                 })
        {
            if (!ling.Equipment.TryGetValue(original.Id, out var item) || item == null
                || !_inventory.TryGetSlotEntity(uid, slot, out var equipped) || equipped != item
                || !_inventory.TryUnequip(uid, slot, force: true))
                continue;

            ling.Equipment.Remove(original.Id);
            QueueDel(item.Value);
            TryToggleItem(uid, replacement, ling, slot);
        }
    }

    private void ConfigureChameleon(EntityUid uid, ChangelingComponent ling)
    {
        var movement = EnsureComp<StealthOnMoveComponent>(uid);
        movement.PassiveVisibilityRate = -0.37f;
        movement.MovementVisibilityRate = ling.Evolved ? 0 : 0.2f;
        Dirty(uid, movement);
    }
}
