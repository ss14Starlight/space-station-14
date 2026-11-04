using System;
using System.Linq;
using Content.Shared._Starlight.Objectives.ObjectivePicker;
using Content.Shared._Starlight.Traits.Antags;
using NUnit.Framework;

namespace Content.Tests.Shared._Starlight.Objectives;

[TestFixture]
public sealed class ObjectiveEvolutionTest
{
    [Test]
    public void OptionalPickerPermitsDecliningEveryObjective()
    {
        var offers = new PotentialObjectivesComponent { MinimumDifficulty = 0 };
        Assert.That(ObjectivePickerSelection.Valid(offers, Array.Empty<Robust.Shared.GameObjects.NetEntity>()), Is.True);
        Assert.That(ObjectivePickerSelection.TryComplete(offers, offers.ObjectiveOptions.Keys,
            Array.Empty<Robust.Shared.GameObjects.NetEntity>(), out var selection), Is.True);
        Assert.That(selection, Is.Empty);
        offers.MinimumDifficulty = 1;
        Assert.That(ObjectivePickerSelection.Valid(offers, selection), Is.False);
    }

    [Test]
    public void DifficultyRankingRewardsMoreCompletedWork()
    {
        var harder = new ObjectiveDifficultyScore().Add(7, true).Add(3, false).Add(0, true);
        var easier = new ObjectiveDifficultyScore().Add(6, true).Add(0, false);
        Assert.That(harder.Selected, Is.EqualTo(10));
        Assert.That(harder.Completed, Is.GreaterThan(easier.Completed));
        Assert.That(harder.Percentage, Is.EqualTo(70));
        Assert.That(easier.Percentage, Is.EqualTo(100));
    }
}
