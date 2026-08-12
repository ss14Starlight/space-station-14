using System.Linq;
using Content.Shared._Starlight.Medical.Virology;

namespace Content.Server._Starlight.Medical.Virology;

/// <summary>
/// Stores a bounded snapshot of the station's current pathogen-type source load.
/// </summary>
internal sealed class PathogenContaminationPool
{
    private const float Maximum = 100f;
    private const float EqualityTolerance = 0.0001f;

    private static readonly PathogenType[] Types = Enum.GetValues<PathogenType>();
    private readonly Dictionary<PathogenType, float> _byType = Types.ToDictionary(type => type, _ => 0f);

    public float Total { get; private set; }

    public float Get(PathogenType type)
        => _byType[type];

    /// <summary>
    /// Updates the contamination pool from raw per-type source contributions.
    /// Values are normalized into a bounded 0-100 pool.
    /// </summary>
    public void Set(IReadOnlyDictionary<PathogenType, float> contributions)
    {
        var requested = Types.Sum(type =>
            contributions.TryGetValue(type, out var amount)
                ? Math.Max(0f, amount)
                : 0f);

        var scale = requested > Maximum
            ? Maximum / requested
            : 1f;
        foreach (var type in Types)
        {
            _byType[type] = contributions.TryGetValue(type, out var amount)
                ? Math.Max(0f, amount) * scale
                : 0f;
        }

        Total = Math.Min(Maximum, requested);
    }

    /// <summary>
    /// Which pathogen types are currently in the lead, or nothing while the station is clean.
    ///
    /// Several can lead at once, and that is the ordinary case rather than an edge case: rot
    /// and mold both feed bacteria and fungus in equal measure, so a station dirtied by
    /// either has the two exactly level. Returning a single winner would settle every one of
    /// those ties the same way, and the type that always lost would never be picked.
    /// </summary>
    public IReadOnlyList<PathogenType> GetDominantTypes()
    {
        var maximum = 0f;
        foreach (var type in Types)
            maximum = Math.Max(maximum, Get(type));

        var leaders = new List<PathogenType>();
        if (maximum <= 0f)
            return leaders;

        foreach (var type in Types)
        {
            // Compared against a tolerance rather than ==, because these values arrive after
            // a divide and a rescale. Two that should match can differ in the last decimal,
            // and an undetected tie is what would bias the pick.
            if (Get(type) >= maximum - EqualityTolerance)
                leaders.Add(type);
        }

        return leaders;
    }

    public void Reset()
    {
        foreach (var type in Types)
        {
            _byType[type] = 0f;
        }

        Total = 0f;
    }
}
