using Robust.Shared.GameStates;

namespace Content.Shared._Starlight.Paper;

/// <summary>
///     Load text into the document from the given text file.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class TextFilePaperContentComponent : Component
{
    /// <summary>
    ///     Name of the file to load in located in the Documents folder in resources.
    /// </summary>
    [DataField(required: true)]
    public string FileName = "";
}
