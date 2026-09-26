using System.Collections.Generic;
using Content.Shared._Starlight.Damage;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.Damageable;

[TestFixture]
[TestOf(typeof(DamageSpecifier))]
public sealed class DamageSpecifierTest
{
    [Test]
    public void TestDamageSpecifierOperations()
    {
        // Test basic math operations.
        // I've already nearly broken these once. When editing the operators.

        DamageSpecifier input1 = new() { DamageDict = Input1 };
        DamageSpecifier input2 = new() { DamageDict = Input2 };
        DamageSpecifier output1 = new() { DamageDict = Output1 };
        DamageSpecifier output2 = new() { DamageDict = Output2 };
        DamageSpecifier output3 = new() { DamageDict = Output3 };
        DamageSpecifier output4 = new() { DamageDict = Output4 };
        DamageSpecifier output5 = new() { DamageDict = Output5 };

        Assert.Multiple(() =>
        {
            Assert.That(-input1, Is.EqualTo(output1));
            Assert.That(input1 / 2, Is.EqualTo(output2));
            Assert.That(input1 * 2, Is.EqualTo(output3));
        });

        var difference = input1 - input2;
        Assert.That(difference, Is.EqualTo(output4));

        var difference2 = -input2 + input1;
        Assert.That(difference, Is.EqualTo(difference2));

        difference.Clamp(-0.25f, 0.25f);
        Assert.That(difference, Is.EqualTo(output5));
    }

    #region Starlight
    [Test]
    public void TestGroupAndMixMaxOperations()
    {
        DamageSpecifier input = new()
        {
            DamageGroupDict = new Dictionary<ProtoId<DamageGroupPrototype>, FixedPoint2>
            {
                { "Burn", -3 },
            },
            MixMax = new DamageSpecifierMixMax
            {
                Value = 1,
                Groups = new List<ProtoId<DamageGroupPrototype>> { "Burn" },
                Types = new List<ProtoId<DamageTypePrototype>> { "Slash", "Blunt" },
            },
        };

        var scaled = input * 2;
        var negated = -input;
        var inverted = input.Invert();
        Assert.Multiple(() =>
        {
            Assert.That(scaled.DamageGroupDict["Burn"], Is.EqualTo(FixedPoint2.New(-6)));
            Assert.That(scaled.MixMax, Is.Not.Null);
            Assert.That(scaled.MixMax!.Value, Is.EqualTo(FixedPoint2.New(2)));
            Assert.That(input.MixMax!.Value, Is.EqualTo(FixedPoint2.New(1)));
            Assert.That(negated.DamageGroupDict["Burn"], Is.EqualTo(FixedPoint2.New(3)));
            Assert.That(negated.MixMax, Is.Not.Null);
            Assert.That(negated.MixMax!.Value, Is.EqualTo(FixedPoint2.New(-1)));
            Assert.That(inverted.DamageGroupDict["Burn"], Is.EqualTo(FixedPoint2.New(3)));
            Assert.That(inverted.MixMax, Is.Not.Null);
            Assert.That(inverted.MixMax!.Value, Is.EqualTo(FixedPoint2.New(-1)));
        });
    }
    #endregion

    private static readonly Dictionary<ProtoId<DamageTypePrototype>, FixedPoint2> Input1 = new()
    {
        { "A", 1.5f },
        { "B", 2 },
        { "C", 3 }
    };

    private static readonly Dictionary<ProtoId<DamageTypePrototype>, FixedPoint2> Input2 = new()
    {
        { "A", 1 },
        { "B", 2 },
        { "C", 5 },
        { "D", 0.05f }
    };

    private static readonly Dictionary<ProtoId<DamageTypePrototype>, FixedPoint2> Output1 = new()
    {
        { "A", -1.5f },
        { "B", -2 },
        { "C", -3 }
    };

    private static readonly Dictionary<ProtoId<DamageTypePrototype>, FixedPoint2> Output2 = new()
    {
        { "A", 0.75f },
        { "B", 1 },
        { "C", 1.5 }
    };

    private static readonly Dictionary<ProtoId<DamageTypePrototype>, FixedPoint2> Output3 = new()
    {
        { "A", 3f },
        { "B", 4 },
        { "C", 6 }
    };

    private static readonly Dictionary<ProtoId<DamageTypePrototype>, FixedPoint2> Output4 = new()
    {
        { "A", 0.5f },
        { "B", 0 },
        { "C", -2 },
        { "D", -0.05f }
    };

    private static readonly Dictionary<ProtoId<DamageTypePrototype>, FixedPoint2> Output5 = new()
    {
        { "A", 0.25f },
        { "B", 0 },
        { "C", -0.25f },
        { "D", -0.05f }
    };
}
