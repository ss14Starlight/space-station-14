using Content.Shared._Sol.Medical.Virology.Components;
using Content.Shared.DeviceLinking;
using Content.Shared.DeviceLinking.Events;
using Robust.Shared.Prototypes;

namespace Content.Server._Sol.Medical.Virology;

/// <summary>
/// Turns the quarantine alarm's light layer and rotating point light on/off from device-link signals.
/// </summary>
public sealed partial class VirologyAlarmLightSystem : EntitySystem
{
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private SharedDeviceLinkSystem _deviceLink = default!;
    [Dependency] private SharedPointLightSystem _pointLight = default!;

    private static readonly ProtoId<SinkPortPrototype> OnPort = "On";
    private static readonly ProtoId<SinkPortPrototype> OffPort = "Off";
    private static readonly ProtoId<SinkPortPrototype> TogglePort = "Toggle";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<VirologyAlarmLightComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<VirologyAlarmLightComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<VirologyAlarmLightComponent, SignalReceivedEvent>(OnSignalReceived);
    }

    private void OnInit(Entity<VirologyAlarmLightComponent> ent, ref ComponentInit args)
    {
        _deviceLink.EnsureSinkPorts(ent, OnPort, OffPort, TogglePort);
    }

    private void OnMapInit(Entity<VirologyAlarmLightComponent> ent, ref MapInitEvent args)
    {
        SetActive(ent, ent.Comp.Active);
    }

    private void OnSignalReceived(Entity<VirologyAlarmLightComponent> ent, ref SignalReceivedEvent args)
    {
        if (args.Port == OnPort)
            SetActive(ent, true);
        else if (args.Port == OffPort)
            SetActive(ent, false);
        else if (args.Port == TogglePort)
            SetActive(ent, !ent.Comp.Active);
    }

    private void SetActive(Entity<VirologyAlarmLightComponent> ent, bool active)
    {
        ent.Comp.Active = active;
        Dirty(ent);

        _pointLight.SetEnabled(ent, active);
        _appearance.SetData(ent, VirologyAlarmLightVisuals.On, active);
    }
}
