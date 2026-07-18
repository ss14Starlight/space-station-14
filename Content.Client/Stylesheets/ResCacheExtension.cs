using Content.Client.Resources;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;

namespace Content.Client.Stylesheets;

public static class ResCacheExtension
{
    public static Font NotoStack(this IResourceCache resCache, string variation = "Regular", int size = 10, bool display = false)
    {
        var ds = display ? "Display" : "";
        var sv = variation.StartsWith("Bold", StringComparison.Ordinal) ? "Bold" : "Regular";
        return resCache.GetFont
        (
            // Ew, but ok
            new[]
            {
                $"/Fonts/NotoSans{ds}/NotoSans{ds}-{variation}.ttf",
                $"/Fonts/NotoSans/NotoSansSymbols-{sv}.ttf",
                "/Fonts/NotoSans/NotoSansSymbols2-Regular.ttf"
            },
            size
        );
    }
}
