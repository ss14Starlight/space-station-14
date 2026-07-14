namespace Content.Server._Sol.Traits.Assorted;

/// <summary>
///     Removes the entity's innate species language(s), keeping the station common tongue
///     (and Machine for synthetics). Deferred until after other trait language effects apply
///     so ethnicity mother tongues are also cleared.
/// </summary>
[RegisterComponent]
public sealed partial class AdoptedOrphanTraitComponent : Component;
