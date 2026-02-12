using System;
using Content.Server._Starlight.UXN;
using NUnit.Framework;
using Robust.Shared.IoC;
using Robust.Shared.Random;

namespace Content.Tests.Shared._Starlight.UXN;

// Basic tests of various damage prototypes and classes.
[TestFixture]
public sealed class UxnShortOpcodeTest : ContentUnitTest
{

    private IRobustRandom _random = default!;

    [OneTimeSetUp]
    public void OneTimeSetup() => _random = IoCManager.Resolve<IRobustRandom>();

    [Test]
    [TestCase(0x00)]
    [TestCase(0x32)]
    [TestCase(0xFF)] //this one *should* wrap to 0x00
    public void INC2(short val)
    {
        var uxn = new UXNProcessor();
        uxn.SystemMem[0x100] = 0x01 + 0x20;
        uxn.WorkingStack.PushShort((ushort)val);
        Assert.That(uxn.Step(), Is.EqualTo(false));
        Assert.That(uxn.WorkingStack.PopShort(true), Is.EqualTo((ushort)(val + 1)));
    }

    [Test]
    public void POP2()
    {
        var uxn = new UXNProcessor();
        uxn.SystemMem[0x100] = 0x02 + 0x20;
        uxn.WorkingStack.PushShort(0x1234);
        Assert.That(uxn.Step(), Is.EqualTo(false));
        Assert.That(uxn.WorkingStack.StackPointer, Is.EqualTo(0x00));
    }

    [Test]
    public void NIP2()
    {
        var uxn = new UXNProcessor();
        uxn.SystemMem[0x100] = 0x03 + 0x20;
        uxn.WorkingStack.PushShort(0x1234);
        uxn.WorkingStack.PushShort(0x5678);
        Assert.That(uxn.Step(), Is.EqualTo(false));
        Assert.That(uxn.WorkingStack.StackPointer, Is.EqualTo(0x02));
        Assert.That(uxn.WorkingStack.PopShort(true), Is.EqualTo(0x5678));
    }

    [Test]
    public void SWP2()
    {
        var uxn = new UXNProcessor();
        uxn.SystemMem[0x100] = 0x04 + 0x20;
        uxn.WorkingStack.PushShort(0x1234);
        uxn.WorkingStack.PushShort(0x5678);
        Assert.That(uxn.Step(), Is.EqualTo(false));
        Assert.That(uxn.WorkingStack.PopShort(true), Is.EqualTo(0x1234));
        Assert.That(uxn.WorkingStack.PopShort(true), Is.EqualTo(0x5678));
    }

    [Test]
    public void ROT2()
    {
        var uxn = new UXNProcessor();
        uxn.SystemMem[0x100] = 0x05 + 0x20;
        uxn.WorkingStack.PushShort(0x1111);
        uxn.WorkingStack.PushShort(0x2222);
        uxn.WorkingStack.PushShort(0x3333);
        Assert.That(uxn.Step(), Is.EqualTo(false));
        Assert.That(uxn.WorkingStack.PopShort(true), Is.EqualTo(0x1111));
        Assert.That(uxn.WorkingStack.PopShort(true), Is.EqualTo(0x3333));
        Assert.That(uxn.WorkingStack.PopShort(true), Is.EqualTo(0x2222));
    }

    [Test]
    public void DUP2()
    {
        var uxn = new UXNProcessor();
        uxn.SystemMem[0x100] = 0x06 + 0x20;
        uxn.WorkingStack.PushShort(0x1122);
        Assert.That(uxn.Step(), Is.EqualTo(false));
        Assert.That(uxn.WorkingStack.PopShort(true), Is.EqualTo(0x1122));
        Assert.That(uxn.WorkingStack.PopShort(true), Is.EqualTo(0x1122));
    }

