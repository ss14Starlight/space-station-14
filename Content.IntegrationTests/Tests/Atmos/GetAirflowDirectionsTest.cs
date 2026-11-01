using Content.Server.Atmos.EntitySystems;
using Content.Shared.Atmos;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Utility;

namespace Content.IntegrationTests.Tests.Atmos;

/// <summary>
/// Class for testing some airflow retrieval API methods.
/// </summary>
public sealed class GetAirflowDirectionsTest : AtmosTest
{
    // i will keep using this test map until it has been drained
    // of all use
    protected override ResPath? TestMapPath => new("Maps/Test/Atmospherics/DeltaPressure/deltapressuretest.yml");

    [Test]
    [TestCase(0, 0, AtmosDirection.All, false)] // Starlight
    [TestCase(0, 1, AtmosDirection.South, false)] // Starlight
    [TestCase(0, -1, AtmosDirection.North, false)] // Starlight
    [TestCase(1, 0, AtmosDirection.West, false)] // Starlight
    [TestCase(-1, 0, AtmosDirection.East, false)] // Starlight
    [TestCase(1, 1, AtmosDirection.Invalid, false)] // Starlight
    [TestCase(100, 100, AtmosDirection.Invalid, false)] // Starlight
    [TestCase(0, 0, AtmosDirection.Invalid, true)] // Starlight
    public async Task TestLookup(int x, int y, AtmosDirection expectedDirections, bool invalidEntity) // Starlight
        => await Server.WaitPost(delegate
        {
            // Starlight - start
            var coords = new Vector2i(x, y);
            var directions = SAtmos.GetAirflowDirections(invalidEntity ? EntityUid.Invalid : RelevantAtmos, coords);
            // Starlight - end
            Assert.That(directions, Is.EqualTo(expectedDirections));
        });
}
