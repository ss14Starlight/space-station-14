using System.Numerics;
using Content.Shared._Starlight.Computers.TacticalPlanning;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Shared.Graphics.RSI;
using Robust.Shared.Utility;

namespace Content.Client._Starlight.Computers.TacticalPlanning.UI;

/// <summary>Reuses existing RSI states for both the picker and placed map symbols.</summary>
public static class TacticalPlanIconTextures
{
    private static readonly Dictionary<TacticalPlanIcon, Texture> Cached = new();

    /// <summary>Loads each approved icon once; map drawing only reads cached textures.</summary>
    public static Texture Get(TacticalPlanIcon icon)
    {
        if (Cached.TryGetValue(icon, out var texture))
            return texture;

        var (path, state) = icon switch
        {
            TacticalPlanIcon.Nuke => ("Objects/Devices/nuke.rsi", "nuclearbomb_deployed"),
            TacticalPlanIcon.Disk => ("Objects/Misc/nukedisk.rsi", "icon"),
            TacticalPlanIcon.Captain => ("Interface/Misc/job_icons.rsi", "Captain"),
            TacticalPlanIcon.Clown => ("Interface/Misc/job_icons.rsi", "Clown"),
            TacticalPlanIcon.C4 => ("Objects/Weapons/Bombs/c4.rsi", "icon"),
            TacticalPlanIcon.Telecomms => ("Structures/Wallmounts/signs.rsi", "telecoms"),
            TacticalPlanIcon.Power => ("Objects/Misc/books.rsi", "icon_lightning"),
            TacticalPlanIcon.Biohazard => ("Effects/crayondecals.rsi", "biohazard"),
            _ => throw new ArgumentOutOfRangeException(nameof(icon)),
        };

        var resource = IoCManager.Resolve<IResourceCache>()
            .GetResource<RSIResource>(new ResPath($"/Textures/{path}"));
        if (!resource.RSI.TryGetState(state, out var rsiState))
            throw new InvalidOperationException($"Missing tactical planning icon {state} in {path}");

        texture = rsiState.GetFrame(RsiDirection.South, 0);
        Cached.Add(icon, texture);
        return texture;
    }

    /// <summary>Item sprites leave wide transparent margins; crop those for legible map markers.</summary>
    public static UIBox2? GetMapRegion(TacticalPlanIcon icon) => icon switch
    {
        TacticalPlanIcon.Disk or TacticalPlanIcon.C4 => UIBox2.FromDimensions(new Vector2(8f), new Vector2(16f)),
        TacticalPlanIcon.Power => UIBox2.FromDimensions(new Vector2(8f), new Vector2(16f)),
        TacticalPlanIcon.Biohazard => UIBox2.FromDimensions(new Vector2(4f), new Vector2(24f)),
        _ => null,
    };
}
