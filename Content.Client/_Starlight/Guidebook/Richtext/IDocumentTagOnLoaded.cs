namespace Content.Client._Starlight.Guidebook.Richtext;

public interface IDocumentTagOnLoaded
{
    /// <summary>
    ///     Invoked by a control after it's done loading its controls.
    ///     Used in cases such as the chemistry guidebook, where there's too many
    ///     controls to load in a single frame.
    /// </summary>
    event Action? OnLoaded;
}
