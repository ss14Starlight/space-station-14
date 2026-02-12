using Robust.Shared.GameStates;

namespace Content.Shared._Starlight.UXN;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class UxnComponent : Component
{
    [ViewVariables, AutoNetworkedField]
    public string CompilerOutput = "";
    
    [ViewVariables, AutoNetworkedField]
    public int AssembledSize = 0;

    [ViewVariables] //if you wanna manually sift through this be my guest
    public List<byte> CompiledRom = new();
}