using System;
using Content.Shared._Starlight;
using Content.Shared._Starlight.UXN;
using Content.Shared._Starlight.UXN.Devices;
using NUnit.Framework;
using Robust.Shared.ContentPack;
using Robust.Shared.IoC;
using Robust.Shared.Log;
using Robust.Shared.Utility;

namespace Content.Tests.Shared._Starlight.UXN;

// Basic tests of various damage prototypes and classes.
[TestFixture]
public sealed class UxnInLangOpcTest : ContentUnitTest
{

    private IResourceManager _resouces = default!;
    private ISawmill _sawmill = default!;
    private readonly ResPath _uxnopctestRom = new("/_Starlight/opctest.rom");

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _resouces = IoCManager.Resolve<IResourceManager>();
        _sawmill = IoCManager.Resolve<ILogManager>().GetSawmill("uxn.opctest");
    }

    [Test]
    public void OpcTest()
    {
        var uxnRunner = new UXNProcessor();
        var mem = uxnRunner.SystemMem;
        var writeHead = 0x100;

        var stream = _resouces.ContentFileRead(_uxnopctestRom);
        Span<byte> span = new byte[32];
        while (stream.CanRead)
        {
            var amnt = stream.Read(span);
            if (amnt == 0) break;
            for (int i = 0; i < amnt; i++)
            {
                mem[writeHead] = span[i];
                writeHead++;
            }
        }

        var stdio = uxnRunner.AttachDevice(0x1, new FakeStdioDevice(""));
        uxnRunner.RunUnlimited();

        _sawmill.Info($"Ran {uxnRunner.InstructionCounter} instructions");
        _sawmill.Info($"UXN Opcode test output:\n{new string(UxnSystem.Codepage437.GetChars([.. stdio.FakedOutput]))}");

        //Make sure run succeded.
        Assert.That(uxnRunner.SystemDevice.Status, Is.EqualTo(0x00));
    }
}
