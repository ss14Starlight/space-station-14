using Content.Shared.Storage.EntitySystems;
using Content.Shared.Whitelist;
using Robust.Shared.GameStates;

namespace Content.Shared._Starlight.Storage.Components;

/// <summary>
/// Adds a verb to pick the last inserted item from a container.
/// Only picks items that match the whitelist.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(PickRandomSystem))]
public sealed partial class PickLastComponent : Component
{
    /// <summary>
    /// Whitelist for potential picked items.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntityWhitelist? Whitelist;

    /// <summary>
    /// Locale id for the pick verb text.
    /// </summary>
    [DataField, AutoNetworkedField]
    public LocId VerbText = "comp-pick-last-verb-text";

    /// <summary>
    /// Locale id for the empty storage message.
    /// </summary>
    [DataField, AutoNetworkedField]
    public LocId EmptyText = "comp-pick-random-empty";
}
