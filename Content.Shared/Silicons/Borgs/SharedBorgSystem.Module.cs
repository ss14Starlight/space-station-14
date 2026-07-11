using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Shared.Examine;
using Content.Shared.Hands.Components;
using Content.Shared.Interaction.Components;
using Content.Shared.Localizations;
using Content.Shared.Silicons.Borgs.Components;
using Content.Shared.Storage; // Starlight-edit
using Content.Shared.Storage.Components; // Starlight-edit
using Robust.Shared.Containers;
#region Starlight
using Content.Shared.IdentityManagement;
using Content.Shared.Interaction;
using Content.Shared.Stacks;
using Content.Shared.Tools.Components;
using Content.Shared.Tools.Systems;
using Content.Shared._Starlight.Silicons;
#endregion Starlight

namespace Content.Shared.Silicons.Borgs;

public abstract partial class SharedBorgSystem
{
    private EntityQuery<BorgModuleComponent> _moduleQuery;
    [Dependency] private SharedToolSystem _tool = default!; //Starlight
    [Dependency] private SharedStackSystem _stacks = default!; // Starlight

    public void InitializeModule()
    {
        SubscribeLocalEvent<BorgModuleComponent, ExaminedEvent>(OnModuleExamine);
        SubscribeLocalEvent<BorgModuleWhitelistComponent, ExaminedEvent>(OnWhitelistExamine);
        SubscribeLocalEvent<BorgModuleComponent, EntGotInsertedIntoContainerMessage>(OnModuleGotInserted);
        SubscribeLocalEvent<BorgModuleComponent, EntGotRemovedFromContainerMessage>(OnModuleGotRemoved);

        SubscribeLocalEvent<SelectableBorgModuleComponent, BorgModuleInstalledEvent>(OnSelectableInstalled);
        SubscribeLocalEvent<SelectableBorgModuleComponent, BorgModuleUninstalledEvent>(OnSelectableUninstalled);
        SubscribeLocalEvent<SelectableBorgModuleComponent, BorgModuleActionSelectedEvent>(OnSelectableAction);

        SubscribeLocalEvent<ItemBorgModuleComponent, ComponentStartup>(OnProvideItemStartup);
        SubscribeLocalEvent<ItemBorgModuleComponent, BorgModuleSelectedEvent>(OnItemModuleSelected);
        SubscribeLocalEvent<ItemBorgModuleComponent, BorgModuleUnselectedEvent>(OnItemModuleUnselected);
        // Starlight begin
        SubscribeLocalEvent<ItemBorgModuleComponent, ExaminedEvent>(OnItemModuleExamine);
        SubscribeLocalEvent<ItemBorgModuleComponent, AfterInteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<ItemBorgModuleComponent, BorgModuleItemExtractionDoAfterEvent>(OnItemExtractionFinished);
        // Starlight end

        SubscribeLocalEvent<ComponentBorgModuleComponent, BorgModuleInstalledEvent>(OnComponentModuleInstalled);
        SubscribeLocalEvent<ComponentBorgModuleComponent, BorgModuleUninstalledEvent>(OnComponentModuleUninstalled);

        SubscribeLocalEvent<ComponentBorgModuleComponent, BorgModuleRelayedEvent<BorgModuleInsertAttemptEvent>>(
            OnComponentModuleInstalledRelay);


        SubscribeLocalEvent<BorgModuleWhitelistComponent, BorgModuleInsertAttemptEvent>(OnCheckWhitelist);
        SubscribeLocalEvent<BorgModuleWhitelistComponent, BorgModuleRelayedEvent<BorgModuleInsertAttemptEvent>>(
            OnCheckBlacklistRelay);


        _moduleQuery = GetEntityQuery<BorgModuleComponent>();
    }

    #region BorgModule
    private void OnModuleExamine(Entity<BorgModuleComponent> ent, ref ExaminedEvent args)
    {
        using (args.PushGroup(nameof(BorgModuleComponent)))
        {
            if (TryFormatList(ent.Comp.BorgFitTypes, "borg-module-fit", "types", out var list))
                args.PushMarkup(list);
        }
    }

