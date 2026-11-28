using Content.Shared.Inventory.Events;
using Content.Shared.Clothing.Components;
using Content.Shared.Actions;
using Content.Shared.Maps;
using Content.Shared.Popups;
using Content.Shared.Physics;
using System.Linq;
using Robust.Shared.Prototypes;
using Content.Shared.Light.Components;
using Robust.Shared.Containers;
using Content.Shared.Mobs.Components;
using Content.Shared.Inventory;
using Content.Shared._Starlight.NullSpace.Components;
using Content.Shared._Starlight.Shadekin.Components;
using Content.Shared.Ghost;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Network;
using Content.Shared.Sprite;
using System.Numerics;
using Content.Shared.PAI;

namespace Content.Shared._Starlight.NullSpace.Systems;

public sealed partial class NullSpacePhaseSystem : EntitySystem
{
    [Dependency] private SharedActionsSystem _actionsSystem = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private SharedGhostSystem _ghost = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private TurfSystem _turf = default!;
    [Dependency] private InventorySystem _inventorySystem = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedScaleVisualsSystem _scaleVisuals = default!;

    private readonly EntProtoId _shadekinShadow = "ShadekinShadow";
    private readonly EntProtoId _shadekinPhaseInEffect = "ShadekinPhaseInEffect";
    private readonly EntProtoId _shadekinPhaseOutEffect = "ShadekinPhaseOutEffect";
    private readonly EntProtoId _nullPhaseAction = "NullPhaseAction";

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<NullPhaseComponent, MapInitEvent>(OnInit);
        SubscribeLocalEvent<NullPhaseComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<NullPhaseComponent, GotEquippedEvent>(OnEquipped);
        SubscribeLocalEvent<NullPhaseComponent, GotUnequippedEvent>(OnUnequipped);
        SubscribeLocalEvent<NullPhaseComponent, NullPhaseActionEvent>(OnPhaseAction);
    }

    private void OnInit(EntityUid uid, NullPhaseComponent component, MapInitEvent args)
        => Toggle(uid, component, true);

    public void OnShutdown(EntityUid uid, NullPhaseComponent component, ComponentShutdown args)
        => Toggle(uid, component, false);

    private void OnEquipped(EntityUid uid, NullPhaseComponent component, GotEquippedEvent args)
    {
        if (!TryComp<ClothingComponent>(uid, out var clothing)
            || !clothing.Slots.HasFlag(args.SlotFlags))
            return;

        EnsureComp<NullPhaseComponent>(args.EquipTarget);
        if (!component.PreventLightFlicker
            || !TryComp<ShadekinComponent>(args.EquipTarget, out var shadekin))
            return;
        component.OriginalFlickerFlagState = shadekin.DoLightFlicker;
        shadekin.DoLightFlicker = false;
    }

    private void OnUnequipped(EntityUid uid, NullPhaseComponent component, GotUnequippedEvent args)
    {
        RemComp<NullPhaseComponent>(args.EquipTarget);
        if (!component.PreventLightFlicker
            || !TryComp<ShadekinComponent>(args.EquipTarget, out var shadekin))
            return;
        shadekin.DoLightFlicker = component.OriginalFlickerFlagState;
    }

    private void OnPhaseAction(EntityUid uid, NullPhaseComponent component, NullPhaseActionEvent args)
    {
        if (CanPhase(uid))
            Phase(uid);

        args.Handled = true;
    }

    private void Toggle(EntityUid uid, NullPhaseComponent component, bool toggle)
    {
        if (toggle)
            _actionsSystem.AddAction(uid, ref component.PhaseAction, _nullPhaseAction, uid);
        else
            _actionsSystem.RemoveAction(uid, component.PhaseAction);
    }

    public bool CanPhase(EntityUid uid)
    {
        if (TryComp<NullSpaceComponent>(uid, out var nullspace))
        {
            var tileref = _turf.GetTileRef(Transform(uid).Coordinates);
            if (tileref != null
            && _physics.GetEntitiesIntersectingBody(uid, (int)CollisionGroup.Impassable).Count > 0)
            {
                _popup.PopupPredicted(Loc.GetString("revenant-in-solid"), uid, uid);
                return false;
            }
        }
        else
        {
            // No phaising if were in a container.
            if (_container.IsEntityInContainer(uid))
            {
                _popup.PopupPredicted(Loc.GetString("phase-fail-generic"), uid, uid);
                return false;
            }

            // No phaising if were blocked by a NullSpaceBlockerComponent entity.
            foreach (var entity in _lookup.GetEntitiesIntersecting(Transform(uid).Coordinates))
            {
                if (HasComp<NullSpaceBlockerComponent>(entity))
                {
                    _popup.PopupPredicted(Loc.GetString("phase-fail-generic"), uid, uid);
                    return false;
                }
            }

            // No phaising if were holding or have an entity with the MobStateComponent (including backpack)
            // pAI has MobState but should be fine to be carried in Phasing.
            if (TryComp<InventoryComponent>(uid, out var inventoryComponent) && _inventorySystem.TryGetSlots(uid, out var slots))
                foreach (var slot in slots)
                    if (_inventorySystem.TryGetSlotEntity(uid, slot.Name, out var slotEnt, inventoryComponent))
                    {
                        if (HasComp<MobStateComponent>(slotEnt) && !HasComp<PAIComponent>(slotEnt))
                        {
                            _popup.PopupPredicted(Loc.GetString("phase-fail-generic"), uid, uid);
                            return false;
                        }

                        if (TryComp<ContainerManagerComponent>(slotEnt, out var containercomp))
                            foreach (var container in containercomp.Containers.Values)
                                foreach (var contEnt in container.ContainedEntities)
                                    if (HasComp<MobStateComponent>(contEnt) && !HasComp<PAIComponent>(contEnt))
                                    {
                                        _popup.PopupPredicted(Loc.GetString("phase-fail-generic"), uid, uid);
                                        return false;
                                    }
                    }
        }

        return true;
    }

    public void Phase(EntityUid uid)
    {
        if (TryComp<NullSpaceComponent>(uid, out var nullspace))
        {
            if (TryComp<ShadekinComponent>(uid, out var shadekin))
            {
                if (shadekin.DoLightFlicker)
                {
                    var lightQuery = _lookup.GetEntitiesInRange(uid, 5, flags: LookupFlags.StaticSundries)
                        .Where(x => HasComp<PoweredLightComponent>(x));
                    foreach (var light in lightQuery)
                        _ghost.DoGhostBooEvent(light);
                }

                SetPhaseEffect(uid, _shadekinPhaseInEffect);
            }
            else
                SetPhaseEffect(uid, _shadekinShadow);

            RemComp(uid, nullspace);
        }
        else
        {
            EnsureComp<NullSpaceComponent>(uid);

            if (TryComp<ShadekinComponent>(uid, out var shadekin))
            {
                if (shadekin.DoLightFlicker)
                {
                    var lightQuery = _lookup.GetEntitiesInRange(uid, 5, flags: LookupFlags.StaticSundries)
                        .Where(x => HasComp<PoweredLightComponent>(x));
                    foreach (var light in lightQuery)
                        _ghost.DoGhostBooEvent(light);
                }

                SetPhaseEffect(uid, _shadekinPhaseOutEffect);
            }
            else
                SetPhaseEffect(uid, _shadekinShadow);
        }
    }

    private void SetPhaseEffect(EntityUid uid, EntProtoId effectproto)
    {
        var effect = PredictedSpawnAtPosition(effectproto, Transform(uid).Coordinates);

        _scaleVisuals.SetSpriteScale(effect, _scaleVisuals.GetSpriteScale(uid));
        _transform.SetLocalRotation(effect, Transform(uid).LocalRotation);
    }
}
