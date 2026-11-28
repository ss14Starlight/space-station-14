// ReSharper disable CheckNamespace

using Content.Shared._Starlight.Computers.RemoteControl;

namespace Content.Shared.Interaction;

public sealed partial class InteractionPopupSystem
{
    [Dependency] private SharedRemoteControlConsoleSystem _remoteControl = default!;
}
