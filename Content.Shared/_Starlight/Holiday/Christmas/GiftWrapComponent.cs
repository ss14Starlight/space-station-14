using Content.Shared.Whitelist;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Holiday.Christmas;

/// <summary>
/// Component for wrapping paper that can wrap items into presents.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class GiftWrapComponent : Component
{
    /// <summary>
    /// The entity prototype to spawn when wrapping an item (the present).
    /// </summary>
    [DataField(required: true)]
    public EntProtoId PresentPrototype = "Present";

    /// <summary>
    /// How long it takes to wrap an item.
    /// </summary>
    [DataField]
    public float WrapDelay = 5f;

    /// <summary>
    /// Sound played when wrapping an item.
    /// </summary>
    [DataField]
    public SoundSpecifier? WrapSound = new SoundPathSpecifier("/Audio/Effects/packetrip.ogg");

    /// <summary>
    /// Whitelist for items that can be wrapped.
    /// </summary>
    [DataField]
    public EntityWhitelist? Whitelist;

    /// <summary>
    /// Blacklist for items that cannot be wrapped.
    /// </summary>
    [DataField]
    public EntityWhitelist? Blacklist;
}
