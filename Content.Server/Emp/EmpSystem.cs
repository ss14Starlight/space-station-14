using Content.Server.Power.EntitySystems;
using Content.Server.Radio;
using Content.Server.SurveillanceCamera;
using Content.Shared.Emp;
using Content.Server._Starlight.Emp;
using Content.Shared.Examine;
using Content.Shared.Weapons.Melee.Events;
using Robust.Server.GameObjects;
using Robust.Shared.Map;
using Content.Shared.Item.ItemToggle;
using Content.Shared.PowerCell;

namespace Content.Server.Emp;

public sealed class EmpSystem : SharedEmpSystem
{
    // 🌟Starlight🌟  start  
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly TransformSystem _transform = default!;
    [Dependency] private readonly PowerCellSystem _powerCell = default!; 
    [Dependency] private readonly ItemToggleSystem _itemToggle = default!; 

    // 🌟Starlight🌟 end

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<EmpOnMeleeHitComponent, MeleeHitEvent>(HandleMeleeHitTrigger); // 🌟Starlight🌟
        SubscribeLocalEvent<EmpImmuneComponent, EmpAttemptEvent>(OnEmpAttempt); //SL edit

        SubscribeLocalEvent<EmpDisabledComponent, RadioSendAttemptEvent>(OnRadioSendAttempt);
        SubscribeLocalEvent<EmpDisabledComponent, RadioReceiveAttemptEvent>(OnRadioReceiveAttempt);
        SubscribeLocalEvent<EmpDisabledComponent, CustomRadioSendAttemptEvent>(OnCustomRadioSendAttempt); //Starlight
        SubscribeLocalEvent<EmpDisabledComponent, CustomRadioReceiveAttemptEvent>(OnCustomRadioReceiveAttempt); //Starlight
        SubscribeLocalEvent<EmpDisabledComponent, ApcToggleMainBreakerAttemptEvent>(OnApcToggleMainBreaker);
        SubscribeLocalEvent<EmpDisabledComponent, SurveillanceCameraSetActiveAttemptEvent>(OnCameraSetActive);
    }

    private void OnRadioSendAttempt(EntityUid uid, EmpDisabledComponent component, ref RadioSendAttemptEvent args)
    {
        args.Cancelled = true;
    }

    // 🌟Starlight🌟 start
    private void HandleMeleeHitTrigger(EntityUid uid, EmpOnMeleeHitComponent comp, MeleeHitEvent args)
    {
        if (args.HitEntities.Count <= 0)
            return;

        if (_itemToggle.IsActivated(uid) &&
            _powerCell.TryUseActivatableCharge(uid))
        {
            if(comp.DisableOnHit)
                _itemToggle.TryDeactivate(uid);
            foreach (var target in args.HitEntities)
                EmpPulse(_transform.GetMapCoordinates(target), comp.Range, comp.EnergyConsumption, comp.DisableDuration);
        }
    }
    
    // 🌟Starlight🌟 end

    private void OnRadioReceiveAttempt(EntityUid uid, EmpDisabledComponent component, ref RadioReceiveAttemptEvent args) => args.Cancelled = true;

    //Starlight begin
    private void OnCustomRadioSendAttempt(EntityUid uid, EmpDisabledComponent component,
        ref CustomRadioSendAttemptEvent args) =>
        args.Cancelled = true;

    private void OnCustomRadioReceiveAttempt(EntityUid uid, EmpDisabledComponent component,
        ref CustomRadioReceiveAttemptEvent args) =>
        args.Cancelled = true;
    //Starlight end
    
    private void OnApcToggleMainBreaker(EntityUid uid, EmpDisabledComponent component, ref ApcToggleMainBreakerAttemptEvent args) => args.Cancelled = true;

    private void OnCameraSetActive(EntityUid uid, EmpDisabledComponent component, ref SurveillanceCameraSetActiveAttemptEvent args) => args.Cancelled = true;
    
    private void OnEmpAttempt(EntityUid uid, EmpImmuneComponent comp, EmpAttemptEvent args) => args.Cancelled = true;
}
