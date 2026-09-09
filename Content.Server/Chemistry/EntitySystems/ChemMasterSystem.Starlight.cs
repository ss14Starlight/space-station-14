using Content.Server.Chemistry.Components;
using Content.Shared._Starlight.Plumbing.Components;
using Content.Shared.Chemistry;
using Content.Shared.FixedPoint;
using Content.Shared.Storage;

namespace Content.Server.Chemistry.EntitySystems
{
    /// <summary>
    /// Contains all the server-side logic for ChemMasters.
    /// <seealso cref="ChemMasterComponent"/>
    /// </summary>
    public sealed partial class ChemMasterSystem : EntitySystem
    {
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

            ClickSound(chemMaster);
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
                var item = Spawn(PatchPrototypeId, Transform(container).Coordinates);
                _storageSystem.Insert(container, item, out _, user: user, storage);
                _labelSystem.Label(item, message.Label);

                _solutionContainerSystem.EnsureSolution(item, SharedChemMaster.PatchSolutionName, out var itemSolution); // Starlight
                _solutionContainerSystem.SetCapacity(itemSolution, message.Dosage); // Starlight
                _solutionContainerSystem.TryAddSolution(itemSolution, withdrawal.SplitSolution(message.Dosage)); // Starlight
            }

            UpdateUiState(chemMaster);
            ClickSound(chemMaster);
        }

        private void OnToggleValveMessage(Entity<ChemMasterComponent> chemMaster, ref ChemMasterToggleValveMessage message)
        {
            if (!TryComp<PlumbingOutletComponent>(chemMaster.Owner, out var plumbingOutlet))
                return;

            plumbingOutlet.Enabled = !plumbingOutlet.Enabled;
            Dirty(chemMaster.Owner, plumbingOutlet);
            UpdateUiState(chemMaster);
            ClickSound(chemMaster);
        }

        // TRIESTE SPECIFIC
        private void OnSetTransferAmountMessage(
            Entity<ChemMasterComponent> chemMaster,
            ref ChemMasterSetTransferAmountMessage message)
        {
            if (!Enum.IsDefined(typeof(ChemMasterReagentAmount), message.Amount))
                return;

            chemMaster.Comp.TransferAmount = message.Amount;
            UpdateUiState(chemMaster);
            ClickSound(chemMaster);
        }
    }
}