    [Test]
    public void OVR2()
    {
        var uxn = new UXNProcessor();
        uxn.SystemMem[0x100] = 0x07 + 0x20;
        uxn.WorkingStack.PushShort(0x1111);
        uxn.WorkingStack.PushShort(0x2222);
        Assert.That(uxn.Step(), Is.EqualTo(false));
        Assert.That(uxn.WorkingStack.PopShort(true), Is.EqualTo(0x1111));
        Assert.That(uxn.WorkingStack.PopShort(true), Is.EqualTo(0x2222));
        Assert.That(uxn.WorkingStack.PopShort(true), Is.EqualTo(0x1111));
    }

    [Test]
    [TestCase(0x1111, 0x1111)]
    [TestCase(0x1111, 0x1212)]
    [TestCase(0x1212, 0x1111)]
    [TestCase(0x1212, 0x1212)]
    public void EQU2(short left, short right)
    {
        var uxn = new UXNProcessor();
        uxn.SystemMem[0x100] = 0x08 + 0x20;
        uxn.WorkingStack.PushShort((ushort)left);
        uxn.WorkingStack.PushShort((ushort)right);
        Assert.That(uxn.Step(), Is.EqualTo(false));
        Assert.That(uxn.WorkingStack.PopByte(true) == 0x01, Is.EqualTo(left == right));
    }

    [Test]
    [TestCase(0x1111, 0x1111)]
    [TestCase(0x1111, 0x1212)]
    [TestCase(0x1212, 0x1111)]
    [TestCase(0x1212, 0x1212)]
    public void NEQ2(short left, short right)
    {
        var uxn = new UXNProcessor();
        uxn.SystemMem[0x100] = 0x09 + 0x20;
        uxn.WorkingStack.PushShort((ushort)left);
        uxn.WorkingStack.PushShort((ushort)right);
        Assert.That(uxn.Step(), Is.EqualTo(false));
        Assert.That(uxn.WorkingStack.PopByte(true) == 0x01, Is.EqualTo(left != right));
    }

    [Test]
    [TestCase(0xf801, 0xf801, false)]
    [TestCase(0x01f8, 0x01f8, false)]
    [TestCase(0xf801, 0x01f8, true)]
    [TestCase(0x01f8, 0xf801, false)]
    public void GTH2(int left, int right, bool expected)
    {
        var uxn = new UXNProcessor();
        uxn.SystemMem[0x100] = 0x0A + 0x20;
        uxn.WorkingStack.PushShort((ushort)left);
        uxn.WorkingStack.PushShort((ushort)right);
        Assert.That(uxn.Step(), Is.EqualTo(false));
        Assert.That(uxn.WorkingStack.PopByte(true) == 0x01, Is.EqualTo(expected));
    }

    [Test]
    [TestCase(0xf801, 0xf801, false)]
    [TestCase(0x01f8, 0x01f8, false)]
    [TestCase(0xf801, 0x01f8, false)]
    [TestCase(0x01f8, 0xf801, true)]
    public void LTH2(int left, int right, bool expected)
    {
        var uxn = new UXNProcessor();
        uxn.SystemMem[0x100] = 0x0B + 0x20;
        uxn.WorkingStack.PushShort((ushort)left);
        uxn.WorkingStack.PushShort((ushort)right);
        Assert.That(uxn.Step(), Is.EqualTo(false));
        Assert.That(uxn.WorkingStack.PopByte(true) == 0x01, Is.EqualTo(expected));
    }

    [Test]
    [TestCase(0x1234)]
    [TestCase(0x5678)]
    [TestCase(0x0000)]
    public void JMP2(short jmp)
    {
        var uxn = new UXNProcessor();
        uxn.SystemMem[0x100] = 0x0C + 0x20;
        uxn.WorkingStack.PushShort((ushort)jmp);
        Assert.That(uxn.Step(), Is.EqualTo(false));
        Assert.That(uxn.PC, Is.EqualTo((ushort)jmp)); //the PC is incr'd by 1 so we add to that
    }

