using System.IO;
using System.Text;
using Content.Server._Starlight.UXN;
using Content.Server._Starlight.UXN.Devices;
using Robust.Shared.ContentPack;
using Robust.Shared.GameObjects;
using Robust.Shared.Log;
using Robust.Shared.Utility;

namespace Content.IntegrationTests.Tests._Starlight.UXN;

[TestFixture]
public sealed class TestUxnCompiler
{

    [Test]
    [TestCase("/_Starlight/Uxn/Rom/hello.rom")]
    [TestCase("/_Starlight/Uxn/Rom/acid.rom", null, null, 0x00)]
    [TestCase("/_Starlight/Uxn/Rom/opctest.rom", null, null, 0x80)]
    [TestCase("/_Starlight/Uxn/Rom/console.rom", "foobar", "baz qux", 0x80)]
    [TestCase("/_Starlight/Uxn/Rom/system_test.rom", null, null, 0x80)]
    #nullable enable
    public async Task RunRomAsTest(string file, string? stdin = null, string? argv = null, byte? expected = 0x01)
    {
        var res = new ResPath(file);
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        var resourceManager = server.ResolveDependency<IResourceManager>();
        var sawmill = server.ResolveDependency<ILogManager>().GetSawmill("uxn.testrunner");

        await server.WaitAssertion(() =>
        {
            var uxnRunner = new UXNProcessor();
            var mem = uxnRunner.SystemMem;
            using var stream = resourceManager.ContentFileRead(res);
            using var ms = new MemoryStream();
            stream.CopyTo(ms);
            var romBytes = ms.ToArray();
            if (romBytes.Length > (ushort.MaxValue - UXNProcessor.RESET_VECTOR + 1)) // 0xFF00 is 0xFF less then 0xFFFF which is the max size of UXN memory.
                Assert.Fail($"ROM {file} is too large to fit in UXN memory!");
            for (int i = 0; i < romBytes.Length; i++)
            {
                mem[(ushort)(UXNProcessor.RESET_VECTOR + i)] = romBytes[i];
            }

            var stdio = uxnRunner.AttachDevice(0x1, new FakeStdioDevice(stdin, argv, sawmill));
            uxnRunner.RunUnlimited();

            sawmill.Info($"Ran {uxnRunner.RealInstructionCounter} instructions");
            sawmill.Info($"Program output:\n{new string(Encoding.ASCII.GetChars([.. stdio.FakedOutput])).Trim()}");

            //Make sure program succeeded.
            Assert.That(uxnRunner.SystemDevice.Status, Is.EqualTo(expected));
        });

        await pair.CleanReturnAsync();
    }
}
