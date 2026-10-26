using Content.Shared.Random.Helpers;
using JetBrains.Annotations;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using System.Linq;

namespace Content.Shared._Starlight.Abstract.Extensions;

/// <summary>
/// Starlight copy of: https://github.com/space-wizards/RobustToolbox/pull/5849
/// TODO: Replace with final implementation once finished.
/// </summary>
public static class RandomPredicted
{
    /// <param name="random">The <see cref="IRobustRandom"/> instance.</param>
    extension(IRobustRandom random)
    {
        /// <summary>
        /// Get a predictable <see cref="IRobustRandom"/> instance seeded with the current tick.
        /// This ensures identical random sequences on both client and server.
        /// </summary>
        /// <param name="timing">The <see cref="IGameTiming"/> to use for seeding.</param>
        /// <param name="seed">Additional seed value to mix with the tick for unique sequences.</param>
        /// <remarks>
        /// NOTE: This means the client can guess what the number will be based on the current tick,
        /// do NOT use this for sensitive applications.
        /// </remarks>
        [PublicAPI]
        public IRobustRandom GetPredictedRandom(IGameTiming timing, int seed = 0)
        {
            var tickValue = (int)timing.CurTick.Value;
            var combinedSeed = SharedRandomExtensions.HashCodeCombine(tickValue, seed);
            var predicted = new RobustRandom();
            predicted.SetSeed(combinedSeed);
            return predicted;
        }

        /// <summary>
        /// Get predictable random <see cref="float"/> value between 0 (included) and 1 (excluded).
        /// </summary>
        /// <param name="timing">The <see cref="IGameTiming"/> to use for seeding.</param>
        /// <param name="seed">Additional seed value to mix with the tick for unique sequences.</param>
        /// <inheritdoc cref="GetPredictedRandom" path="/remarks"/>
        [PublicAPI]
        public float NextFloatPredicted(IGameTiming timing, int seed = 0) =>
            random.GetPredictedRandom(timing, seed).NextFloat();

        /// <summary>
        /// Get predictable random <see cref="float"/> value in range of <paramref name="minValue"/> (included) and <paramref name="maxValue"/> (excluded).
        /// </summary>
        /// <param name="timing">The <see cref="IGameTiming"/> to use for seeding.</param>
        /// <param name="minValue">Random value should be greater or equal to this value.</param>
        /// <param name="maxValue">Random value should be less than this value.</param>
        /// <param name="seed">Additional seed value to mix with the tick for unique sequences.</param>
        /// <inheritdoc cref="GetPredictedRandom" path="/remarks"/>
        [PublicAPI]
        public float NextFloatPredicted(IGameTiming timing, float minValue, float maxValue, int seed = 0) =>
            random.GetPredictedRandom(timing, seed).NextFloat(minValue, maxValue);

        /// <summary>
        /// Get predictable random <see cref="float"/> value in range of 0 (included) and <paramref name="maxValue"/> (excluded).
        /// </summary>
        /// <param name="timing">The <see cref="IGameTiming"/> to use for seeding.</param>
        /// <param name="maxValue">Random value should be less than this value.</param>
        /// <param name="seed">Additional seed value to mix with the tick for unique sequences.</param>
        /// <inheritdoc cref="GetPredictedRandom" path="/remarks"/>
        [PublicAPI]
        public float NextFloatPredicted(IGameTiming timing, float maxValue, int seed = 0) =>
            random.GetPredictedRandom(timing, seed).NextFloat(maxValue);

        /// <summary>
        /// Get predictable random <see cref="int"/> value.
        /// </summary>
        /// <param name="timing">The <see cref="IGameTiming"/> to use for seeding.</param>
        /// <param name="seed">Additional seed value to mix with the tick for unique sequences.</param>
        /// <inheritdoc cref="GetPredictedRandom" path="/remarks"/>
        [PublicAPI]
        public int NextPredicted(IGameTiming timing, int seed = 0) => random.GetPredictedRandom(timing, seed).Next();

        /// <summary>
        /// Get predictable random <see cref="int"/> value in range of 0 (included) and <paramref name="maxValue"/> (excluded).
        /// </summary>
        /// <param name="timing">The <see cref="IGameTiming"/> to use for seeding.</param>
        /// <param name="maxValue">Random value should be less than this value.</param>
        /// <param name="seed">Additional seed value to mix with the tick for unique sequences.</param>
        /// <inheritdoc cref="GetPredictedRandom" path="/remarks"/>
        [PublicAPI]
        public int NextPredictedMax(IGameTiming timing, int maxValue, int seed = 0) =>
            random.GetPredictedRandom(timing, seed).Next(maxValue);

        /// <summary>
        /// Get predictable random <see cref="int"/> value in range of <paramref name="minValue"/> (included) and <paramref name="maxValue"/> (excluded).
        /// </summary>
        /// <param name="timing">The <see cref="IGameTiming"/> to use for seeding.</param>
        /// <param name="minValue">Random value should be greater or equal to this value.</param>
        /// <param name="maxValue">Random value should be less than this value.</param>
        /// <param name="seed">Additional seed value to mix with the tick for unique sequences.</param>
        /// <inheritdoc cref="GetPredictedRandom" path="/remarks"/>
        [PublicAPI]
        public int NextPredictedRange(IGameTiming timing, int minValue, int maxValue,
            int seed = 0) => random.GetPredictedRandom(timing, seed).Next(minValue, maxValue);

