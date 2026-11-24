using System.Numerics;
using Content.Client._Starlight.UserInterface.Controls;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Prototypes;

namespace Content.Client._Starlight.Lobby.UI;

/// <summary>
/// Picks a species from cards showing how each looks, with a short summary in the tooltip
/// and a button opening its guidebook page.
/// Keeps the <see cref="OptionButton"/> methods the profile editor already calls.
/// </summary>
public sealed class SpeciesPicker : ScrollContainer
{
    private const float CardWidth = 112;
    private const float CardHeight = 132;

    private readonly WrapContainer _cards = new()
    {
        SeparationOverride = 6,
        CrossSeparationOverride = 6,
        Margin = new Thickness(4),
    };
    private readonly Dictionary<int, (ContainerButton Card, EntityPrototypeView View, EntProtoId? Doll)> _items = new();

    public event Action<SegmentSelectedEventArgs>? OnItemSelected;

    /// <summary>
    /// Raised by the question mark of a card, with the id of that card.
    /// </summary>
    public event Action<int>? OnGuidebookRequested;

    public SpeciesPicker()
    {
        HScrollEnabled = false;
        VerticalExpand = true;
        HorizontalExpand = true;
        ReserveScrollbarSpace = true;
        AddChild(_cards);
    }

    // The dolls only exist while the picker is on screen.
    protected override void EnteredTree()
    {
        base.EnteredTree();
        SetDolls(true);
    }

    protected override void ExitedTree()
    {
        base.ExitedTree();
        SetDolls(false);
    }

    private void SetDolls(bool shown)
    {
        foreach (var (_, view, doll) in _items.Values)
            view.SetPrototype(shown ? doll : null);
    }

    public void Clear()
    {
        _cards.RemoveAllChildren();
        _items.Clear();
    }

    /// <param name="tooltip">Short summary of the species, shown on hover.</param>
    /// <param name="hasGuide">Whether the card gets a button opening the guidebook.</param>
    public void AddItem(string name, int id, EntProtoId? doll, string? tooltip = null, bool hasGuide = false)
    {
        var view = new EntityPrototypeView
        {
            Scale = new Vector2(3, 3),
            SetSize = new Vector2(96, 96),
            HorizontalAlignment = HAlignment.Center,
        };

        var card = new ContainerButton
        {
            SetSize = new Vector2(CardWidth, CardHeight),
            ToolTip = tooltip ?? name,
            StyleClasses = { MarkingCard.StyleClassCard },
        };
        card.AddChild(new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            Margin = new Thickness(4),
            Children =
            {
                view,
                new Label { Text = name, ClipText = true, HorizontalAlignment = HAlignment.Center },
            },
        });

        if (hasGuide)
        {
            var guide = new Button
            {
                Text = "?",
                ToolTip = Loc.GetString("humanoid-profile-editor-guidebook-button-tooltip"),
                HorizontalAlignment = HAlignment.Right,
                VerticalAlignment = VAlignment.Top,
                Margin = new Thickness(0, 2, 2, 0),
                StyleClasses = { "ButtonSquare" },
            };
            guide.OnPressed += _ => OnGuidebookRequested?.Invoke(id);
            card.AddChild(guide);
        }

        card.OnPressed += _ => OnItemSelected?.Invoke(new SegmentSelectedEventArgs(id));

        if (IsInsideTree)
            view.SetPrototype(doll);

        _items[id] = (card, view, doll);
        _cards.AddChild(card);
    }

    public void SelectId(int id)
    {
        foreach (var (itemId, (card, _, _)) in _items)
        {
            if (itemId == id)
                card.AddStyleClass(MarkingCard.StyleClassPicked);
            else
                card.RemoveStyleClass(MarkingCard.StyleClassPicked);
        }
    }
}
