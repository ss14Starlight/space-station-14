using System.Collections.Generic;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

// ReSharper disable once CheckNamespace
namespace Content.MapRenderer;


[JsonConverter(typeof(JsonStringEnumConverter<MapViewerType>))]
public enum MapViewerType
{
    [JsonStringEnumMemberName("station")]
    Station,

    [JsonStringEnumMemberName("shuttle")]
    Shuttle,
}

public sealed class ViewerMap
{
    public string MapId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public MapViewerType Type { get; set; }
    public string? Attribution { get; set; }
    public List<ViewerGrid> Grids { get; set; } = new();
    public List<ViewerParallaxGroup> ParallaxLayers { get; set; } = new();

    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true,
        IndentSize = 4,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };
}

public sealed class ViewerGrid
{
    public string GridId { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public bool Tiled { get; set; } = true;
    public int TileSize { get; set; }

    /// <summary>
    /// Top left corner of the grid image, in pixels with y pointing down.
    /// </summary>
    public ViewerPoint Offset { get; set; }

    public ViewerExtent Extent { get; set; }

    /// <summary>
    /// Tiles are loaded from <c>{url}/{x}/{y}/0</c>.
    /// </summary>
    public string Url { get; set; } = string.Empty;
}

public sealed class ViewerParallaxGroup
{
    public ViewerPoint Scale { get; set; } = new(1, 1);
    public ViewerPoint Offset { get; set; } = new(0, 0);
    public bool Static { get; set; }
    public float? MinScale { get; set; }
    public List<ViewerParallaxLayer> Layers { get; set; } = new();
}

public sealed class ViewerParallaxLayer
{
    public string Url { get; set; } = string.Empty;
    public string Composition { get; set; } = "source-over";

    /// <summary>
    /// How far the layer moves per pixel of camera movement, 0 keeps it fixed to the screen.
    /// </summary>
    public ViewerPoint ParallaxScale { get; set; } = new(0, 0);
}

public readonly record struct ViewerPoint(float X, float Y);

public readonly record struct ViewerExtent(ViewerPoint A, ViewerPoint B);

public sealed class ViewerIndexEntry
{
    public string MapId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public MapViewerType Type { get; set; }
}
