using System.Linq;
using Content.Client._Starlight.UserInterface.Controls;
using Content.Client.Stylesheets;
using Content.Shared._Starlight.TextToSpeech;
using Content.Shared.Humanoid;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Prototypes;

namespace Content.Client._Starlight.Lobby.UI;

/// <summary>
/// Picks a text-to-speech voice inside the character editor: search, filters as buttons,
/// and a card per voice with its own preview button, so trying voices doesn't need picking them first.
/// </summary>
public sealed class VoicePicker : BoxContainer
{
    private const int Any = -1;

    private readonly VoiceTagTaxonomy _taxonomy;
    private readonly LineEdit _search;
    private readonly SegmentedButtons _sexFilter = new();
    private readonly SegmentedButtons _pitchFilter = new();
    private readonly WrapContainer _tagFilter = new() { SeparationOverride = 4, CrossSeparationOverride = 4 };
    private readonly Label _shownLabel = new() { StyleClasses = { StyleClass.LabelSubText } };
    private readonly BoxContainer _list = new()
    {
        Orientation = LayoutOrientation.Vertical,
        SeparationOverride = 4,
        Margin = new Thickness(0, 0, 4, 0),
    };

    private readonly List<(VoicePrototype Voice, HashSet<VoiceTag> Tags, ContainerButton Card)> _cards = new();
    private readonly List<(VoiceTag Tag, Button Button)> _tagButtons = new();
    private VoiceTag? _tag;
    private string? _selected;

    public event Action<VoicePrototype>? OnVoiceSelected;
    public event Action<VoicePrototype>? OnPreviewRequested;

    public VoicePicker(List<VoicePrototype> voices, IPrototypeManager prototypes)
    {
        Orientation = LayoutOrientation.Vertical;
        HorizontalExpand = true;
        VerticalExpand = true;
        SeparationOverride = 8;

        _taxonomy = new VoiceTagTaxonomy(voices, prototypes);

        _search = new LineEdit
        {
            PlaceHolder = Loc.GetString("voice-selector-placeholder"),
            HorizontalExpand = true,
            Margin = new Thickness(10, 8),
            StyleClasses = { "TraitsSearchInput" },
        };
        _search.OnTextChanged += _ => ApplyFilter();
        AddChild(new PanelContainer { StyleClasses = { "TraitsSearchBar" }, Children = { _search } });

        _sexFilter.AddItem(Loc.GetString("voice-selector-any"), Any);
        _sexFilter.AddItem(Loc.GetString("voice-selector-gender-masculine"), (int) Sex.Male);
        _sexFilter.AddItem(Loc.GetString("voice-selector-gender-feminine"), (int) Sex.Female);
        _sexFilter.AddItem(Loc.GetString("voice-selector-gender-neutral"), (int) Sex.Unsexed);
        _sexFilter.SelectId(Any);
        _sexFilter.OnItemSelected += _ => ApplyFilter();

        _pitchFilter.AddItem(Loc.GetString("voice-selector-any"), Any);
        foreach (var pitch in Enum.GetValues<VoicePitch>())
            _pitchFilter.AddItem(Loc.GetString($"voice-selector-pitch-{pitch.ToString().ToLowerInvariant()}"), (int) pitch);
        _pitchFilter.SelectId(Any);
        _pitchFilter.OnItemSelected += _ => ApplyFilter();

        AddChild(new BoxContainer
        {
            Orientation = LayoutOrientation.Horizontal,
            SeparationOverride = 12,
            Children =
            {
                _sexFilter,
                _pitchFilter,
                new Control { HorizontalExpand = true },
                _shownLabel,
            },
        });

        // Tags as chips: one at a time, clicking the lit one again clears it.
        foreach (var tag in _taxonomy.CachedTags)
        {
            var button = new Button
            {
                Text = tag.ToDisplayName(IoCManager.Resolve<ILocalizationManager>()),
                ToggleMode = true,
                StyleClasses = { StyleClass.ButtonSmall },
            };
            button.OnPressed += _ =>
            {
                _tag = _tag.Equals(tag) ? (VoiceTag?) null : tag;
                foreach (var (other, otherButton) in _tagButtons)
                    otherButton.Pressed = _tag.Equals(other);
                ApplyFilter();
            };
            _tagButtons.Add((tag, button));
            _tagFilter.AddChild(button);
        }
        // In a scroll container: a bare wrap container asks for all the width offered and squeezes the side preview away.
        AddChild(new ScrollContainer
        {
            HScrollEnabled = false,
            SetHeight = 64,
            Visible = _tagButtons.Count > 0,
            Children = { _tagFilter },
        });

        AddChild(new ScrollContainer
        {
            VerticalExpand = true,
            HScrollEnabled = false,
            Children = { _list },
        });

        // Built once, the filters only hide cards: there can be hundreds of voices.
        foreach (var voice in voices.OrderBy(v => Loc.GetString(v.Name)))
            AddCard(voice);

        ApplyFilter();
    }

