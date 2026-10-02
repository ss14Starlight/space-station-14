using Content.Shared.DoAfter;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.CosmicCult.Components;

[RegisterComponent]
public sealed partial class CosmicRiftPurgeComponent : Component
{
    public DoAfterId? DoAfterId;

    [DataField]
    public TimeSpan PurgeTime = TimeSpan.FromSeconds(25);

    [DataField]
    public float DistanceThreshold = 1.5f;

    [DataField]
    public float MovementThreshold = 0.5f;

    [DataField]
    public EntProtoId PurgeVFX = "CleanseEffectVFX";

    [DataField]
    public SoundSpecifier PurgeSFX = new SoundPathSpecifier("/Audio/_Starlight/CosmicCult/effigy_pulse.ogg");

    [DataField]
    public SoundSpecifier BeamSFX = new SoundPathSpecifier("/Audio/Weapons/Guns/Gunshots/laser_cannon2.ogg");
}
