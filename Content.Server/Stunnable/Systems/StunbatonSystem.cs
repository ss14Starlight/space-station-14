using Content.Server.Power.Components;
using Content.Server.Power.Events;
using Content.Shared.PowerCell;
using Content.Server.Power.EntitySystems;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Damage.Events;
using Content.Shared.Examine;
using Content.Shared.Item.ItemToggle;
using Content.Shared.Item.ItemToggle.Components;
using Content.Shared.Popups;
using Content.Shared.Power;
using Content.Shared.Power.Components;
using Content.Shared.Power.EntitySystems;
using Content.Shared.Stunnable;
using Content.Shared.CombatMode;
using Content.Shared.Interaction;
using Content.Shared.Tag;
using Robust.Shared.Containers;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Timing;
using Content.Shared.PowerCell.Components;
using Content.Shared._Starlight.Stunnable;

namespace Content.Server.Stunnable.Systems
{
    public sealed partial class StunbatonSystem : SharedStunbatonSystem
    {
        [Dependency] private RiggableSystem _riggableSystem = default!;
        [Dependency] private SharedPopupSystem _popup = default!;
        [Dependency] private SharedBatterySystem _battery = default!;
        [Dependency] private ItemToggleSystem _itemToggle = default!;
        #region Starlight
        [Dependency] private PowerCellSystem _powerCell = default!;
        [Dependency] private SharedCombatModeSystem _combatMode = default!;
        [Dependency] private IGameTiming _gameTiming = default!;
        [Dependency] private SharedAudioSystem _audio = default!;
        [Dependency] private TagSystem _tagSystem = default!;
        [Dependency] private SharedAppearanceSystem _appearance = default!;
        #endregion

        public override void Initialize()
        {
            base.Initialize();

            SubscribeLocalEvent<StunbatonComponent, AfterInteractEvent>(OnStunbatonAfterInteract); // Starlight-edit
            SubscribeLocalEvent<StunbatonComponent, ExaminedEvent>(OnExamined);
            SubscribeLocalEvent<StunbatonComponent, StaminaDamageOnHitAttemptEvent>(OnStaminaHitAttempt);
            SubscribeLocalEvent<StunbatonComponent, ChargeChangedEvent>(OnChargeChanged);
            SubscribeLocalEvent<StunbatonComponent, EntInsertedIntoContainerMessage>(OnCellSlotInserted); // Starlight-edit
            SubscribeLocalEvent<StunbatonComponent, EntRemovedFromContainerMessage>(OnCellSlotRemoved); // Starlight-edit
        }


        // 🌟Starlight🌟 start
        private void OnStunbatonAfterInteract(Entity<StunbatonComponent> entity, ref AfterInteractEvent args) // Handle special interaction when using stunbaton on a riot shield
        {
            // Only handle interaction if stunbaton is the used item
            if (args.Used != entity.Owner)
                return;

            if (args.Target == null || args.Target == entity.Owner) // Prevent interaction if no target or if user is clicking on themselves
                return;

            var target = args.Target.Value;
            // Check if target has the Shield tag
            if (!_tagSystem.HasTag(target, "Shield"))
                return;

            // Check if user is NOT in combat mode
            if (_combatMode.IsInCombatMode(args.User))
                return;

            // Check cooldown (3 second delay between interactions)
            if (_gameTiming.CurTime - entity.Comp.LastBashTime < entity.Comp.BashDelay)
                return;

            // Check if shield is held in one of the user's hands by comparing transforms
            // If the shield is held, its parent transform should be the user entity
            var shieldTransform = Transform(target);
            if (shieldTransform.ParentUid != args.User)
                return;

            // Update cooldown in component
            entity.Comp.LastBashTime = _gameTiming.CurTime;

            // Display message
            var userName = MetaData(args.User).EntityName;
            var emoteMessage = Loc.GetString(entity.Comp.ShieldBashMessage, ("entityName", userName));
            _popup.PopupEntity(emoteMessage, target, args.User);

            // Play sound effect for all players in vicinity
            _audio.PlayPvs(entity.Comp.ShieldBashSound, target);
        }
        // 🌟Starlight🌟 end

        private void OnStaminaHitAttempt(Entity<StunbatonComponent> entity, ref StaminaDamageOnHitAttemptEvent args)
        {
            // 🌟Starlight🌟 start
            // Stunbatons check for power cells if they have no BatteryComponent
            Entity<BatteryComponent>? batteryEntity = null;
            if (!_itemToggle.IsActivated(entity.Owner) ||
            !(TryComp(entity.Owner, out BatteryComponent? battery) ||
            _powerCell.TryGetBatteryFromSlot(entity.Owner, out batteryEntity)) ||
            !_battery.TryUseCharge(batteryEntity.HasValue ? batteryEntity.Value.AsNullable() : (entity.Owner, battery), entity.Comp.EnergyPerUse))
            {
                args.Cancelled = true;
            }
            // 🌟Starlight🌟 end
        }

