using Content.Shared.Construction.Prototypes;

// ReSharper disable CheckNamespace
namespace Content.Client.Construction;

public sealed partial class ConstructionPlacementHijack
{
    #region Starlight

    public bool IsQuickConstruction { get; private set; }

    public ConstructionPlacementHijack(
        ConstructionSystem constructionSystem,
        ConstructionPrototype? prototype,
        bool isQuickConstruction)
        : this(constructionSystem, prototype)
        => IsQuickConstruction = isQuickConstruction;

    #endregion
}