    [Test]
    [TestCase(0x1234, true)]
    [TestCase(0x5678, true)]
    [TestCase(0x0000, true)]
    [TestCase(0x1234, false)]
    [TestCase(0x5678, false)]
    [TestCase(0x0000, false)]
    public void JCN2(short jmp, bool cond)
    {
        var uxn = new UXNProcessor();
        uxn.SystemMem[0x100] = 0x0D + 0x20;
        uxn.WorkingStack.PushByte((byte)(cond ? 0x01 : 0x00));
        uxn.WorkingStack.PushShort((ushort)jmp);
        Assert.That(uxn.Step(), Is.EqualTo(false));
        Assert.That(uxn.PC, Is.EqualTo(cond ? (ushort)jmp : 0x101)); //the PC is incr'd by 1 so we add to that
    }

    [Test]
    [TestCase(0x1234)]
    [TestCase(0x5678)]
    [TestCase(0x0000)]
    public void JSR2(short jmp)
    {
        var uxn = new UXNProcessor();
        uxn.SystemMem[0x100] = 0x0E + 0x20;
        uxn.WorkingStack.PushShort((ushort)jmp);
        Assert.That(uxn.Step(), Is.EqualTo(false));
        Assert.That(uxn.PC, Is.EqualTo((ushort)jmp));
        Assert.That(uxn.ReturnStack.PopShort(false), Is.EqualTo(0x101)); //and the return stack should point to the instr after JSR
    }

    [Test]
    public void STH2()
    {
        var uxn = new UXNProcessor();
        uxn.SystemMem[0x100] = 0x0F + 0x20;
        uxn.WorkingStack.PushShort(0x1234);
        Assert.That(uxn.Step(), Is.EqualTo(false));
        Assert.That(uxn.ReturnStack.PopShort(false), Is.EqualTo(0x1234));
    }

    [Test]
    public void LDZ2()
    {
        var uxn = new UXNProcessor();
        uxn.SystemMem[0x100] = 0x10 + 0x20;
        uxn.SystemMem[0x00] = 0x12;
        uxn.SystemMem[0x01] = 0x34;
        uxn.WorkingStack.PushByte(0x00);
        Assert.That(uxn.Step(), Is.EqualTo(false));
        Assert.That(uxn.WorkingStack.PopShort(false), Is.EqualTo(0x1234));
    }

    [Test]
    public void LDZ2_Wrapping()
    {
        var uxn = new UXNProcessor();
        uxn.SystemMem[0x100] = 0x10 + 0x20;
        uxn.SystemMem[0xff] = 0x12;
        uxn.SystemMem[0x00] = 0x34;
        uxn.WorkingStack.PushByte(0xff);
        Assert.That(uxn.Step(), Is.EqualTo(false));
        Assert.That(uxn.WorkingStack.PopShort(false), Is.EqualTo(0x1234));
    }

    [Test]
    public void STZ2()
    {
        var uxn = new UXNProcessor();
        uxn.SystemMem[0x100] = 0x11 + 0x20;
        uxn.WorkingStack.PushShort(0x1234);
        uxn.WorkingStack.PushByte(0x00);
        Assert.That(uxn.Step(), Is.EqualTo(false));
        Assert.That(uxn.SystemMem[0x00], Is.EqualTo(0x12));
        Assert.That(uxn.SystemMem[0x01], Is.EqualTo(0x34));
    }

    [Test]
    public void STZ2_Wrapping()
    {
        var uxn = new UXNProcessor();
        uxn.SystemMem[0x100] = 0x11 + 0x20;
        uxn.WorkingStack.PushShort(0x1234);
        uxn.WorkingStack.PushByte(0xff);
        Assert.That(uxn.Step(), Is.EqualTo(false));
        Assert.That(uxn.SystemMem[0xff], Is.EqualTo(0x12));
        Assert.That(uxn.SystemMem[0x00], Is.EqualTo(0x34));
    }
    
