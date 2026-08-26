using System.Linq;
using Content.Server._Starlight.Cargo.Components;
using Content.Server._Starlight.Cargo.TamperSeal.Components;
using Content.Server.Cargo.Systems;
using Content.Server.Materials;
using Content.Server.Popups;
using Content.Server.Stack;
using Content.Server.Station.Systems;
using Content.Server.Storage.EntitySystems;
using Content.Shared._Starlight.Cargo.MaterialDispenser;
using Content.Shared._Starlight.Cargo.TamperSeal.Components;
using Content.Shared.Cargo.Prototypes;
using Content.Shared.Containers;
using Content.Shared.EntityTable;
using Content.Shared.EntityTable.EntitySelectors;
using Content.Shared.Materials;
using Content.Shared.Popups;
using Content.Shared.Storage.Components;
using Content.Shared.Tools;
using JetBrains.Annotations;
using Robust.Server.Containers;
using Robust.Server.GameObjects;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Server._Starlight.Cargo.MaterialDispenser;

/// <summary>
/// This handles the MaterialDispenserComponent and its interactions with Lathe's
/// </summary>
[UsedImplicitly]
public sealed partial class MaterialDispenserSystem : EntitySystem
{
    [Dependency] private PopupSystem _popupSystem = default!;
    [Dependency] private MaterialStorageSystem _materialStorageSystem = default!;
    [Dependency] private EntityStorageSystem _storageSystem = default!;
    [Dependency] private UserInterfaceSystem _userInterfaceSystem = default!;
    [Dependency] private IPrototypeManager _prototypeManager = default!;
    [Dependency] private TransformSystem _transformSystem = default!;
    [Dependency] private PricingSystem _pricingSystem = default!;
    [Dependency] private StationSystem _stationSystem = default!;


    /// <inheritdoc/>
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<MaterialDispenserComponent, ComponentStartup>(SubscribeUpdateUiState);
        SubscribeLocalEvent<MaterialDispenserComponent, BoundUIOpenedEvent>(SubscribeUpdateUiState);
        SubscribeLocalEvent<MaterialDispenserComponent, MaterialEntityInsertedEvent>(SubscribeUpdateUiState);
        SubscribeLocalEvent<MaterialDispenserComponent, EntRemovedFromContainerMessage>(SubscribeUpdateUiState);

