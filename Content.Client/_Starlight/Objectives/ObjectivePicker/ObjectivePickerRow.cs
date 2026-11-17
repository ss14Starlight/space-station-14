using System.Numerics;
using Robust.Shared.Maths;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Content.Shared._Starlight.Objectives.ObjectivePicker;

namespace Content.Client._Starlight.Objectives.ObjectivePicker;

/// <summary>
/// Reserves the difficulty column before measuring the wrapped objective text.
/// </summary>
public sealed class ObjectivePickerRow : Control
{
    private const float IconWidth = 32;
    private const float TitleLeft = IconWidth + 6;
    private const float ColumnGap = 8;
    private const float DifficultyWidth = 90;
    private readonly TextureRect _icon;
    public RichTextLabel ObjectiveLabel { get; }
    public Label DifficultyLabel { get; }

    public ObjectivePickerRow(Texture? icon, string title, float difficulty)
    {
        HorizontalExpand = true;
        MouseFilter = MouseFilterMode.Ignore;
        _icon = new TextureRect { Texture = icon, SetSize = new Vector2(IconWidth, IconWidth) };
        ObjectiveLabel = new RichTextLabel { Text = title, RectClipContent = true };
        DifficultyLabel = new Label
        {
            Text = ObjectiveDifficultyDisplay.Format(difficulty),
            Align = Label.AlignMode.Right,
            ClipText = true,
        };
        AddChild(_icon);
        AddChild(ObjectiveLabel);
        AddChild(DifficultyLabel);
    }

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        _icon.Measure(new Vector2(IconWidth, availableSize.Y));
        DifficultyLabel.Measure(new Vector2(DifficultyWidth, availableSize.Y));
        var titleWidth = Math.Max(0, availableSize.X - TitleLeft - ColumnGap - DifficultyWidth);
        ObjectiveLabel.Measure(new Vector2(titleWidth, availableSize.Y));
        return new Vector2(TitleLeft + ObjectiveLabel.DesiredSize.X + ColumnGap + DifficultyWidth,
            Math.Max(IconWidth, Math.Max(ObjectiveLabel.DesiredSize.Y, DifficultyLabel.DesiredSize.Y)));
    }

    protected override Vector2 ArrangeOverride(Vector2 finalSize)
    {
        var difficultyLeft = Math.Max(TitleLeft + ColumnGap, finalSize.X - DifficultyWidth);
        _icon.Arrange(UIBox2.FromDimensions(Vector2.Zero, new Vector2(IconWidth, finalSize.Y)));
        ObjectiveLabel.Arrange(UIBox2.FromDimensions(new Vector2(TitleLeft, 0),
            new Vector2(Math.Max(0, difficultyLeft - ColumnGap - TitleLeft), finalSize.Y)));
        DifficultyLabel.Arrange(UIBox2.FromDimensions(new Vector2(difficultyLeft, 0),
            new Vector2(Math.Max(0, finalSize.X - difficultyLeft), finalSize.Y)));
        return finalSize;
    }
}
