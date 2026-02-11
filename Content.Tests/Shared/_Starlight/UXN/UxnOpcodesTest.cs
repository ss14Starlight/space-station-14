using System;
using Content.Server._Starlight.UXN;
using NUnit.Framework;
using Robust.Shared.IoC;
using Robust.Shared.Random;

namespace Content.Tests.Shared._Starlight.UXN;

public sealed class TestDevice : UXNDevice
{
    public byte? dei = null;
    public byte? deo = null;

    public override void ReadValue(byte memTarget, Byte256 deviceMem, UXNProcessor proc) => dei = memTarget;
    public override void WriteValue(byte memTarget, Byte256 deviceMem, UXNProcessor proc) => deo = memTarget;
}

// Basic tests of various damage prototypes and classes.
[TestFixture]
public sealed class UxnOpcodeTest : ContentUnitTest
{

    private IRobustRandom _random = default!;

    [OneTimeSetUp]
    public void OneTimeSetup() => _random = IoCManager.Resolve<IRobustRandom>();

    [Test]
    public void BRK()
    {
        var uxn = new UXNProcessor();
        Assert.That(uxn.Step(), Is.EqualTo(true));
        Assert.That(uxn.PC, Is.EqualTo(0x101));
    }

    [Test]
    [TestCase(0x00)]
    [TestCase(0x32)]
    [TestCase(0xFF)] //this one *should* wrap to 0x00
    public void INC(byte val)
    {
        var uxn = new UXNProcessor();
        uxn.SystemMem[0x100] = 0x01;
        uxn.WorkingStack.PushByte(val);
        Assert.That(uxn.Step(), Is.EqualTo(false));
        Assert.That(uxn.WorkingStack.PopByte(true), Is.EqualTo<byte>((byte)(val + 1)));
    }

    [Test]
    public void POP()
    {
        var uxn = new UXNProcessor();
        uxn.SystemMem[0x100] = 0x02;
        uxn.WorkingStack.PushByte(0x32);
        Assert.That(uxn.Step(), Is.EqualTo(false));
        Assert.That(uxn.WorkingStack.StackPointer, Is.EqualTo(0x00));
    }

    [Test]
    public void NIP()
    {
        var uxn = new UXNProcessor();
        uxn.SystemMem[0x100] = 0x03;
        uxn.WorkingStack.PushShort(0x1234);
        Assert.That(uxn.Step(), Is.EqualTo(false));
        Assert.That(uxn.WorkingStack.StackPointer, Is.EqualTo(0x01));
        Assert.That(uxn.WorkingStack.PopByte(true), Is.EqualTo(0x34));
    }

    [Test]
    public void SWP()
    {
        var uxn = new UXNProcessor();
        uxn.SystemMem[0x100] = 0x04;
        uxn.WorkingStack.PushShort(0x1234);
        Assert.That(uxn.Step(), Is.EqualTo(false));
        Assert.That(uxn.WorkingStack.PopByte(true), Is.EqualTo(0x12));
        Assert.That(uxn.WorkingStack.PopByte(true), Is.EqualTo(0x34));
    }

    [Test]
    public void ROT()
    {
        var uxn = new UXNProcessor();
        uxn.SystemMem[0x100] = 0x05;
        uxn.WorkingStack.PushByte(0x11);
        uxn.WorkingStack.PushByte(0x22);
        uxn.WorkingStack.PushByte(0x33);
        Assert.That(uxn.Step(), Is.EqualTo(false));
        Assert.That(uxn.WorkingStack.PopByte(true), Is.EqualTo(0x11));
        Assert.That(uxn.WorkingStack.PopByte(true), Is.EqualTo(0x33));
        Assert.That(uxn.WorkingStack.PopByte(true), Is.EqualTo(0x22));
    }

    [Test]
    public void DUP()
    {
        var uxn = new UXNProcessor();
        uxn.SystemMem[0x100] = 0x06;
        uxn.WorkingStack.PushByte(0x11);
        Assert.That(uxn.Step(), Is.EqualTo(false));
        Assert.That(uxn.WorkingStack.PopByte(true), Is.EqualTo(0x11));
        Assert.That(uxn.WorkingStack.PopByte(true), Is.EqualTo(0x11));
    }

