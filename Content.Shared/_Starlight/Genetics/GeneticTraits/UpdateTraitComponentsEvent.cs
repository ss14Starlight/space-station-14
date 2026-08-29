namespace Content.Shared._Starlight.Genetics.GeneticTraits;

public sealed class UpdateTraitComponentsEvent(TraitDict traits) : EntityEventArgs
{
    public TraitDict Traits = traits;
}
