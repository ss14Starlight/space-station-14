using Robust.Client.UserInterface;
using Robust.Client.Timing;
using JetBrains.Annotations;
using Content.Shared._FarHorizons.Power.Generation.FissionGenerator;
using Content.Client.UserInterface;
using System.Diagnostics.CodeAnalysis;

namespace Content.Client._FarHorizons.Power.UI;

/// <summary>
/// Initializes a <see cref="GasTurbineWindow"/> and updates it when new server messages are received.
/// </summary>
[UsedImplicitly]
public sealed class GasTurbineBoundUserInterface : BoundUserInterface, IBuiPreTickUpdate
{
    [Dependency] private readonly IClientGameTiming _gameTiming = null!;
    [Dependency] private readonly IEntityManager _entityManager = null!;

    [ViewVariables]
    private GasTurbineWindow? _window;

    private BuiPredictionState? _pred;
    private InputCoalescer<float> _flowRateCoalescer;
    private InputCoalescer<float> _statorLoadCoalescer;

    public GasTurbineBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey) => IoCManager.InjectDependencies(this);

    protected override void Open()
    {
        EntityUid? turbineUid = null;
        if (_entityManager.TryGetComponent<GasTurbineMonitorComponent>(Owner, out var turbineMonitorComponent))
            if (!_entityManager.TryGetEntity(turbineMonitorComponent.turbine, out turbineUid) || turbineUid == null
                || !_entityManager.HasComponent<GasTurbineComponent>(turbineUid))
                return;

        base.Open();

        _pred = new BuiPredictionState(this, _gameTiming);

        _window = this.CreateWindow<GasTurbineWindow>();
        if (_entityManager.EntityExists(turbineUid))
            _window.SetEntity(turbineUid.Value, Owner);
        else
            _window.SetEntity(Owner);

        _window.TurbineFlowRateChanged += val => _flowRateCoalescer.Set(val);
        _window.TurbineStatorLoadChanged += val => _statorLoadCoalescer.Set(val);
        Update();
    }

    void IBuiPreTickUpdate.PreTickUpdate()
    {
        if (_flowRateCoalescer.CheckIsModified(out var flowRateValue))
            _pred!.SendMessage(new GasTurbineChangeFlowRateMessage(flowRateValue));

        if (_statorLoadCoalescer.CheckIsModified(out var statorLoadValue))
            _pred!.SendMessage(new GasTurbineChangeStatorLoadMessage(statorLoadValue));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        if (state is not GasTurbineBuiState turbineState)
            return;

        if (!_entityManager.TryGetComponent<GasTurbineComponent>(Owner, out var comp))
            if(!TryGetTurbineComp(Owner, out comp))
                return;

        foreach (var replayMsg in _pred!.MessagesToReplay())
        {
            switch (replayMsg)
            {
                case GasTurbineChangeFlowRateMessage setFlowRate:
                    turbineState.FlowRate = Math.Clamp(setFlowRate.FlowRate, 0f, comp.FlowRateMax);
                    break;

                case GasTurbineChangeStatorLoadMessage setStatorLoad:
                    turbineState.StatorLoad = Math.Max(setStatorLoad.StatorLoad, 1000f);
                    break;
            }
        }

        _window?.Update(turbineState);
    }

    public bool TryGetTurbineComp(EntityUid uid, [NotNullWhen(true)] out GasTurbineComponent? turbineComponent)
    {
        turbineComponent = null;
        if (!_entityManager.TryGetComponent<GasTurbineMonitorComponent>(uid, out var turbineMonitor))
            return false;

        if (!_entityManager.TryGetEntity(turbineMonitor.turbine, out var turbineUid) || turbineUid == null)
            return false;

        if (!_entityManager.TryGetComponent<GasTurbineComponent>(turbineUid, out var turbine))
            return false;

        turbineComponent = turbine;
        return true;
    }
}