    [Test]
    public void OVR()
    {
        var uxn = new UXNProcessor();
        uxn.SystemMem[0x100] = 0x07;
        uxn.WorkingStack.PushByte(0x11);
        uxn.WorkingStack.PushByte(0x22);
        Assert.That(uxn.Step(), Is.EqualTo(false));
        Assert.That(uxn.WorkingStack.PopByte(true), Is.EqualTo(0x11));
        Assert.That(uxn.WorkingStack.PopByte(true), Is.EqualTo(0x22));
        Assert.That(uxn.WorkingStack.PopByte(true), Is.EqualTo(0x11));
    }

    [Test]
    [TestCase(0x11, 0x11)]
    [TestCase(0x11, 0x12)]
    [TestCase(0x12, 0x11)]
    [TestCase(0x12, 0x12)]
    public void EQU(byte left, byte right)
    {
        var uxn = new UXNProcessor();
        uxn.SystemMem[0x100] = 0x08;
        uxn.WorkingStack.PushByte(left);
        uxn.WorkingStack.PushByte(right);
        Assert.That(uxn.Step(), Is.EqualTo(false));
        Assert.That(uxn.WorkingStack.PopByte(true) == 0x01, Is.EqualTo(left == right));
    }

    [Test]
    [TestCase(0x11, 0x11)]
    [TestCase(0x11, 0x12)]
    [TestCase(0x12, 0x11)]
    [TestCase(0x12, 0x12)]
    public void NEQ(byte left, byte right)
    {
        var uxn = new UXNProcessor();
        uxn.SystemMem[0x100] = 0x09;
        uxn.WorkingStack.PushByte(left);
        uxn.WorkingStack.PushByte(right);
        Assert.That(uxn.Step(), Is.EqualTo(false));
        Assert.That(uxn.WorkingStack.PopByte(true) == 0x01, Is.EqualTo(left != right));
    }

    [Test]
    [TestCase(0xf8, 0xf8, false)]
    [TestCase(0x01, 0x01, false)]
    [TestCase(0xf8, 0x01, true)]
    [TestCase(0x01, 0xf8, false)]
    public void GTH(byte left, byte right, bool expected)
    {
        var uxn = new UXNProcessor();
        uxn.SystemMem[0x100] = 0x0A;
        uxn.WorkingStack.PushByte(left);
        uxn.WorkingStack.PushByte(right);
        Assert.That(uxn.Step(), Is.EqualTo(false));
        Assert.That(uxn.WorkingStack.PopByte(true) == 0x01, Is.EqualTo(expected));
    }

    [Test]
    [TestCase(0xf8, 0xf8, false)]
    [TestCase(0x01, 0x01, false)]
    [TestCase(0xf8, 0x01, false)]
    [TestCase(0x01, 0xff, true)]
    public void LTH(byte left, byte right, bool expected)
    {
        var uxn = new UXNProcessor();
        uxn.SystemMem[0x100] = 0x0B;
        uxn.WorkingStack.PushByte(left);
        uxn.WorkingStack.PushByte(right);
        Assert.That(uxn.Step(), Is.EqualTo(false));
        Assert.That(uxn.WorkingStack.PopByte(true) == 0x01, Is.EqualTo(expected));
    }

    [Test]
    [TestCase(0xfe)] //jumps back 2 bytes to the instr just *before* the jmp
    [TestCase(0x10)]
    public void JMP(byte jmp)
    {
        var uxn = new UXNProcessor();
        uxn.SystemMem[0x100] = 0x0C;
        uxn.WorkingStack.PushByte(jmp);
        Assert.That(uxn.Step(), Is.EqualTo(false));
        Assert.That(uxn.PC, Is.EqualTo((ushort)(0x101 + (sbyte)jmp))); //the PC is incr'd by 1 so we add to that
    }

    [Test]
    [TestCase(0xfe, true)] //jumps back 2 bytes to the instr just *before* the jmp
    [TestCase(0x10, true)]
    [TestCase(0xfe, false)] //cond is false
    [TestCase(0x10, false)]
    public void JCN(byte jmp, bool cond)
    {
        var uxn = new UXNProcessor();
        uxn.SystemMem[0x100] = 0x0D;
        uxn.WorkingStack.PushByte((byte)(cond ? 0x01 : 0x00));
        uxn.WorkingStack.PushByte(jmp);
        Assert.That(uxn.Step(), Is.EqualTo(false));
        Assert.That(uxn.PC, Is.EqualTo(cond ? (ushort)(0x101 + (sbyte)jmp) : 0x101)); //the PC is incr'd by 1 so we add to that
    }

