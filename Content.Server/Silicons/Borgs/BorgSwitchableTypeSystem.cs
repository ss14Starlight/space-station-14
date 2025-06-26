using System.Linq; //Starlight
using Content.Server.Inventory;
using Content.Server.Radio.Components;
using Content.Server.Silicons.Laws;
using Content.Shared.Coordinates; //Starlight
using Content.Shared.Inventory;
using Content.Shared.NameModifier.Components; //Starlight
using Content.Shared.PowerCell.Components; //Starlight
using Content.Shared.Silicons.Borgs;
using Content.Shared.Silicons.Borgs.Components;
using Content.Shared.Silicons.Laws;
using Content.Shared.Silicons.Laws.Components;
using Robust.Server.Containers; //Starlight
using Robust.Shared.Containers; //Starlight
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Server.Silicons.Borgs;

/// <summary>
/// Server-side logic for borg type switching. Handles more heavyweight and server-specific switching logic.
/// </summary>
public sealed class BorgSwitchableTypeSystem : SharedBorgSwitchableTypeSystem
{
    [Dependency] private readonly BorgSystem _borgSystem = default!;
    [Dependency] private readonly ServerInventorySystem _inventorySystem = default!;
    //#region Starlight
    [Dependency] private readonly ContainerSystem _containerSystem = default!;
    [Dependency] private readonly SiliconLawSystem _siliconLawSystem = default!;
    [Dependency] private readonly MetaDataSystem _metaDataSystem = default!;
    //#endregion Starlight
    
