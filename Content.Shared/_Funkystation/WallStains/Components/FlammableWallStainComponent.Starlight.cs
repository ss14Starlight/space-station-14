namespace Content.Shared._Funkystation.WallStains.Components;

public sealed partial class FlammableWallStainComponent : Component
{
    [ViewVariables]
    public bool SelfOxidizing { get; set; }

    [ViewVariables]
    public bool NeedsSpread { get; set; }
}
