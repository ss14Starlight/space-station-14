using Content.Shared.Whitelist;
using Robust.Shared.GameStates;

namespace Content.Shared._Starlight.Construction.Components;

/// <summary>
/// Refuses to anchor this entity on a tile where something anchored matches <see cref="Blacklist"/>.
/// The anchoring counterpart of the NoEntityInTile construction condition.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class AnchorTileBlacklistComponent : Component
{
    [DataField(required: true)]
    public EntityWhitelist Blacklist = new();

    [DataField]
    public LocId Popup = "anchorable-tile-blacklisted";
}
