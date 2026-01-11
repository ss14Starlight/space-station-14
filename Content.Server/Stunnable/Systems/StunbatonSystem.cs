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

namespace Content.Server.Stunnable.Systems
{
    public sealed class StunbatonSystem : SharedStunbatonSystem
    {
        [Dependency] private readonly RiggableSystem _riggableSystem = default!;
        [Dependency] private readonly SharedPopupSystem _popup = default!;
        [Dependency] private readonly PowerCellSystem _powerCell = default!; // 🌟Starlight🌟
        [Dependency] private readonly PredictedBatterySystem _battery = default!;
        [Dependency] private readonly ItemToggleSystem _itemToggle = default!;

        public override void Initialize()
        {
            base.Initialize();

            SubscribeLocalEvent<StunbatonComponent, ExaminedEvent>(OnExamined);
            SubscribeLocalEvent<StunbatonComponent, SolutionContainerChangedEvent>(OnSolutionChange);
            SubscribeLocalEvent<StunbatonComponent, StaminaDamageOnHitAttemptEvent>(OnStaminaHitAttempt);
            SubscribeLocalEvent<StunbatonComponent, PredictedBatteryChargeChangedEvent>(OnChargeChanged);
        }

        private void OnStaminaHitAttempt(Entity<StunbatonComponent> entity, ref StaminaDamageOnHitAttemptEvent args)
        {
            // 🌟Starlight🌟 start
            // Stunbatons check for power cells if they have no BatteryComponent
            Entity<PredictedBatteryComponent>? batteryEntity = null;
            if (!_itemToggle.IsActivated(entity.Owner) ||
            !(TryComp(entity.Owner, out PredictedBatteryComponent? battery) ||
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
            Entity<PredictedBatteryComponent>? batteryEnt = null;
            if (TryComp<PredictedBatteryComponent>(entity.Owner, out var battery) ||
             _powerCell.TryGetBatteryFromSlot(entity.Owner, out batteryEnt)) // WHY did this get changed to return an entity, aaaa >_<
            {
                if(batteryEnt.HasValue)
                    battery = batteryEnt.Value;
                if (battery != null)
                {
                    var count = (int)(battery.LastCharge / entity.Comp.EnergyPerUse);
                    args.PushMarkup(Loc.GetString("melee-battery-examine", ("color", "yellow"), ("count", count)));
                }
            }
            // 🌟Starlight🌟 end
        }

        protected override void TryTurnOn(Entity<StunbatonComponent> entity, ref ItemToggleActivateAttemptEvent args)
        {
            base.TryTurnOn(entity, ref args);

            // 🌟Starlight🌟 start
            Entity<PredictedBatteryComponent>? batteryEnt = null;
            if (TryComp<PredictedBatteryComponent>(entity.Owner, out var battery) ||
             _powerCell.TryGetBatteryFromSlot(entity.Owner, out batteryEnt)) // WHY did this get changed to return an entity, aaaa >_<
            {
                if(batteryEnt.HasValue)
                    battery = batteryEnt.Value;
                if (battery != null)
                {
                    if (battery.LastCharge < entity.Comp.EnergyPerUse)
                    {
                        args.Cancelled = true;
                        if (args.User != null)
                        {
                            _popup.PopupEntity(Loc.GetString("stunbaton-component-low-charge"), (EntityUid)args.User, (EntityUid)args.User);
                        }
                        return;
                    }
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
                !TryComp<PredictedBatteryComponent>(entity, out var battery))
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

        private void OnChargeChanged(Entity<StunbatonComponent> entity, ref PredictedBatteryChargeChangedEvent args)
        {
            // 🌟Starlight🌟 start
            Entity<PredictedBatteryComponent>? batteryEnt = null;
            if (TryComp<PredictedBatteryComponent>(entity.Owner, out var battery) ||
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
