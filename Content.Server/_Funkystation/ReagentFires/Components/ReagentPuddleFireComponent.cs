using Robust.Shared.Audio;

namespace Content.Server._Funkystation.ReagentFires.Components
{
    /// <summary>
    /// Added to puddles that contain flammable reagents and are currently burning.
    /// </summary>
    [RegisterComponent]
    public sealed partial class ReagentPuddleFireComponent : Component
    {
        [ViewVariables]
        public bool OnFire { get; set; } = false;

        [ViewVariables]
        public int FireState { get; set; } = 4;

        [ViewVariables]
        public int Flammability { get; set; } = 0;

        [ViewVariables]
        public bool SelfOxidizing { get; set; } = false;

        [ViewVariables]
        public float Accumulator { get; set; } = 0f;

        [ViewVariables]
        public EntityUid? PlayingStream { get; set; } = null;

        [ViewVariables]
        public EntityUid? FireEffectEntity { get; set; } = null;

        [ViewVariables(VVAccess.ReadWrite), DataField("sound")]
        public SoundSpecifier LoopingSound { get; set; } = new SoundPathSpecifier("/Audio/_Funkystation/Effects/Fire/bigfire.ogg");
    }
}
