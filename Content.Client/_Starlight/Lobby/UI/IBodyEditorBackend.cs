using Content.Shared.Humanoid.Markings;

namespace Content.Client._Starlight.Lobby.UI;

/// <summary>
/// The data behind the body tab: the part tree, the markings on each part and the colors.
/// The tab only talks to this, so the storage can change without touching the UI:
/// <see cref="HumanoidBodyEditorBackend"/> edits today's flat appearance, Trauma Med swaps in its body profile.
/// </summary>
public interface IBodyEditorBackend
{
    /// <summary>
    /// Raised after any edit, and when the edited character is replaced.
    /// </summary>
    event Action? Changed;

    BodyEditorPart? Root { get; }

    IReadOnlyList<BodyEditorGroup> GetGroups(BodyEditorPart part);

    /// <summary>
    /// The markings of a group, in render order: later ones are drawn on top.
    /// </summary>
    IReadOnlyList<Marking> GetMarkings(BodyEditorGroup group);

    IReadOnlyList<MarkingPrototype> GetAvailable(BodyEditorGroup group);

    /// <summary>
    /// The colors a marking would get if added now, for its preview.
    /// </summary>
    IReadOnlyList<Color> GetDefaultColors(MarkingPrototype marking);

    /// <summary>
    /// Adds the marking, or removes it if the group already has it. A full group drops its oldest marking.
    /// </summary>
    void ToggleMarking(BodyEditorGroup group, MarkingPrototype marking);

    void RemoveMarking(BodyEditorGroup group, string markingId);

    /// <summary>
    /// Moves a marking to where <paramref name="targetId"/> is, shifting the rest.
    /// </summary>
    void MoveMarking(BodyEditorGroup group, string markingId, string targetId);

    /// <summary>
    /// Colors that matter for a part: the body-wide ones first, then those of its markings.
    /// </summary>
    IReadOnlyList<BodyEditorColor> GetColors(BodyEditorPart part);

    void SetColor(string colorId, Color color);

    void SetGlowing(string colorId, bool glowing);

    /// <summary>
    /// Sprite layer keys the preview looks up on the character sprite, with the part each belongs to.
    /// </summary>
    IEnumerable<(object Key, BodyEditorPart Part)> GetLayers();
}

/// <summary>
/// A clickable piece of the body, such as the head or the left hand.
/// </summary>
public sealed record BodyEditorPart(string Id, LocId Name, IReadOnlyList<BodyEditorPart> Children)
{
    public bool Contains(BodyEditorPart? part)
    {
        if (part == null)
            return false;

        if (part.Id == Id)
            return true;

        foreach (var child in Children)
        {
            if (child.Contains(part))
                return true;
        }

        return false;
    }
}

/// <summary>
/// A kind of marking on a part, such as hair on the head. <see cref="Max"/> is -1 when unlimited.
/// </summary>
public sealed record BodyEditorGroup(string Id, string Name, int Max, int Min);

/// <summary>
/// One color the player can pick. A <see cref="Tone"/> color is picked on a slider instead,
/// for species whose skin only goes along a fixed range.
/// </summary>
public sealed record BodyEditorColor(
    string Id,
    string Label,
    Color Value,
    Func<Color, Color>? Constrain = null,
    BodyEditorTone? Tone = null,
    bool? Glowing = null);

public sealed record BodyEditorTone(Func<Color, float> ToTone, Func<float, Color> FromTone);
