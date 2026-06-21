using Content.Server._Starlight.TextToSpeech;
using NUnit.Framework;

namespace Content.Tests.Server.TextToSpeech;

[TestFixture]
public sealed class TTSTests
{
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

    [Test]
    public void TestCleanTextStripsInvalidCharacters()
    {
        // Standard stripping behavior
        Assert.That(TTSSystem.CleanText("[color=red]Hello[/color] world!"), Is.EqualTo("Hello world!"));
        Assert.That(TTSSystem.CleanText("Hello @#$% world!"), Is.EqualTo("Hello  world!"));
    }
}
