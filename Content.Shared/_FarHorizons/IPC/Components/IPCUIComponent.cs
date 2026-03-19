namespace Content.Shared._FarHorizons.Silicons.IPC.Components;

[RegisterComponent]
public sealed partial class IPCUserInterfaceComponent : Component
{
    public TimeSpan NextUpdate = TimeSpan.Zero;

    public TimeSpan RefreshRate = TimeSpan.FromSeconds(5);
}