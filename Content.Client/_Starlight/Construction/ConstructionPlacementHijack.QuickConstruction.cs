using Content.Shared.Construction.Prototypes;

// ReSharper disable CheckNamespace
namespace Content.Client.Construction;

public sealed partial class ConstructionPlacementHijack
{
    public bool IsQuickConstruction { get; private set; }

    public ConstructionPlacementHijack(
        ConstructionSystem constructionSystem,
        ConstructionPrototype? prototype,
        bool isQuickConstruction)
        : this(constructionSystem, prototype)
        => IsQuickConstruction = isQuickConstruction;
}
