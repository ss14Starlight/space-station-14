using Robust.Shared.Audio;

namespace Content.Shared._Starlight.Gnome;

[RegisterComponent]
public sealed partial class GnomeComponent : Component
{
    [DataField]
    public string OutfitName = "GnomeOutfit";

    [DataField]
    public float NewHeight = 0.8f; // fun sized

    [DataField]
    public SoundSpecifier GnomeSound = new SoundPathSpecifier("/Audio/_Starlight/Items/Toys/gnome_hahe.ogg");

    [DataField]
    public string AutoEmote = "GnomeGiggle";
}