    [Test]
    [TestCase(0xfd)]
    [TestCase(0x10)]
    public void LDR2(byte offset)
    {
        var uxn = new UXNProcessor();
        uxn.SystemMem[0x100] = 0x12 + 0x20;
        ushort target = (ushort)(0x101 + (sbyte)offset);
        uxn.SystemMem[target] = 0x12;
        uxn.SystemMem[(ushort)(target + 1)] = 0x34;
        uxn.WorkingStack.PushByte(offset);
        Assert.That(uxn.Step(), Is.EqualTo(false));
        Assert.That(uxn.WorkingStack.PopShort(true), Is.EqualTo(0x1234));
    }

    [Test]
    [TestCase(0xfd)]
    [TestCase(0x10)]
    public void STR2(byte offset)
    {
        var uxn = new UXNProcessor();
        uxn.SystemMem[0x100] = 0x13 + 0x20;
        ushort target = (ushort)(0x101 + (sbyte)offset);
        uxn.WorkingStack.PushShort(0x1234);
        uxn.WorkingStack.PushByte(offset);
        Assert.That(uxn.Step(), Is.EqualTo(false));
        Assert.That(uxn.SystemMem[target], Is.EqualTo(0x12));
        Assert.That(uxn.SystemMem[(ushort)(target+1)], Is.EqualTo(0x34));
    }

    [Test]
    public void LDA2()
    {
        var uxn = new UXNProcessor();
        uxn.SystemMem[0x100] = 0x14 + 0x20;
        uxn.SystemMem[0x1234] = 0x56;
        uxn.SystemMem[0x1235] = 0x78;
        uxn.WorkingStack.PushShort(0x1234);
        Assert.That(uxn.Step(), Is.EqualTo(false));
        var result = uxn.WorkingStack.PopShort(true);
        Assert.That(result, Is.EqualTo(0x5678));
    }

    [Test]
    public void LDA2_Wrapping()
    {
        var uxn = new UXNProcessor();
        uxn.SystemMem[0x100] = 0x14 + 0x20;
        uxn.SystemMem[0xffff] = 0x56;
        uxn.SystemMem[0x0000] = 0x78;
        uxn.WorkingStack.PushShort(0xffff);
        Assert.That(uxn.Step(), Is.EqualTo(false));
        Assert.That(uxn.WorkingStack.PopShort(true), Is.EqualTo(0x5678));
    }

    [Test]
    public void STA2()
    {
        var uxn = new UXNProcessor();
        uxn.SystemMem[0x100] = 0x15 + 0x20;
        uxn.WorkingStack.PushShort(0x5678);
        uxn.WorkingStack.PushShort(0x1234);
        Assert.That(uxn.Step(), Is.EqualTo(false));
        Assert.That(uxn.SystemMem[0x1234], Is.EqualTo(0x56));
        Assert.That(uxn.SystemMem[0x1235], Is.EqualTo(0x78));
    }

    [Test]
    public void STA2_Wrapping()
    {
        var uxn = new UXNProcessor();
        uxn.SystemMem[0x100] = 0x15 + 0x20;
        uxn.WorkingStack.PushShort(0x5678);
        uxn.WorkingStack.PushShort(0xffff);
        Assert.That(uxn.Step(), Is.EqualTo(false));
        Assert.That(uxn.SystemMem[0xffff], Is.EqualTo(0x56));
        Assert.That(uxn.SystemMem[0x0000], Is.EqualTo(0x78));
    }

    [Test]
    public void DEI2()
    {
        var uxn = new UXNProcessor();
        uxn.SystemMem[0x100] = 0x16 + 0x20;
        uxn.DevMem[0x00] = 0x12;
        uxn.DevMem[0x01] = 0x34;
        var testdev = new TestDevice();
        uxn.AttachDevice(0x00, testdev);
        uxn.WorkingStack.PushByte(0x00);
        Assert.That(uxn.Step(), Is.EqualTo(false));
        Assert.That(uxn.WorkingStack.PopShort(true), Is.EqualTo(0x1234));
        Assert.That(testdev.dei, Is.Not.Null);
        Assert.That(testdev.dei, Is.EqualTo(0x01)); //cause it will read the end
    }

