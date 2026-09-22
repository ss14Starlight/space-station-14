using Robust.Shared.Audio;

namespace Content.Server._Funkystation.ReagentFires.Components;

/// <summary>
/// Caches ignition and burn state for puddles that contain flammable reagents.
/// </summary>
[RegisterComponent]
public sealed partial class ReagentPuddleFireComponent : Component
{
    [ViewVariables]
    public bool OnFire { get; set; }

    [ViewVariables]
    public int FireState { get; set; } = 4;

    [ViewVariables]
    public int Flammability { get; set; }

    [ViewVariables]
    public bool SelfOxidizing { get; set; }

    [ViewVariables]
    public EntityUid? PlayingStream { get; set; }

    [ViewVariables]
    public EntityUid? FireEffectEntity { get; set; }

    [ViewVariables(VVAccess.ReadWrite), DataField("sound")]
    public SoundSpecifier LoopingSound { get; set; } = new SoundPathSpecifier("/Audio/_Funkystation/Effects/Fire/bigfire.ogg");

    [ViewVariables]
    public float VolumeFactor { get; set; } = 1f;
}
