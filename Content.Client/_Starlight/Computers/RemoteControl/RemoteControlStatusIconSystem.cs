using Content.Shared._Starlight.Computers.RemoteControl;
using Content.Shared.StatusIcon;
using Content.Shared.StatusIcon.Components;
using Robust.Client.GameObjects;
using Robust.Shared.Utility;

namespace Content.Client._Starlight.Computers.RemoteControl;

public sealed partial class RemoteControlStatusIconSystem : EntitySystem
{
    private static readonly StatusIconData _remoteControlIcon = new()
    {
        Icon = new SpriteSpecifier.Rsi(new ResPath("/Textures/_Starlight/Effects/remote_control.rsi"), "remote_control"),
        LocationPreference = StatusIconLocationPreference.Left,
        Offset = -28, // Offset from the top. Trying to place it at the bottom of the sprite. I am guessing.
    };

    [SubscribeLocalEvent]
    private void OnGetStatusIcons(EntityUid uid, RemoteControlActiveComponent component,
        ref GetStatusIconsEvent args) =>
        args.StatusIcons.Add(_remoteControlIcon);
}
