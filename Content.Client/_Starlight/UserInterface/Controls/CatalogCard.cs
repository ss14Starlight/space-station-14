using System.Linq;
using Robust.Client.Graphics;
using System.Numerics;
using Content.Client._Starlight.Lobby.UI;
using Content.Client.Stylesheets;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Prototypes;

namespace Content.Client._Starlight.UserInterface.Controls;

public sealed class CatalogCard : ContainerButton
{
    public static readonly Vector2 DefaultSize = new(104, 118);

    private static readonly Color _dimModulate = new(1f, 1f, 1f, 0.35f);

    private readonly BoxContainer _content;
    private readonly BoxContainer _previewSlot = new() { HorizontalAlignment = HAlignment.Center, SetSize = new Vector2(64, 64) };
    private readonly WrapLabel _name = new() { HorizontalExpand = true };
    private readonly BoxContainer _details = new() { Orientation = BoxContainer.LayoutOrientation.Vertical, Visible = false };
    private readonly Label _topLeft = new()
    {
        HorizontalAlignment = HAlignment.Left,
        VerticalAlignment = VAlignment.Top,
        Margin = new Thickness(6, 2, 0, 0),
        Visible = false,
    };
    private readonly Label _topRight = new()
    {
        StyleClasses = { StyleClass.LabelSubText },
        HorizontalAlignment = HAlignment.Right,
        VerticalAlignment = VAlignment.Top,
        Margin = new Thickness(0, 2, 6, 0),
        Visible = false,
    };
    private readonly TextureRect _lock = new()
    {
        TexturePath = "/Textures/Interface/VerbIcons/lock.svg.192dpi.png",
        SetSize = new Vector2(18, 18),
        Stretch = TextureRect.StretchMode.KeepAspectCentered,
        HorizontalAlignment = HAlignment.Right,
        VerticalAlignment = VAlignment.Top,
        Margin = new Thickness(0, 4, 4, 0),
        Visible = false,
    };

    public CatalogCard(string name, Vector2? size = null)
    {
        SetSize = size ?? DefaultSize;
        AddStyleClass(MarkingCard.StyleClassCard);
        _name.FullText = name;
        _content = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            Margin = new Thickness(4),
            SeparationOverride = 2,
            Children = { _previewSlot, _name, _details },
        };
        AddChild(_content);
        AddChild(_topLeft);
        AddChild(_topRight);
        AddChild(_lock);
    }

    public string Title
    {
        get => _name.FullText;
        set => _name.FullText = value;
    }

    /// <summary>
    /// Extra text the search matches besides the title, such as a description.
    /// </summary>
    public string SearchText { get; set; } = string.Empty;

    public void SetPreview(Control preview)
    {
        _previewSlot.RemoveAllChildren();
        preview.HorizontalAlignment = HAlignment.Center;
        preview.VerticalAlignment = VAlignment.Center;
        preview.HorizontalExpand = true;
        _previewSlot.AddChild(preview);
    }

    public EntityPrototypeView SetPreview(EntProtoId? prototype)
    {
        var view = new EntityPrototypeView { Scale = new Vector2(2, 2), SetSize = new Vector2(64, 64) };
        view.SetPrototype(prototype);
        SetPreview(view);
        return view;
    }

    public void SetDetails(params (string Text, Color? Color)[] lines)
        => SetDetails(lines, null);

    /// <summary>
    /// Shows small single-line texts under the title. The suffix goes at the end of the last line
    /// and stays readable when that line has to be shortened.
    /// </summary>
    public void SetDetails(IReadOnlyList<(string Text, Color? Color)> lines, string? suffix)
    {
        _details.RemoveAllChildren();
        _details.Visible = lines.Count > 0;
        for (var i = 0; i < lines.Count; i++)
        {
            var (text, color) = lines[i];
            _details.AddChild(new WrapLabel
            {
                FullText = text,
                Suffix = i == lines.Count - 1 ? suffix : null,
                MaxLines = 1,
                HorizontalExpand = true,
                StyleClasses = { StyleClass.LabelSubText },
                FontColorOverride = color,
            });
        }
    }

    public void SetTopLeft(string? text)
    {
        _topLeft.Visible = text != null;
        _topLeft.Text = text;
    }

    public void SetTopRight(string? text)
    {
        _topRight.Visible = text != null;
        _topRight.Text = text;
    }

    public void SetLocked(bool locked, bool showLock = true)
    {
        Disabled = locked;
        _content.Modulate = locked ? _dimModulate : Color.White;
        _lock.Visible = locked && showLock;
    }

    public bool Picked
    {
        get => HasStyleClass(MarkingCard.StyleClassPicked);
        set
        {
            if (value)
                AddStyleClass(MarkingCard.StyleClassPicked);
            else
                RemoveStyleClass(MarkingCard.StyleClassPicked);
        }
    }

    public bool Matches(string search)
        => search.Length == 0
        || Title.Contains(search, StringComparison.CurrentCultureIgnoreCase)
        || SearchText.Contains(search, StringComparison.CurrentCultureIgnoreCase);
}