    [Test]
    [TestCase(0xfe)]
    [TestCase(0x10)]
    public void JSR(byte jmp)
    {
        var uxn = new UXNProcessor();
        uxn.SystemMem[0x100] = 0x0E;
        uxn.WorkingStack.PushByte(jmp);
        Assert.That(uxn.Step(), Is.EqualTo(false));
        Assert.That(uxn.PC, Is.EqualTo((ushort)(0x101 + (sbyte)jmp)));
        Assert.That(uxn.ReturnStack.PopShort(false), Is.EqualTo(0x101)); //and the return stack should point to the instr after JSR
    }

    [Test]
    public void STH()
    {
        var uxn = new UXNProcessor();
        uxn.SystemMem[0x100] = 0x0F;
        uxn.WorkingStack.PushByte(0x32);
        Assert.That(uxn.Step(), Is.EqualTo(false));
        Assert.That(uxn.ReturnStack.PopByte(false), Is.EqualTo(0x32));
    }

    [Test]
    public void LDZ()
    {
        var uxn = new UXNProcessor();
        uxn.SystemMem[0x100] = 0x10;
        uxn.SystemMem[0x00] = 0x32;
        uxn.WorkingStack.PushByte(0x00);
        Assert.That(uxn.Step(), Is.EqualTo(false));
        Assert.That(uxn.WorkingStack.PopByte(false), Is.EqualTo(0x32));
    }

    [Test]
    public void STZ()
    {
        var uxn = new UXNProcessor();
        uxn.SystemMem[0x100] = 0x11;
        uxn.WorkingStack.PushByte(0x32);
        uxn.WorkingStack.PushByte(0x00);
        Assert.That(uxn.Step(), Is.EqualTo(false));
        Assert.That(uxn.SystemMem[0x00], Is.EqualTo(0x32));
    }

    [Test]
    [TestCase(0xfe)]
    [TestCase(0x10)]
    public void LDR(byte offset)
    {
        var uxn = new UXNProcessor();
        uxn.SystemMem[0x100] = 0x12;
        ushort target = (ushort)(0x101 + (sbyte)offset);
        uxn.SystemMem[target] = 0x32;
        uxn.WorkingStack.PushByte(offset);
        Assert.That(uxn.Step(), Is.EqualTo(false));
        Assert.That(uxn.WorkingStack.PopByte(true), Is.EqualTo(0x32));
    }

    [Test]
    [TestCase(0xfe)]
    [TestCase(0x10)]
    public void STR(byte offset)
    {
        var uxn = new UXNProcessor();
        uxn.SystemMem[0x100] = 0x13;
        ushort target = (ushort)(0x101 + (sbyte)offset);
        uxn.WorkingStack.PushByte(0x32);
        uxn.WorkingStack.PushByte(offset);
        Assert.That(uxn.Step(), Is.EqualTo(false));
        Assert.That(uxn.SystemMem[target], Is.EqualTo(0x32));
    }

    [Test]
    public void LDA()
    {
        var uxn = new UXNProcessor();
        uxn.SystemMem[0x100] = 0x14;
        uxn.SystemMem[0x1234] = 0x32;
        uxn.WorkingStack.PushShort(0x1234);
        Assert.That(uxn.Step(), Is.EqualTo(false));
        Assert.That(uxn.WorkingStack.PopByte(true), Is.EqualTo(0x32));
    }

    [Test]
    public void STA()
    {
        var uxn = new UXNProcessor();
        uxn.SystemMem[0x100] = 0x15;
        uxn.WorkingStack.PushByte(0x32);
        uxn.WorkingStack.PushShort(0x1234);
        Assert.That(uxn.Step(), Is.EqualTo(false));
        Assert.That(uxn.SystemMem[0x1234], Is.EqualTo(0x32));
    }

    [Test]
    public void DEI()
    {
        var uxn = new UXNProcessor();
        uxn.SystemMem[0x100] = 0x16;
        uxn.DevMem[0x00] = 0x32;
        var testdev = new TestDevice();
        uxn.AttachDevice(0x00, testdev);
        Assert.That(uxn.Step(), Is.EqualTo(false));
        Assert.That(uxn.WorkingStack.PopByte(true), Is.EqualTo(0x32));
        Assert.That(testdev.dei, Is.EqualTo(0x00));
    }

