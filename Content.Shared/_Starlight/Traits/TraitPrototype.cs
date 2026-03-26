using Content.Shared._Starlight.Traits.Effects;
using Content.Shared.Roles;
using Content.Shared.Whitelist;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.Traits;

/// <summary>
/// Prototype for a character trait in Starlight.
/// Traits modify character behavior through condition-checked effects.
/// </summary>
[Prototype]
public sealed partial class TraitPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>
    /// Localization key for the trait's display name.
    /// </summary>
    [DataField(required: true)]
    public LocId Name;

    /// <summary>
    /// Localization key for the trait's description.
    /// </summary>
    [DataField(required: true)]
    public LocId Description;

    /// <summary>
    /// The category this trait belongs to.
    /// </summary>
    [DataField(required: true)]
    public ProtoId<TraitCategoryPrototype> Category;

    /// <summary>
    /// How many trait points this trait costs (positive) or grants (negative).
    /// </summary>
    [DataField]
    public int Cost = 1;

    /// <summary>
    /// Conditions that must be met for this trait to be selectable and applied.
    /// All conditions must pass for the trait to be valid.
    /// </summary>
    [DataField]
    public HashSet<JobRequirement>? Requirements;

    /// <summary>
    /// Effects to apply to the entity when this trait is selected.
    /// Effects are applied in order.
    /// </summary>
    [DataField]
    public List<BaseTraitEffect> Effects = new();

    /// <summary>
    /// Other traits that are mutually exclusive with this one.
    /// </summary>
    [DataField]
    public List<ProtoId<TraitPrototype>> Conflicts = new();

    /// <summary>
    /// Don't apply this trait to entities this whitelist IS NOT valid for.
    /// </summary>
    [DataField]
    public EntityWhitelist? Whitelist;

    /// <summary>
    /// Don't apply this trait to entities this whitelist IS valid for. (hence, a blacklist)
    /// </summary>
    [DataField]
    public EntityWhitelist? Blacklist;
}