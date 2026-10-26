using Content.Server.Construction;
using Content.Shared._Starlight.Power.CombustionTurbine;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.Interaction;
using Content.Shared.Stacks;
using Content.Shared.Tools;
using Content.Shared.Wires;
using Robust.Shared.Prototypes;

namespace Content.Server._Starlight.Power.CombustionTurbine;

public sealed partial class CombustionTurbineSystem
{
    [Dependency] private IPrototypeManager _proto = default!;

    private static readonly ProtoId<ToolQualityPrototype> PryingQuality = "Prying";

    #region Sections

    public CombustionTurbinePartComponent? GetPart(Entity<CombustionTurbineStageComponent> ent)
        => _itemSlots.GetItemOrNull(ent, ent.Comp.PartSlot) is { } item
        && TryComp<CombustionTurbinePartComponent>(item, out var part)
        ? part
        : null;

    private bool IsRunning(EntityUid uid)
        => GetRotor(uid) is { } rotor && (rotor.Comp.Enabled || rotor.Comp.Rpm > 0f);

    [SubscribeLocalEvent]
    private void OnStagePanelChanged(Entity<CombustionTurbineStageComponent> ent, ref PanelChangedEvent args)
        => _itemSlots.SetLock(ent, ent.Comp.PartSlot, !args.Open);

    [SubscribeLocalEvent]
    private void OnStageInsertAttempt(Entity<CombustionTurbineStageComponent> ent, ref ItemSlotInsertAttemptEvent args)
    {
        if (args.Slot.ID != ent.Comp.PartSlot)
            return;

        if (!TryComp<CombustionTurbinePartComponent>(args.Item, out var part)
            || part.Stage != ent.Comp.Stage
            || IsRunning(ent))
        {
            args.Cancelled = true;
        }
    }

    [SubscribeLocalEvent]
    private void OnStageEjectAttempt(Entity<CombustionTurbineStageComponent> ent, ref ItemSlotEjectAttemptEvent args)
    {
        if (args.Slot.ID == ent.Comp.PartSlot && IsRunning(ent))
            args.Cancelled = true;
    }

    [SubscribeLocalEvent(before: [typeof(ConstructionSystem), typeof(ItemSlotsSystem)])]
    private void OnStageInteractUsing(Entity<CombustionTurbineStageComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        if (TryComp<CombustionTurbinePartComponent>(args.Used, out var part))
        {
            var error = part.Stage != ent.Comp.Stage ? "combustion-turbine-part-wrong-stage"
                : !IsPanelOpen(ent) ? "combustion-turbine-part-panel-closed"
                : IsRunning(ent) ? "combustion-turbine-part-running"
                : GetPart(ent) != null ? "combustion-turbine-part-occupied"
                : null;

            if (error == null)
                return;

            _popup.PopupEntity(Loc.GetString(error), ent, args.User);
            args.Handled = true;
            return;
        }

        if (!IsPanelOpen(ent) || GetPart(ent) == null || !_tool.HasQuality(args.Used, PryingQuality))
            return;

        args.Handled = true;
        if (IsRunning(ent))
        {
            _popup.PopupEntity(Loc.GetString("combustion-turbine-part-running"), ent, args.User);
            return;
        }

        _tool.UseTool(args.Used, args.User, ent, ent.Comp.PartPryDelay, PryingQuality, new CombustionTurbinePartPryDoAfterEvent());
    }

    [SubscribeLocalEvent]
    private void OnStagePryDoAfter(Entity<CombustionTurbineStageComponent> ent, ref CombustionTurbinePartPryDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        args.Handled = true;
        if (!_itemSlots.TryGetSlot(ent, ent.Comp.PartSlot, out var slot))
            return;

        _itemSlots.TryEjectToHands(ent, slot, args.User);
    }

    #endregion

    #region Parts

    [SubscribeLocalEvent(before: [typeof(ConstructionSystem), typeof(ItemSlotsSystem)])]
    private void OnPartInteractUsing(Entity<CombustionTurbinePartComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || !TryComp<StackComponent>(args.Used, out var stack))
            return;

        args.Handled = true;
        if (ent.Comp.NextUpgrade is not { } upgrade)
        {
            _popup.PopupEntity(Loc.GetString("combustion-turbine-part-max-tier"), ent, args.User);
            return;
        }

        if (stack.StackTypeId != upgrade.Stack)
        {
            _popup.PopupEntity(Loc.GetString("combustion-turbine-part-wrong-material"), ent, args.User);
            return;
        }

        if (_stack.GetCount((args.Used, stack)) < upgrade.Amount)
        {
            _popup.PopupEntity(Loc.GetString("combustion-turbine-part-not-enough",
                    ("amount", upgrade.Amount),
                    ("material", GetStackName(upgrade.Stack))),
                ent,
                args.User);
            return;
        }

        var delay = TimeSpan.FromSeconds(ent.Comp.UpgradeDelayPerTier * ent.Comp.Tier);
        _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, args.User, delay, new CombustionTurbinePartUpgradeDoAfterEvent(), ent, ent, args.Used)
        {
            BreakOnMove = true,
            NeedHand = true,
        });
    }

    [SubscribeLocalEvent]
    private void OnPartUpgradeDoAfter(Entity<CombustionTurbinePartComponent> ent, ref CombustionTurbinePartUpgradeDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Used is not { } used)
            return;

        args.Handled = true;
        if (ent.Comp.NextUpgrade is not { } upgrade
            || !TryComp<StackComponent>(used, out var stack)
            || stack.StackTypeId != upgrade.Stack
            || !_stack.TryUse((used, stack), upgrade.Amount))
        {
            return;
        }

        ent.Comp.Tier++;
        _popup.PopupEntity(Loc.GetString("combustion-turbine-part-upgraded", ("tier", ent.Comp.Tier)), ent, args.User);
    }

    [SubscribeLocalEvent]
    private void OnPartExamined(Entity<CombustionTurbinePartComponent> ent, ref ExaminedEvent args)
    {
        using (args.PushGroup(nameof(CombustionTurbinePartComponent)))
        {
            args.PushMarkup(Loc.GetString("combustion-turbine-part-examine-tier",
                ("tier", ent.Comp.Tier),
                ("rpm", (int) ent.Comp.GetMaxRpm()),
                ("temperature", (int) ent.Comp.GetMaxTemperature())));

            args.PushMarkup(Loc.GetString(ent.Comp.Stage switch
                {
                    CombustionTurbineStage.Compressor => "combustion-turbine-part-examine-compressor",
                    CombustionTurbineStage.Rotor => "combustion-turbine-part-examine-rotor",
                    _ => "combustion-turbine-part-examine-stator",
                },
                ("value", (int) MathF.Round(ent.Comp.GetEfficiency() * 100f))));

            args.PushMarkup(ent.Comp.NextUpgrade is { } upgrade
                ? Loc.GetString("combustion-turbine-part-examine-upgrade",
                    ("amount", upgrade.Amount),
                    ("material", GetStackName(upgrade.Stack)))
                : Loc.GetString("combustion-turbine-part-examine-max-tier"));
        }
    }

    private string GetStackName(ProtoId<StackPrototype> stack)
        => _proto.TryIndex(stack, out var proto) ? Loc.GetString(proto.Name) : stack.Id;

    #endregion
}