        /// <summary>
        /// Get predictable random <see cref="double"/> value between 0 (included) and 1 (excluded).
        /// </summary>
        /// <param name="timing">The <see cref="IGameTiming"/> to use for seeding.</param>
        /// <param name="seed">Additional seed value to mix with the tick for unique sequences.</param>
        /// <inheritdoc cref="GetPredictedRandom" path="/remarks"/>
        [PublicAPI]
        public double NextDoublePredicted(IGameTiming timing, int seed = 0) =>
            random.GetPredictedRandom(timing, seed).NextDouble();

        /// <summary>
        /// Get predictable random <see cref="double"/> value in range of <paramref name="minValue"/> (included) and <paramref name="maxValue"/> (excluded).
        /// </summary>
        /// <param name="timing">The <see cref="IGameTiming"/> to use for seeding.</param>
        /// <param name="minValue">Random value should be greater or equal to this value.</param>
        /// <param name="maxValue">Random value should be less than this value.</param>
        /// <param name="seed">Additional seed value to mix with the tick for unique sequences.</param>
        /// <inheritdoc cref="GetPredictedRandom" path="/remarks"/>
        [PublicAPI]
        public double NextDoublePredicted(IGameTiming timing, double minValue, double maxValue, int seed = 0) =>
            random.GetPredictedRandom(timing, seed).NextDouble(minValue, maxValue);

        /// <summary>
        /// Get predictable random <see cref="byte"/> value between 0 (included) and <see cref="byte.MaxValue"/> (excluded).
        /// </summary>
        /// <param name="timing">The <see cref="IGameTiming"/> to use for seeding.</param>
        /// <param name="seed">Additional seed value to mix with the tick for unique sequences.</param>
        /// <inheritdoc cref="GetPredictedRandom" path="/remarks"/>
        [PublicAPI]
        public byte NextBytePredicted(IGameTiming timing, int seed = 0) =>
            (byte)random.GetPredictedRandom(timing, seed).Next(byte.MaxValue);

        /// <summary>
        /// Predictably fill buffer with random bytes (values).
        /// </summary>
        /// <param name="timing">The <see cref="IGameTiming"/> to use for seeding.</param>
        /// <param name="buffer">Buffer to fill with random bytes.</param>
        /// <param name="seed">Additional seed value to mix with the tick for unique sequences.</param>
        /// <inheritdoc cref="GetPredictedRandom" path="/remarks"/>
        [PublicAPI]
        public void NextBytesPredicted(IGameTiming timing, byte[] buffer, int seed = 0) =>
            random.GetPredictedRandom(timing, seed).NextBytes(buffer);

        /// <summary>
        /// Get predictable random <see cref="Angle"/> value in range of 0 (included) and <see cref="MathF.Tau"/> (excluded).
        /// </summary>
        /// <param name="timing">The <see cref="IGameTiming"/> to use for seeding.</param>
        /// <param name="seed">Additional seed value to mix with the tick for unique sequences.</param>
        /// <inheritdoc cref="GetPredictedRandom" path="/remarks"/>
        [PublicAPI]
        public Angle NextAnglePredicted(IGameTiming timing, int seed = 0) =>
            random.GetPredictedRandom(timing, seed).NextAngle();

        /// <summary>
        /// Get predictable random <see cref="Angle"/> value in range of <paramref name="minValue"/> (included) and <paramref name="maxValue"/> (excluded).
        /// </summary>
        /// <param name="timing">The <see cref="IGameTiming"/> to use for seeding.</param>
        /// <param name="minValue">Random value should be greater or equal to this value.</param>
        /// <param name="maxValue">Random value should be less than this value.</param>
        /// <param name="seed">Additional seed value to mix with the tick for unique sequences.</param>
        /// <inheritdoc cref="GetPredictedRandom" path="/remarks"/>
        [PublicAPI]
        public Angle NextAnglePredicted(IGameTiming timing, Angle minValue, Angle maxValue, int seed = 0) =>
            random.GetPredictedRandom(timing, seed).NextAngle(minValue, maxValue);

        /// <summary>
        /// Predictably shuffle a collection using the specified tick as seed.
        /// </summary>
        /// <param name="timing">The <see cref="IGameTiming"/> to use for seeding.</param>
        /// <param name="list">The collection to shuffle.</param>
        /// <param name="seed">Additional seed value to mix with the tick for unique sequences.</param>
        /// <inheritdoc cref="GetPredictedRandom" path="/remarks"/>
        [PublicAPI]
        public void ShufflePredicted<T>(IGameTiming timing, IList<T> list, int seed = 0) =>
            random.GetPredictedRandom(timing, seed).Shuffle(list);

        /// <summary>
        /// Picks a predictable random element from a collection.
        /// </summary>
        /// <param name="timing">The <see cref="IGameTiming"/> to use for seeding.</param>
        /// <param name="list">The collection to pick from.</param>
        /// <param name="seed">Additional seed value to mix with the tick for unique sequences.</param>
        /// <inheritdoc cref="GetPredictedRandom" path="/remarks"/>
        [PublicAPI]
        public T PickPredicted<T>(IGameTiming timing, IReadOnlyList<T> list, int seed = 0)
        {
            var index = random.NextPredictedMax(timing, list.Count, seed);
            return list[index];
        }

        /// <summary>
        /// Have a certain chance to return a boolean.
        /// </summary>
        /// <param name="timing">The <see cref="IGameTiming"/> to use for seeding.</param>
        /// <param name="chance">The chance to pass, from 0 to 1.</param>
        /// <param name="seed">Additional seed value to mix with the tick for unique sequences.</param>
        /// <inheritdoc cref="GetPredictedRandom" path="/remarks"/>
        [PublicAPI]
        public bool ProbPredicted(IGameTiming timing, float chance, int seed = 0) =>
            random.NextDoublePredicted(timing, seed) < chance;
    }
}
