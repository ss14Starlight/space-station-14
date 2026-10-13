using Content.Server._Starlight.TextToSpeech;
using NUnit.Framework;

namespace Content.Tests.Server.TextToSpeech;

/// <summary>
/// Unit tests verifying the text cleaning and normalization behavior in TTSSystem.
/// </summary>
[TestFixture]
public sealed class TTSTests
{
    [TestCase("0", "zero")]
    [TestCase("999", "nine hundred ninety-nine")]
    [TestCase("1000", "one thousand")]
    [TestCase("12345", "twelve thousand three hundred forty-five")]
    [TestCase("1000000", "one million")]
    [TestCase("1,000", "one thousand")]
    [TestCase("1,234,567", "one million two hundred thirty-four thousand five hundred sixty-seven")]
    [TestCase("-1000", "negative one thousand")]
    [TestCase("-1,000", "negative one thousand")]
    [TestCase("1000.5", "one thousand point five")]
    [TestCase("1,000.5", "one thousand point five")]
    [TestCase("-1000.5", "negative one thousand point five")]
    [TestCase("9223372036854775807", "nine quintillion two hundred twenty-three quadrillion three hundred seventy-two trillion thirty-six billion eight hundred fifty-four million seven hundred seventy-five thousand eight hundred seven")]
    [TestCase("-9223372036854775808", "negative nine quintillion two hundred twenty-three quadrillion three hundred seventy-two trillion thirty-six billion eight hundred fifty-four million seven hundred seventy-five thousand eight hundred eight")]
    [TestCase("9223372036854775808", "9223372036854775808")]
    [TestCase("-9223372036854775809", "-9223372036854775809")]
    [TestCase("9,223,372,036,854,775,808", "9,223,372,036,854,775,808")]
    [TestCase("9223372036854775808.5", "9223372036854775808.5")]
    [TestCase("We need 1000 credits, not 2000.", "We need one thousand credits, not two thousand.")]
    public void TestCleanTextConvertsNumbers(string text, string expected)
    {
        Assert.That(TTSSystem.CleanText(text), Is.EqualTo(expected));
    }

    /// <summary>
    /// Verifies that standard ASCII apostrophes (used in contractions, accents, or plurals) are preserved.
    /// </summary>
    [Test]
    public void TestCleanTextPreservesApostrophes()
    {
        // Contractions
        Assert.That(TTSSystem.CleanText("I'm here"), Is.EqualTo("I'm here"));
        Assert.That(TTSSystem.CleanText("I'll do it"), Is.EqualTo("I'll do it"));
        Assert.That(TTSSystem.CleanText("don't stop"), Is.EqualTo("don't stop"));
        Assert.That(TTSSystem.CleanText("won't stop"), Is.EqualTo("won't stop"));

        // Accent ending/beginning and plural apostrophes
        Assert.That(TTSSystem.CleanText("thinkin'"), Is.EqualTo("thinkin'"));
        Assert.That(TTSSystem.CleanText("'ello"), Is.EqualTo("'ello"));
        Assert.That(TTSSystem.CleanText("Chris' coat"), Is.EqualTo("Chris' coat"));
    }

    /// <summary>
    /// Verifies that unicode smart quotes (both single quotes and smart apostrophes) are normalized to standard ASCII apostrophes.
    /// </summary>
    [Test]
    public void TestCleanTextNormalizesSmartQuotes()
    {
        // Smart quote normalization
        Assert.That(TTSSystem.CleanText("I’m here"), Is.EqualTo("I'm here"));
        Assert.That(TTSSystem.CleanText("I’ll do it"), Is.EqualTo("I'll do it"));
        Assert.That(TTSSystem.CleanText("don’t stop"), Is.EqualTo("don't stop"));
        Assert.That(TTSSystem.CleanText("‘ello"), Is.EqualTo("'ello"));
        Assert.That(TTSSystem.CleanText("Chris’ coat"), Is.EqualTo("Chris' coat"));
    }

    /// <summary>
    /// Verifies that formatting tags (like BBCode) and non-alphanumeric/non-punctuation characters are stripped.
    /// </summary>
    [Test]
    public void TestCleanTextStripsInvalidCharacters()
    {
        // Standard stripping behavior
        Assert.That(TTSSystem.CleanText("[color=red]Hello[/color] world!"), Is.EqualTo("Hello world!"));
        Assert.That(TTSSystem.CleanText("Hello @#$% world!"), Is.EqualTo("Hello  world!"));
    }
}
