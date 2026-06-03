using Content.Shared._Starlight.Medical.Body.Part;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.Buckle.Components;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.DoAfter;
using Content.Shared.Interaction;
using Content.Shared.Inventory;
using Content.Shared.Item;
using Content.Shared.Item.ItemToggle.Components;
using Content.Shared.Popups;
using Content.Shared.Starlight.Medical.Surgery.Effects.Step;
using Content.Shared.Starlight.Medical.Surgery.Events;
using Content.Shared.Starlight.Medical.Surgery.Steps;
using Robust.Shared.Prototypes;
using System.Linq;
using Content.Shared.Damage;
using Robust.Shared.Timing;
using Robust.Shared.Random;
using Content.Shared.Damage.Systems;
using Content.Shared.Weapons.Melee;
using Content.Shared._Starlight.Abstract.Extensions;
using Content.Shared.Tools.Components;
using Robust.Shared.Audio;

namespace Content.Shared.Starlight.Medical.Surgery;
// Based on the RMC14.
// https://github.com/RMC-14/RMC-14
public abstract partial class SharedSurgerySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private DamageableSystem _damageableSystem = default!;

    private void InitializeSteps()
    {
        SubscribeLocalEvent<SurgeryStepComponent, SurgeryStepCompleteEvent>(OnStepComplete);
        SubscribeLocalEvent<SurgeryClearProgressComponent, SurgeryStepCompleteEvent>(OnClearProgressStep);
        SubscribeLocalEvent<SurgeryStepComponent, SurgeryStepEvent>(OnStep);
        SubscribeLocalEvent<SurgeryTargetComponent, SurgeryDoAfterEvent>(OnTargetDoAfter);
        SubscribeLocalEvent<SurgeryTargetComponent, AccessibleOverrideEvent>(OnOverrideAccess);

        SubscribeLocalEvent<SurgeryStepComponent, SurgeryCanPerformStepEvent>(OnCanPerformStep);

        Subs.BuiEvents<SurgeryTargetComponent>(SurgeryUIKey.Key, subs => subs.Event<SurgeryStepChosenBuiMsg>(OnSurgeryTargetStepChosen));
    }
    private void OnTargetDoAfter(Entity<SurgeryTargetComponent> ent, ref SurgeryDoAfterEvent args)
    {
        if (!_timing.IsFirstTimePredicted)
            return;

        if (args.Cancelled ||
            args.Handled ||
            args.Target is not { } target ||
            !IsSurgeryValid(ent, target, args.Surgery, args.Step, out var surgery, out var part, out var step) ||
            !PreviousStepsComplete(ent, part, surgery, args.Step) ||
            !CanPerformStep(args.User, ent, part.Comp.PartType, step, false))
        {
            Log.Warning($"{ToPrettyString(args.User)} tried to start invalid surgery.");
            Dirty(ent);
            if (args.Target.HasValue && TryComp<BodyPartComponent>(args.Target.Value, out var dirtyPart))
                Dirty(args.Target.Value, dirtyPart, Comp<MetaDataComponent>(args.Target.Value));
            return;
        }

        // Calculate success

        var tools = GetTools(args.User);
        var (validTool, damage) = GetBestTool(tools);

        var random = RandomPredicted.GetPredictedRandom(_random, _timing);
        var successRate = CalculateStepSuccessRate(args.User, ent, step, validTool, out var reason, out _); // Reason of lowered rate.
        var alwaysSuccess = validTool != EntityUid.Invalid && TryGetBehavior(validTool, step) is { } behavior && behavior.AlwaysSuccess;
#pragma warning disable CS0618 // To bypass unnecessary warning on prob methods for System.Random(which is returned in predicted random)
        if (!alwaysSuccess && !random.Prob(successRate))
        {

            if (string.IsNullOrEmpty(reason))
                reason = "Because of carelessness, your hand shook. You need to start this step all over again!";

            _damageableSystem.TryChangeDamage(ent.Owner, damage, true, origin: args.User);
            _popup.PopupEntity(reason, args.User, PopupType.SmallCaution);
            return;
        }
#pragma warning restore CS0618

        var ev = new SurgeryStepEvent(args.User, ent, part, tools)
        {
            StepProto = args.Step,
            SurgeryProto = args.Surgery,
        };
        RaiseLocalEvent(step, ref ev);

        if (ev.IsCancelled) return;
        var evComplete = new SurgeryStepCompleteEvent(args.User, ent, part, tools)
        {
            StepProto = args.Step,
            SurgeryProto = args.Surgery,
            IsFinal = surgery.Comp.Steps[^1] == args.Step,
        };
        RaiseLocalEvent(step, ref evComplete);

        RefreshUI(ent);
    }

    public (EntityUid tool, DamageSpecifier damage) GetBestTool(List<EntityUid> tools)
    {
        DamageSpecifier damage = new();
        var validTool = EntityUid.Invalid;
        if (tools.FirstOrDefault() is { Valid: true } heldItem && TryComp<MeleeWeaponComponent>(heldItem, out var melee) && melee?.Damage != null) // First item it's by default held item so it has bigger priority.
        {
            damage = melee.Damage;
            validTool = heldItem;
        }
        else
        {
            foreach (var tool in tools)
                if (TryComp(tool, out MeleeWeaponComponent? toolMelee) && toolMelee.Damage.GetTotal() > damage.GetTotal())
                {
                    damage = toolMelee.Damage;
                    validTool = tool;
                }
        }

        return (validTool, damage);
    }

    private void OnClearProgressStep(Entity<SurgeryClearProgressComponent> ent, ref SurgeryStepCompleteEvent args)
    {
        var progress = Comp<SurgeryProgressComponent>(args.Part);
        progress.CompletedSteps.Clear();
        progress.CompletedSurgeries.Clear();
        progress.StartedSurgeries.Clear();
    }
    private void OnStepComplete(Entity<SurgeryStepComponent> ent, ref SurgeryStepCompleteEvent args)
    {
        if (TryComp<SurgeryClearProgressComponent>(ent, out _)) return;
        if (TryComp<SurgeryProgressComponent>(args.Part, out var progress))
        {
            progress.CompletedSteps.Add($"{args.SurgeryProto}:{args.StepProto}");
            if (!progress.StartedSurgeries.Contains(args.SurgeryProto) && !args.IsFinal)
                progress.StartedSurgeries.Add(args.SurgeryProto);
            if (progress.StartedSurgeries.Contains(args.SurgeryProto) && args.IsFinal)
                progress.StartedSurgeries.Remove(args.SurgeryProto);
        }
        else
        {
            progress = new SurgeryProgressComponent { CompletedSteps = [$"{args.SurgeryProto}:{args.StepProto}"]};
            if(!args.IsFinal)
                progress.StartedSurgeries.Add(args.SurgeryProto);
            AddComp(args.Part, progress);
        }
        if (args.IsFinal)
            progress.CompletedSurgeries.Add(args.SurgeryProto);
    }
    private void OnStep(Entity<SurgeryStepComponent> ent, ref SurgeryStepEvent args)
    {
        foreach (var reg in (ent.Comp.Tools ?? []).Values)
        {
            var tool = args.Tools.FirstOrDefault(x => HasComp(x, reg.Component.GetType()));
            if (tool == default) continue;

            var behavior = TryGetBehavior(tool, ent.Comp);
            if (behavior != null)
            {
                if (behavior.EndSound != null)
                    _audio.PlayPredicted(behavior.EndSound, tool, null);

                if (ent.Comp.ReagentId != null && behavior.ReagentContainer != null && _solutionContainerSystem.TryGetSolution(tool, behavior.ReagentContainer, out var solution))
                    _solutionContainerSystem.RemoveReagent(solution.Value, new ReagentQuantity(ent.Comp.ReagentId, ent.Comp.ReagentQuantity));
            }
        }

        foreach (var reg in (ent.Comp.Add ?? []).Values)
        {
            var compType = reg.Component.GetType();
            if (HasComp(args.Part, compType))
                continue;
            var newComp = _compFactory.GetComponent(compType);
            _serialization.CopyTo(reg.Component, ref newComp, notNullableOverride: true);
            AddComp(args.Part, newComp);
        }

        if (ent.Comp.BodyAdd != null)
            EntityManager.AddComponents(args.Body, ent.Comp.BodyAdd, false);

        foreach (var reg in (ent.Comp.Remove ?? []).Values)
            RemComp(args.Part, reg.Component.GetType());

        foreach (var reg in (ent.Comp.BodyRemove ?? []).Values)
            RemComp(args.Body, reg.Component.GetType());
    }

    private void OnOverrideAccess(Entity<SurgeryTargetComponent> ent, ref AccessibleOverrideEvent args)
    {
        // Check if the entity is the target to avoid giving the hooked entity access to everything.
        // If we already have access we don't need to run more code.
        if (args.Accessible || args.Target != ent.Owner)
            return;

        var xform = Transform(ent);
        var root = _containers.GetContainingContainers((ent, xform)).FirstOrDefault(x => x.ID == SharedBodySystem.BodyRootContainerId); //get the root container
        if (root == null)
            return;
        if (!_interaction.CanAccess(args.User, root.Owner))
            return;

        args.Accessible = true;
        args.Handled = true;
    }

    private void OnCanPerformStep(Entity<SurgeryStepComponent> ent, ref SurgeryCanPerformStepEvent args)
    {
        if (HasComp<SurgeryOperatingTableConditionComponent>(ent)
            && (!TryComp(args.Body, out BuckleComponent? buckle) || !HasComp<OperatingTableComponent>(buckle.BuckledTo)))
        {
            args.Invalid = StepInvalidReason.NeedsOperatingTable;
            return;
        }

        RaiseLocalEvent(args.Body, ref args);

        if (args.Invalid != StepInvalidReason.None)
            return;

        if (ent.Comp.RequireRemovedArmor
            && _inventory.TryGetContainerSlotEnumerator(args.Body, out var enumerator, args.TargetSlots))
        {
            var items = 0f;
            var total = 0f;
            while (enumerator.MoveNext(out var con))
            {
                total++;
                if (con.ContainedEntity != null && !_tag.HasTag(con.ContainedEntity.Value, ent.Comp.CompatibleArmorTag))
                    items++;
            }

            if (items > 0)
            {
                args.Invalid = StepInvalidReason.Armor;
                args.Popup = $"You need to take off armor from patient to perform this step!";
                return;
            }
        }

        if (args.Invalid != StepInvalidReason.None || ent.Comp.Tools == null)
            return;

        foreach (var reg in ent.Comp.Tools.Values)
        {
            var tool = args.Tools.FirstOrDefault(x => HasComp(x, reg.Component.GetType()));
            if (tool == default)
            {
                args.Invalid = StepInvalidReason.MissingTool;

                if (reg.Component is ISurgeryToolComponent surgeryComp)
                    args.Popup = $"You need {surgeryComp.ToolName} to perform this step!";
                else if (reg.Component is ToolComponent toolComp)
                    args.Popup = $"You need a tool with {string.Join(", ", toolComp.Qualities)} qualities to perform this step!";

                return;
            }
            else if (TryComp<ItemToggleComponent>(tool, out var togglable) && !togglable.Activated)
            {
                args.Invalid = StepInvalidReason.DisabledTool;

                if (reg.Component is ISurgeryToolComponent toolComp)
                    args.Popup = $"You need to enable {toolComp.ToolName} to perform this step!";

                return;
            }
            else if (TryComp<SurgeryItemSizeConditionComponent>(ent, out var itemSizeComp) && TryComp<ItemComponent>(tool, out var item) && _item.GetSizePrototype(item.Size) > _item.GetSizePrototype(itemSizeComp.Size))
            {
                args.Invalid = StepInvalidReason.TooHigh;
                return;
            }
            else if (ent.Comp.ReagentId != null && _solutionContainerSystem.GetTotalPrototypeQuantity(tool, ent.Comp.ReagentId) < ent.Comp.ReagentQuantity)
            {
                args.Invalid = StepInvalidReason.NotEnoughReagent;
                if (reg.Component is ISurgeryToolComponent toolComp)
                    args.Popup = $"You need at least {ent.Comp.ReagentQuantity}u of {ent.Comp.ReagentId} in {toolComp.ToolName} to perform this step!";
                return;
            }
            else if (reg.Component is ToolComponent targetToolComp && TryComp<ToolComponent>(tool, out var toolComp))
            {
                if (TryComp<MultipleToolComponent>(tool, out var multipleTools) && !targetToolComp.Qualities.Any(x => x == multipleTools.CurrentQualityID))
                {
                    args.Invalid = StepInvalidReason.InvalidMode;

                    args.Popup = $"You need to change your tool to any quality from this list: '{string.Join(", ", targetToolComp.Qualities)}' to perform this step!";
                    return;
                }
                else if (!toolComp.Qualities.All(x => targetToolComp.Qualities.Contains(x)))
                {
                    args.Invalid = StepInvalidReason.MissingTool;

                    args.Popup = $"You need a tool with {string.Join(", ", targetToolComp.Qualities)} qualities to perform this step!";
                    return;
                }
            }

            args.ValidTools.Add(tool);
        }
    }

    private void OnSurgeryTargetStepChosen(Entity<SurgeryTargetComponent> ent, ref SurgeryStepChosenBuiMsg args)
    {
        var user = args.Actor;
        if (GetEntity(args.Entity) is not { Valid: true } body
            || GetEntity(args.Part) is not { Valid: true } targetPart
            || !IsSurgeryValid(body, targetPart, args.Surgery, args.Step, out var surgery, out var part, out var step)
            || !_entitySystem.TryGetSingleton(args.Step, out var stepEnt)
            || !TryComp(stepEnt, out SurgeryStepComponent? stepComp)
            || !CanPerformStep(user, body, part.Comp.PartType, step, true, out _, out _, out var validTools))
            return;

        if (!PreviousStepsComplete(body, part, surgery, args.Step) || IsStepComplete(part, args.Surgery, args.Step))
        {
            var progress = Comp<SurgeryProgressComponent>(part);
            Dirty(part, progress);
            RefreshUI(ent);
            return;
        }

        if (_net.IsServer && TryComp(step, out MetaDataComponent? meta))
        {
            var surgeonName = MetaData(user).EntityName;
            _popup.PopupEntity($"{surgeonName.ToLower()} starts {meta.EntityName.ToLower()}", part, PopupType.LargeCaution);
        }

        float SmallestSuccessRate = 1f;

        var bestSpeed = 1f;

        SoundSpecifier? startSound = null;

        foreach (var tool in validTools)
        {
            var behavior = TryGetBehavior(tool, stepComp);
            if (behavior == null)
                continue;
            bestSpeed = MathF.Max(bestSpeed, behavior.Speed);

            SmallestSuccessRate = Math.Min(SmallestSuccessRate, behavior.SuccessRate);

            if (behavior.StartSound != null) startSound = behavior.StartSound;
        }

        if (startSound != null)
            _audio.PlayPvs(startSound, user);

        if (TryComp(body, out TransformComponent? xform))
            _rotateToFace.TryFaceCoordinates(user, _transform.GetMapCoordinates(body, xform).Position);

        var ev = new SurgeryDoAfterEvent(args.Surgery, args.Step, SmallestSuccessRate);
        var doAfter = new DoAfterArgs(EntityManager, user, stepComp.Duration / bestSpeed, ev, body, part)
        {
            BreakOnMove = true,
            DuplicateCondition = DuplicateConditions.SameTarget,
            ForceNet = true,
            DistanceTarget = ent.Owner,
        };
        _doAfter.TryStartDoAfter(doAfter);
    }

    public (Entity<SurgeryComponent> Surgery, int Step)? GetNextStep(EntityUid body, EntityUid part, EntityUid surgery) => GetNextStep(body, part, surgery, []);
    private (Entity<SurgeryComponent> Surgery, int Step)? GetNextStep(EntityUid body, EntityUid part, Entity<SurgeryComponent?> surgery, List<EntityUid> requirements)
    {
        if (!Resolve(surgery, ref surgery.Comp))
            return null;

        if (requirements.Contains(surgery))
            throw new ArgumentException($"Surgery {surgery} has a requirement loop: {string.Join(", ", requirements)}");

        requirements.Add(surgery);

        if (surgery.Comp.Requirement is { } requirementsIds)
        {
            foreach (var requirementId in requirementsIds)
            {
                if (!_entitySystem.TryGetSingleton(requirementId, out var requirement)
                    && GetNextStep(body, part, requirement, requirements) is { } requiredNext
                    && IsSurgeryValid(body, part, requirementId, requiredNext.Surgery.Comp.Steps[requiredNext.Step], out _, out _, out _))
                    return requiredNext;
            }
        }

        if (!TryComp<SurgeryProgressComponent>(part, out var progress))
        {
            AddComp<SurgeryProgressComponent>(part);
            return ((surgery, surgery.Comp), 0);
        }
        var surgeryProto = Prototype(surgery);
        for (var i = 0; i < surgery.Comp.Steps.Count; i++)
            if (!progress.CompletedSteps.Contains($"{surgeryProto?.ID}:{surgery.Comp.Steps[i]}"))
                return ((surgery, surgery.Comp), i);

        return null;
    }

    public bool PreviousStepsComplete(EntityUid body, EntityUid part, Entity<SurgeryComponent> surgery, EntProtoId step)
    {
        if (surgery.Comp.Requirement is { } requirements)
        {
            foreach (var requirement in requirements)
            {
                if (!_entitySystem.TryGetSingleton(requirement, out var requiredEnt)
                    || !TryComp(requiredEnt, out SurgeryComponent? requiredComp)
                    || !PreviousStepsComplete(body, part, (requiredEnt, requiredComp), step)
                    && IsSurgeryValid(body, part, requirement, step, out _, out _, out _))
                    return false;
            }
        }

        foreach (var surgeryStep in surgery.Comp.Steps)
        {
            if (surgeryStep == step)
                break;

            if (Prototype(surgery.Owner) is not EntityPrototype surgProto || !IsStepComplete(part, surgProto.ID, surgeryStep))
                return false;
        }

        return true;
    }

    public bool CanPerformStep(EntityUid user, EntityUid body, BodyPartType part, EntityUid step, bool doPopup) => CanPerformStep(user, body, part, step, doPopup, out _, out _, out _);
    public bool CanPerformStep(EntityUid user, EntityUid body, BodyPartType part, EntityUid step, bool doPopup, out string? popup, out StepInvalidReason reason, out HashSet<EntityUid> validTools)
    {
        var slot = part switch
        {
            BodyPartType.Head => SlotFlags.HEAD | SlotFlags.MASK | SlotFlags.EYES,
            BodyPartType.Torso => SlotFlags.OUTERCLOTHING | SlotFlags.INNERCLOTHING,
            BodyPartType.Arm => SlotFlags.OUTERCLOTHING | SlotFlags.INNERCLOTHING,
            BodyPartType.Hand => SlotFlags.GLOVES,
            BodyPartType.Leg => SlotFlags.OUTERCLOTHING | SlotFlags.LEGS,
            BodyPartType.Foot => SlotFlags.FEET,
            BodyPartType.Tail => SlotFlags.NONE,
            BodyPartType.Other => SlotFlags.NONE,
            _ => SlotFlags.NONE
        };

        var check = new SurgeryCanPerformStepEvent(user, body, GetTools(user), slot);
        RaiseLocalEvent(step, ref check);
        popup = check.Popup;
        validTools = check.ValidTools;

        if (check.Invalid != StepInvalidReason.None)
        {
            if (doPopup && check.Popup != null)
                _popup.PopupEntity(check.Popup, user, PopupType.SmallCaution);

            reason = check.Invalid;
            return false;
        }

        reason = default;
        return true;
    }

    public bool IsStepComplete(EntityUid part, EntProtoId surgery, EntProtoId step)
    {
        if (TryComp<SurgeryProgressComponent>(part, out var comp))
            return comp.CompletedSteps.Contains($"{surgery}:{step}");
        AddComp<SurgeryProgressComponent>(part);
        return false;
    }

    private SurgeryToolBehavior? TryGetBehavior(EntityUid tool, EntityUid step)
    {
        if (TryComp<SurgeryStepComponent>(step, out var stepComp))
            return TryGetBehavior(tool, stepComp);

        return null;
    }

    private SurgeryToolBehavior? TryGetBehavior(EntityUid tool, SurgeryStepComponent? step)
    {
        if (step != null && step.Tools != null && TryComp<MultipleSurgeryToolComponent>(tool, out var multipleSurgeryTool))
        {
            var preferredTool = step.Tools.FirstOrDefault();
            if (multipleSurgeryTool.Behaviors.TryGetValue(_compFactory.GetComponentName(preferredTool.Value.Component.GetType()), out var toolBehavior))
                return toolBehavior;
        }

        if (TryComp<SurgeryToolComponent>(tool, out var toolComp))
            return toolComp.Behavior;

        return null;
    }
}