    [Test]
    public void DEI2_Wrapping()
    {
        var uxn = new UXNProcessor();
        uxn.SystemMem[0x100] = 0x16 + 0x20;
        uxn.DevMem[0xff] = 0x12;
        uxn.DevMem[0x00] = 0x34;
        var testdev = new TestDevice();
        var testdev2 = new TestDevice();
        uxn.AttachDevice(0x0, testdev);
        uxn.AttachDevice(0xf, testdev2);
        uxn.WorkingStack.PushByte(0xff);
        Assert.That(uxn.Step(), Is.EqualTo(false));
        Assert.That(uxn.WorkingStack.PopShort(true), Is.EqualTo(0x1234));
        Assert.That(testdev.dei, Is.EqualTo(0x00));
        Assert.That(testdev2.dei, Is.EqualTo(0xff));
    }

    [Test]
    public void DEO2()
    {
        var uxn = new UXNProcessor();
        uxn.SystemMem[0x100] = 0x17 + 0x20;
        uxn.WorkingStack.PushShort(0x1234);
        uxn.WorkingStack.PushByte(0x00);
        var testdev = new TestDevice();
        uxn.AttachDevice(0x0, testdev);
        Assert.That(uxn.Step(), Is.EqualTo(false));
        Assert.That(uxn.DevMem[0x00], Is.EqualTo(0x12));
        Assert.That(uxn.DevMem[0x01], Is.EqualTo(0x34));
        Assert.That(testdev.deo, Is.EqualTo(0x01));
    }

    [Test]
    public void DEO2_Wrapping()
    {
        var uxn = new UXNProcessor();
        uxn.SystemMem[0x100] = 0x17 + 0x20;
        uxn.WorkingStack.PushShort(0x1234);
        uxn.WorkingStack.PushByte(0xff);
        var testdev = new TestDevice();
        var testdev2 = new TestDevice();
        uxn.AttachDevice(0xf, testdev);
        uxn.AttachDevice(0x0, testdev2);
        Assert.That(uxn.Step(), Is.EqualTo(false));
        Assert.That(uxn.DevMem[0xff], Is.EqualTo(0x12));
        Assert.That(uxn.DevMem[0x00], Is.EqualTo(0x34));
        Assert.That(testdev.deo, Is.EqualTo(0xff));
        Assert.That(testdev2.deo, Is.EqualTo(0x00));
    }

    [Test]
    [Repeat(10, false)]
    public void ADD2()
    {
        var left = (ushort)((_random.NextByte() << 8) + _random.NextByte());
        var right = (ushort)((_random.NextByte() << 8) + _random.NextByte());
        var uxn = new UXNProcessor();
        uxn.SystemMem[0x100] = 0x18 + 0x20;
        uxn.WorkingStack.PushShort(left);
        uxn.WorkingStack.PushShort(right);
        Assert.That(uxn.Step(), Is.EqualTo(false));
        Assert.That(uxn.WorkingStack.PopShort(true), Is.EqualTo((ushort)(left + right)));
    }

    [Test]
    [Repeat(10, false)]
    public void SUB2()
    {
        var left = (ushort)((_random.NextByte() << 8) + _random.NextByte());
        var right = (ushort)((_random.NextByte() << 8) + _random.NextByte());
        var uxn = new UXNProcessor();
        uxn.SystemMem[0x100] = 0x19 + 0x20;
        uxn.WorkingStack.PushShort(left);
        uxn.WorkingStack.PushShort(right);
        Assert.That(uxn.Step(), Is.EqualTo(false));
        Assert.That(uxn.WorkingStack.PopShort(true), Is.EqualTo((ushort)(left - right)));
    }

