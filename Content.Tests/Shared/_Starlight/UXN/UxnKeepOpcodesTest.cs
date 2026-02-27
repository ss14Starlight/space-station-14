using System;
using Content.Server._Starlight.UXN;
using NUnit.Framework;
using Robust.Shared.IoC;
using Robust.Shared.Random;

namespace Content.Tests.Shared._Starlight.UXN;

// Basic tests of various damage prototypes and classes.
[TestFixture]
public sealed class UxnKeepOpcodeTest : ContentUnitTest
{

    private IRobustRandom _random = default!;

    [OneTimeSetUp]
    public void OneTimeSetup() => _random = IoCManager.Resolve<IRobustRandom>();

    [Test]
    [TestCase(0x00)]
    [TestCase(0x32)]
    [TestCase(0xFF)] //this one *should* wrap to 0x00
    public void INCk(byte val)
    {
        var uxn = new UXNProcessor();
        uxn.SystemMem[0x100] = (byte)UxnOpcode.INC | (byte)UxnOpcodeFlag.Short;
        uxn.WorkingStack.PushByte(val);
        Assert.That(uxn.Step(), Is.False);
        Assert.That(uxn.WorkingStack.PopByte(true), Is.EqualTo<byte>((byte)(val + 1)));
        Assert.That(uxn.WorkingStack.PopByte(true), Is.EqualTo<byte>(val));
    }

    [Test]
    public void POPk()
    {
        var uxn = new UXNProcessor();
        uxn.SystemMem[0x100] = (byte)UxnOpcode.POP | (byte)UxnOpcodeFlag.Short;
        uxn.WorkingStack.PushByte(0x32);
        Assert.That(uxn.Step(), Is.False);
        Assert.That(uxn.WorkingStack.StackPointer, Is.EqualTo(0x01)); //cause it is a cannonical No-Op
        Assert.That(uxn.WorkingStack.PopByte(true), Is.EqualTo<byte>(0x32));
    }

    [Test]
    public void NIPk()
    {
        var uxn = new UXNProcessor();
        uxn.SystemMem[0x100] = (byte)UxnOpcode.NIP | (byte)UxnOpcodeFlag.Short;
        uxn.WorkingStack.PushShort(0x1234);
        Assert.That(uxn.Step(), Is.False);
        Assert.That(uxn.WorkingStack.StackPointer, Is.EqualTo(0x03));
        Assert.That(uxn.WorkingStack.PopByte(true), Is.EqualTo(0x34));
        Assert.That(uxn.WorkingStack.PopShort(true), Is.EqualTo(0x1234));
    }

    [Test]
    public void SWPk()
    {
        var uxn = new UXNProcessor();
        uxn.SystemMem[0x100] = (byte)UxnOpcode.SWP | (byte)UxnOpcodeFlag.Short;
        uxn.WorkingStack.PushShort(0x1234);
        Assert.That(uxn.Step(), Is.False);
        Assert.That(uxn.WorkingStack.PopShort(true), Is.EqualTo(0x3412));
        Assert.That(uxn.WorkingStack.PopShort(true), Is.EqualTo(0x1234));
    }

    [Test]
    public void ROTk()
    {
        var uxn = new UXNProcessor();
        uxn.SystemMem[0x100] = (byte)UxnOpcode.ROT | (byte)UxnOpcodeFlag.Short;
        uxn.WorkingStack.PushByte(0x11);
        uxn.WorkingStack.PushByte(0x22);
        uxn.WorkingStack.PushByte(0x33);
        Assert.That(uxn.Step(), Is.False);
        Assert.That(uxn.WorkingStack.PopByte(true), Is.EqualTo(0x11));
        Assert.That(uxn.WorkingStack.PopByte(true), Is.EqualTo(0x33));
        Assert.That(uxn.WorkingStack.PopByte(true), Is.EqualTo(0x22));
        Assert.That(uxn.WorkingStack.PopByte(true), Is.EqualTo(0x33));
        Assert.That(uxn.WorkingStack.PopByte(true), Is.EqualTo(0x22));
        Assert.That(uxn.WorkingStack.PopByte(true), Is.EqualTo(0x11));
    }

