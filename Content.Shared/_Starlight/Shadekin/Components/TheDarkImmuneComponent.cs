namespace Content.Shared._Starlight.Shadekin;

/// <summary>
/// Protect the Ent or Wearer of the Ent from suffering from "The Dark" effect.
/// </summary>
[RegisterComponent]
public sealed partial class TheDarkImmuneComponent : Component
{
    [DataField]
    public bool Ranged = false;
}
