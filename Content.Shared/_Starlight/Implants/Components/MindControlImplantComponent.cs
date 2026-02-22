namespace Content.Shared._Starlight.Implants.Components;
﻿
/// <summary>
/// Component for Mind Control implants.
/// </summary>
[RegisterComponent]
public sealed partial class MindControlImplantComponent : Component
{
    /// <summary>
    /// Implants owner
    /// </summary>
    [DataField] 
    public EntityUid Master; 
}