    [Test]
    public void DUPk()
    {
        var uxn = new UXNProcessor();
        uxn.SystemMem[0x100] = (byte)UxnOpcode.DUP | (byte)UxnOpcodeFlag.Short;
        uxn.WorkingStack.PushByte(0x11);
        Assert.That(uxn.Step(), Is.False);
        Assert.That(uxn.WorkingStack.PopByte(true), Is.EqualTo(0x11));
        Assert.That(uxn.WorkingStack.PopByte(true), Is.EqualTo(0x11));
        Assert.That(uxn.WorkingStack.PopByte(true), Is.EqualTo(0x11));
    }

    [Test]
    public void OVRk()
    {
        var uxn = new UXNProcessor();
        uxn.SystemMem[0x100] = (byte)UxnOpcode.OVR | (byte)UxnOpcodeFlag.Short;
        uxn.WorkingStack.PushByte(0x11);
        uxn.WorkingStack.PushByte(0x22);
        Assert.That(uxn.Step(), Is.False);
        Assert.That(uxn.WorkingStack.PopByte(true), Is.EqualTo(0x11));
        Assert.That(uxn.WorkingStack.PopByte(true), Is.EqualTo(0x22));
        Assert.That(uxn.WorkingStack.PopByte(true), Is.EqualTo(0x11));
        Assert.That(uxn.WorkingStack.PopByte(true), Is.EqualTo(0x22));
        Assert.That(uxn.WorkingStack.PopByte(true), Is.EqualTo(0x11));
    }

    [Test]
    [TestCase(0x11, 0x11)]
    [TestCase(0x11, 0x12)]
    [TestCase(0x12, 0x11)]
    [TestCase(0x12, 0x12)]
    public void EQUk(byte left, byte right)
    {
        var uxn = new UXNProcessor();
        uxn.SystemMem[0x100] = (byte)UxnOpcode.EQU | (byte)UxnOpcodeFlag.Short;
        uxn.WorkingStack.PushByte(left);
        uxn.WorkingStack.PushByte(right);
        Assert.That(uxn.Step(), Is.False);
        Assert.That(uxn.WorkingStack.PopByte(true) == (byte)UxnOpcode.INC, Is.EqualTo(left == right));
        Assert.That(uxn.WorkingStack.PopByte(true), Is.EqualTo(right));
        Assert.That(uxn.WorkingStack.PopByte(true), Is.EqualTo(left));
    }

    [Test]
    [TestCase(0x11, 0x11)]
    [TestCase(0x11, 0x12)]
    [TestCase(0x12, 0x11)]
    [TestCase(0x12, 0x12)]
    public void NEQk(byte left, byte right)
    {
        var uxn = new UXNProcessor();
        uxn.SystemMem[0x100] = (byte)UxnOpcode.NEQ | (byte)UxnOpcodeFlag.Short;
        uxn.WorkingStack.PushByte(left);
        uxn.WorkingStack.PushByte(right);
        Assert.That(uxn.Step(), Is.False);
        Assert.That(uxn.WorkingStack.PopByte(true) == (byte)UxnOpcode.INC, Is.EqualTo(left != right));
        Assert.That(uxn.WorkingStack.PopByte(true), Is.EqualTo(right));
        Assert.That(uxn.WorkingStack.PopByte(true), Is.EqualTo(left));
    }

    [Test]
    [TestCase(0x12, 0x34)]
    [TestCase(0x34, 0x12)]
    [TestCase(0x00, 0xff)]
    [TestCase(0x00, 0x00)]
    public void GTHk(byte left, byte right)
    {
        var uxn = new UXNProcessor();
        uxn.SystemMem[0x100] = (byte)UxnOpcode.GTH | (byte)UxnOpcodeFlag.Short;
        uxn.WorkingStack.PushByte(left);
        uxn.WorkingStack.PushByte(right);
        Assert.That(uxn.Step(), Is.False);
        Assert.That(uxn.WorkingStack.PopByte(true) == (byte)UxnOpcode.INC, Is.EqualTo(left > right));
        Assert.That(uxn.WorkingStack.PopByte(true), Is.EqualTo(right));
        Assert.That(uxn.WorkingStack.PopByte(true), Is.EqualTo(left));
    }

