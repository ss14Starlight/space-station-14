using Content.Server.Medical;
using Content.Server.Medical.Components;
using Content.Shared._Starlight.Actions.Events;
using Content.Shared.Emp;
using Content.Shared.MedicalScanner;
using Robust.Server.GameObjects;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Player;

namespace Content.Server._Starlight.Medical;

public sealed partial class HealthSelfAnalyzerSystem : EntitySystem
{
    [Dependency] private HealthAnalyzerSystem _healthAnalyzerSystem = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private UserInterfaceSystem _uiSystem = default!;

    private const string HealthAnalyzerBoundUserInterface = "HealthAnalyzerBoundUserInterface";

    [SubscribeLocalEvent]
    private void OnHealthSelfAnalyze(Entity<HealthSelfAnalyzerComponent> entity, ref HealthSelfAnalyzeActionEvent args)
    {
        if(HasComp<EmpDisabledComponent>(entity))
            return;

        entity.Comp.Toggled = !entity.Comp.Toggled;
        ToggleUi(entity, entity.Comp.Toggled);
    }

    [SubscribeLocalEvent]
    private void OnEmpPulse(Entity<HealthSelfAnalyzerComponent> entity, ref EmpPulseEvent args)
    {
        args.Affected = true;
        args.Disabled = true;

        if (!entity.Comp.Toggled)
            return;

        ToggleUi(entity, false);
    }

    [SubscribeLocalEvent]
    private void OnEmpRemoved(Entity<HealthSelfAnalyzerComponent> entity, ref EmpDisabledRemovedEvent args)
    {
        if (!entity.Comp.Toggled)
            return;

        ToggleUi(entity, true);
    }

    private void ToggleUi(EntityUid uid, bool toggled)
    {
        if (!TryComp<HealthAnalyzerComponent>(uid, out var analyzerComp) ||
            !TryComp<UserInterfaceComponent>(uid, out var interfaceComp) ||
            !TryComp<ActorComponent>(uid, out var actorComp))
            return;

        if (toggled)
        {
            if (!_uiSystem.HasUi(uid, HealthAnalyzerUiKey.Key))
                _uiSystem.SetUi(uid, HealthAnalyzerUiKey.Key, new InterfaceData(HealthAnalyzerBoundUserInterface));

            _audio.PlayEntity(analyzerComp.ScanningBeginSound, actorComp.PlayerSession, uid);
            _healthAnalyzerSystem.BeginAnalyzingEntity((uid, analyzerComp), uid);
            _uiSystem.OpenUi((uid, interfaceComp), HealthAnalyzerUiKey.Key, uid);
        }
        else
        {
            if(!_uiSystem.IsUiOpen(uid, HealthAnalyzerUiKey.Key))
                return;

            _audio.PlayEntity(analyzerComp.ScanningEndSound, actorComp.PlayerSession, uid);
            _healthAnalyzerSystem.StopAnalyzingEntity((uid, analyzerComp), uid);
            _uiSystem.CloseUi((uid, interfaceComp), HealthAnalyzerUiKey.Key);
        }
    }
}
