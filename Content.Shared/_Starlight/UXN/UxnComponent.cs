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

    [DataField] //if you wanna manually sift through this be my guest, not autonetwork because the client has no reason to know the bytes yet, mabey enable it if eg: we add a hex viewer later
    public List<byte> CompiledRom = new();
}
