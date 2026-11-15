using System.Collections.Generic;
using System.Linq;
using Content.Shared._Starlight.Objectives.ObjectivePicker;
using NUnit.Framework;
using Robust.Shared.GameObjects;

namespace Content.Tests.Shared._Starlight.Objectives;

[TestFixture]
public sealed class ObjectivePickerSelectionTest
{
    private static PotentialObjectivesComponent Offers(float minimum, params float[] difficulties)
    {
        var offers = new PotentialObjectivesComponent { MinimumDifficulty = minimum };
        for (var i = 0; i < difficulties.Length; i++)
        {
            var id = new NetEntity(i + 1);
            offers.ObjectiveOptions[id] = default;
            offers.Difficulties[id] = difficulties[i];
        }
        return offers;
    }

    [Test]
    public void SelectionHasAMinimumButNoCountOrDifficultyMaximum()
    {
        var offers = Offers(6, 1, 1, 1, 1, 1, 1, 1, 1);
        Assert.That(ObjectivePickerSelection.Valid(offers, offers.ObjectiveOptions.Keys.Take(5).ToArray()), Is.False);
        Assert.That(ObjectivePickerSelection.Valid(offers, offers.ObjectiveOptions.Keys.Take(6).ToArray()), Is.True);
        Assert.That(ObjectivePickerSelection.Valid(offers, offers.ObjectiveOptions.Keys.ToArray()), Is.True);
    }

    [Test]
    public void StoryObjectivesDoNotSatisfyTheDifficultyMinimum()
    {
        var offers = Offers(3, 0, 0, 1.5f, 1.5f);
        Assert.That(ObjectivePickerSelection.Valid(offers, offers.ObjectiveOptions.Keys.Take(3).ToArray()), Is.False);
        Assert.That(ObjectivePickerSelection.Valid(offers, offers.ObjectiveOptions.Keys.ToArray()), Is.True);
        Assert.That(ObjectivePickerSelection.TryComplete(offers, offers.ObjectiveOptions.Keys,
            new[] { new NetEntity(1) }, out var selected), Is.True);
        Assert.That(selected, Does.Contain(new NetEntity(1)));
        Assert.That(ObjectivePickerSelection.Difficulty(offers, selected), Is.EqualTo(3));
    }

    [Test]
    public void CompletionBacktracksAroundAnIncompatibleFirstChoice()
    {
        var offers = Offers(6, 4, 3, 3);
        var first = new NetEntity(1);
        var second = new NetEntity(2);
        var third = new NetEntity(3);
        offers.Conflicts[first] = new() { second, third };
        offers.Conflicts[second] = new() { first };
        offers.Conflicts[third] = new() { first };
        Assert.That(ObjectivePickerSelection.TryComplete(offers, offers.ObjectiveOptions.Keys,
            System.Array.Empty<NetEntity>(), out var selected), Is.True);
        Assert.That(selected, Is.EquivalentTo(new[] { second, third }));
        Assert.That(ObjectivePickerSelection.Valid(offers, new[] { first, second }), Is.False);
        Assert.That(ObjectivePickerSelection.TryComplete(offers, offers.ObjectiveOptions.Keys,
            new[] { first }, out _), Is.False, "A mulligan must keep a viable selected objective.");
    }

    [Test]
    public void ExhaustedLimitsAreRejectedAndExcludedFromAutomaticSelection()
    {
        var offers = Offers(3, 3, 1.5f, 1.5f);
        var exhausted = new NetEntity(1);
        offers.UnavailableObjectives.Add(exhausted);
        Assert.That(ObjectivePickerSelection.Valid(offers, new[] { exhausted }), Is.False);
        Assert.That(ObjectivePickerSelection.TryComplete(offers, offers.ObjectiveOptions.Keys,
            System.Array.Empty<NetEntity>(), out var selected), Is.True);
        Assert.That(selected, Does.Not.Contain(exhausted));
        Assert.That(ObjectivePickerSelection.Valid(offers, selected), Is.True);
    }

    [Test]
    public void UnknownOffersAndDuplicateOrderingCannotInflateDifficulty()
    {
        var offers = Offers(3, 1.5f);
        Assert.That(ObjectivePickerSelection.Valid(offers, new[] { new NetEntity(99) }), Is.False);
        Assert.That(ObjectivePickerSelection.Valid(offers, new[] { new NetEntity(1), new NetEntity(1) }), Is.False);
        Assert.That(ObjectivePickerSelection.TryComplete(offers,
            new[] { new NetEntity(1), new NetEntity(1), new NetEntity(99) },
            System.Array.Empty<NetEntity>(), out _), Is.False);
    }
}
