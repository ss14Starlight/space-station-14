using Content.Shared.Actions;
using Content.Shared.Dataset;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.Samurai;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(SharedSamuraiCodeSystem))]
public sealed partial class SamuraiCodesComponent : Component
{
    /// <summary>
    /// Whether to include SharedCodes that all samurai have.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool FollowsSharedCodes = true;

    /// <summary>
    /// The non-shared Codes that are active.
    /// </summary>
    [DataField, AutoNetworkedField]
    public List<SamuraiCode> Codes = new();

    /// <summary>
    /// Whether to allow emagging to add a random wildcard code.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool CanBeEmagged = false;

    /// <summary>
    /// Notification sound played if your codes change.
    /// </summary>
    [DataField, AutoNetworkedField]
    public SoundSpecifier? CodesChangedSound = new SoundPathSpecifier("/Audio/_Starlight/Thaven/moods_changed.ogg");

    [DataField(serverOnly: true)]
    public EntityUid? Action;

    /// <summary>
    /// will grab 1 code from each of these datasets on round start/map init
    /// </summary>
    [DataField(serverOnly: true)]
    public List<ProtoId<DatasetPrototype>> CodeDatasets =  new() { SharedSamuraiCodeSystem.BaseDataset, SharedSamuraiCodeSystem.BaseDataset, SharedSamuraiCodeSystem.BaseDataset };

    /// <summary>
    /// what dataset will the "wildcard" code be pulled from
    /// </summary>
    [DataField(serverOnly: true)]
    public ProtoId<DatasetPrototype> Wildcard = SharedSamuraiCodeSystem.BaseDataset;

    /// <summary>
    /// Chance of getting a random "wildcard" code added during an ion storm.
    /// </summary>
    [DataField]
    public float IonStormCodeChance = 0f;
}

public sealed partial class ToggleCodesScreenEvent : InstantActionEvent;

[NetSerializable, Serializable]
public enum SamuraiCodesUiKey : byte
{
    Key
}

/// <summary>
/// BUI state to tell the client what the shared codes are.
/// </summary>
[Serializable, NetSerializable]
public sealed class SamuraiCodesBuiState(List<SamuraiCode> sharedCodes) : BoundUserInterfaceState
{
    public readonly List<SamuraiCode> SharedCodes = sharedCodes;
}
