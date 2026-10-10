using System.Numerics;
using Content.Client.Resources;
using Content.Shared._Starlight.UserInterface;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;

namespace Content.Client._Starlight.Stylesheets;

public static class ThemeBox
{
    public static StyleBoxTexture Create(StyleThemeBox decor, IResourceCache resCache)
    {
        var box = new StyleBoxTexture
        {
            Texture = resCache.GetTexture(decor.Texture),
            Mode = decor.Tile ? StyleBoxTexture.StretchMode.Tile : StyleBoxTexture.StretchMode.Stretch,
            TextureScale = new Vector2(decor.Scale, decor.Scale),
            Modulate = decor.Modulate,
        };

        var patch = decor.Patch;
        if (patch.Length >= 4)
        {
            box.SetPatchMargin(StyleBox.Margin.Left, patch[0]);
            box.SetPatchMargin(StyleBox.Margin.Top, patch[1]);
            box.SetPatchMargin(StyleBox.Margin.Right, patch[2]);
            box.SetPatchMargin(StyleBox.Margin.Bottom, patch[3]);
        }
        else if (patch.Length > 0)
        {
            box.SetPatchMargin(StyleBox.Margin.All, patch[0]);
        }

        if (decor.Expand > 0)
            box.SetExpandMargin(StyleBox.Margin.All, decor.Expand);

        if (decor.Content is { } content)
            box.SetContentMarginOverride(StyleBox.Margin.All, content);

        return box;
    }

    public static StyleBox Or(StyleThemeBox? decor, IResourceCache resCache, StyleBox fallback)
    {
        if (decor == null)
            return fallback;

        try
        {
            return Create(decor, resCache);
        }
        catch (Exception e)
        {
            Logger.GetSawmill("style").Error($"Failed to load theme texture {decor.Texture}, falling back to the flat style: {e}");
            return fallback;
        }
    }
}
