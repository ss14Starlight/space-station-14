
using System;
using System.IO;
using System.Reflection;
using System.Text;
using Content.Server._Starlight.UXN;
using Content.Server._Starlight.UXN.Devices;
using NUnit.Framework;

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
        using var ms = new MemoryStream();
        rom.CopyTo(ms);
        var romBytes = ms.ToArray();
        if (romBytes.Length > (ushort.MaxValue - UXNProcessor.RESET_VECTOR + 1)) // 0xFF00 is 0xFF less then 0xFFFF which is the max size of UXN memory.
            Assert.Fail($"The compiler rom is somehow too large to fit in UXN memory!");
        for (int i = 0; i < romBytes.Length; i++)
        {
            mem[(ushort)(UXNProcessor.RESET_VECTOR + i)] = romBytes[i];
        }

        using Stream tal = Assembly.GetExecutingAssembly().GetManifestResourceStream("Content.Tests.opctest.tal")!;
        using StreamReader talReader = new StreamReader(tal);
        var uxnTal = talReader.ReadToEnd();

        var stdio = uxn.AttachDevice(0x01, new FakeStdioDevice(uxnTal));

        uxn.RunUnlimited();

        Console.WriteLine($"Assembled UXN program in {uxn.RealInstructionCounter} instructions, FakedStdio provided {stdio.CharCount}/{uxnTal.Length} chars");
        Console.WriteLine($"Program output:\n{new string(Encoding.ASCII.GetChars([.. stdio.FakedError])).Trim()}");

        Assert.That(uxn.SystemDevice.Status, Is.GreaterThanOrEqualTo(0x80)); //since anything lesser is a error
        Assert.That(stdio.FakedOutput, Has.Count.EqualTo(3373)); //and this is the expected rom size of compiled opctest
    }
}
