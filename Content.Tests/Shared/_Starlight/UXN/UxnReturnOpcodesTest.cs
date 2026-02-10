using System;
using Content.Server._Starlight.UXN;
using NUnit.Framework;
using Robust.Shared.IoC;
using Robust.Shared.Random;

namespace Content.Tests.Shared._Starlight.UXN;

// Basic tests of various damage prototypes and classes.
[TestFixture]
public sealed class UxnReturnOpcodeTest : ContentUnitTest
{

    private IRobustRandom _random = default!;

    [OneTimeSetUp]
    public void OneTimeSetup() => _random = IoCManager.Resolve<IRobustRandom>();

    [Test]
    [TestCase(0x00)]
    [TestCase(0x32)]
    [TestCase(0xFF)] //this one *should* wrap to 0x00
    public void INCr(byte val)
    {
        var uxn = new UXNProcessor();
        uxn.SystemMem[0x100] = 0x01 + 0x40;
        uxn.ReturnStack.PushByte(val);
        Assert.That(uxn.Step(), Is.EqualTo(false));
        Assert.That(uxn.ReturnStack.PopByte(true), Is.EqualTo<byte>((byte)(val + 1)));
    }

    [Test]
    public void POPr()
    {
        var uxn = new UXNProcessor();
        uxn.SystemMem[0x100] = 0x02 + 0x40;
        uxn.ReturnStack.PushByte(0x32);
        Assert.That(uxn.Step(), Is.EqualTo(false));
        Assert.That(uxn.ReturnStack.StackPointer, Is.EqualTo(0x00));
    }

    [Test]
    public void NIPr()
    {
        var uxn = new UXNProcessor();
        uxn.SystemMem[0x100] = 0x03 + 0x40;
        uxn.ReturnStack.PushShort(0x1234);
        Assert.That(uxn.Step(), Is.EqualTo(false));
        Assert.That(uxn.ReturnStack.StackPointer, Is.EqualTo(0x01));
        Assert.That(uxn.ReturnStack.PopByte(true), Is.EqualTo(0x34));
    }

    [Test]
    public void SWPr()
    {
        var uxn = new UXNProcessor();
        uxn.SystemMem[0x100] = 0x04 + 0x40;
        uxn.ReturnStack.PushShort(0x1234);
        Assert.That(uxn.Step(), Is.EqualTo(false));
        Assert.That(uxn.ReturnStack.PopByte(true), Is.EqualTo(0x12));
        Assert.That(uxn.ReturnStack.PopByte(true), Is.EqualTo(0x34));
    }

    [Test]
    public void ROTr()
    {
        var uxn = new UXNProcessor();
        uxn.SystemMem[0x100] = 0x05 + 0x40;
        uxn.ReturnStack.PushByte(0x11);
        uxn.ReturnStack.PushByte(0x22);
        uxn.ReturnStack.PushByte(0x33);
        Assert.That(uxn.Step(), Is.EqualTo(false));
        Assert.That(uxn.ReturnStack.PopByte(true), Is.EqualTo(0x11));
        Assert.That(uxn.ReturnStack.PopByte(true), Is.EqualTo(0x33));
        Assert.That(uxn.ReturnStack.PopByte(true), Is.EqualTo(0x22));
    }

    [Test]
    public void DUPr()
    {
        var uxn = new UXNProcessor();
        uxn.SystemMem[0x100] = 0x06 + 0x40;
        uxn.ReturnStack.PushByte(0x11);
        Assert.That(uxn.Step(), Is.EqualTo(false));
        Assert.That(uxn.ReturnStack.PopByte(true), Is.EqualTo(0x11));
        Assert.That(uxn.ReturnStack.PopByte(true), Is.EqualTo(0x11));
    }

    [Test]
    public void OVRr()
    {
        var uxn = new UXNProcessor();
        uxn.SystemMem[0x100] = 0x07 + 0x40;
        uxn.ReturnStack.PushByte(0x11);
        uxn.ReturnStack.PushByte(0x22);
        Assert.That(uxn.Step(), Is.EqualTo(false));
        Assert.That(uxn.ReturnStack.PopByte(true), Is.EqualTo(0x11));
        Assert.That(uxn.ReturnStack.PopByte(true), Is.EqualTo(0x22));
        Assert.That(uxn.ReturnStack.PopByte(true), Is.EqualTo(0x11));
    }