    [Test]
    [TestCase(0x12, 0x34)]
    [TestCase(0x34, 0x12)]
    [TestCase(0x00, 0xff)]
    [TestCase(0xff, 0x00)]
    [TestCase(0x00, 0x00)]
    public void LTHk(byte right, byte left)
    {
        var uxn = new UXNProcessor();
        uxn.SystemMem[0x100] = (byte)UxnOpcode.LTH | (byte)UxnOpcodeFlag.Short;
        uxn.WorkingStack.PushByte(left);
        uxn.WorkingStack.PushByte(right);
        Assert.That(uxn.Step(), Is.False);
        Assert.That(uxn.WorkingStack.PopByte(true) == (byte)UxnOpcode.INC, Is.EqualTo(left < right));
        Assert.That(uxn.WorkingStack.PopByte(true), Is.EqualTo(right));
        Assert.That(uxn.WorkingStack.PopByte(true), Is.EqualTo(left));
    }

    [Test]
    [TestCase(0xfe)] //jumps back 2 bytes to the instr just *before* the jmp
    [TestCase(0x10)]
    public void JMPk(byte jmp)
    {
        var uxn = new UXNProcessor();
        uxn.SystemMem[0x100] = (byte)UxnOpcode.JMP | (byte)UxnOpcodeFlag.Short;
        uxn.WorkingStack.PushByte(jmp);
        Assert.That(uxn.Step(), Is.False);
        Assert.That(uxn.PC, Is.EqualTo((ushort)(0x101 + (sbyte)jmp))); //the PC is incr'd by 1 so we add to that
        Assert.That(uxn.WorkingStack.PopByte(true), Is.EqualTo(jmp));
    }

    [Test]
    [TestCase(0xfe, true)] //jumps back 2 bytes to the instr just *before* the jmp
    [TestCase(0x10, true)]
    [TestCase(0xfe, false)] //cond is false
    [TestCase(0x10, false)]
    public void JCNk(byte jmp, bool cond)
    {
        var uxn = new UXNProcessor();
        uxn.SystemMem[0x100] = (byte)UxnOpcode.JCN | (byte)UxnOpcodeFlag.Short;
        uxn.WorkingStack.PushByte((byte)(cond ? 0x01 : 0x00));
        uxn.WorkingStack.PushByte(jmp);
        Assert.That(uxn.Step(), Is.False);
        Assert.That(uxn.PC, Is.EqualTo(cond ? (ushort)(0x101 + (sbyte)jmp) : 0x101)); //the PC is incr'd by 1 so we add to that
        Assert.That(uxn.WorkingStack.PopByte(true), Is.EqualTo(jmp));
        Assert.That(uxn.WorkingStack.PopByte(true) == (byte)UxnOpcode.INC, Is.EqualTo(cond));

    }

    [Test]
    [TestCase(0xfe)]
    [TestCase(0x10)]
    public void JSRk(byte jmp)
    {
        var uxn = new UXNProcessor();
        uxn.SystemMem[0x100] = (byte)UxnOpcode.JSR | (byte)UxnOpcodeFlag.Short;
        uxn.WorkingStack.PushByte(jmp);
        Assert.That(uxn.Step(), Is.False);
        Assert.That(uxn.PC, Is.EqualTo((ushort)(0x101 + (sbyte)jmp)));
        Assert.That(uxn.ReturnStack.PopShort(false), Is.EqualTo(0x101)); //and the return stack should point to the instr after JSR
        Assert.That(uxn.WorkingStack.PopByte(true), Is.EqualTo(jmp));
    }