    private void AddCard(VoicePrototype voice)
    {
        var tags = _taxonomy.ResolvePresentingTags(voice);
        var details = new List<string> { SexName(voice.Sex) };
        if (voice.Pitch is { } pitch)
            details.Add(Loc.GetString($"voice-selector-pitch-{pitch.ToString().ToLowerInvariant()}"));
        var loc = IoCManager.Resolve<ILocalizationManager>();
        details.AddRange(tags.OrderBy(t => t.Value).Select(t => t.ToDisplayName(loc)));

        var preview = new Button
        {
            Text = "▶",
            ToolTip = Loc.GetString("voice-selector-preview"),
            VerticalAlignment = VAlignment.Center,
            StyleClasses = { StyleClass.ButtonSquare },
        };
        preview.OnPressed += _ => OnPreviewRequested?.Invoke(voice);

        var card = new ContainerButton
        {
            HorizontalExpand = true,
            StyleClasses = { MarkingCard.StyleClassCard },
            Children =
            {
                new BoxContainer
                {
                    Orientation = LayoutOrientation.Horizontal,
                    Margin = new Thickness(8, 6),
                    SeparationOverride = 10,
                    Children =
                    {
                        preview,
                        new BoxContainer
                        {
                            Orientation = LayoutOrientation.Vertical,
                            HorizontalExpand = true,
                            Children =
                            {
                                new Label { Text = Loc.GetString(voice.Name), StyleClasses = { "TraitsEntryNameLabel" } },
                                new Label { Text = string.Join(" · ", details), StyleClasses = { StyleClass.LabelSubText }, ClipText = true },
                            },
                        },
                    },
                },
            },
        };
        card.OnPressed += _ =>
        {
            Select(voice.ID);
            OnVoiceSelected?.Invoke(voice);
        };

        _cards.Add((voice, tags, card));
        _list.AddChild(card);
    }

    private static string SexName(Sex sex) => Loc.GetString(sex switch
    {
        Sex.Male => "voice-selector-gender-masculine",
        Sex.Female => "voice-selector-gender-feminine",
        _ => "voice-selector-gender-neutral",
    });

    /// <summary>
    /// Highlights the voice the profile uses.
    /// </summary>
    public void Select(string? voiceId)
    {
        _selected = voiceId;
        foreach (var (voice, _, card) in _cards)
        {
            if (voice.ID == voiceId)
                card.AddStyleClass(MarkingCard.StyleClassPicked);
            else
                card.RemoveStyleClass(MarkingCard.StyleClassPicked);
        }
    }

    private void ApplyFilter()
    {
        var search = _search.Text.Trim();
        var sex = _sexFilter.SelectedId;
        var pitch = _pitchFilter.SelectedId;
        var shown = 0;

        foreach (var (voice, tags, card) in _cards)
        {
            var visible =
                (sex == Any || voice.Sex == (Sex) sex) &&
                (pitch == Any || voice.Pitch is { } p && (int) p == pitch) &&
                (_tag == null || tags.Contains(_tag.Value)) &&
                (search.Length == 0 ||
                 Loc.GetString(voice.Name).Contains(search, StringComparison.CurrentCultureIgnoreCase) ||
                 tags.Any(t => t.Value.Contains(search, StringComparison.InvariantCultureIgnoreCase)));

            card.Visible = visible;
            if (visible)
                shown++;
        }

        _shownLabel.Text = Loc.GetString("voice-selector-shown", ("shown", shown), ("total", _cards.Count));
    }
}
