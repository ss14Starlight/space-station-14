namespace Content.Server._Starlight.InternalMarkers.Components;

/// <summary>
///     The intent of this component is to track objects and it's data for script purposes.
///     Because Toolshed is server-side only, this only has to exist here and doesn't need to be networked.
/// </summary>
[RegisterComponent]
public sealed partial class ToolshedMarkerComponent : Component
{
    /// <summary>
    ///     Named ID of our marker component
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public String Id = "";

    /// <summary>
    ///     The data it will be storing, if any
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public Dictionary<String, String> data = [];
}