        SubscribeLocalEvent<MaterialDispenserComponent, MaterialDispenserAmountButton>(OnAmountButtonMessage);
        SubscribeLocalEvent<MaterialDispenserComponent, MaterialDispenserDepartmentSelected>(OnDepartmentSelectMessage);
        SubscribeLocalEvent<MaterialDispenserComponent, MaterialDispenserModeChange>(OnModeChange);
        SubscribeLocalEvent<MaterialDispenserComponent, MaterialDispenserEjectCrate>(OnCrateEjectMessage);
    }

    private void OnCrateEjectMessage(Entity<MaterialDispenserComponent> ent, ref MaterialDispenserEjectCrate args)
    {
        var materialProto = _prototypeManager.Index(ent.Comp.CrateMaterial);
        var crateMaterial = materialProto.ID;
        var crateMaterialAmount = ent.Comp.CrateMaterialAmount;
        var sheetVolume = _materialStorageSystem.GetSheetVolume(materialProto);
        var stationId = _stationSystem.GetOwningStation(ent.Owner);
        if (stationId == null)
        {
            return;
        }

        if (_materialStorageSystem.GetMaterialAmount(ent, crateMaterial) < crateMaterialAmount * sheetVolume)
        {
            _popupSystem.PopupCursor(Loc.GetString("material-dispenser-insufficient-materials",[("amount", ent.Comp.CrateMaterialAmount), ("material", ent.Comp.CrateMaterial.Id)] ), args.Actor, PopupType.MediumCaution);

            return;
        }

        _materialStorageSystem.TryChangeMaterialAmount(ent, crateMaterial, -crateMaterialAmount * sheetVolume);

        var item = Spawn(ent.Comp.CrateId, new EntityCoordinates(ent.Owner, 0, 0));
        foreach (var spawnedMat in ent.Comp.Buffer.Select(material => _materialStorageSystem.SpawnMultipleFromMaterial(material.Value, material.Key, Transform(item).Coordinates)).SelectMany(spawnedMats => spawnedMats)) _storageSystem.Insert(spawnedMat, item);

        ent.Comp.Buffer.Clear();

        _transformSystem.Unanchor(item);
        if (TryComp<TamperSealableComponent>(item, out var tamperSealable))
        {
            var recipient = _prototypeManager.Index(ent.Comp.Account);
            var seal = EnsureComp<TamperSealComponent>(item);
            seal.Recipient = ent.Comp.Account;
            seal.RecipientName = recipient.TamperSealName;
            seal.RecipientExamineColor = recipient.Color;
            seal.Color = recipient.TamperSealColor;
            seal.Accesses = new List<TamperSealAccessPattern>(recipient.TamperSealAccesses);
            seal.DestroyToolQualities = new HashSet<ProtoId<ToolQualityPrototype>>(tamperSealable.DestroyToolQualities);
            var value = EnsureComp<TamperSealValueComponent>(item);
            value.StationId = (EntityUid)stationId;
            var price = _pricingSystem.GetPrice(item);
            value.Value = (int)price;
            value.Reward = (int)Math.Floor(ent.Comp.RewardMultiplier * price); // Rewards rounded down.
            value.Penalty = 0; // Penalties dont apply since miners provided materials.
            value.Refund = 0; // Refunds dont apply since miners provided materials.
        }

        DirtyEntity(item);

        UpdateUiState(ent);
    }

    private void OnModeChange(Entity<MaterialDispenserComponent> ent, ref MaterialDispenserModeChange args)
    {
        ent.Comp.Mode = args.Mode;
        UpdateUiState(ent);
    }

    private void OnAmountButtonMessage(Entity<MaterialDispenserComponent> ent, ref MaterialDispenserAmountButton args)
    {
        if (args.Amount <= 0 || !_prototypeManager.TryIndex<MaterialPrototype>(args.Material, out var materialProto))
            return;
        var sheetVolume = _materialStorageSystem.GetSheetVolume(materialProto);
        var amount = args.Amount * sheetVolume;
        // Transfer from the materialstorage component container to the buffer container kept inside the component or the other way around
        if (ent.Comp.Mode == MaterialDispenserMode.Transfer)
        {
            if (!args.FromBuffer)
            {
                if (ent.Comp.Buffer.ContainsKey(args.Material) && ent.Comp.Buffer[args.Material] >= amount)
                {
                    if (!_materialStorageSystem.TryChangeMaterialAmount(ent, args.Material, amount)) return;
                    ent.Comp.Buffer[args.Material] -= amount;

                    if (ent.Comp.Buffer[args.Material] <= 0) ent.Comp.Buffer.Remove(args.Material);
                }
            }
            else
            {
                if (!_materialStorageSystem.TryChangeMaterialAmount(ent, args.Material, -amount)) return;
                if (!ent.Comp.Buffer.ContainsKey(args.Material)) ent.Comp.Buffer.Add(args.Material, 0);
                ent.Comp.Buffer[args.Material] += amount;
            }
        }
        // Remove from buffer or storage and eject onto the floor
        else if (ent.Comp.Mode == MaterialDispenserMode.Eject)
        {
            if (!args.FromBuffer)
            {
                if (ent.Comp.Buffer.ContainsKey(args.Material) && ent.Comp.Buffer[args.Material] >= amount)
                {
                    ent.Comp.Buffer[args.Material] -= amount;
                    if (!_materialStorageSystem.TryChangeMaterialAmount(ent, args.Material, amount)) return;

                    if (ent.Comp.Buffer[args.Material] <= 0) ent.Comp.Buffer.Remove(args.Material);
                }
            }

            _materialStorageSystem.EjectMaterial(ent.Owner, args.Material, amount);
        }

        UpdateUiState(ent);
    }

    private void OnDepartmentSelectMessage(Entity<MaterialDispenserComponent> ent,
        ref MaterialDispenserDepartmentSelected args)
    {
        if (!_prototypeManager.TryIndex<CargoAccountPrototype>(args.Department, out _))
            return;
        ent.Comp.Account = args.Department;
        UpdateUiState(ent);
    }

    private void SubscribeUpdateUiState<T>(Entity<MaterialDispenserComponent> ent, ref T ev) 
        => UpdateUiState(ent);

    private void UpdateUiState(Entity<MaterialDispenserComponent> ent)
    {
        var (owner, materialDispenser) = ent;
        var state = new MaterialDispenserBoundUserInterfaceState(materialDispenser.Mode, materialDispenser.Account.Id,
            materialDispenser.Buffer);
        _userInterfaceSystem.SetUiState(owner, MaterialDispenserUiKey.Key, state);
    }
}