    [Test]
    [Repeat(10, false)]
    public void MUL2()
    {
        var left = (ushort)((_random.NextByte() << 8) + _random.NextByte());
        var right = (ushort)((_random.NextByte() << 8) + _random.NextByte());
        var uxn = new UXNProcessor();
        uxn.SystemMem[0x100] = 0x1A + 0x20;
        uxn.WorkingStack.PushShort(left);
        uxn.WorkingStack.PushShort(right);
        Assert.That(uxn.Step(), Is.EqualTo(false));
        Assert.That(uxn.WorkingStack.PopShort(true), Is.EqualTo((ushort)(left * right)));
    }

    [Test]
    [Repeat(10, false)]
    public void DIV2()
    {
        var left = (ushort)((_random.NextByte() << 8) + _random.NextByte());
        var right = (ushort)((_random.NextByte() << 8) + _random.NextByte());
        var uxn = new UXNProcessor();
        uxn.SystemMem[0x100] = 0x1B + 0x20;
        uxn.WorkingStack.PushShort(left);
        uxn.WorkingStack.PushShort(right);
        Assert.That(uxn.Step(), Is.EqualTo(false));
        Assert.That(uxn.WorkingStack.PopShort(true), Is.EqualTo((ushort)(left / right)));
    }

    [Test]
    [Repeat(10, false)]
    public void AND2()
    {
        var uxn = new UXNProcessor();
        uxn.SystemMem[0x100] = 0x1C + 0x20;
        var left = (ushort)((_random.NextByte() << 8) + _random.NextByte());
        var right = (ushort)((_random.NextByte() << 8) + _random.NextByte());
        uxn.WorkingStack.PushShort(left);
        uxn.WorkingStack.PushShort(right);
        Assert.That(uxn.Step(), Is.EqualTo(false));
        Assert.That(uxn.WorkingStack.PopShort(true), Is.EqualTo(left & right));
    }

    [Test]
    [Repeat(10, false)]
    public void ORA2()
    {
        var uxn = new UXNProcessor();
        uxn.SystemMem[0x100] = 0x1D + 0x20;
        var left = (ushort)((_random.NextByte() << 8) + _random.NextByte());
        var right = (ushort)((_random.NextByte() << 8) + _random.NextByte());
        uxn.WorkingStack.PushShort(left);
        uxn.WorkingStack.PushShort(right);
        Assert.That(uxn.Step(), Is.EqualTo(false));
        Assert.That(uxn.WorkingStack.PopShort(true), Is.EqualTo(left | right));
    }

    [Test]
    [Repeat(10, false)]
    public void EOR2()
    {
        var uxn = new UXNProcessor();
        uxn.SystemMem[0x100] = 0x1E + 0x20;
        var left = (ushort)((_random.NextByte() << 8) + _random.NextByte());
        var right = (ushort)((_random.NextByte() << 8) + _random.NextByte());
        uxn.WorkingStack.PushShort(left);
        uxn.WorkingStack.PushShort(right);
        Assert.That(uxn.Step(), Is.EqualTo(false));
        Assert.That(uxn.WorkingStack.PopShort(true), Is.EqualTo(left ^ right));
    }

    [Test]
    [TestCase(0x1234, 0x00, 0x1234)]
    [TestCase(0x1234, 0x10, 0x2468)] // left by 1. double the value
    [TestCase(0x1234, 0x01, 0x091a)] // right by 1. half the value
    public void SFT2(short input, byte sft, short output)
    {
        var uxn = new UXNProcessor();
        uxn.SystemMem[0x100] = 0x1F + 0x20;
        uxn.WorkingStack.PushShort((ushort)input);
        uxn.WorkingStack.PushByte(sft);
        Assert.That(uxn.Step(), Is.EqualTo(false));
        Assert.That(uxn.WorkingStack.PopShort(true), Is.EqualTo(output));
    }
}
