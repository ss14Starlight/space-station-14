using System;
using Content.Server._Starlight.UXN;
using NUnit.Framework;
using Robust.Shared.IoC;
using Robust.Shared.Random;

namespace Content.Tests.Shared._Starlight.UXN;

// Basic tests of various damage prototypes and classes.
[TestFixture]
public sealed class UxnImmediateOpcodeTest : ContentUnitTest
{
    [Test]
    [TestCase(0xfe, true)] //jumps back 2 bytes to the instr just *before* the jmp
    [TestCase(0x10, true)]
    [TestCase(0xfe, false)] //cond is false
    [TestCase(0x10, false)]
    public void JCI(short jmp, bool cond)
    {
        var uxn = new UXNProcessor();
        uxn.SystemMem[0x100] = 0x20;
        uxn.WorkingStack.PushByte((byte)(cond ? 0x01 : 0x00));
        uxn.SystemMem[0x101] = (byte)(jmp >> 8);
        uxn.SystemMem[0x102] = (byte)(jmp & 0xFF);
        Assert.That(uxn.Step(), Is.EqualTo(false));
        Assert.That(uxn.PC, Is.EqualTo(cond ? (ushort)(0x103 + (ushort)jmp) : 0x103)); //the PC is incr'd by 1 so we add to that
    }

    [Test]
    [TestCase(0xfe)] //jumps back 2 bytes to the instr just *before* the jmp
    [TestCase(0x10)]
    public void JMI(short jmp)
    {
        var uxn = new UXNProcessor();
        uxn.SystemMem[0x100] = 0x40;
        uxn.SystemMem[0x101] = (byte)(jmp >> 8);
        uxn.SystemMem[0x102] = (byte)(jmp & 0xFF);
        Assert.That(uxn.Step(), Is.EqualTo(false));
        Assert.That(uxn.PC, Is.EqualTo((ushort)(0x103 + (ushort)jmp))); //the PC is incr'd by 1 so we add to that
    }

    [Test]
    [TestCase(0xfe)]
    [TestCase(0x10)]
    public void JSR(short jmp)
    {
        var uxn = new UXNProcessor();
        uxn.SystemMem[0x100] = 0x60;
        uxn.SystemMem[0x101] = (byte)(jmp >> 8);
        uxn.SystemMem[0x102] = (byte)(jmp & 0xFF);
        Assert.That(uxn.Step(), Is.EqualTo(false));
        Assert.That(uxn.PC, Is.EqualTo((ushort)(0x103 + (ushort)jmp)));
        Assert.That(uxn.ReturnStack.PopShort(false), Is.EqualTo(0x103)); //and the return stack should point to the instr after JSR
    }

    [Test]
    public void LIT()
    {
        var uxn = new UXNProcessor();
        uxn.SystemMem[0x100] = 0x80;
        uxn.SystemMem[0x101] = 0x12;
        Assert.That(uxn.Step(), Is.EqualTo(false));
        Assert.That(uxn.PC, Is.EqualTo((ushort)0x102));
        Assert.That(uxn.WorkingStack.PopByte(false), Is.EqualTo(0x12));
    }

    [Test]
    public void LIT2()
    {
        var uxn = new UXNProcessor();
        uxn.SystemMem[0x100] = 0xa0;
        uxn.SystemMem[0x101] = 0x12;
        uxn.SystemMem[0x102] = 0x34;
        Assert.That(uxn.Step(), Is.EqualTo(false));
        Assert.That(uxn.PC, Is.EqualTo((ushort)0x103));
        Assert.That(uxn.WorkingStack.PopShort(false), Is.EqualTo(0x1234));
    }

    [Test]
    public void LITr()
    {
        var uxn = new UXNProcessor();
        uxn.SystemMem[0x100] = 0xC0;
        uxn.SystemMem[0x101] = 0x12;
        Assert.That(uxn.Step(), Is.EqualTo(false));
        Assert.That(uxn.PC, Is.EqualTo((ushort)0x102));
        Assert.That(uxn.ReturnStack.PopByte(false), Is.EqualTo(0x12));
    }

    [Test]
    public void LIT2r()
    {
        var uxn = new UXNProcessor();
        uxn.SystemMem[0x100] = 0xe0;
        uxn.SystemMem[0x101] = 0x12;
        uxn.SystemMem[0x102] = 0x34;
        Assert.That(uxn.Step(), Is.EqualTo(false));
        Assert.That(uxn.PC, Is.EqualTo((ushort)0x103));
        Assert.That(uxn.ReturnStack.PopShort(false), Is.EqualTo(0x1234));
    }
}
