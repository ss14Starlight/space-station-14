using Content.Shared._Starlight.Computers.RemoteControl;

namespace Content.Client._Starlight.Computers.RemoteControl;

public sealed class RemoteControlConsoleSystem : SharedRemoteControlConsoleSystem
{
    protected override void UpdateUi(EntityUid uid, RemoteControlConsoleComponent component)
    {
        if (SharedUi.TryGetOpenUi(uid, RemoteControlUIKey.Key, out var bui))
            bui.Update();
    }
}