    [Test]
    public void STHk()
    {
        var uxn = new UXNProcessor();
        uxn.SystemMem[0x100] = (byte)UxnOpcode.STH | (byte)UxnOpcodeFlag.Short;
        uxn.WorkingStack.PushByte(0x32);
        Assert.That(uxn.Step(), Is.False);
        Assert.That(uxn.ReturnStack.PopByte(false), Is.EqualTo(0x32));
        Assert.That(uxn.WorkingStack.PopByte(true), Is.EqualTo(0x32));
    }

    [Test]
    public void LDZk()
    {
        var uxn = new UXNProcessor();
        uxn.SystemMem[0x100] = (byte)UxnOpcode.LDZ | (byte)UxnOpcodeFlag.Short;
        uxn.SystemMem[0x00] = 0x32;
        uxn.WorkingStack.PushByte(0x00);
        Assert.That(uxn.Step(), Is.False);
        Assert.That(uxn.WorkingStack.PopByte(false), Is.EqualTo(0x32));
        Assert.That(uxn.WorkingStack.PopByte(true), Is.EqualTo(0x00));
    }

    [Test]
    public void STZk()
    {
        var uxn = new UXNProcessor();
        uxn.SystemMem[0x100] = (byte)UxnOpcode.STZ | (byte)UxnOpcodeFlag.Short;
        uxn.WorkingStack.PushByte(0x32);
        uxn.WorkingStack.PushByte(0x00);
        Assert.That(uxn.Step(), Is.False);
        Assert.That(uxn.SystemMem[0x00], Is.EqualTo(0x32));
        Assert.That(uxn.WorkingStack.PopShort(true), Is.EqualTo(0x3200));
    }

    [Test]
    [TestCase(0xfe)]
    [TestCase(0x10)]
    public void LDRk(byte offset)
    {
        var uxn = new UXNProcessor();
        uxn.SystemMem[0x100] = (byte)UxnOpcode.LDR | (byte)UxnOpcodeFlag.Short;
        ushort target = (ushort)(0x101 + (sbyte)offset);
        uxn.SystemMem[target] = 0x32;
        uxn.WorkingStack.PushByte(offset);
        Assert.That(uxn.Step(), Is.False);
        Assert.That(uxn.WorkingStack.PopByte(true), Is.EqualTo(0x32));
        Assert.That(uxn.WorkingStack.PopByte(true), Is.EqualTo(offset));
    }

    [Test]
    [TestCase(0xfe)]
    [TestCase(0x10)]
    public void STRk(byte offset)
    {
        var uxn = new UXNProcessor();
        uxn.SystemMem[0x100] = (byte)UxnOpcode.STR | (byte)UxnOpcodeFlag.Short;
        ushort target = (ushort)(0x101 + (sbyte)offset);
        uxn.WorkingStack.PushByte(0x32);
        uxn.WorkingStack.PushByte(offset);
        Assert.That(uxn.Step(), Is.False);
        Assert.That(uxn.SystemMem[target], Is.EqualTo(0x32));
        Assert.That(uxn.WorkingStack.PopByte(true), Is.EqualTo(offset));
    }

    [Test]
    public void LDAk()
    {
        var uxn = new UXNProcessor();
        uxn.SystemMem[0x100] = (byte)UxnOpcode.LDA | (byte)UxnOpcodeFlag.Short;
        uxn.SystemMem[0x1234] = 0x32;
        uxn.WorkingStack.PushShort(0x1234);
        Assert.That(uxn.Step(), Is.False);
        Assert.That(uxn.WorkingStack.PopByte(true), Is.EqualTo(0x32));
        Assert.That(uxn.WorkingStack.PopShort(true), Is.EqualTo(0x1234));
    }

    [Test]
    public void STAk()
    {
        var uxn = new UXNProcessor();
        uxn.SystemMem[0x100] = (byte)UxnOpcode.STA | (byte)UxnOpcodeFlag.Short;
        uxn.WorkingStack.PushByte(0x32);
        uxn.WorkingStack.PushShort(0x1234);
        Assert.That(uxn.Step(), Is.False);
        Assert.That(uxn.SystemMem[0x1234], Is.EqualTo(0x32));
        Assert.That(uxn.WorkingStack.PopShort(true), Is.EqualTo(0x1234));
        Assert.That(uxn.WorkingStack.PopByte(true), Is.EqualTo(0x32));
    }

