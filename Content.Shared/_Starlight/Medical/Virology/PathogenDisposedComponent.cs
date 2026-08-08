namespace Content.Shared._Starlight.Medical.Virology;

/// <summary>
/// Marks something the crew has thrown away. Binning rubbish is dealing with it, so a
/// disposed entity stops counting as contamination for good rather than resuming the moment
/// it is dumped onto the disposal room floor.
/// </summary>
[RegisterComponent]
public sealed partial class PathogenDisposedComponent : Component;
