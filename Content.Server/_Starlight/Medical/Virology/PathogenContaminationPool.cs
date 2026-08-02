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

    public IReadOnlyList<PathogenType> GetDominantTypes()
    {
        var maximum = Types.Max(Get);
        if (maximum <= 0f)
            return Array.Empty<PathogenType>();

        return Types
            .Where(type => Math.Abs(Get(type) - maximum) <= EqualityTolerance)
            .ToList();
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
