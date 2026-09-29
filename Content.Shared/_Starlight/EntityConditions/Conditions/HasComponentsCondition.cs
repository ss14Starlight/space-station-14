using Content.Shared.EntityConditions;
using Content.Shared.Localizations;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.EntityConditions.Conditions;

/// <inheritdoc cref="EntityCondition"/>
public sealed partial class HasComponentsCondition : EntityConditionBase<HasComponentsCondition>
{
    [DataField(required: true)]
    public ComponentRegistry Components = default!;

    [DataField]
    public bool All = false;

    public override string EntityConditionGuidebookText(IPrototypeManager prototype)
    {

        List<String> componentNames = new();

        foreach (var registration in Components)
            componentNames.Add(registration.Key);

        var names = ContentLocalizationManager.FormatListToOr(componentNames);

        return Loc.GetString("entity-condition-guidebook-has-components",
            ("name", names),
            ("shouldhave", !Inverted));
    }
}