    [Test]
    public void DEIk()
    {
        var uxn = new UXNProcessor();
        uxn.SystemMem[0x100] = (byte)UxnOpcode.DEI | (byte)UxnOpcodeFlag.Short;
        uxn.DevMem[0x00] = 0x32;
        var testdev = new TestDevice();
        uxn.AttachDevice(0x00, testdev);
        Assert.That(uxn.Step(), Is.False);
        Assert.That(uxn.WorkingStack.PopByte(true), Is.EqualTo(0x32));
        Assert.That(testdev.dei, Is.EqualTo(0x00));
        Assert.That(uxn.WorkingStack.PopByte(true), Is.EqualTo(0x00));
    }

    [Test]
    public void DEOk()
    {
        var uxn = new UXNProcessor();
        uxn.SystemMem[0x100] = (byte)UxnOpcode.DEO | (byte)UxnOpcodeFlag.Short;
        uxn.WorkingStack.PushByte(0x32);
        uxn.WorkingStack.PushByte(0x00);
        var testdev = new TestDevice();
        uxn.AttachDevice(0x00, testdev);
        Assert.That(uxn.Step(), Is.False);
        Assert.That(uxn.DevMem[0x00], Is.EqualTo(0x32));
        Assert.That(testdev.deo, Is.EqualTo(0x00));
        Assert.That(uxn.WorkingStack.PopByte(true), Is.EqualTo(0x00));
    }

    [Test]
    [TestCase(0x00, 0x00)]
    [TestCase(0x01, 0x01)]
    [TestCase(0x10, 0x20)]
    [TestCase(0xff, 0xff)]
    public void ADDk(byte left, byte right)
    {
        var uxn = new UXNProcessor();
        uxn.SystemMem[0x100] = (byte)UxnOpcode.ADD | (byte)UxnOpcodeFlag.Short;
        uxn.WorkingStack.PushByte(left);
        uxn.WorkingStack.PushByte(right);
        Assert.That(uxn.Step(), Is.False);
        Assert.That(uxn.WorkingStack.PopByte(true), Is.EqualTo((byte)(left + right)));
        Assert.That(uxn.WorkingStack.PopByte(true), Is.EqualTo(right));
        Assert.That(uxn.WorkingStack.PopByte(true), Is.EqualTo(left));
    }

    [Test]
    [TestCase(0x00, 0x00)]
    [TestCase(0x01, 0x01)]
    [TestCase(0x10, 0x20)]
    [TestCase(0xff, 0xff)]
    public void SUBk(byte left, byte right)
    {
        var uxn = new UXNProcessor();
        uxn.SystemMem[0x100] = (byte)UxnOpcode.SUB | (byte)UxnOpcodeFlag.Short;
        uxn.WorkingStack.PushByte(left);
        uxn.WorkingStack.PushByte(right);
        Assert.That(uxn.Step(), Is.False);
        Assert.That(uxn.WorkingStack.PopByte(true), Is.EqualTo((byte)(left - right)));
        Assert.That(uxn.WorkingStack.PopByte(true), Is.EqualTo(right));
        Assert.That(uxn.WorkingStack.PopByte(true), Is.EqualTo(left));
    }

    [Test]
    [TestCase(0x00, 0x00)]
    [TestCase(0x01, 0x01)]
    [TestCase(0x10, 0x20)]
    [TestCase(0xff, 0xff)]
    public void MULk(byte left, byte right)
    {
        var uxn = new UXNProcessor();
        uxn.SystemMem[0x100] = (byte)UxnOpcode.MUL | (byte)UxnOpcodeFlag.Short;
        uxn.WorkingStack.PushByte(left);
        uxn.WorkingStack.PushByte(right);
        Assert.That(uxn.Step(), Is.False);
        Assert.That(uxn.WorkingStack.PopByte(true), Is.EqualTo((byte)(left * right)));
        Assert.That(uxn.WorkingStack.PopByte(true), Is.EqualTo(right));
        Assert.That(uxn.WorkingStack.PopByte(true), Is.EqualTo(left));
    }

