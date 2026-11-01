using Robust.Shared.Audio;
using Robust.Shared.GameStates;

namespace Content.Shared._Starlight.StationRadio.Components;

[RegisterComponent, NetworkedComponent]
public sealed partial class VinylComponent : Component
{
    /// <summary>
    /// What song should be played when the vinyl is played
    /// </summary>
    [DataField] public SoundPathSpecifier? Song;

    /// <summary>
    /// Color of the disc label, used to tint the disc shown on the player.
    /// </summary>
    [DataField] public Color LabelColor = Color.White;

    /// <summary>
    /// Cover pattern of the sleeve or case holding this disc. Picked from the prototype id when unset.
    /// </summary>
    [DataField] public int? CoverPattern;

    /// <summary>
    /// Main cover colour. Derived from <see cref="LabelColor"/> when unset.
    /// </summary>
    [DataField] public Color? CoverColor;

    /// <summary>
    /// Colour of the cover pattern. Contrasts with the main colour when unset.
    /// </summary>
    [DataField] public Color? CoverAccent;
}
