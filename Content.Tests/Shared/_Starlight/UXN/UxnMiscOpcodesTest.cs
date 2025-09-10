using System;
using Content.Shared._Starlight.UXN;
using NUnit.Framework;
using Robust.Shared.IoC;
using Robust.Shared.Random;

namespace Content.Tests.Shared._Starlight.UXN;

// Basic tests of various damage prototypes and classes.
[TestFixture]
public sealed class UxnMiscOpcodeTest : ContentUnitTest
{

    private IRobustRandom _random = default!;

    [OneTimeSetUp]
    public void OneTimeSetup() => _random = IoCManager.Resolve<IRobustRandom>();

    [Test]
    [TestCase(0x1111, 0x1111)]
    [TestCase(0x1111, 0x1212)]
    [TestCase(0x1212, 0x1111)]
    [TestCase(0x1212, 0x1212)]
    public void EQU2k(short left, short right)
    {
        var uxn = new UXNProcessor();
        uxn.SystemMem[0x100] = 0x08 + 0x20;
        uxn.WorkingStack.PushShort((ushort)left);
        uxn.WorkingStack.PushShort((ushort)right);
        Assert.That(uxn.Step(), Is.EqualTo(false));
        Assert.That(uxn.WorkingStack.PopByte(true) == 0x01, Is.EqualTo(left == right));
    }

}
