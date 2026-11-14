using System.Numerics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Input;

namespace Content.Client._Starlight.Lobby.UI;

/// <summary>
/// A square marking preview. Available markings toggle on click, picked ones can also be dragged
/// onto each other to change which is drawn on top, and have a remove button.
/// </summary>
public sealed class MarkingCard : ContainerButton
{
    public const string StyleClassCard = "MarkingCard";
    public const string StyleClassPicked = "MarkingCardPicked";
    public const string StyleClassDragged = "MarkingCardDragged";
    public const string StyleClassDropTarget = "MarkingCardDropTarget";
    public const string StyleClassRemove = "MarkingCardRemove";

    private readonly IMarkingCardDragHost? _dragHost;

    public string MarkingId { get; }

    /// <summary>
    /// Raised by a remove button on a picked card.
    /// </summary>
    public event Action? OnRemove;

    public MarkingCard(string markingId, Control preview, bool picked, IMarkingCardDragHost? dragHost = null)
    {
        MarkingId = markingId;
        _dragHost = dragHost;
        AddStyleClass(StyleClassCard);
        if (picked)
            AddStyleClass(StyleClassPicked);

        // A container button lays every child over its whole area, so alignment is enough to place them.
        preview.MouseFilter = MouseFilterMode.Ignore;
        preview.HorizontalAlignment = HAlignment.Center;
        preview.VerticalAlignment = VAlignment.Center;
        AddChild(preview);

        if (dragHost != null)
        {
            var remove = new Button
            {
                Text = "✕",
                StyleClasses = { StyleClassRemove },
                ToolTip = Loc.GetString("body-editor-remove-marking"),
                HorizontalAlignment = HAlignment.Right,
                VerticalAlignment = VAlignment.Top,
            };
            remove.OnPressed += _ => OnRemove?.Invoke();
            AddChild(remove);
        }
    }

    public void SetDragState(bool dragged, bool dropTarget)
    {
        SetStyleClass(StyleClassDragged, dragged);
        SetStyleClass(StyleClassDropTarget, dropTarget);
    }

    private void SetStyleClass(string styleClass, bool on)
    {
        if (on)
            AddStyleClass(styleClass);
        else
            RemoveStyleClass(styleClass);
    }

    protected override void KeyBindDown(GUIBoundKeyEventArgs args)
    {
        if (_dragHost != null && args.Function == EngineKeyFunctions.UIClick)
            _dragHost.BeginDrag(this);

        base.KeyBindDown(args);
    }

    protected override void KeyBindUp(GUIBoundKeyEventArgs args)
    {
        if (_dragHost != null && args.Function == EngineKeyFunctions.UIClick)
            _dragHost.EndDrag();

        base.KeyBindUp(args);
    }

    protected override void MouseEntered()
    {
        base.MouseEntered();
        _dragHost?.DragOver(this);
    }

    /// <summary>
    /// Scales a 32px sprite to fill as much of a card as whole pixels allow.
    /// </summary>
    public static float ScaleFor(Vector2 cellSize)
        => MathF.Max(1f, MathF.Floor((MathF.Min(cellSize.X, cellSize.Y) - 8f) / 32f));
}

/// <summary>
/// Reorders picked <see cref="MarkingCard"/>s while one is dragged over another.
/// </summary>
public interface IMarkingCardDragHost
{
    void BeginDrag(MarkingCard card);
    void DragOver(MarkingCard card);
    void EndDrag();
}