    private void OnWhitelistExamine(Entity<BorgModuleWhitelistComponent> ent, ref ExaminedEvent args)
    {
        using (args.PushGroup(nameof(BorgModuleComponent), 1))
        {
            args.PushMarkup(Loc.GetString(ent.Comp.WhitelistInfo));
        }
    }

    private bool TryFormatList(List<LocId>? list, string messageId, string listId, [NotNullWhen(true)] out string? formattedList)
    {
        formattedList = null;

        if (list == null || list.Count == 0)
            return false;

        var entries = ContentLocalizationManager.FormatList([.. list.Select(s => Loc.GetString(s))]);

        formattedList = Loc.GetString(messageId, (listId, entries));
        return true;
    }

    private void OnModuleGotInserted(Entity<BorgModuleComponent> module, ref EntGotInsertedIntoContainerMessage args)
    {
        if (_timing.ApplyingState)
            return; // The changes are already networked with the same game state

        var chassis = args.Container.Owner;

        if (!TryComp<BorgChassisComponent>(chassis, out var chassisComp) ||
            args.Container != chassisComp.ModuleContainer ||
            !chassisComp.Active)
            return;

        InstallModule((chassis, chassisComp), module.AsNullable());
    }

    private void OnModuleGotRemoved(Entity<BorgModuleComponent> module, ref EntGotRemovedFromContainerMessage args)
    {
        if (_timing.ApplyingState)
            return; // The changes are already networked with the same game state

        var chassis = args.Container.Owner;

        if (!TryComp<BorgChassisComponent>(chassis, out var chassisComp) ||
            args.Container != chassisComp.ModuleContainer)
            return;

        UninstallModule((chassis, chassisComp), module.AsNullable());
    }
     #endregion

    #region ItemBorgModule
    private void OnSelectableInstalled(Entity<SelectableBorgModuleComponent> module, ref BorgModuleInstalledEvent args)
    {
        var chassis = args.ChassisEnt;

        if (_actions.AddAction(chassis, ref module.Comp.ModuleSwapActionEntity, out var action, module.Comp.ModuleSwapAction, module.Owner))
        {
            Dirty(module); // for ModuleSwapActionEntity after the action has been spawned
            var actEnt = (module.Comp.ModuleSwapActionEntity.Value, action);
            _actions.SetEntityIcon(actEnt, module.Owner);
            if (TryComp<BorgModuleIconComponent>(module, out var moduleIconComp))
                _actions.SetIcon(actEnt, moduleIconComp.Icon);

            /// Set a custom name and description on the action. The borg module action prototypes are shared across
            /// all modules. Extract localized names, then populate variables with the info from the module itself.
            var moduleName = Name(module);
            var actionMetaData = MetaData(module.Comp.ModuleSwapActionEntity.Value);

            var instanceName = Loc.GetString("borg-module-action-name", ("moduleName", moduleName));
            _metaData.SetEntityName(module.Comp.ModuleSwapActionEntity.Value, instanceName, actionMetaData);
            var instanceDesc = Loc.GetString("borg-module-action-description", ("moduleName", moduleName));
            _metaData.SetEntityDescription(module.Comp.ModuleSwapActionEntity.Value, instanceDesc, actionMetaData);
        }

        if (!TryComp<BorgChassisComponent>(chassis, out var chassisComp))
            return;

        if (chassisComp.SelectedModule == null)
            SelectModule((chassis, chassisComp), module.Owner);
    }

    private void OnSelectableUninstalled(Entity<SelectableBorgModuleComponent> module, ref BorgModuleUninstalledEvent args)
    {
        var chassis = args.ChassisEnt;
        _actions.RemoveProvidedActions(chassis, module.Owner);
        if (!TryComp<BorgChassisComponent>(chassis, out var chassisComp))
            return;

        if (chassisComp.SelectedModule == module.Owner)
            UnselectModule((chassis, chassisComp));
    }

    private void OnSelectableAction(Entity<SelectableBorgModuleComponent> module, ref BorgModuleActionSelectedEvent args)
    {
        var chassis = args.Performer;
        if (!TryComp<BorgChassisComponent>(chassis, out var chassisComp))
            return;

        var selected = chassisComp.SelectedModule;

        args.Handled = true;
        UnselectModule((chassis, chassisComp));

        if (selected != module.Owner)
        {
            SelectModule((chassis, chassisComp), module.Owner);
        }
    }