    [Test]
    [TestCase(0x11, 0x11)]
    [TestCase(0x11, 0x12)]
    [TestCase(0x12, 0x11)]
    [TestCase(0x12, 0x12)]
    public void EQUr(byte left, byte right)
    {
        var uxn = new UXNProcessor();
        uxn.SystemMem[0x100] = 0x08 + 0x40;
        uxn.ReturnStack.PushByte(left);
        uxn.ReturnStack.PushByte(right);
        Assert.That(uxn.Step(), Is.EqualTo(false));
        Assert.That(uxn.ReturnStack.PopByte(true) == 0x01, Is.EqualTo(left == right));
    }

    [Test]
    [TestCase(0x11, 0x11)]
    [TestCase(0x11, 0x12)]
    [TestCase(0x12, 0x11)]
    [TestCase(0x12, 0x12)]
    public void NEQr(byte left, byte right)
    {
        var uxn = new UXNProcessor();
        uxn.SystemMem[0x100] = 0x09 + 0x40;
        uxn.ReturnStack.PushByte(left);
        uxn.ReturnStack.PushByte(right);
        Assert.That(uxn.Step(), Is.EqualTo(false));
        Assert.That(uxn.ReturnStack.PopByte(true) == 0x01, Is.EqualTo(left != right));
    }

    [Test]
    [TestCase(0xf8, 0xf8, false)]
    [TestCase(0x01, 0x01, false)]
    [TestCase(0xf8, 0x01, true)]
    [TestCase(0x01, 0xf8, false)]
    public void GTH(byte left, byte right, bool expected)
    {
        var uxn = new UXNProcessor();
        uxn.SystemMem[0x100] = 0x0A + 0x40;
        uxn.ReturnStack.PushByte(left);
        uxn.ReturnStack.PushByte(right);
        Assert.That(uxn.Step(), Is.EqualTo(false));
        Assert.That(uxn.ReturnStack.PopByte(true) == 0x01, Is.EqualTo(expected));
    }

    [Test]
    [TestCase(0xf8, 0xf8, false)]
    [TestCase(0x01, 0x01, false)]
    [TestCase(0xf8, 0x01, false)]
    [TestCase(0x01, 0xff, true)]
    public void LTHr(byte left, byte right, bool expected)
    {
        var uxn = new UXNProcessor();
        uxn.SystemMem[0x100] = 0x0B + 0x40;
        uxn.ReturnStack.PushByte(left);
        uxn.ReturnStack.PushByte(right);
        Assert.That(uxn.Step(), Is.EqualTo(false));
        Assert.That(uxn.ReturnStack.PopByte(true) == 0x01, Is.EqualTo(expected));
    }

    [Test]
    [TestCase(0xfe)] //jumps back 2 bytes to the instr just *before* the jmp
    [TestCase(0x10)]
    public void JMPr(byte jmp)
    {
        var uxn = new UXNProcessor();
        uxn.SystemMem[0x100] = 0x0C + 0x40;
        uxn.ReturnStack.PushByte(jmp);
        Assert.That(uxn.Step(), Is.EqualTo(false));
        Assert.That(uxn.PC, Is.EqualTo((ushort)(0x101 + (sbyte)jmp))); //the PC is incr'd by 1 so we add to that
    }

    [Test]
    [TestCase(0xfe, true)] //jumps back 2 bytes to the instr just *before* the jmp
    [TestCase(0x10, true)]
    [TestCase(0xfe, false)] //cond is false
    [TestCase(0x10, false)]
    public void JCNr(byte jmp, bool cond)
    {
        var uxn = new UXNProcessor();
        uxn.SystemMem[0x100] = 0x0D + 0x40;
        uxn.ReturnStack.PushByte((byte)(cond ? 0x01 : 0x00));
        uxn.ReturnStack.PushByte(jmp);
        Assert.That(uxn.Step(), Is.EqualTo(false));
        Assert.That(uxn.PC, Is.EqualTo(cond ? (ushort)(0x101 + (sbyte)jmp) : 0x101)); //the PC is incr'd by 1 so we add to that
    }

    [Test]
    [TestCase(0xfe)]
    [TestCase(0x10)]
    public void JSRr(byte jmp)
    {
        var uxn = new UXNProcessor();
        uxn.SystemMem[0x100] = 0x0E + 0x40;
        uxn.ReturnStack.PushByte(jmp);
        Assert.That(uxn.Step(), Is.EqualTo(false));
        Assert.That(uxn.PC, Is.EqualTo((ushort)(0x101 + (sbyte)jmp)));
        Assert.That(uxn.WorkingStack.PopShort(false), Is.EqualTo(0x101)); //and the return stack should point to the instr after JSR
    }