    [Test]
    public void DEO()
    {
        var uxn = new UXNProcessor();
        uxn.SystemMem[0x100] = 0x17;
        uxn.WorkingStack.PushByte(0x32);
        uxn.WorkingStack.PushByte(0x00);
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
    public void ADD(byte left, byte right)
    {
        var uxn = new UXNProcessor();
        uxn.SystemMem[0x100] = 0x18;
        uxn.WorkingStack.PushByte(left);
        uxn.WorkingStack.PushByte(right);
        Assert.That(uxn.Step(), Is.EqualTo(false));
        Assert.That(uxn.WorkingStack.PopByte(true), Is.EqualTo((byte)(left + right)));
    }

    [Test]
    [TestCase(0x00, 0x00)]
    [TestCase(0x01, 0x01)]
    [TestCase(0x10, 0x20)]
    [TestCase(0xff, 0xff)]
    public void SUB(byte left, byte right)
    {
        var uxn = new UXNProcessor();
        uxn.SystemMem[0x100] = 0x19;
        uxn.WorkingStack.PushByte(left);
        uxn.WorkingStack.PushByte(right);
        Assert.That(uxn.Step(), Is.EqualTo(false));
        Assert.That(uxn.WorkingStack.PopByte(true), Is.EqualTo((byte)(left - right)));
    }

    [Test]
    [TestCase(0x00, 0x00)]
    [TestCase(0x01, 0x01)]
    [TestCase(0x10, 0x20)]
    [TestCase(0xff, 0xff)]
    public void MUL(byte left, byte right)
    {
        var uxn = new UXNProcessor();
        uxn.SystemMem[0x100] = 0x1A;
        uxn.WorkingStack.PushByte(left);
        uxn.WorkingStack.PushByte(right);
        Assert.That(uxn.Step(), Is.EqualTo(false));
        Assert.That(uxn.WorkingStack.PopByte(true), Is.EqualTo((byte)(left * right)));
    }

    [Test]
    [TestCase(0x00, 0x00)]
    [TestCase(0x10, 0x00)]
    [TestCase(0x01, 0x01)]
    [TestCase(0x10, 0x20)]
    [TestCase(0xff, 0xff)]
    public void DIV(byte left, byte right)
    {
        var uxn = new UXNProcessor();
        uxn.SystemMem[0x100] = 0x1B;
        uxn.WorkingStack.PushByte(left);
        uxn.WorkingStack.PushByte(right);
        Assert.That(uxn.Step(), Is.EqualTo(false));
        Assert.That(uxn.WorkingStack.PopByte(true), Is.EqualTo(right != 0 ? (byte)(left / right) : 0x00));
    }

    [Test]
    [Repeat(5, false)]
    public void AND()
    {
        var uxn = new UXNProcessor();
        uxn.SystemMem[0x100] = 0x1C;
        var left = _random.NextByte();
        var right = _random.NextByte();
        uxn.WorkingStack.PushByte(left);
        uxn.WorkingStack.PushByte(right);
        Assert.That(uxn.Step(), Is.EqualTo(false));
        Assert.That(uxn.WorkingStack.PopByte(true), Is.EqualTo(left & right));
    }

    [Test]
    [Repeat(5, false)]
    public void ORA()
    {
        var uxn = new UXNProcessor();
        uxn.SystemMem[0x100] = 0x1D;
        var left = _random.NextByte();
        var right = _random.NextByte();
        uxn.WorkingStack.PushByte(left);
        uxn.WorkingStack.PushByte(right);
        Assert.That(uxn.Step(), Is.EqualTo(false));
        Assert.That(uxn.WorkingStack.PopByte(true), Is.EqualTo(left | right));
    }

    [Test]
    [Repeat(5, false)]
    public void XOR()
    {
        var uxn = new UXNProcessor();
        uxn.SystemMem[0x100] = 0x1E;
        var left = _random.NextByte();
        var right = _random.NextByte();
        uxn.WorkingStack.PushByte(left);
        uxn.WorkingStack.PushByte(right);
        Assert.That(uxn.Step(), Is.EqualTo(false));
        Assert.That(uxn.WorkingStack.PopByte(true), Is.EqualTo(left ^ right));
    }

    [Test]
    [TestCase(0x12, 0x00, 0x12)]
    [TestCase(0x34, 0x10, 0x68)] // left by 1. double the value
    [TestCase(0x32, 0x01, 0x19)] // right by 1. half the value
    public void SFT(byte input, byte sft, byte output)
    {
        var uxn = new UXNProcessor();
        uxn.SystemMem[0x100] = 0x1F;
        uxn.WorkingStack.PushByte(input);
        uxn.WorkingStack.PushByte(sft);
        Assert.That(uxn.Step(), Is.EqualTo(false));
        Assert.That(uxn.WorkingStack.PopByte(true), Is.EqualTo(output));
    }
}
