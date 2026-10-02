using Content.Server.Popups;
using Content.Shared._Starlight.Restrict;

namespace Content.Server._Starlight.Restrict;

/// <summary>
/// Server-side implementation.
/// </summary>
public sealed partial class RestrictByEquippedTagSystem : SharedRestrictByEquippedTagSystem
{
    [Dependency] private PopupSystem _popup = default!;

    protected override void PopupClient(string message, EntityUid user)
        => _popup.PopupEntity(message, user, user);
}
