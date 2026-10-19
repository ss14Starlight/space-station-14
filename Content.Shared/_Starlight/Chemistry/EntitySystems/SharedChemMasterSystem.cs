using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Server.Chemistry.Components;
using Content.Shared._Starlight.Plumbing.Components;
using Content.Shared.Administration.Logs;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Database;
using Content.Shared.DragDrop;
using Content.Shared.FixedPoint;
using Content.Shared.Labels.Components;
using Content.Shared.Labels.EntitySystems;
using Content.Shared.Popups;
using Content.Shared.Storage;
using Content.Shared.Storage.EntitySystems;
using Content.Shared.Tag;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;

// ReSharper disable CheckNamespace
namespace Content.Shared.Chemistry.EntitySystems;

public abstract partial class SharedChemMasterSystem : EntitySystem
{
    [Dependency] private SharedPopupSystem _popupSystem = default!;
    [Dependency] private SharedAudioSystem _audioSystem = default!;
    [Dependency] private SharedSolutionContainerSystem _solutionContainerSystem = default!;
    [Dependency] private ItemSlotsSystem _itemSlotsSystem = default!;
    [Dependency] private SharedUserInterfaceSystem _userInterfaceSystem = default!;
    [Dependency] private SharedStorageSystem _storageSystem = default!;
    [Dependency] private LabelSystem _labelSystem = default!;
    [Dependency] private ISharedAdminLogManager _adminLogger = default!;
    [Dependency] private TagSystem _tag = default!;
    [Dependency] private SolutionTransferSystem _solutionTransferSystem = default!;

