using Content.Shared.Random.Helpers;
using JetBrains.Annotations;

namespace Content.Shared._Starlight.Random;

/// <summary>
/// Stateless, tick-independent rolls: the same inputs always give the same result on both client and server.
/// Use it when an outcome must stay fixed for a given pair of entities,
/// no matter when or how many times it is re-evaluated during prediction.
/// </summary>
/// <remarks>
/// The result is fully predictable from the inputs, do NOT use this for anything sensitive.
/// </remarks>
[PublicAPI]
public static class DeterministicRandom
{
    /// <summary>
    /// Returns a value between 0 (included) and 1 (included) derived from <paramref name="seed"/>.
    /// </summary>
    public static float Roll(int seed)
    {
        var hash = (uint)seed;
        hash ^= hash >> 16;
        hash *= 0x7feb352d;
        hash ^= hash >> 15;
        hash *= 0x846ca68b;
        hash ^= hash >> 16;

        return hash / (float)uint.MaxValue;
    }

    /// <remarks>
    /// Never feed <see cref="HashCode.Combine{T1,T2}"/> into <see cref="Roll(int)"/>: it's seeded randomly per process,
    /// so client and server would roll differently.
    /// </remarks>
    /// <inheritdoc cref="Roll(int)"/>
    public static float Roll(params int[] seeds)
        => Roll(SharedRandomExtensions.HashCodeCombine(seeds));

    /// <summary>
    /// Returns a value between 0 (included) and 1 (included) for a pair of entities.
    /// Order matters: (a, b) and (b, a) give different results.
    /// </summary>
    public static float Roll(NetEntity a, NetEntity b)
        => Roll(a.Id, b.Id);

    /// <summary>
    /// Whether the roll for a pair of entities is below <paramref name="chance"/>.
    /// </summary>
    public static bool Prob(NetEntity a, NetEntity b, float chance)
        => Roll(a, b) < chance;

    /// <summary>
    /// Whether the roll for a list of parameters is below <paramref name="chance"/>.
    /// </summary>
    public static bool Prob(float chance, params int[] seeds)
        => Roll(seeds) < chance;
}
