using Content.Shared._Starlight.Changeling;
using Content.Shared._Starlight.Overlay.Components;

namespace Content.Server._Starlight.Changeling;

public sealed partial class ChangelingSystem
{
    private void ToggleEnhancedEyes(EntityUid uid, ChangelingComponent ling, ref ActionAugmentedEyesightEvent args)
    {
        if (args.Handled)
            return;
        if (ling.EyesAction != null)
            StopEnhancedEyes(uid, ling);
        else
        {
            if (!CanActivateAdaptation(uid, ling))
                return;
            var eyes = EnsureComp<ChangelingEyesComponent>(uid);
            eyes.AddedNight = !TryComp<NightVisionComponent>(uid, out var night);
            eyes.NightWasActive = night?.Active ?? false;
            night ??= EnsureComp<NightVisionComponent>(uid);
            night.Active = true;
            Dirty(uid, night);
            SetAdaptationAction(ref ling.EyesAction, args.Action);
            UpdateEnhancedEyes(uid, ling);
        }
        ShowAbilityFeedback(uid, args.Action, ling.EyesAction != null);
        args.Handled = true;
    }

    private void UpdateEnhancedEyes(EntityUid uid, ChangelingComponent ling)
    {
        if (!ling.Evolved || !TryComp<ChangelingEyesComponent>(uid, out var eyes) || eyes.ThermalConfigured)
            return;
        eyes.ThermalConfigured = true;
        eyes.AddedThermal = !TryComp<ThermalVisionComponent>(uid, out var thermal);
        eyes.ThermalWasActive = thermal?.Active ?? false;
        if (thermal == null)
        {
            thermal = new ThermalVisionComponent { Active = true, GrantToggleAction = false };
            AddComp(uid, thermal);
        }
        thermal.Active = true;
        if (eyes.AddedThermal && thermal.ActionEntity is { } toggle)
        {
            _actions.RemoveAction(toggle);
            thermal.ActionEntity = null;
        }
        Dirty(uid, thermal);
    }

    private void StopEnhancedEyes(EntityUid uid, ChangelingComponent ling)
    {
        if (ling.EyesAction == null)
            return;
        SetAdaptationAction(ref ling.EyesAction, null);
        if (!TryComp<ChangelingEyesComponent>(uid, out var eyes))
            return;
        if (eyes.AddedNight)
            RemComp<NightVisionComponent>(uid);
        else if (TryComp<NightVisionComponent>(uid, out var night))
        {
            night.Active = eyes.NightWasActive;
            Dirty(uid, night);
        }
        if (eyes.AddedThermal)
            RemComp<ThermalVisionComponent>(uid);
        else if (ling.Evolved && TryComp<ThermalVisionComponent>(uid, out var thermal))
        {
            thermal.Active = eyes.ThermalWasActive;
            Dirty(uid, thermal);
        }
        RemComp<ChangelingEyesComponent>(uid);
    }
}
