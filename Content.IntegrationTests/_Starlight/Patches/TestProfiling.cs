namespace Content.IntegrationTests._Starlight.Patches;

/// <summary>
///     Switch for the per-test timing summaries (<see cref="SystemTimingPatch"/>, <see cref="EventTimingSummaryPatch"/>).
///     They hook every entity event dispatch and export the whole metrics registry around each test,
///     which slows down the entire run, so they only turn on when <c>SS14_TEST_PROFILE=1</c> is set.
/// </summary>
internal static class TestProfiling
{
    internal static readonly bool Enabled = Environment.GetEnvironmentVariable("SS14_TEST_PROFILE") is "1" or "true";
}