    private void OnProvideItemStartup(Entity<ItemBorgModuleComponent> module, ref ComponentStartup args)
    {
        _container.EnsureContainer<Container>(module.Owner, module.Comp.HoldingContainer);
    }

    private void OnItemModuleSelected(Entity<ItemBorgModuleComponent> module, ref BorgModuleSelectedEvent args)
    {
        ProvideItems(args.Chassis, module.AsNullable());
    }

    private void OnItemModuleUnselected(Entity<ItemBorgModuleComponent> module, ref BorgModuleUnselectedEvent args)
    {
        RemoveProvidedItems(args.Chassis, module.AsNullable());
    }

    private void ProvideItems(Entity<BorgChassisComponent?> chassis, Entity<ItemBorgModuleComponent?> module)
    {
        if (!Resolve(chassis, ref chassis.Comp) || !Resolve(module, ref module.Comp))
            return;

        if (!TryComp<HandsComponent>(chassis, out var hands))
            return;

        if (!_container.TryGetContainer(module, module.Comp.HoldingContainer, out var container))
            return;

        var xform = Transform(chassis);

        for (var i = 0; i < module.Comp.Hands.Count; i++)
        {
            var hand = module.Comp.Hands[i];
            var handId = $"{GetNetEntity(module.Owner)}-hand-{i}";

            _hands.AddHand((chassis.Owner, hands), handId, hand.Hand);
            EntityUid? item = null;

            if (module.Comp.Spawned)
            {
                if (module.Comp.StoredItems.TryGetValue(handId, out var storedItem))
                {
                    // Starlight edit start - This is required because of the Personnel uppies module, since we allow people to escape from it,
                    // We need a way to check whether the person is still in the container.
                    if (Exists(storedItem) && container.Contains(storedItem))
                    {
                        item = storedItem;
                    }
                    else
                    {
                        module.Comp.StoredItems.Remove(handId);
                        // Starlight: re-spawn item that was consumed externally (e.g. holocuffs applied to a target)
                        if (hand.Item is { } respawnProto)
                            item = PredictedSpawnAtPosition(respawnProto, xform.Coordinates);
                    }
                    // Starlight edit end
                }
            }
            else if (hand.Item is { } itemProto)
            {
                item = PredictedSpawnAtPosition(itemProto, xform.Coordinates);
            }

            if (item is { } pickUp)
            {
                _hands.DoPickup(chassis, handId, pickUp, hands);

                if (!hand.ForceRemovable && hand.Hand.Whitelist == null && hand.Hand.Blacklist == null)
                {
                    _tag.AddTag(pickUp, module.Comp.ModuleItemTag); // Starlight
                    EnsureComp<UnremoveableComponent>(pickUp);
                }

                // Starlight: stamp borg-owner info onto borg handcuffs so they can return home
                if (TryComp<BorgHandcuffComponent>(pickUp, out var borgCuff))
                {
                    borgCuff.OwnerChassis = chassis.Owner;
                    borgCuff.HandId = handId;
                    Dirty(pickUp, borgCuff);
                    EnsureComp<UnremoveableComponent>(pickUp); // always undroppable
                }
            }
        }

        module.Comp.Spawned = true;
        Dirty(module);
    }

    private void RemoveProvidedItems(Entity<BorgChassisComponent?> chassis, Entity<ItemBorgModuleComponent?> module)
    {
        if (!Resolve(chassis, ref chassis.Comp) || !Resolve(module, ref module.Comp))
            return;

        if (!TryComp<HandsComponent>(chassis, out var hands))
            return;

        if (!_container.TryGetContainer(module, module.Comp.HoldingContainer, out var container))
            return;

        if (TerminatingOrDeleted(module))
            return;

        for (var i = 0; i < module.Comp.Hands.Count; i++)
        {
            var handId = $"{GetNetEntity(module.Owner)}-hand-{i}";

            if (_hands.TryGetHeldItem((chassis.Owner, hands), handId, out var held))
            {
                RemComp<UnremoveableComponent>(held.Value);
                _container.Insert(held.Value, container);
                module.Comp.StoredItems[handId] = held.Value;
            }
            else
            {
                module.Comp.StoredItems.Remove(handId);
            }

            _hands.RemoveHand((chassis.Owner, hands), handId);
        }

        Dirty(module);
    }
    #endregion
    #region ComponentBorgModule
    private void OnComponentModuleInstalled(Entity<ComponentBorgModuleComponent> ent, ref BorgModuleInstalledEvent args)
    {
        var chassis = args.ChassisEnt;
        EntityManager.AddComponents(chassis, ent.Comp.Components);
    }