        private void OnExamined(Entity<StunbatonComponent> entity, ref ExaminedEvent args)
        {
            var onMsg = _itemToggle.IsActivated(entity.Owner)
            ? Loc.GetString("comp-stunbaton-examined-on")
            : Loc.GetString("comp-stunbaton-examined-off");
            args.PushMarkup(onMsg);

            // 🌟Starlight🌟 start
            Entity<BatteryComponent>? batteryEnt = null;
            if (TryComp<BatteryComponent>(entity.Owner, out var battery) ||
                _powerCell.TryGetBatteryFromSlot(entity.Owner, out batteryEnt))
            {
                if (batteryEnt.HasValue)
                {
                    battery = batteryEnt.Value;
                }

                if(battery == null)
                    return;

                var count = (int)(_battery.GetCharge(battery.Owner) / entity.Comp.EnergyPerUse);
                args.PushMarkup(Loc.GetString("melee-battery-examine", ("color", "yellow"), ("count", count)));
            }

            // 🌟Starlight🌟 end
        }

        protected override void TryTurnOn(Entity<StunbatonComponent> entity, ref ItemToggleActivateAttemptEvent args)
        {
            base.TryTurnOn(entity, ref args);

            // 🌟Starlight🌟 start
            Entity<BatteryComponent>? batteryEnt = null;
            if (TryComp<BatteryComponent>(entity.Owner, out var battery) ||
                _powerCell.TryGetBatteryFromSlot(entity.Owner, out batteryEnt))
            {
                if (batteryEnt.HasValue)
                {
                    battery = batteryEnt.Value;
                    if(battery == null)
                        return;

                    if (_battery.GetCharge(battery.Owner) < entity.Comp.EnergyPerUse)
                    {
                        args.Cancelled = true;
                        if (args.User != null)
                        {
                            _popup.PopupEntity(Loc.GetString("stunbaton-component-low-charge"), (EntityUid)args.User, (EntityUid)args.User);
                        }
                        return;
                    }

                    if (TryComp<RiggableComponent>(battery.Owner, out var rig) && rig.IsRigged)
                    {
                        _riggableSystem.Explode(entity.Owner, _battery.GetCharge(battery.Owner), args.User);
                    }
                    UpdateAppearance(entity, isActive: true);
                }
            }
            // 🌟Starlight🌟 end
        }

        #region Starlight
        protected override void TryTurnOff(Entity<StunbatonComponent> ent, ref ItemToggleDeactivateAttemptEvent args)
        {
            base.TryTurnOff(ent, ref args);
            if(args.Cancelled)
                return;
            UpdateAppearance(ent, isActive: false);
        }
        private void OnChargeChanged(Entity<StunbatonComponent> entity, ref ChargeChangedEvent args)
        {
            // 🌟Starlight🌟 start
            Entity<BatteryComponent>? batteryEnt = null;
            if (TryComp<BatteryComponent>(entity.Owner, out var battery) ||
                _powerCell.TryGetBatteryFromSlot(entity.Owner, out batteryEnt))
            {
                if(batteryEnt.HasValue)
                    battery = batteryEnt.Value;
                if (battery != null)
                {
                    if (battery.LastCharge < entity.Comp.EnergyPerUse)
                    {
                        _itemToggle.TryDeactivate(entity.Owner, predicted: false);
                        UpdateAppearance(entity, isActive: false);
                    }
                }
            }
            // 🌟Starlight🌟 end
        }

        private void OnCellSlotInserted(Entity<StunbatonComponent> ent, ref EntInsertedIntoContainerMessage args)
        {
            UpdateAppearance(ent);
        }
        private void OnCellSlotRemoved(Entity<StunbatonComponent> ent, ref EntRemovedFromContainerMessage args)
        {
            UpdateAppearance(ent);
        }

        private void UpdateAppearance(EntityUid uid, StunbatonComponent? comp = null, AppearanceComponent? appearance = null, bool isActive = false)
        {
            if (!Resolve(uid, ref comp, ref appearance, false))
                return;

            Entity<BatteryComponent>? batteryEnt = null;
            if (TryComp<BatteryComponent>(uid, out var battery) ||
                _powerCell.TryGetBatteryFromSlot(uid, out batteryEnt))
            {
                _appearance.SetData(uid, StunbatonVisuals.Stunbaton_on, isActive);
                _appearance.SetData(uid, StunbatonVisuals.Stunbaton_off, !isActive);
                _appearance.SetData(uid, StunbatonVisuals.Stunbaton_nocell, false);
            }
            else
            {
                _appearance.SetData(uid, StunbatonVisuals.Stunbaton_on, false);
                _appearance.SetData(uid, StunbatonVisuals.Stunbaton_off, false);
                _appearance.SetData(uid, StunbatonVisuals.Stunbaton_nocell, true);
            }
        }
        #endregion
    }
}
