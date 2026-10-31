using System.Collections.Generic;
using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Server._Starlight.Economy;
using Content.Shared._Starlight.Economy;
using Content.Shared.Roles;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Starlight.Economy;

[TestOf(typeof(SalarySystem))]
[TestOf(typeof(ItemPriceManager))]
public sealed class EconomyPrototypesTest : GameTest
{
    private static readonly ProtoId<SalariesPrototype> _salaries = "standart";

    private static readonly HashSet<ProtoId<JobPrototype>> _unpaidJobs =
    [
        "CentCommServiceWorker",
        "K9", // TBH: unintended, should be fixed
        "MakeshiftID",
        "Passenger",
        "RadioHost", // TBH: unintended, should be fixed
    ];

    [Test]
    public async Task SelectableJobsHaveSalaries()
        => await Server.WaitAssertion(() =>
        {
            var salaries = SProtoMan.Index(_salaries);

            var unpaid = SProtoMan.EnumeratePrototypes<JobPrototype>()
                .Where(job => job.SetPreference && !Pair.IsTestPrototype(job) && !salaries.Jobs.ContainsKey(job.ID) && !_unpaidJobs.Contains(job.ID))
                .Select(job => job.ID)
                .ToList();

            var stale = _unpaidJobs.Where(job => salaries.Jobs.ContainsKey(job) || !SProtoMan.HasIndex(job)).ToList();
            // Zero is a deliberate "no pay" rate (visitors, prisoners), only negative pay is a mistake.
            var negative = salaries.Jobs.Where(kv => kv.Value < 0).Select(kv => $"{kv.Key}: {kv.Value}").ToList();

            using (Assert.EnterMultipleScope())
            {
                Assert.That(unpaid, Is.Empty,
                    $"Selectable jobs without a salary: {string.Join(", ", unpaid)}. Add them to salaries.yml or to {nameof(_unpaidJobs)}.");
                Assert.That(stale, Is.Empty,
                    $"{nameof(_unpaidJobs)} lists jobs that are paid or no longer exist: {string.Join(", ", stale)}.");
                Assert.That(negative, Is.Empty, $"Salaries must not be negative: {string.Join(", ", negative)}.");
            }
        });

    [Test]
    public async Task ItemPriceCategoriesExist()
    {
        var compFactory = Server.ResolveDependency<IComponentFactory>();

        await Server.WaitAssertion(() =>
        {
            var badRanges = SProtoMan.EnumeratePrototypes<PriceCategoryPrototype>()
                .Where(cat => cat.Min <= 0 || cat.Max < cat.Min)
                .Select(cat => $"{cat.ID} ({cat.Min}..{cat.Max})")
                .ToList();

            var unknown = new List<string>();
            foreach (var proto in SProtoMan.EnumeratePrototypes<EntityPrototype>())
            {
                if (proto.Abstract || Pair.IsTestPrototype(proto))
                    continue;

                if (!proto.TryComp<ItemPriceComponent>(out var price, compFactory))
                    continue;

                if (string.IsNullOrEmpty(price.PriceCategory))
                    continue;

                if (!SProtoMan.HasIndex<PriceCategoryPrototype>(price.PriceCategory))
                    unknown.Add($"{proto.ID} -> '{price.PriceCategory}'");
            }

            using (Assert.EnterMultipleScope())
            {
                Assert.That(badRanges, Is.Empty, $"Price categories with an empty or non-positive range: {string.Join(", ", badRanges)}.");
                Assert.That(unknown, Is.Empty, $"Items reference missing price categories: {string.Join(", ", unknown)}.");
            }
        });
    }

    [Test]
    public async Task ItemPricesAreStableAndInRange()
    {
        var compFactory = Server.ResolveDependency<IComponentFactory>();
        var prices = Server.System<ItemPriceManager>();

        await Server.WaitAssertion(() =>
        {
            prices.ResetForNewRound();

            using (Assert.EnterMultipleScope())
            {
                foreach (var proto in SProtoMan.EnumeratePrototypes<EntityPrototype>())
                {
                    if (proto.Abstract || !proto.TryComp<ItemPriceComponent>(out var comp, compFactory))
                        continue;

                    if (string.IsNullOrEmpty(comp.PriceCategory)
                        || !SProtoMan.TryIndex<PriceCategoryPrototype>(comp.PriceCategory, out var category))
                        continue;

                    var first = prices.GetPriceForPrototype(proto.ID, comp.PriceCategory);
                    var second = prices.GetPriceForPrototype(proto.ID, comp.PriceCategory);

                    Assert.That(first, Is.Not.Null, $"{proto.ID} got no price from {comp.PriceCategory}.");
                    Assert.That(second, Is.EqualTo(first), $"{proto.ID} changed price within a round.");
                    Assert.That(first, Is.InRange(category.Min, category.Max),
                        $"{proto.ID} costs {first}, outside {category.ID} ({category.Min}..{category.Max}).");
                }
            }
        });
    }
}
