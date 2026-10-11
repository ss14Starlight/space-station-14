using Content.Shared.DoAfter;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.CosmicCult.Components;

[RegisterComponent]
public sealed partial class CosmicMalignRiftComponent : Component
{
    public DoAfterId? DoAfterId = null;

    [DataField] public bool Used;

    [DataField] public bool Occupied;

    [DataField] public TimeSpan AbsorbTime = TimeSpan.FromSeconds(30);
    [DataField] public TimeSpan PurgeTime = TimeSpan.FromSeconds(25);
    [DataField] public float DistanceThreshold = 1.5f;

    [DataField] public float MovementThreshold = 0.5f;
    [DataField] public EntProtoId PurgeVFX = "CleanseEffectVFX";
    [DataField] public SoundSpecifier PurgeSFX = new SoundPathSpecifier("/Audio/_Starlight/CosmicCult/effigy_pulse.ogg");
    [DataField] public SoundSpecifier BeamSFX = new SoundPathSpecifier("/Audio/Weapons/Guns/Gunshots/laser_cannon2.ogg");
}