    private static readonly EntProtoId PillPrototypeId = "Pill";
    private static readonly EntProtoId PatchPrototypeId = "Patch";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ChemMasterComponent, ChemMasterSetModeMessage>(OnSetModeMessage);
        SubscribeLocalEvent<ChemMasterComponent, ChemMasterSortingTypeCycleMessage>(OnCycleSortingTypeMessage);
        SubscribeLocalEvent<ChemMasterComponent, ChemMasterSetPillTypeMessage>(OnSetPillTypeMessage);
        SubscribeLocalEvent<ChemMasterComponent, ChemMasterReagentAmountButtonMessage>(OnReagentButtonMessage);
        SubscribeLocalEvent<ChemMasterComponent, ChemMasterReagentCustomAmountButtonMessage>(OnCustomReagentButtonMessage);
        SubscribeLocalEvent<ChemMasterComponent, ChemMasterCreatePillsMessage>(OnCreatePillsMessage);
        SubscribeLocalEvent<ChemMasterComponent, ChemMasterCreatePatchesMessage>(OnCreatePatchesMessage);
        SubscribeLocalEvent<ChemMasterComponent, ChemMasterOutputToBottleMessage>(OnOutputToBottleMessage);
        SubscribeLocalEvent<ChemMasterComponent, ChemMasterOutputDrawSourceMessage>(OnSetDrawSourceMessage);
        SubscribeLocalEvent<ChemMasterComponent, ChemMasterToggleValveMessage>(OnToggleValveMessage);
        SubscribeLocalEvent<ChemMasterComponent, ChemMasterSetTransferAmountMessage>(OnSetTransferAmountMessage); // TRIESTE
    }

    protected virtual void UpdateUi(Entity<ChemMasterComponent> ent, ChemMasterUIDirtyFlags flags)
    {
    }

    private void OnSetModeMessage(Entity<ChemMasterComponent> chemMaster, ref ChemMasterSetModeMessage message)
    {
        // Ensure the mode is valid, either Transfer or Discard.
        if (!Enum.IsDefined(message.ChemMasterMode))
            return;

        chemMaster.Comp.Mode = message.ChemMasterMode;
        Dirty(chemMaster);
        UpdateUi(chemMaster, ChemMasterUIDirtyFlags.Settings);
        ClickSound(chemMaster, message.Actor);
    }

    private void OnCycleSortingTypeMessage(Entity<ChemMasterComponent> chemMaster, ref ChemMasterSortingTypeCycleMessage message)
    {
        chemMaster.Comp.SortingType++;
        if (chemMaster.Comp.SortingType > ChemMasterSortingType.Latest)
            chemMaster.Comp.SortingType = ChemMasterSortingType.None;
        Dirty(chemMaster);
        UpdateUi(chemMaster, ChemMasterUIDirtyFlags.Settings);
        ClickSound(chemMaster, message.Actor);
    }

    private void OnSetPillTypeMessage(Entity<ChemMasterComponent> chemMaster, ref ChemMasterSetPillTypeMessage message)
    {
        // Ensure valid pill type. There are 20 pills selectable, 0-19.
        if (message.PillType > SharedChemMaster.PillTypes - 1)
            return;

        chemMaster.Comp.PillType = message.PillType;
        Dirty(chemMaster);
        UpdateUi(chemMaster, ChemMasterUIDirtyFlags.Settings);
        ClickSound(chemMaster, message.Actor);
    }

    private void OnReagentButtonMessage(Entity<ChemMasterComponent> chemMaster, ref ChemMasterReagentAmountButtonMessage message)
    {
        // Ensure the amount corresponds to one of the reagent amount buttons.
        if (!Enum.IsDefined(typeof(ChemMasterReagentAmount), message.Amount))
            return;

        switch (chemMaster.Comp.Mode)
        {
            case ChemMasterMode.Transfer:
                TransferReagents(chemMaster, message.ReagentId, message.Amount.GetFixedPoint(), message.FromBuffer);
                break;
            case ChemMasterMode.Discard:
                DiscardReagents(chemMaster, message.ReagentId, message.Amount.GetFixedPoint(), message.FromBuffer);
                break;
            default:
                // Invalid mode.
                return;
        }

        UpdateUi(chemMaster, ChemMasterUIDirtyFlags.Solutions);
        ClickSound(chemMaster, message.Actor);
    }

    private void OnSetDrawSourceMessage(Entity<ChemMasterComponent> chemMaster, ref ChemMasterOutputDrawSourceMessage message)
    {
        //Ensure draw source is valid, either from the internal buffer or the inserted beaker
        if (!Enum.IsDefined(message.DrawSource))
            return;

        chemMaster.Comp.DrawSource = message.DrawSource;
        Dirty(chemMaster);
        UpdateUi(chemMaster, ChemMasterUIDirtyFlags.Settings);
        ClickSound(chemMaster, message.Actor);
    }

    private void TransferReagents(Entity<ChemMasterComponent> chemMaster, ReagentId id, FixedPoint2 amount, bool fromBuffer)
    {
        var container = _itemSlotsSystem.GetItemOrNull(chemMaster, SharedChemMaster.InputSlotName);
        if (container is null ||
            !_solutionContainerSystem.TryGetFitsInDispenser(container.Value, out var containerSoln, out var containerSolution) ||
            !_solutionContainerSystem.TryGetSolution(chemMaster.Owner, SharedChemMaster.BufferSolutionName, out var bufferSoln, out var bufferSolution))
        {
            return;
        }

        if (fromBuffer) // Buffer to container
        {
            amount = FixedPoint2.Min(amount, containerSolution.AvailableVolume);

            amount = bufferSolution.RemoveReagent(id, amount, preserveOrder: true);
            _solutionContainerSystem.UpdateChemicals(bufferSoln.Value);
            _solutionContainerSystem.TryAddReagent(containerSoln.Value, id, amount, out var _);
        }
        else // Container to buffer
        {
            amount = FixedPoint2.Min(amount, containerSolution.GetReagentQuantity(id), bufferSolution.AvailableVolume);

            _solutionContainerSystem.RemoveReagent(containerSoln.Value, id, amount);
            _solutionContainerSystem.TryAddReagent(bufferSoln.Value, id, amount, out _);
        }

        Dirty(containerSoln.Value);
        Dirty(bufferSoln.Value);
    }

    private void DiscardReagents(Entity<ChemMasterComponent> chemMaster, ReagentId id, FixedPoint2 amount, bool fromBuffer)
    {
        if (fromBuffer)
        {
            if (_solutionContainerSystem.TryGetSolution(chemMaster.Owner, SharedChemMaster.BufferSolutionName, out var bufferSoln, out var bufferSolution))
            {
                bufferSolution.RemoveReagent(id, amount, preserveOrder: true);
                _solutionContainerSystem.UpdateChemicals(bufferSoln.Value);
            }
        }
        else
        {
            var container = _itemSlotsSystem.GetItemOrNull(chemMaster, SharedChemMaster.InputSlotName);
            if (container is not null &&
                _solutionContainerSystem.TryGetFitsInDispenser(container.Value, out var containerSolution, out _))
            {
                _solutionContainerSystem.RemoveReagent(containerSolution.Value, id, amount);
                Dirty(containerSolution.Value);
            }
            else
                return;
        }
    }

    private void OnCreatePillsMessage(Entity<ChemMasterComponent> chemMaster, ref ChemMasterCreatePillsMessage message)
    {
        var user = message.Actor;
        var maybeContainer = _itemSlotsSystem.GetItemOrNull(chemMaster, SharedChemMaster.OutputSlotName);
        if (maybeContainer is not { Valid: true } container
            || !TryComp(container, out StorageComponent? storage))
        {
            return; // output can't fit pills
        }

        // Ensure the number is valid.
        if (message.Number == 0 || !_storageSystem.HasSpace((container, storage)))
            return;

        // Ensure the amount is valid.
        if (message.Dosage == 0 || message.Dosage > chemMaster.Comp.PillDosageLimit)
            return;

        // Ensure label length is within the character limit.
        if (message.Label.Length > SharedChemMaster.LabelMaxLength)
            return;

        var needed = message.Dosage * message.Number;

        if (!WithdrawFromSource(chemMaster, needed, user, out var withdrawal))
            return;
        var containerLabel = string.IsNullOrWhiteSpace(message.ContainerLabel)
            ? message.Label
            : message.ContainerLabel;

        _labelSystem.Label(container, containerLabel);

        for (var i = 0; i < message.Number; i++)
        {
            var item = PredictedSpawnAtPosition(PillPrototypeId, Transform(container).Coordinates);
            _storageSystem.Insert(container, item, out _, user: user, storage);
            _labelSystem.Label(item, message.Label);

            _solutionContainerSystem.EnsureSolution(item, SharedChemMaster.PillSolutionName, out var itemSolution);
            itemSolution.Comp.Solution.MaxVolume = message.Dosage;

            _solutionContainerSystem.TryAddSolution(itemSolution, withdrawal.SplitSolution(message.Dosage));

            var pill = EnsureComp<PillComponent>(item);
            pill.PillType = chemMaster.Comp.PillType;
            Dirty(item, pill);

            // Log pill creation by a user
            _adminLogger.Add(LogType.Action, LogImpact.Low, $"{ToPrettyString(user):user} printed {ToPrettyString(item):pill} {SharedSolutionContainerSystem.ToPrettyString(itemSolution.Comp.Solution)}");
        }

        ClickSound(chemMaster, message.Actor);
        Dirty<StorageComponent>((container, storage));
        UpdateUi(chemMaster, ChemMasterUIDirtyFlags.Inventory | ChemMasterUIDirtyFlags.Solutions);
    }

    private void OnOutputToBottleMessage(Entity<ChemMasterComponent> chemMaster, ref ChemMasterOutputToBottleMessage message)
    {
        var user = message.Actor;
        var maybeContainer = _itemSlotsSystem.GetItemOrNull(chemMaster, SharedChemMaster.OutputSlotName);
        if (maybeContainer is not { Valid: true } container
            || !_solutionContainerSystem.TryGetSolution(container, SharedChemMaster.BottleSolutionName, out var soln, out var solution))
        {
            return; // output can't fit reagents
        }

        // Ensure the amount is valid.
        if (message.Dosage == 0 || message.Dosage > solution.AvailableVolume)
            return;

        // Ensure label length is within the character limit.
        if (message.Label.Length > SharedChemMaster.LabelMaxLength)
            return;

        if (!WithdrawFromSource(chemMaster, message.Dosage, user, out var withdrawal))
            return;

        _labelSystem.Label(container, message.Label);
        _solutionContainerSystem.TryAddSolution(soln.Value, withdrawal);

        // Log bottle creation by a user
        _adminLogger.Add(LogType.Action, LogImpact.Low,
            $"{ToPrettyString(user):user} bottled {ToPrettyString(container):bottle} {SharedSolutionContainerSystem.ToPrettyString(solution)}");

        ClickSound(chemMaster, message.Actor);
        UpdateUi(chemMaster, ChemMasterUIDirtyFlags.Inventory | ChemMasterUIDirtyFlags.Solutions);
    }

    private bool WithdrawFromSource(
        Entity<ChemMasterComponent> chemMaster,
        FixedPoint2 neededVolume,
        EntityUid? user,
        [NotNullWhen(returnValue: true)] out Solution? outputSolution)
    {
        outputSolution = null;

        Solution? solution;
        Entity<SolutionComponent>? soln = null;

        switch (chemMaster.Comp.DrawSource)
        {
            case ChemMasterDrawSource.Internal:
                if (!_solutionContainerSystem.TryGetSolution(chemMaster.Owner, SharedChemMaster.BufferSolutionName, out soln, out solution))
                    return false;

                if (solution.Volume == 0)
                {
                    if (user is { } uid)
                        _popupSystem.PopupPredictedCursor(Loc.GetString("chem-master-window-buffer-empty-text"), uid);

                    return false;
                }
                if (neededVolume > solution.Volume)
                {
                    if (user is { } uid)
                        _popupSystem.PopupPredictedCursor(Loc.GetString("chem-master-window-buffer-low-text"), uid);

                    return false;
                }

                break;

            case ChemMasterDrawSource.External:
                if (_itemSlotsSystem.GetItemOrNull(chemMaster, SharedChemMaster.InputSlotName) is not { } container)
                {
                    if (user.HasValue)
                        _popupSystem.PopupPredictedCursor(Loc.GetString("chem-master-window-no-beaker-text"), user.Value);
                    return false;
                }

                if (!_solutionContainerSystem.TryGetFitsInDispenser(container, out soln, out solution))
                    return false;

                if (solution.Volume == 0)
                {
                    if (user is { } uid)
                        _popupSystem.PopupPredictedCursor(Loc.GetString("chem-master-window-beaker-empty-text"), uid);

                    return false;
                }
                if (neededVolume > solution.Volume)
                {
                    if (user is { } uid)
                        _popupSystem.PopupPredictedCursor(Loc.GetString("chem-master-window-beaker-low-text"), uid);

                    return false;
                }

                break;

            default:
                return false;
        }

        outputSolution = solution.SplitSolution(neededVolume);

        if (soln.HasValue)
            _solutionContainerSystem.UpdateChemicals(soln.Value);

        return true;
    }

    private void ClickSound(Entity<ChemMasterComponent> chemMaster, EntityUid? user) =>
        _audioSystem.PlayPredicted(chemMaster.Comp.ClickSound, chemMaster, user, AudioParams.Default.WithVolume(-2f));

    private void OnCustomReagentButtonMessage(Entity<ChemMasterComponent> chemMaster, ref ChemMasterReagentCustomAmountButtonMessage message)
    {
        if (message.Amount <= FixedPoint2.Zero || message.Amount > FixedPoint2.New(1000))
            return;

        switch (chemMaster.Comp.Mode)
        {
            case ChemMasterMode.Transfer:
                TransferReagents(chemMaster, message.ReagentId, message.Amount, message.FromBuffer);
                break;
            case ChemMasterMode.Discard:
                DiscardReagents(chemMaster, message.ReagentId, message.Amount, message.FromBuffer);
                break;
            default:
                return;
        }

        UpdateUi(chemMaster, ChemMasterUIDirtyFlags.Solutions);
        ClickSound(chemMaster, message.Actor);
    }

    private void OnCreatePatchesMessage(Entity<ChemMasterComponent> chemMaster, ref ChemMasterCreatePatchesMessage message)
    {
        var user = message.Actor;
        var maybeContainer = _itemSlotsSystem.GetItemOrNull(chemMaster, SharedChemMaster.OutputSlotName);
        if (maybeContainer is not { Valid: true } container || !TryComp(container, out StorageComponent? storage))
            return; // output can't fit pills

        // Ensure the number is valid.
        if (message.Number == 0 || !_storageSystem.HasSpace((container, storage)))
            return;

        // Ensure the amount is valid.
        if (message.Dosage == 0 || message.Dosage > chemMaster.Comp.PatchDosageLimit)
            return;

        // Ensure label length is within the character limit.
        if (message.Label.Length > SharedChemMaster.LabelMaxLength)
            return;

        var needed = message.Dosage * message.Number;
        if (!WithdrawFromSource(chemMaster, needed, user, out var withdrawal))
            return;

        var containerLabel = string.IsNullOrWhiteSpace(message.ContainerLabel)
            ? message.Label
            : message.ContainerLabel;

        _labelSystem.Label(container, containerLabel);

        for (var i = 0; i < message.Number; i++)
        {
            var item = PredictedSpawnAtPosition(PatchPrototypeId, Transform(container).Coordinates);
            _storageSystem.Insert(container, item, out _, user: user, storage);
            _labelSystem.Label(item, message.Label);

            _solutionContainerSystem.EnsureSolution(item, SharedChemMaster.PatchSolutionName, out var itemSolution);
            _solutionContainerSystem.SetCapacity(itemSolution, message.Dosage);
            _solutionContainerSystem.TryAddSolution(itemSolution, withdrawal.SplitSolution(message.Dosage));
        }

        ClickSound(chemMaster, message.Actor);
        Dirty<StorageComponent>((container, storage));
        UpdateUi(chemMaster, ChemMasterUIDirtyFlags.Inventory | ChemMasterUIDirtyFlags.Solutions);
    }

    private void OnToggleValveMessage(Entity<ChemMasterComponent> chemMaster, ref ChemMasterToggleValveMessage message)
    {
        if (!TryComp<PlumbingOutletComponent>(chemMaster.Owner, out var plumbingOutlet))
            return;

        plumbingOutlet.Enabled = !plumbingOutlet.Enabled;
        Dirty(chemMaster.Owner, plumbingOutlet);
        UpdateUi(chemMaster, ChemMasterUIDirtyFlags.PlumbingOutput);
        ClickSound(chemMaster, message.Actor);
    }

    // TRIESTE SPECIFIC
    private void OnSetTransferAmountMessage(
        Entity<ChemMasterComponent> chemMaster,
        ref ChemMasterSetTransferAmountMessage message)
    {
        if (!Enum.IsDefined(typeof(ChemMasterReagentAmount), message.Amount))
            return;

        chemMaster.Comp.TransferAmount = message.Amount;
        Dirty(chemMaster);
        UpdateUi(chemMaster, ChemMasterUIDirtyFlags.Settings);
        ClickSound(chemMaster, message.Actor);
    }
}