    [Test]
    [TestCase(0x00, 0x00)]
    [TestCase(0x10, 0x00)]
    [TestCase(0x01, 0x01)]
    [TestCase(0x10, 0x20)]
    [TestCase(0xff, 0xff)]
    public void DIVk(byte left, byte right)
    {
        var uxn = new UXNProcessor();
        uxn.SystemMem[0x100] = (byte)UxnOpcode.DIV | (byte)UxnOpcodeFlag.Short;
        uxn.WorkingStack.PushByte(left);
        uxn.WorkingStack.PushByte(right);
        Assert.That(uxn.Step(), Is.False);
        Assert.That(uxn.WorkingStack.PopByte(true), Is.EqualTo(right != 0 ? (byte)(left / right) : 0x00));
        Assert.That(uxn.WorkingStack.PopByte(true), Is.EqualTo(right));
        Assert.That(uxn.WorkingStack.PopByte(true), Is.EqualTo(left));
    }

    [Test]
    [Repeat(5, false)]
    public void ANDk()
    {
        var uxn = new UXNProcessor();
        uxn.SystemMem[0x100] = (byte)UxnOpcode.AND | (byte)UxnOpcodeFlag.Short;
        var left = _random.NextByte();
        var right = _random.NextByte();
        uxn.WorkingStack.PushByte(left);
        uxn.WorkingStack.PushByte(right);
        Assert.That(uxn.Step(), Is.False);
        Assert.That(uxn.WorkingStack.PopByte(true), Is.EqualTo(left & right));
        Assert.That(uxn.WorkingStack.PopByte(true), Is.EqualTo(right));
        Assert.That(uxn.WorkingStack.PopByte(true), Is.EqualTo(left));
    }

    [Test]
    [Repeat(5, false)]
    public void ORAk()
    {
        var uxn = new UXNProcessor();
        uxn.SystemMem[0x100] = (byte)UxnOpcode.OR | (byte)UxnOpcodeFlag.Short;
        var left = _random.NextByte();
        var right = _random.NextByte();
        uxn.WorkingStack.PushByte(left);
        uxn.WorkingStack.PushByte(right);
        Assert.That(uxn.Step(), Is.False);
        Assert.That(uxn.WorkingStack.PopByte(true), Is.EqualTo(left | right));
    }

    [Test]
    [Repeat(5, false)]
    public void EORk()
    {
        var uxn = new UXNProcessor();
        uxn.SystemMem[0x100] = (byte)UxnOpcode.XOR | (byte)UxnOpcodeFlag.Short;
        var left = _random.NextByte();
        var right = _random.NextByte();
        uxn.WorkingStack.PushByte(left);
        uxn.WorkingStack.PushByte(right);
        Assert.That(uxn.Step(), Is.False);
        Assert.That(uxn.WorkingStack.PopByte(true), Is.EqualTo(left ^ right));
        Assert.That(uxn.WorkingStack.PopByte(true), Is.EqualTo(right));
        Assert.That(uxn.WorkingStack.PopByte(true), Is.EqualTo(left));
    }

    [Test]
    [TestCase(0x12, 0x00, 0x12)]
    [TestCase(0x34, 0x10, 0x68)] // left by 1. double the value
    [TestCase(0x32, 0x01, 0x19)] // right by 1. half the value
    public void SFTk(byte input, byte sft, byte output)
    {
        var uxn = new UXNProcessor();
        uxn.SystemMem[0x100] = (byte)UxnOpcode.SFT | (byte)UxnOpcodeFlag.Short;
        uxn.WorkingStack.PushByte(input);
        uxn.WorkingStack.PushByte(sft);
        Assert.That(uxn.Step(), Is.False);
        Assert.That(uxn.WorkingStack.PopByte(true), Is.EqualTo(output));
        Assert.That(uxn.WorkingStack.PopByte(true), Is.EqualTo(sft));
        Assert.That(uxn.WorkingStack.PopByte(true), Is.EqualTo(input));
    }
}
