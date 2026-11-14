namespace Content.Shared.Fax.Components;

public sealed partial class FaxableObjectComponent
{

    /// <summary>
    /// Starlight-edit
    /// The text to output when faxing this object. Must be Document name
    /// </summary>
    [DataField, AutoNetworkedField] public string OutputtingText = "";

    /// <summary>
    /// Starlight-edit
    /// The achievement to give when faxing this object.
    /// </summary>
    [DataField, AutoNetworkedField] public string Achievement = "";

}
