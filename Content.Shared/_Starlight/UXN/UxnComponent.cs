using Robust.Shared.Utility;

namespace Content.Shared._Starlight.UXN;

[RegisterComponent]
public sealed partial class UxnComponent : Component
{
    [ViewVariables]
    public string CompilerOutput = "";
    
    [ViewVariables]
    public int AssembledSize = 0;
}