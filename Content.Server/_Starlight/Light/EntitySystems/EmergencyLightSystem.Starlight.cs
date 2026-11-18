using Content.Server.Light.Components;

// ReSharper disable CheckNamespace
namespace Content.Server.Light.EntitySystems;

public sealed partial class EmergencyLightSystem
{
    [SubscribeLocalEvent]
    private void OnEmergencyInit(Entity<EmergencyLightComponent> ent, ref ComponentInit args)
    {
        // Starlight: Defaults to Empty instead of Charging (0) so initial SetState fires EmergencyLightEvent to ensure ActiveEmergencyLightComponent is added.
        ent.Comp.State = EmergencyLightState.Empty;
    }
}
