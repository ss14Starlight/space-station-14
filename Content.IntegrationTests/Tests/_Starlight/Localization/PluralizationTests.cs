using Robust.Shared.Localization;
using NUnit.Framework;

namespace Content.IntegrationTests.Tests._Starlight.Localization;

[TestFixture]
public sealed class PluralizationTests
{
    [Test]
    [TestCase(3, "cow", "There were 3 cows.")]
    [TestCase(3, "thief", "There were 3 thieves.")]
    [TestCase(3, "carp", "There were 3 carp.")]
    public async Task EORPluralizationTest(int count, string antag, string expected)
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        var locMan = server.ResolveDependency<ILocalizationManager>();

        var result = locMan.GetString("objectives-round-end-result", ("count", count), ("agent", antag));

        Assert.That(result, Is.EqualTo(expected));

        await pair.CleanReturnAsync();
    }
}
