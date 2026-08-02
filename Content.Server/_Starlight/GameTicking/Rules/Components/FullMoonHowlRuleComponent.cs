using Robust.Shared.Audio;
using Robust.Shared.Prototypes;
using Content.Shared.Tag;

namespace Content.Server._Starlight.GameTicking.Rules.Components;

[RegisterComponent, Access(typeof(FullMoonHowlRule))]
public sealed partial class FullMoonHowlRuleComponent : Component
{
    [DataField]
    public SoundSpecifier HowlSound = new SoundCollectionSpecifier("VulpkaninHowls");

    [DataField]
    public HashSet<string> EligibleSpecies = new() { "Vulpkanin", "ProtoVulp" };

    [DataField]
    public HashSet<ProtoId<TagPrototype>> EligibleMobTags = new() { "DogEmotes" };
}
