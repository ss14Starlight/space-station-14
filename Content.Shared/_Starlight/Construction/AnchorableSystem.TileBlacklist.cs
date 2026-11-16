using Content.Shared._Starlight.Construction.Components;
using Content.Shared.Whitelist;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

// ReSharper disable once CheckNamespace
namespace Content.Shared.Construction.EntitySystems;

public sealed partial class AnchorableSystem
{
    [Dependency] private EntityWhitelistSystem _whitelist = default!;

    /// <summary>
    /// True (and shows a popup) if <paramref name="uid"/> refuses to anchor at <paramref name="location"/>
    /// because of its <see cref="AnchorTileBlacklistComponent"/>.
    /// </summary>
    private bool AnchorTileBlacklisted(EntityUid uid, EntityCoordinates location, EntityUid user)
    {
        if (!TryComp<AnchorTileBlacklistComponent>(uid, out var blacklist)
            || _transformSystem.GetGrid(location) is not { } gridUid
            || !TryComp<MapGridComponent>(gridUid, out var grid))
            return false;

        foreach (var entity in _map.GetAnchoredEntities((gridUid, grid), _map.LocalToTile(gridUid, grid, location)))
        {
            if (entity == uid || !_whitelist.IsWhitelistPass(blacklist.Blacklist, entity))
                continue;

            _popup.PopupClient(Loc.GetString(blacklist.Popup), uid, user);
            return true;
        }

        return false;
    }
}
