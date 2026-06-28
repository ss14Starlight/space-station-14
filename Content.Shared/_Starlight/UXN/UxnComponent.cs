using Robust.Shared.GameStates;

namespace Content.Shared._Starlight.UXN;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class UxnComponent : Component
{
    /// <summary>
    /// what is the text the compiler output.
    /// </summary>
    [DataField, AutoNetworkedField]
    public string CompilerOutput = "";

    /// <summary>
    /// where should the (empty) container slot be created on the chip when it is assembling a program
    /// </summary>
    [DataField]
    public string ContainerId = "";

    /// <summary>
    /// what is the size in bytes of the assembled program.
    /// </summary>
    [DataField, AutoNetworkedField]
    public int AssembledSize = 0;

    [ViewVariables] //if you wanna manually sift through this be my guest
    public List<byte> CompiledRom = new();
}