    private void OnComponentModuleUninstalled(Entity<ComponentBorgModuleComponent> ent,
        ref BorgModuleUninstalledEvent args)
    {
        var chassis = args.ChassisEnt;
        EntityManager.RemoveComponents(chassis, ent.Comp.Components);
    }

    private void OnComponentModuleInstalledRelay(Entity<ComponentBorgModuleComponent> ent,
        ref BorgModuleRelayedEvent<BorgModuleInsertAttemptEvent> args)
    {
        if (args.Args.Cancelled ||
            !TryComp<ComponentBorgModuleComponent>(args.Args.ModuleEnt, out var newModule))
            return;

        foreach (var comp in newModule.Components)
        {
            if (ent.Comp.Components.TryGetComponent(comp.Key, out _))
            {
                args.Args.Cancelled = true;
                args.Args.Reason = Loc.GetString("borg-module-incompatible", ("existing", ent));
            }
        }
    }
    #endregion

    #region ModuleWhitelist

    private void OnCheckWhitelist(Entity<BorgModuleWhitelistComponent> ent, ref BorgModuleInsertAttemptEvent args)
    {
        if (args.Cancelled || !TryComp<BorgChassisComponent>(args.ChassisEnt, out var chassis))
            return;

        //loop over all other contained modules to see if any conflict with this module's blacklist
        //while simultaneously checking if any module fits its prerequisite criteria
        var prerequisiteFulfilled = ent.Comp.ModuleWhitelist == null; // Starlight. prerequisiteFulfilled starts false, so modules that only use moduleBlacklist can be rejected when the chassis has no modules. This fixes that.
        foreach (var containedModuleUid in chassis.ModuleContainer.ContainedEntities)
        {
            if (_whitelist.IsWhitelistPass(ent.Comp.ModuleBlacklist, containedModuleUid))
            {
                args.Reason = Loc.GetString("borg-module-incompatible", ("existing", containedModuleUid));
                args.Cancelled = true;
                return;
            }
            if (!prerequisiteFulfilled && _whitelist.IsWhitelistPassOrNull(ent.Comp.ModuleWhitelist, containedModuleUid))
                prerequisiteFulfilled = true;
        }
        if (!prerequisiteFulfilled)
        {
            args.Reason = Loc.GetString("borg-module-prerequisite-unfulfilled");
            args.Cancelled = true;
        }
    }

    private void OnCheckBlacklistRelay(Entity<BorgModuleWhitelistComponent> ent, ref BorgModuleRelayedEvent<BorgModuleInsertAttemptEvent> args)
    {
        if (args.Args.Cancelled)
            return;

        if (_whitelist.IsWhitelistPass(ent.Comp.ModuleBlacklist, args.Args.ModuleEnt))
        {
            args.Args.Cancelled = true;
            args.Args.Reason = Loc.GetString("borg-module-incompatible", ("existing", ent));
        }
    }

    //TODO: Replace this with a relayed event based system once there's a QueueRemove
    //or something similar implemented that defers entity removal from containers to the following tick
    //this cannot be implemented as a relayed event because the act of removing a module
    //from a chassis modifies the relay's foreach loop collection to be modified, thus throwing an error

