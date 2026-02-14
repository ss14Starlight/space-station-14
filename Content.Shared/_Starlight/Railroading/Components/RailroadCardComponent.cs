using Robust.Shared.Utility;
using static Robust.Shared.Utility.SpriteSpecifier;

namespace Content.Shared._Starlight.Railroading;

[RegisterComponent]
public sealed partial class RailroadCardComponent : Component
{
    [DataField(required: true)]
    public LocId Title;

    [DataField(required: true)]
    public LocId Description;

    [DataField(required: true)]
    public string Icon;

    /// <summary>
    /// If true, will show objective is antag Summary, if false its will show in the Card list.
    /// * Note: Its will only show if antag/gamerule AntagSelectionComponent with agentname.
    /// </summary>
    [DataField]
    public bool ShowObjective = false;

    [DataField]
    public Color Color = Color.White;

    [DataField]
    public Color IconColor = Color.White;

    [DataField]
    public Texture? Image; // This thing just for single images, list for random

    [DataField]
    public List<Texture> Images = [];
}
