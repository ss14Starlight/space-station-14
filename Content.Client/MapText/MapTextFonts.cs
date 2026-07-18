using Content.Shared.MapText;
using Robust.Shared.Utility;

namespace Content.Client.MapText;

/// <summary>
/// Maps map-text font ids to resource paths without relying on font prototypes.
/// </summary>
internal static class MapTextFonts
{
    private static readonly Dictionary<string, ResPath> FontPaths = new()
    {
        [SharedMapTextComponent.DefaultFont] = new("/Fonts/NotoSans/NotoSans-Regular.ttf"),
        ["DefaultItalic"] = new("/Fonts/NotoSans/NotoSans-Italic.ttf"),
        ["DefaultBold"] = new("/Fonts/NotoSans/NotoSans-Bold.ttf"),
        ["DefaultBoldItalic"] = new("/Fonts/NotoSans/NotoSans-BoldItalic.ttf"),
        ["NotoSansDisplay"] = new("/Fonts/NotoSansDisplay/NotoSansDisplay-Regular.ttf"),
        ["NotoSansDisplayItalic"] = new("/Fonts/NotoSansDisplay/NotoSansDisplay-Italic.ttf"),
        ["NotoSansDisplayBold"] = new("/Fonts/NotoSansDisplay/NotoSansDisplay-Bold.ttf"),
        ["NotoSansDisplayBoldItalic"] = new("/Fonts/NotoSansDisplay/NotoSansDisplay-BoldItalic.ttf"),
        ["BoxRound"] = new("/Fonts/Boxfont-round/Boxfont Round.ttf"),
        ["AnimalSilence"] = new("/Fonts/Animal Silence.otf"),
        ["Monospace"] = new("/EngineFonts/NotoSans/NotoSansMono-Regular.ttf"),
        ["Emoji"] = new("/Fonts/NotoEmoji.ttf"),
    };

    public static ResPath DefaultPath => FontPaths[SharedMapTextComponent.DefaultFont];

    public static bool TryGetPath(string fontId, out ResPath path) => FontPaths.TryGetValue(fontId, out path);
}
