using Content.Shared.Trigger.Components.Effects;
using Robust.Shared.GameStates;

namespace Content.Shared._Starlight.Camera.Trigger;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ScreenshakeUserOnTriggerComponent : BaseXOnTriggerComponent
{
    [DataField] public ScreenshakeParameters? Translation;
    [DataField] public ScreenshakeParameters? Rotation;
}
