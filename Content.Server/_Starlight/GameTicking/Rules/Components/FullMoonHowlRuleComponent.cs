using Robust.Shared.Audio;

namespace Content.Server._Starlight.GameTicking.Rules.Components;

[RegisterComponent, Access(typeof(FullMoonHowlRule))]
public sealed partial class FullMoonHowlRuleComponent : Component
{
    [DataField]
    public SoundSpecifier HowlSound = new SoundCollectionSpecifier("VulpkaninHowls");

    [DataField]
    public HashSet<string> EligibleSpecies = new() { "Vulpkanin", "ProtoVulp" };
}
