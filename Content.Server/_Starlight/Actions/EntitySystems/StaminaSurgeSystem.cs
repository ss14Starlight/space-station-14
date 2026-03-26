using Content.Server.Actions;
using Content.Shared._Starlight.Actions.Components;
using Content.Shared._Starlight.Actions.EntitySystems;
using Content.Shared._Starlight.Actions.Events;
using Content.Shared.Alert;
using Content.Shared.Damage.Components;
using Content.Shared.Nutrition.EntitySystems;
using Robust.Shared.Timing;

namespace Content.Server._Starlight.Actions.EntitySystems;

public sealed class StaminaSurgeSystem : SharedStaminaSurgeSystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly ActionsSystem _actions = default!;
    [Dependency] private readonly AlertsSystem _alerts = default!;
    [Dependency] private readonly HungerSystem _hunger = default!;
    [Dependency] private readonly ThirstSystem _thirst = default!;
    
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<StaminaSurgeComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<StaminaSurgeComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<StaminaSurgeActionEvent>(OnAction);
    }

    private void OnStartup(EntityUid uid, StaminaSurgeComponent component, ComponentStartup args) =>
        _actions.AddAction(uid, ref component.ActionEntity, component.Action);

    private void OnShutdown(EntityUid uid, StaminaSurgeComponent component, ComponentShutdown args)
    {
        if (Deleted(uid) || component.ActionEntity is null) return;
        _actions.RemoveAction((uid, null), component.ActionEntity);
    }

    private void OnAction(StaminaSurgeActionEvent ev)
    {
        if (ev.Handled) return;
        var uid = ev.Performer;
        if (!TryComp<StaminaComponent>(uid, out var stamina)) return;
        if (!TryComp<StaminaSurgeComponent>(uid, out var surge)) return;

        var duration = surge.Duration ?? TimeSpan.Zero; // fallback in case its somehow null
        // calculate this now so it can be used for modifier entries.
        var endTime = _timing.CurTime + duration;
        surge.EffectEndTime = endTime;

        // Don't ask me why, EntityUid says some BS about it not being serializable despite never being used in a serialized network event. This codebase is stupid.
        if (surge.StaminaCooldownModifier is not null)
            stamina.CooldownModifiers.Add((GetNetEntity(uid), surge.StaminaCooldownModifier.Value, endTime));
        if (surge.StaminaRegenModifier is not null)
            stamina.DecayModifiers.Add((GetNetEntity(uid), surge.StaminaRegenModifier.Value, endTime));
        if (surge.StaminaResistModifier is not null)
            stamina.ResistanceModifiers.Add((GetNetEntity(uid), surge.StaminaResistModifier.Value, endTime));
        
        if (surge.HungerDrain is not null)
                _hunger.AddHungerDrain(uid, surge.HungerDrain.Value, endTime);
        
        if (surge.ThirstDrain is not null)
                _thirst.AddThirstDrain(uid, surge.ThirstDrain.Value, endTime);
        
        _alerts.ShowAlert(uid, surge.SurgeAlert);
        surge.Active = true;
        ev.Handled = true;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<StaminaComponent, StaminaSurgeComponent>();
        while (query.MoveNext(out var uid, out var stamina, out var surge))
        {
            if (!surge.Active || _timing.CurTime < surge.EffectEndTime) continue;
            surge.Active = false;
            
            stamina.CooldownModifiers.RemoveAll(x => x.Item1 == GetNetEntity(uid) && x.Item3 == surge.EffectEndTime);
            stamina.DecayModifiers.RemoveAll(x => x.Item1 == GetNetEntity(uid) && x.Item3 == surge.EffectEndTime);
            stamina.ResistanceModifiers.RemoveAll(x => x.Item1 == GetNetEntity(uid) && x.Item3 == surge.EffectEndTime);

            _alerts.ClearAlert(uid, surge.SurgeAlert);
            
            _hunger.RemoveHungerDrain(uid, surge.EffectEndTime);
            _thirst.RemoveThirstDrain(uid, surge.EffectEndTime);
        }
    }
}