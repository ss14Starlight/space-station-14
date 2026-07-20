using Content.Shared.Polymorph;
using Robust.Shared.Prototypes;

namespace Content.Shared.EntityEffects.Effects;

/// <inheritdoc cref="EntityEffect"/>
public sealed partial class Polymorph : EntityEffectBase<Polymorph>
{
    /// <summary>
    ///     What polymorph prototype is used on effect
    /// </summary>
    [DataField(required: true)]
    public ProtoId<PolymorphPrototype> Prototype;

    public override string? EntityEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys, ILocalizationManager loc) // Starlight
    {
        if (!prototype.TryIndex(Prototype, out var polymorph))
            return null;

        // PolymorphConfiguration.Entity is serverOnly, so clients may not have it.
        var entityId = polymorph.Configuration.Entity;
        var entityName = !string.IsNullOrEmpty(entityId.Id) && prototype.TryIndex(entityId, out EntityPrototype? entity)
            ? entity.Name
            : Prototype.Id;

        return loc.GetString("entity-effect-guidebook-make-polymorph",
            ("chance", Probability),
            ("entityname", entityName));
    }
}
