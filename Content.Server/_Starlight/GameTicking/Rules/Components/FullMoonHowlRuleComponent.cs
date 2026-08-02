using Robust.Shared.Audio;

namespace Content.Server._Starlight.GameTicking.Rules.Components;

[RegisterComponent, Access(typeof(FullMoonHowlRule))]
public sealed partial class FullMoonHowlRuleComponent : Component
{
    [DataField]
    public SoundSpecifier HowlSound = new SoundCollectionSpecifier("VulpkaninHowls");

    /// <summary>
    /// Eligible entities are matched by <see cref="Content.Shared.Inventory.InventoryComponent.SpeciesId"/>.
    /// Vulpkanin and ProtoVulp both use "vulpkanin"; corgis/borgis use "dog"; puppies/McGriff use "puppy".
    /// </summary>
    [DataField]
    public HashSet<string> EligibleInventorySpecies = new() { "vulpkanin", "dog", "puppy" };
}
