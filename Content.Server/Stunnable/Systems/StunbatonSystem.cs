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

#region Starlight
using Content.Shared.CombatMode;
using Content.Shared.Interaction;
using Content.Shared.Tag;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Timing;
#endregion

namespace Content.Server.Stunnable.Systems
{
    public sealed class StunbatonSystem : SharedStunbatonSystem
    {
        [Dependency] private readonly RiggableSystem _riggableSystem = default!;
        [Dependency] private readonly SharedPopupSystem _popup = default!;
        [Dependency] private readonly SharedBatterySystem _battery = default!;
        [Dependency] private readonly ItemToggleSystem _itemToggle = default!;
        #region Starlight
        [Dependency] private readonly PowerCellSystem _powerCell = default!;
        [Dependency] private readonly SharedCombatModeSystem _combatMode = default!;
        [Dependency] private readonly IGameTiming _gameTiming = default!;
        [Dependency] private readonly SharedAudioSystem _audio = default!;
        [Dependency] private readonly TagSystem _tagSystem = default!;
        #endregion

        public override void Initialize()
        {
            base.Initialize();

            SubscribeLocalEvent<StunbatonComponent, AfterInteractEvent>(OnStunbatonAfterInteract); // Starlight-edit
            SubscribeLocalEvent<StunbatonComponent, ExaminedEvent>(OnExamined);
            SubscribeLocalEvent<StunbatonComponent, SolutionContainerChangedEvent>(OnSolutionChange);
            SubscribeLocalEvent<StunbatonComponent, StaminaDamageOnHitAttemptEvent>(OnStaminaHitAttempt);
            SubscribeLocalEvent<StunbatonComponent, ChargeChangedEvent>(OnChargeChanged);
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
                    battery = batteryEnt.Value;
                if (battery != null)
                {
                    var count = (int)(_battery.GetCharge((entity.Owner, battery)) / entity.Comp.EnergyPerUse);
                    args.PushMarkup(Loc.GetString("melee-battery-examine", ("color", "yellow"), ("count", count)));
                }
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
                    battery = batteryEnt.Value;
                if (battery != null && _battery.GetCharge((entity.Owner, battery)) < entity.Comp.EnergyPerUse)
                {
                    args.Cancelled = true;
                    if (args.User != null)
                    {
                        _popup.PopupEntity(Loc.GetString("stunbaton-component-low-charge"), (EntityUid)args.User, (EntityUid)args.User);
                    }
                    return;
                }
            }
            // 🌟Starlight🌟 end

            if (TryComp<RiggableComponent>(entity, out var rig) && rig.IsRigged)
            {
                _riggableSystem.Explode(entity.Owner, _battery.GetCharge((entity, battery)), args.User);
            }
        }

        // https://github.com/space-wizards/space-station-14/pull/17288#discussion_r1241213341
        private void OnSolutionChange(Entity<StunbatonComponent> entity, ref SolutionContainerChangedEvent args)
        {
            // Explode if baton is activated and rigged.
            if (!TryComp<RiggableComponent>(entity, out var riggable) ||
                !TryComp<BatteryComponent>(entity, out var battery))
                return;

            if (_itemToggle.IsActivated(entity.Owner) && riggable.IsRigged)
                _riggableSystem.Explode(entity.Owner, _battery.GetCharge((entity, battery)));
        }

        // TODO: Not used anywhere?
        private void SendPowerPulse(EntityUid target, EntityUid? user, EntityUid used)
        {
            RaiseLocalEvent(target, new PowerPulseEvent()
            {
                Used = used,
                User = user
            });
        }

        private void OnChargeChanged(Entity<StunbatonComponent> entity, ref ChargeChangedEvent args)
        {
            // 🌟Starlight🌟 start
            Entity<BatteryComponent>? batteryEnt = null;
            if (TryComp<BatteryComponent>(entity.Owner, out var battery) ||
             _powerCell.TryGetBatteryFromSlot(entity.Owner, out batteryEnt)) // WHY did this get changed to return an entity, aaaa >_<
            {
                if(batteryEnt.HasValue)
                    battery = batteryEnt.Value;
                if (battery != null)
                {
                    if (battery.LastCharge < entity.Comp.EnergyPerUse)
                    {
                        _itemToggle.TryDeactivate(entity.Owner, predicted: false);
                    }
                }
            }
            // 🌟Starlight🌟 end
        }
    }
}