    [Test]
    public void STHr()
    {
        var uxn = new UXNProcessor();
        uxn.SystemMem[0x100] = 0x0F + 0x40;
        uxn.ReturnStack.PushByte(0x32);
        Assert.That(uxn.Step(), Is.EqualTo(false));
        Assert.That(uxn.WorkingStack.PopByte(false), Is.EqualTo(0x32));
    }

    [Test]
    public void LDZr()
    {
        var uxn = new UXNProcessor();
        uxn.SystemMem[0x100] = 0x10 + 0x40;
        uxn.SystemMem[0x00] = 0x32;
        uxn.ReturnStack.PushByte(0x00);
        Assert.That(uxn.Step(), Is.EqualTo(false));
        Assert.That(uxn.ReturnStack.PopByte(false), Is.EqualTo(0x32));
    }

    [Test]
    public void STZr()
    {
        var uxn = new UXNProcessor();
        uxn.SystemMem[0x100] = 0x11 + 0x40;
        uxn.ReturnStack.PushByte(0x32);
        uxn.ReturnStack.PushByte(0x00);
        Assert.That(uxn.Step(), Is.EqualTo(false));
        Assert.That(uxn.SystemMem[0x00], Is.EqualTo(0x32));
    }

    [Test]
    [TestCase(0xfe)]
    [TestCase(0x10)]
    public void LDRr(byte offset)
    {
        var uxn = new UXNProcessor();
        uxn.SystemMem[0x100] = 0x12 + 0x40;
        var target = 0x101 + (sbyte)offset;
        uxn.SystemMem[target] = 0x32;
        uxn.ReturnStack.PushByte(offset);
        Assert.That(uxn.Step(), Is.EqualTo(false));
        Assert.That(uxn.ReturnStack.PopByte(true), Is.EqualTo(0x32));
    }

    [Test]
    [TestCase(0xfe)]
    [TestCase(0x10)]
    public void STRr(byte offset)
    {
        var uxn = new UXNProcessor();
        uxn.SystemMem[0x100] = 0x13 + 0x40;
        var target = 0x101 + (sbyte)offset;
        uxn.ReturnStack.PushByte(0x32);
        uxn.ReturnStack.PushByte(offset);
        Assert.That(uxn.Step(), Is.EqualTo(false));
        Assert.That(uxn.SystemMem[target], Is.EqualTo(0x32));
    }

    [Test]
    public void LDAr()
    {
        var uxn = new UXNProcessor();
        uxn.SystemMem[0x100] = 0x14 + 0x40;
        uxn.SystemMem[0x1234] = 0x32;
        uxn.ReturnStack.PushShort(0x1234);
        Assert.That(uxn.Step(), Is.EqualTo(false));
        Assert.That(uxn.ReturnStack.PopByte(true), Is.EqualTo(0x32));
    }

    [Test]
    public void STAr()
    {
        var uxn = new UXNProcessor();
        uxn.SystemMem[0x100] = 0x15 + 0x40;
        uxn.ReturnStack.PushByte(0x32);
        uxn.ReturnStack.PushShort(0x1234);
        Assert.That(uxn.Step(), Is.EqualTo(false));
        Assert.That(uxn.SystemMem[0x1234], Is.EqualTo(0x32));
    }

    [Test]
    public void DEIr()
    {
        var uxn = new UXNProcessor();
        uxn.SystemMem[0x100] = 0x16 + 0x40;
        uxn.DevMem[0x00] = 0x32;
        var testdev = new TestDevice();
        uxn.AttachDevice(0x00, testdev);
        Assert.That(uxn.Step(), Is.EqualTo(false));
        Assert.That(uxn.ReturnStack.PopByte(true), Is.EqualTo(0x32));
        Assert.That(testdev.dei, Is.EqualTo(0x00));
    }

    [Test]
    public void DEOr()
    {
        var uxn = new UXNProcessor();
        uxn.SystemMem[0x100] = 0x17 + 0x40;
        uxn.ReturnStack.PushByte(0x32);
        uxn.ReturnStack.PushByte(0x00);
        var testdev = new TestDevice();
        uxn.AttachDevice(0x00, testdev);
        Assert.That(uxn.Step(), Is.EqualTo(false));
        Assert.That(uxn.DevMem[0x00], Is.EqualTo(0x32));
        Assert.That(testdev.deo, Is.EqualTo(0x00));
    }

    [Test]
    [TestCase(0x00, 0x00)]
    [TestCase(0x01, 0x01)]
    [TestCase(0x10, 0x20)]
    [TestCase(0xff, 0xff)]
    public void ADDr(byte left, byte right)
    {
        var uxn = new UXNProcessor();
        uxn.SystemMem[0x100] = 0x18 + 0x40;
        uxn.ReturnStack.PushByte(left);
        uxn.ReturnStack.PushByte(right);
        Assert.That(uxn.Step(), Is.EqualTo(false));
        Assert.That(uxn.ReturnStack.PopByte(true), Is.EqualTo((byte)(left + right)));
    }

