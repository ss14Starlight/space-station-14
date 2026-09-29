using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.GhostTheme;

[Prototype]
public sealed partial class GhostThemeCategoryPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField]
    public string Name { get; private set; } = string.Empty;

    [DataField]
    public int Priority { get; private set; }

    [DataField]
    public bool HideIfNoUnlocks { get; private set; }

    [DataField]
    public bool IsFilter { get; private set; }
}