    /// This function removes all modules who are now invalidated by the removal of removedModule
    private void ValidateWhitelists(Entity<BorgChassisComponent> chassis, EntityUid removedModule)
    {
        var toRemove = new List<EntityUid>();
        foreach (var containedModuleUid in chassis.Comp.ModuleContainer.ContainedEntities)
        {
            if (containedModuleUid == removedModule ||
                !TryComp<BorgModuleWhitelistComponent>(containedModuleUid, out var whitelist) ||
                whitelist.ModuleWhitelist == null)
                continue;

            var keep = false;

            foreach (var checkAgainstModuleUid in chassis.Comp.ModuleContainer.ContainedEntities)
            {
                if (checkAgainstModuleUid == containedModuleUid ||
                    checkAgainstModuleUid == removedModule)
                    continue;

                if (_whitelist.IsWhitelistPass(whitelist.ModuleWhitelist, checkAgainstModuleUid))
                {
                    keep = true;
                    break;
                }
            }
            if (!keep)
                toRemove.Add(containedModuleUid);
        }

        foreach (var moduleUid in toRemove)
        {
            _container.Remove(moduleUid, chassis.Comp.ModuleContainer);
        }
    }
    #endregion

    #region Starlight
    private void OnItemModuleExamine(Entity<ItemBorgModuleComponent> ent, ref ExaminedEvent args)
    {
        var items = new List<EntityUid>();
        CollectExtractableItems(ent, items);
        if (items.Count == 0)
            return;

        var grouped = new Dictionary<string, (string Name, int Count)>();
        foreach (var item in items)
        {
            var name = Identity.Name(item, EntityManager);
            var count = _stacks.GetCount(item);

            string key;
            if (TryComp<StackComponent>(item, out var stack))
                key = $"stack:{stack.StackTypeId}";
            else if (TryComp<MetaDataComponent>(item, out var meta) && meta.EntityPrototype is { } proto)
                key = $"proto:{proto.ID}";
            else
                key = $"uid:{item}";
            if (grouped.TryGetValue(key, out var existing))
                grouped[key] = (existing.Name, existing.Count + count);
            else
                grouped[key] = (name, count);
        }

        var entries = grouped.Values
            .Select(g => Loc.GetString("borg-module-item-contents-entry",
                ("count", g.Count),
                ("item", g.Name)))
            .ToList();

        using (args.PushGroup(nameof(ItemBorgModuleComponent)))
        {
            args.PushMarkup(Loc.GetString("borg-module-item-contents",
                ("items", ContentLocalizationManager.FormatList(entries))));
        }
    }

    private void OnInteractUsing(Entity<ItemBorgModuleComponent> ent, ref AfterInteractUsingEvent args)
    {
        if (args.Handled)
            return;

        if (!TryComp<ToolComponent>(args.Used, out var tool)
            || !_tool.HasQuality(args.Used, ent.Comp.ItemExtractionMethod, tool))
            return;

        args.Handled = true;

        if (!args.CanReach)
        {
            if (_timing.IsFirstTimePredicted)
                _popup.PopupClient(Loc.GetString("interaction-system-user-interaction-cannot-reach"), args.User, args.User);
            return;
        }

        var extractable = new List<EntityUid>();
        CollectExtractableItems(ent, extractable);
        if (extractable.Count == 0)
        {
            _popup.PopupClient(Loc.GetString("borg-module-item-extract-empty", ("module", ent.Owner)), ent.Owner, args.User);
            return;
        }

        _tool.UseTool(args.Used,
            args.User,
            ent.Owner,
            ent.Comp.ItemExtractionDelay,
            ent.Comp.ItemExtractionMethod,
            new BorgModuleItemExtractionDoAfterEvent(),
            toolComponent: tool);
    }

    private void OnItemExtractionFinished(Entity<ItemBorgModuleComponent> ent, ref BorgModuleItemExtractionDoAfterEvent args)
    {
        if (args.Cancelled)
            return;

        if (!TryExtractItems(ent))
        {
            _popup.PopupClient(Loc.GetString("borg-module-item-extract-empty", ("module", ent.Owner)), ent.Owner, args.User);
            return;
        }

        _popup.PopupClient(Loc.GetString("borg-module-item-extract-success", ("module", ent.Owner)), ent.Owner, args.User);
        _audio.PlayPredicted(ent.Comp.ItemExtractionSound, ent.Owner, args.User);
    }

    private void CollectExtractableItems(Entity<ItemBorgModuleComponent> module, List<EntityUid> into)
    {
        ForEachModuleProvidedItem(module, (item, _) =>
        {
            var before = into.Count;
            CollectNestedContents(item, into);
            if (into.Count > before)
                return;
            if (!_tag.HasTag(item, module.Comp.ModuleItemTag))
                into.Add(item);
        });
    }

