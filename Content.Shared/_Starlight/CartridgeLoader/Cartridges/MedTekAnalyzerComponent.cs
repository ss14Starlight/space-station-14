using Robust.Shared.GameStates;

namespace Content.Shared._Starlight.CartridgeLoader.Cartridges;

[RegisterComponent, NetworkedComponent]
public sealed partial class MedTekAnalyzerComponent : Component
{
    [DataField]
    public LocId VerbText = "med-tek-analyze-verb-name";

    [DataField]
    public int VerbPriority = 2;
}
