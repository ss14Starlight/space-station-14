using Content.IntegrationTests.Fixtures;
using Robust.Shared.Localization;

namespace Content.IntegrationTests.Tests._Starlight.Localization;

[TestFixture]
public sealed class PluralizationTests : GameTest
{
    [Test]
    [TestCase(3, "cow", "There were 3 cows.")]
    [TestCase(3, "thief", "There were 3 thieves.")]
    [TestCase(3, "carp", "There were 3 carp.")]
    public async Task EORPluralizationTest(int count, string antag, string expected)
    {
        var pair = Pair;
        var server = pair.Server;

        var locMan = server.ResolveDependency<ILocalizationManager>();

        var result = locMan.GetString("objectives-round-end-result", ("count", count), ("agent", antag));

        Assert.That(result, Is.EqualTo(expected));
    }

    [TestCase("mailbox-nanochat-message-one", "You have a package waiting at the engineering mailbox.")]
    [TestCase("mailbox-nanochat-message-many", "You have 5 packages waiting at the engineering mailbox.")]
    public void MailboxNanoChatMessagePluralizationTest(string messageKey, string expected)
    {
        var locMan = Pair.Server.ResolveDependency<ILocalizationManager>();

        var result = locMan.GetString(messageKey,
            ("count", 5),
            ("mailbox", "engineering mailbox"));

        Assert.That(result, Is.EqualTo(expected));
    }
}