public sealed class FullRowPanel : PanelContainer
{
    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        var size = base.MeasureOverride(availableSize);
        return new Vector2(MathF.Max(size.X, availableSize.X), size.Y);
    }
}

public sealed class WrapLabel : Label
{
    private float _wrappedFor = -1;

    public int MaxLines { get; set; } = 2;

    public string FullText
    {
        get;
        set
        {
            field = value;
            _wrappedFor = -1;
            Text = WithSuffix(value);
        }
    } = string.Empty;

    public string? Suffix
    {
        get;
        set
        {
            field = string.IsNullOrEmpty(value) ? null : value;
            _wrappedFor = -1;
            Text = WithSuffix(FullText);
        }
    }

    private string WithSuffix(string text)
        => Suffix == null ? text : string.Concat(text, " ", Suffix);

    public WrapLabel()
    {
        RectClipContent = true;
        Align = AlignMode.Center;
    }

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        if (float.IsFinite(availableSize.X) && availableSize.X > 0 && !MathHelper.CloseTo(availableSize.X, _wrappedFor))
        {
            _wrappedFor = availableSize.X;
            var wrapped = Wrap(availableSize.X);
            if (wrapped != Text)
                Text = wrapped;
        }

        var size = base.MeasureOverride(availableSize);
        return new Vector2(0, size.Y);
    }

    private string Wrap(float width)
    {
        var font = FontOverride ?? (TryGetStyleProperty<Font>(StylePropertyFont, out var styled) ? styled : UserInterfaceManager.ThemeDefaults.DefaultFont);

        var lines = new List<string>();
        var line = string.Empty;
        foreach (var word in FullText.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var candidate = line.Length == 0 ? word : string.Concat(line, " ", word);
            if (line.Length == 0 || TextWidth(font, candidate) <= width)
            {
                line = candidate;
                continue;
            }

            lines.Add(line);
            line = word;
        }

        if (line.Length > 0)
            lines.Add(line);

        if (lines.Count > MaxLines)
        {
            var rest = string.Join(" ", lines.Skip(MaxLines - 1));
            lines.RemoveRange(MaxLines - 1, lines.Count - MaxLines + 1);
            lines.Add(Ellipsize(font, rest, width));
        }

        for (var i = 0; i < lines.Count; i++)
        {
            if (TextWidth(font, lines[i]) > width)
                lines[i] = Ellipsize(font, lines[i], width);
        }

        if (Suffix != null)
        {
            if (lines.Count == 0)
                lines.Add(string.Empty);

            var tail = lines[^1].Length == 0 ? Suffix : string.Concat(" ", Suffix);
            var room = MathF.Max(0, width - TextWidth(font, tail));
            if (TextWidth(font, lines[^1]) > room)
                lines[^1] = Ellipsize(font, lines[^1], room);
            lines[^1] = string.Concat(lines[^1], tail);
        }

        return string.Join("\n", lines);
    }

    private string Ellipsize(Font font, string text, float width)
    {
        const string Ellipsis = "...";
        if (TextWidth(font, text) <= width)
            return text;

        var cut = text.Length;
        while (cut > 0 && TextWidth(font, string.Concat(text.AsSpan(0, cut).TrimEnd(), Ellipsis)) > width)
            cut--;
        return string.Concat(text.AsSpan(0, cut).TrimEnd(), Ellipsis);
    }

    private float TextWidth(Font font, string text)
    {
        var total = 0f;
        foreach (var rune in text.EnumerateRunes())
        {
            if (font.GetCharMetrics(rune, UIScale) is { } metrics)
                total += metrics.Advance;
        }

        return total / UIScale;
    }
}
