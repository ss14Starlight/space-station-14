using Robust.Shared.GameObjects;

namespace Content.Server._Starlight.Computers.RemoteControl;

[RegisterComponent]
public sealed partial class RemoteControlControllerComponent : Component
{
    public HashSet<EntityUid> Consoles = new();
}
