using Content.Client._Starlight.Guidebook;
using NUnit.Framework;

namespace Content.Tests.Client.Guidebook;

/// <summary>
///     Checks title and alias matching independently of the guidebook UI.
/// </summary>
[TestFixture]
[TestOf(typeof(GuidebookSearchEntry))]
public sealed class GuidebookSearchEntryTests
{
    /// <summary>
    ///     Sample entry with alternative law names for matching tests.
    /// </summary>
    private static GuidebookSearchEntry CorporateLaw => new("Corporate Law", new[] { "Space Law", "Legal Code" });

    /// <summary>
    ///     Verifies that aliases, joined names, and reordered words can find an entry.
    /// </summary>
    [TestCase("corporate law")]
    [TestCase("CORPORATELAW")]
    [TestCase("space law")]
    [TestCase("spacelaw")]
    [TestCase("space-law")]
    [TestCase("law corporate")]
    [TestCase("legal law")]
    [TestCase(" law\tcorporate ")]
    [TestCase("")]
    public void FindsTitlesAndAliases(string query)
    {
        Assert.That(CorporateLaw.Matches(query), Is.True);
    }

    /// <summary>
    ///     Rejects queries containing words absent from the entry and its aliases.
    /// </summary>
    [TestCase("engineering")]
    [TestCase("space law handbook")]
    public void RequiresAllSearchWords(string query)
    {
        Assert.That(CorporateLaw.Matches(query), Is.False);
    }
}
