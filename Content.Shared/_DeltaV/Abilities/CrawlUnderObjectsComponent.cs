using Content.Shared.Actions;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._DeltaV.Abilities;

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
}

[Serializable, NetSerializable]
public enum SneakMode : byte
{
    Enabled
}

public sealed partial class ToggleCrawlingStateEvent : InstantActionEvent { }
