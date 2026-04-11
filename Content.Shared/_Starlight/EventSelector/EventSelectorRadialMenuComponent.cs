using Content.Shared.GameTicking.Components;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Shared._Starlight.EventSelector;

/// <summary>
/// This component brings up a radial menu with different events that activate depending on what you select.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class EventSelectorRadialMenuComponent : Component
{
    /// <summary>
    /// List of all radial menu entries
    /// </summary>
    [DataField, AutoNetworkedField]
    public List<EventSelectorRadialMenuEntry> RadialMenuEntries = [];
}

/// <summary>
/// Information about one specific radial menu button for the <see cref="EventSelectorRadialMenuComponent"/>
/// Contains all information about one specific button!
/// </summary>
[DataDefinition, Serializable, NetSerializable]
public sealed partial class EventSelectorRadialMenuEntry
{
    /// <summary>
    /// Name of the tooltip that will be displayed
    /// </summary>
    [DataField]
    public LocId? Name;

    /// <summary>
    /// Will use this entity as the icon, this has priority over <see cref="SpriteSpecifierIcon"/>
    /// </summary>
    [DataField]
    public EntProtoId? ProtoIdIcon;

    /// <summary>
    /// A sprite specifier to use for the radial menu icon
    /// </summary>
    [DataField]
    public SpriteSpecifier? SpriteSpecifierIcon;

    /// <summary>
    /// The game rule that will be started when selected
    /// </summary>
    [DataField(required: true)]
    public EntProtoId<GameRuleComponent> GameRule;

    /// <summary>
    /// Length of cooldown after the event is selected
    /// </summary>
    [DataField(required: true)]
    public TimeSpan UseDelay;
}

/// <summary>
/// Message sent from the client w/ the index of the radial menu entry they selected
/// </summary>
[Serializable, NetSerializable]
public sealed class EventSelectorOnRadialMenuSelectMessage(int index) : BoundUserInterfaceMessage
{
    public int Index = index;
}

[Serializable, NetSerializable]
public enum EventSelectorRadialMenuKey : byte
{
    Key
}
