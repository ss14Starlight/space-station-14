namespace Content.Client._Starlight.Construction;

/// <summary>
/// Player written text attached to a comment construction ghost. Never leaves the client.
/// </summary>
[RegisterComponent]
public sealed partial class ConstructionCommentComponent : Component
{
    [ViewVariables]
    public string Text = string.Empty;
}