    private bool TryExtractItems(Entity<ItemBorgModuleComponent> module)
    {
        var extracted = false;
        var dropCoords = Transform(module).Coordinates;
        ForEachModuleProvidedItem(module, (item, inChassisHand) =>
        {
            if (EmptyNestedStorage(item))
                extracted = true;

            if (_tag.HasTag(item, module.Comp.ModuleItemTag))
                return;
            if (inChassisHand)
            {
                if (!TryComp<BorgModuleComponent>(module, out var borgModule)
                    || borgModule.InstalledEntity is not { } chassis
                    || !TryComp<HandsComponent>(chassis, out var hands))
                    return;
                RemComp<UnremoveableComponent>(item);
                if (_hands.TryDrop((chassis, hands), item, checkActionBlocker: false))
                    extracted = true;
                return;
            }
            if (_container.TryGetContainingContainer(item, out var container)
                && _container.Remove(item, container, force: true, destination: dropCoords))
                extracted = true;
        });
        return extracted;
    }

    private void ForEachModuleProvidedItem(Entity<ItemBorgModuleComponent> module, Action<EntityUid, bool> action)
    {
        if (_container.TryGetContainer(module, module.Comp.HoldingContainer, out var container))
        {
            foreach (var item in container.ContainedEntities.ToArray())
                action(item, false);
        }
        if (!TryComp<BorgModuleComponent>(module, out var borgModule)
            || borgModule.InstalledEntity is not { } chassis
            || !TryComp<HandsComponent>(chassis, out var hands))
            return;
        for (var i = 0; i < module.Comp.Hands.Count; i++)
        {
            var handId = $"{GetNetEntity(module.Owner)}-hand-{i}";
            if (_hands.TryGetHeldItem((chassis, hands), handId, out var held))
                action(held.Value, true);
        }
    }

    private void CollectNestedContents(EntityUid item, List<EntityUid> into)
    {
        if (TryComp<EntityStorageComponent>(item, out var entityStorage))
            into.AddRange(entityStorage.Contents.ContainedEntities);
        if (TryComp<StorageComponent>(item, out var storage)
            && storage.Container != null)
            into.AddRange(storage.Container.ContainedEntities);
        if (!TryComp<ContainerManagerComponent>(item, out var manager))
            return;

        foreach (var nested in _container.GetAllContainers(item, manager))
        {
            if (nested is ContainerSlot)
                continue;

            if (TryComp<EntityStorageComponent>(item, out var es) && ReferenceEquals(nested, es.Contents))
                continue;
            if (TryComp<StorageComponent>(item, out var st) && st.Container != null && ReferenceEquals(nested, st.Container))
                continue;
            into.AddRange(nested.ContainedEntities);
        }
    }

    private bool EmptyNestedStorage(EntityUid item)
    {
        var emptied = false;
        var dest = Transform(item).Coordinates;
        if (TryComp<EntityStorageComponent>(item, out var entityStorage))
        {
            foreach (var ent in entityStorage.Contents.ContainedEntities.ToArray())
            {
                if (_container.Remove(ent, entityStorage.Contents, force: true, destination: dest))
                    emptied = true;
            }
        }
        if (TryComp<StorageComponent>(item, out var storage)
            && storage.Container != null
            && storage.Container.ContainedEntities.Count > 0)
        {
            _container.EmptyContainer(storage.Container, force: true, destination: dest);
            emptied = true;
        }
        if (!TryComp<ContainerManagerComponent>(item, out var manager))
            return emptied;
        foreach (var nested in _container.GetAllContainers(item, manager))
        {
            if (nested is ContainerSlot || nested.ContainedEntities.Count == 0)
                continue;
            if (TryComp<EntityStorageComponent>(item, out var es) && ReferenceEquals(nested, es.Contents))
                continue;
            if (TryComp<StorageComponent>(item, out var st) && st.Container != null && ReferenceEquals(nested, st.Container))
                continue;
            _container.EmptyContainer(nested, force: true, destination: dest);
            emptied = true;
        }
        return emptied;
    }
    #endregion Starlight
}
