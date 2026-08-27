using Content.Server.Medical;
using Content.Server.Medical.Components;
using Content.Shared._Starlight.Actions.Events;
using Content.Shared.DoAfter;
using Content.Shared.MedicalScanner;
using Robust.Server.GameObjects;
using Robust.Shared.Audio.Systems;

namespace Content.Server._Starlight.Medical;

public sealed partial class HealthSelfAnalyzerSystem : EntitySystem
{
    [Dependency] private HealthAnalyzerSystem _healthAnalyzerSystem = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private UserInterfaceSystem _uiSystem = default!;

    private const string HealthAnalyzerBoundUserInterface = "HealthAnalyzerBoundUserInterface";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HealthAnalyzerComponent, HealthSelfAnalyzeActionEvent>(OnHealthSelfAnalyze);
    }

    //private void OnHealthSelfAnalyze(EntityUid uid, HealthAnalyzerComponent comp, HealthSelfAnalyzeActionEvent args) =>
    //    _doAfterSystem.TryStartDoAfter(new DoAfterArgs(EntityManager, args.Performer, comp.ScanDelay,
    //        new HealthAnalyzerDoAfterEvent(), uid, uid, uid));

    private void OnHealthSelfAnalyze(Entity<HealthAnalyzerComponent> entity, ref HealthSelfAnalyzeActionEvent args)
    {
        if (!TryComp<UserInterfaceComponent>(entity, out var comp))
            return;

        if (_uiSystem.IsUiOpen((entity, comp), HealthAnalyzerUiKey.Key))
        {
            _uiSystem.CloseUi((entity, comp), HealthAnalyzerUiKey.Key);
            _healthAnalyzerSystem.StopAnalyzingEntity(entity, args.Performer);
            return;
        }

        if (!_uiSystem.HasUi(entity, HealthAnalyzerUiKey.Key))
            _uiSystem.SetUi((entity, comp),  HealthAnalyzerUiKey.Key, new InterfaceData(HealthAnalyzerBoundUserInterface));

        _audio.PlayEntity(entity.Comp.ScanningEndSound, entity, entity);
        _healthAnalyzerSystem.BeginAnalyzingEntity(entity, args.Performer);
        _uiSystem.OpenUi((entity, comp), HealthAnalyzerUiKey.Key, args.Performer);

    }
}
