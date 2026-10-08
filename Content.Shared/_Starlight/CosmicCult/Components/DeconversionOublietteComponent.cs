using Content.Shared.DoAfter;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared._Starlight.CosmicCult.Components;

/// <summary>
/// Component for targets being cleansed of corruption.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentPause, AutoGenerateComponentState]
public sealed partial class DeconversionOublietteComponent : Component
{
    public DoAfterId? DoAfterId = null;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField]
    [AutoPausedField]
    public TimeSpan CooldownTime = default!;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField]
    [AutoPausedField]
    public TimeSpan EmoteTime = default!;

    [DataField] public TimeSpan EmoteMinTime = TimeSpan.FromSeconds(4);
    [DataField] public TimeSpan EmoteMaxTime = TimeSpan.FromSeconds(11);
    [DataField] public TimeSpan CooldownWait = TimeSpan.FromSeconds(15);
    [DataField] public TimeSpan DeconversionTime = TimeSpan.FromSeconds(35);
    [DataField] public float AnomalyPurgeHealth = -999f;
    [DataField] public EntityUid? Victim;
    [DataField] public OublietteStates OublietteState;

    [DataField, AutoNetworkedField] public bool Powered;
    [DataField, AutoNetworkedField] public bool CanInteract = true;
    [DataField, AutoNetworkedField] public bool EjectContents = false;

    [DataField] public EntProtoId PurgeVFX = "CleanseEffectVFX";
    [DataField] public SoundSpecifier PurgeSFX = new SoundPathSpecifier("/Audio/_Starlight/CosmicCult/effigy_pulse.ogg");

}

[Serializable, NetSerializable]
public enum OublietteVisuals : byte
{
    Contents,
}

[Serializable, NetSerializable]
public enum OublietteVisualLayers : byte
{
    BaseLayer,
    UnshadedLayer,
}

[Serializable, NetSerializable]
public enum OublietteStates : byte
{
    Idle,
    Cooldown,
    Active,
}