    [Test]
    [TestCase(0x00, 0x00)]
    [TestCase(0x01, 0x01)]
    [TestCase(0x10, 0x20)]
    [TestCase(0xff, 0xff)]
    public void SUBr(byte left, byte right)
    {
        var uxn = new UXNProcessor();
        uxn.SystemMem[0x100] = 0x19 + 0x40;
        uxn.ReturnStack.PushByte(left);
        uxn.ReturnStack.PushByte(right);
        Assert.That(uxn.Step(), Is.EqualTo(false));
        Assert.That(uxn.ReturnStack.PopByte(true), Is.EqualTo((byte)(left - right)));
    }

    [Test]
    [TestCase(0x00, 0x00)]
    [TestCase(0x01, 0x01)]
    [TestCase(0x10, 0x20)]
    [TestCase(0xff, 0xff)]
    public void MULr(byte left, byte right)
    {
        var uxn = new UXNProcessor();
        uxn.SystemMem[0x100] = 0x1A + 0x40;
        uxn.ReturnStack.PushByte(left);
        uxn.ReturnStack.PushByte(right);
        Assert.That(uxn.Step(), Is.EqualTo(false));
        Assert.That(uxn.ReturnStack.PopByte(true), Is.EqualTo((byte)(left * right)));
    }

    [Test]
    [TestCase(0x00, 0x00)]
    [TestCase(0x10, 0x00)]
    [TestCase(0x01, 0x01)]
    [TestCase(0x10, 0x20)]
    [TestCase(0xff, 0xff)]
    public void DIVr(byte left, byte right)
    {
        var uxn = new UXNProcessor();
        uxn.SystemMem[0x100] = 0x1B + 0x40;
        uxn.ReturnStack.PushByte(left);
        uxn.ReturnStack.PushByte(right);
        Assert.That(uxn.Step(), Is.EqualTo(false));
        Assert.That(uxn.ReturnStack.PopByte(true), Is.EqualTo(right != 0 ? (byte)(left / right) : 0x00));
    }

    [Test]
    [Repeat(5, false)]
    public void ANDr()
    {
        var uxn = new UXNProcessor();
        uxn.SystemMem[0x100] = 0x1C + 0x40;
        var left = _random.NextByte();
        var right = _random.NextByte();
        uxn.ReturnStack.PushByte(left);
        uxn.ReturnStack.PushByte(right);
        Assert.That(uxn.Step(), Is.EqualTo(false));
        Assert.That(uxn.ReturnStack.PopByte(true), Is.EqualTo(left & right));
    }

    [Test]
    [Repeat(5, false)]
    public void ORAr()
    {
        var uxn = new UXNProcessor();
        uxn.SystemMem[0x100] = 0x1D + 0x40;
        var left = _random.NextByte();
        var right = _random.NextByte();
        uxn.ReturnStack.PushByte(left);
        uxn.ReturnStack.PushByte(right);
        Assert.That(uxn.Step(), Is.EqualTo(false));
        Assert.That(uxn.ReturnStack.PopByte(true), Is.EqualTo(left | right));
    }

    [Test]
    [Repeat(5, false)]
    public void EORr()
    {
        var uxn = new UXNProcessor();
        uxn.SystemMem[0x100] = 0x1E + 0x40;
        var left = _random.NextByte();
        var right = _random.NextByte();
        uxn.ReturnStack.PushByte(left);
        uxn.ReturnStack.PushByte(right);
        Assert.That(uxn.Step(), Is.EqualTo(false));
        Assert.That(uxn.ReturnStack.PopByte(true), Is.EqualTo(left ^ right));
    }

    [Test]
    [TestCase(0x12, 0x00, 0x12)]
    [TestCase(0x34, 0x10, 0x68)] // left by 1. double the value
    [TestCase(0x32, 0x01, 0x19)] // right by 1. half the value
    public void SFTr(byte input, byte sft, byte output)
    {
        var uxn = new UXNProcessor();
        uxn.SystemMem[0x100] = 0x1F + 0x40;
        uxn.ReturnStack.PushByte(input);
        uxn.ReturnStack.PushByte(sft);
        Assert.That(uxn.Step(), Is.EqualTo(false));
        Assert.That(uxn.ReturnStack.PopByte(true), Is.EqualTo(output));
    }
}
