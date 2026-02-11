
using System;
using System.IO;
using System.Reflection;
using Content.Server._Starlight.UXN;
using Content.Server._Starlight.UXN.Devices;
using Content.Tests;
using NUnit.Framework;
using Robust.Shared.Utility;

namespace Content.Tests.Shared._Starlight.UXN;

[TestFixture]
public sealed class UxnTestCompiler : ContentUnitTest
{
    [Test]
    public void UxnTestCompileOpctest()
    {
        var uxn = new UXNProcessor();
        using Stream rom = Assembly.GetExecutingAssembly().GetManifestResourceStream("Content.Tests.drifloon.rom")!;

        var mem = uxn.SystemMem;
        ushort writeHead = 0x100;
        Span<byte> span = new byte[32];
        while (rom.CanRead)
        {
            var amnt = rom.Read(span);
            if (amnt == 0) break;
            for (int i = 0; i < amnt; i++)
            {
                mem[writeHead] = span[i];
                writeHead++;
            }
        }

        using Stream tal = Assembly.GetExecutingAssembly().GetManifestResourceStream("Content.Tests.opctest.tal")!;
        using StreamReader talReader = new StreamReader(tal);
        var uxnTal = talReader.ReadToEnd();

        var stdio = uxn.AttachDevice(0x01, new FakeStdioDevice(uxnTal));

        uxn.RunUnlimited();

        Console.WriteLine($"Assembled UXN program in {uxn.RealInstructionCounter} instructions, FakedStdio provided {stdio.CharCount}/{uxnTal.Length} chars");

        Assert.That(uxn.SystemDevice.Status, Is.GreaterThanOrEqualTo(0x80)); //since anything lesser is a error
        Assert.That(stdio.FakedOutput, Has.Count.EqualTo(3373)); //and this is the expected rom size of compiled opctest
    }
}