using Content.Shared.Actions;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.CrawlUnder;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CrawlUnderObjectsComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool Enabled = false;

    /// <summary>
    ///     List of fixtures that had their collision mask changed.
    ///     Required for re-adding the collision mask.
    /// </summary>
    [DataField, AutoNetworkedField]
    public List<(string key, int originalMask)> ChangedFixtures = new();

    [DataField]
    public float SneakSpeedModifier = 0.5f;

    /// <summary>
    ///     CLIENT ONLY variable to save the draw old draw depth of the entity before it started crawling.
    /// </summary>
    [DataField]
    public int? OriginalDrawDepth;

    /// <summary>
    ///     Reference to the action itself.
    /// </summary>
    [DataField]
    public EntityUid? ToggleHideAction;

    [DataField]
    public EntProtoId? ActionProto;

    /// <summary>
    /// If true, hands are blocked while sneaking (cannot equip/hold items).
    /// </summary>
    [DataField]
    public bool BlockHands = true;

    /// <summary>
    ///     CLIENT ONLY When we last showed a failed-to-do-xyz popup for an interaction that was blocked by sneaking.
    /// </summary>
    [DataField]
    public TimeSpan LastFailedPopup = TimeSpan.Zero;

    /// <summary>
    ///     CLIENT ONLY Minimum time between failed-to-do-xyz popups.
    /// </summary>
    [DataField]
    public TimeSpan FailedPopupCooldown = TimeSpan.FromMilliseconds(500);
}

[Serializable, NetSerializable]
public enum SneakMode : byte
{
    Enabled
}

public sealed partial class ToggleCrawlingStateEvent : InstantActionEvent
{
}