    protected override void SelectBorgModule(Entity<BorgSwitchableTypeComponent> ent, ProtoId<BorgTypePrototype> borgType)
    {
        var prototype = Prototypes.Index(borgType);

        //#region Starlight
        if (prototype.Transformation is not null)
        {


            if (!TryComp(ent.Owner, out BorgChassisComponent? borgChassis))
            {
                Log.Warning($"Borg {ent} did not have a borg chassis component? Aborting transformation into {borgType.Id}");
                return;
            }
            if (!TryComp(ent.Owner, out PowerCellSlotComponent? powerCellSlot))
            {
                Log.Warning($"Borg {ent} did not have a power cell slot component? Aborting transformation into {borgType.Id}");
                return;
            }

            var newChasis = SpawnAtPosition(prototype.Transformation, ent.Owner.ToCoordinates());

            var chassisChecks = true;
            if (!TryComp(newChasis, out BorgChassisComponent? newBorgChassis))
            {
                Log.Warning($"Borg prototype {prototype.Transformation} did not have a borg chassis component? Aborting transformation into {borgType.Id}");
                chassisChecks = false;
            }
            if (!TryComp(ent.Owner, out PowerCellSlotComponent? newPowerCellSlot))
            {
                Log.Warning($"Borg prototype {prototype.Transformation} did not have a power cell slot component? Aborting transformation into {borgType.Id}");
                chassisChecks = false;
            }
            if (!TryComp(ent.Owner, out SiliconLawProviderComponent? newSiliconLawProvider))
            {
                Log.Warning($"Borg prototype {prototype.Transformation} did not have a silicon law provider component? Aborting transformation into {borgType.Id}");
                chassisChecks = false;
            }
            if (!chassisChecks)
            {
                Del(newChasis);
                return;
            }

            if (borgChassis == null || newBorgChassis == null || powerCellSlot == null || newPowerCellSlot == null || newSiliconLawProvider == null)
            {
                Log.Error($"required comps were found but returned null. this is a engine bug as they should not be null if previous checks passed.");
                return;
            }

            TryComp<NameModifierComponent>(ent.Owner, out var oldNameMod);
            var oldMeta = MetaData(ent.Owner);
            var newMeta = MetaData(newChasis);
            _metaDataSystem.SetEntityName(newChasis, oldNameMod?.BaseName ?? oldMeta.EntityName, newMeta);
            
            TryTransferContainerContents(ent.Owner, newChasis, borgChassis.BrainContainerId, newBorgChassis.BrainContainer);
            //why do I manually get the container? cause for some reason the power cell is NOT on the PowerCellSlotComponent. cause WHY would it...
            TryTransferContainerContents(ent.Owner, newChasis, powerCellSlot.CellSlotId, _containerSystem.GetContainer(newChasis, newPowerCellSlot.CellSlotId));
            //if I pray to god hard enough un-selected borgs wont be able to have modules inserted early at any point in the future.

            var ev = new GetSiliconLawsEvent(ent.Owner);
            RaiseLocalEvent(ent.Owner, ref ev);
            _siliconLawSystem.SetLaws(ev.Laws.Laws, newChasis, null);

            Del(ent.Owner);
            return;
        }
        //#endregion

        // Assign radio channels
        string[] radioChannels = [.. ent.Comp.InherentRadioChannels, .. prototype.RadioChannels];
        if (TryComp(ent, out IntrinsicRadioTransmitterComponent? transmitter))
            transmitter.Channels = [.. radioChannels];

        if (TryComp(ent, out ActiveRadioComponent? activeRadio))
            activeRadio.Channels = [.. radioChannels];

        // Borg transponder for the robotics console
        if (TryComp(ent, out BorgTransponderComponent? transponder))
        {
            _borgSystem.SetTransponderSprite(
                (ent.Owner, transponder),
                new SpriteSpecifier.Rsi(new ResPath("Mobs/Silicon/chassis.rsi"), prototype.SpriteBodyState));

            _borgSystem.SetTransponderName(
                (ent.Owner, transponder),
                Loc.GetString($"borg-type-{borgType}-transponder"));
        }

        // Configure modules
        if (TryComp(ent, out BorgChassisComponent? chassis))
        {
            var chassisEnt = (ent.Owner, chassis);
            _borgSystem.SetMaxModules(
                chassisEnt,
                prototype.ExtraModuleCount + prototype.DefaultModules.Length);

            _borgSystem.SetModuleWhitelist(chassisEnt, prototype.ModuleWhitelist);

            foreach (var module in prototype.DefaultModules)
            {
                var moduleEntity = Spawn(module);
                var borgModule = Comp<BorgModuleComponent>(moduleEntity);
                _borgSystem.SetBorgModuleDefault((moduleEntity, borgModule), true);
                _borgSystem.InsertModule(chassisEnt, moduleEntity);
            }
        }

        // Configure special components
        if (Prototypes.TryIndex(ent.Comp.SelectedBorgType, out var previousPrototype))
        {
            if (previousPrototype.AddComponents is { } removeComponents)
                EntityManager.RemoveComponents(ent, removeComponents);
        }

        if (prototype.AddComponents is { } addComponents)
        {
            EntityManager.AddComponents(ent, addComponents);
        }

        // Configure inventory template (used for hat spacing)
        if (TryComp(ent, out InventoryComponent? inventory))
        {
            _inventorySystem.SetTemplateId((ent.Owner, inventory), prototype.InventoryTemplateId);
        }

        base.SelectBorgModule(ent, borgType);
    }

    //#region starlight
    //copied almost verbatim from BuildMech.cs
    private void TryTransferContainerContents(EntityUid from, EntityUid to, string sourceContainer,
        BaseContainer destContainer)
    {
            if (!TryComp(from, out ContainerManagerComponent? containerManager))
            {
                Logger.Warning($"Borg entity {from} did not have a container manager! Aborting transformation");
                return;
            }
            
            if (!_containerSystem.TryGetContainer(from, sourceContainer, out var originalContainer, containerManager))
            {
                return;
            }

            List<EntityUid> entitiesToTransfer = originalContainer.ContainedEntities.ToList(); //we need to copy the list, as we are modifying the original container.

            foreach (var entity in entitiesToTransfer)
            {
                if (_containerSystem.TryRemoveFromContainer(entity, true, out bool wasInContainer))
                {
                    //all other items except the last that we process will just end up on the ground
                    _containerSystem.Insert(entity, destContainer);
                }
            }
    }
    //#endregion
}
