using Content.Client.UserInterface.Controls;
using Robust.Shared.Utility;

// ReSharper disable once CheckNamespace
namespace Content.Client.RCD;

public sealed partial class RCDMenuBoundUserInterface
{
    private const string RpdTextures = "/Textures/_Starlight/Interface/Radial/RPD/";

    private static readonly Dictionary<string, string> CategoryGroups = new()
    {
        ["TransitTubes"] = "Transit",
        ["TransitJunctions"] = "Transit",
        ["TransitStations"] = "Transit",
        ["TransitControl"] = "Transit",
        ["TransitLevels"] = "Transit",
    };

    private static readonly Dictionary<string, (string Tooltip, SpriteSpecifier Sprite)> GroupInfo = new()
    {
        ["Transit"] = ("rpd-component-transit", Texture("transit_station.png")),
    };

    private static SpriteSpecifier Texture(string file)
        => new SpriteSpecifier.Texture(new ResPath(RpdTextures + file));

    private static List<RadialMenuOptionBase> GroupCategories(List<(string Category, RadialMenuOptionBase Option)> categories)
    {
        var result = new List<RadialMenuOptionBase>();
        var groups = new Dictionary<string, List<RadialMenuOptionBase>>();

        foreach (var (category, option) in categories)
        {
            if (!CategoryGroups.TryGetValue(category, out var group))
            {
                result.Add(option);
                continue;
            }

            if (!groups.TryGetValue(group, out var list))
            {
                list = [];
                groups.Add(group, list);
            }

            list.Add(option);
        }

        foreach (var (group, list) in groups)
        {
            // A group with a single category shows that category directly.
            if (list.Count == 1)
            {
                result.Add(list[0]);
                continue;
            }

            var info = GroupInfo[group];
            result.Add(new RadialMenuNestedLayerOption(list)
            {
                IconSpecifier = RadialMenuIconSpecifier.With(info.Sprite),
                ToolTip = Loc.GetString(info.Tooltip),
            });
        }

        return result;
    }
}
