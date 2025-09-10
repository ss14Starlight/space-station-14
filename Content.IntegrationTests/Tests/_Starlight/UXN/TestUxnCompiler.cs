using System.IO;
using System.Text;
using Content.Shared._Starlight;
using Content.Shared._Starlight.UXN;
using Content.Shared._Starlight.UXN.Devices;
using Pidgin;
using Robust.Shared.ContentPack;
using Robust.Shared.GameObjects;
using Robust.Shared.Log;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.Manager;
using Robust.Shared.Serialization.Markdown;
using Robust.Shared.Utility;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace Content.IntegrationTests.Tests._Starlight.UXN;

[TestFixture]
public sealed class TestUxnCompiler
{

    private readonly ResPath _uxntalSourceFile = new("/_Starlight/Uxntal/opctest.tal");
    private readonly ResPath _uxnopctestRom = new("/_Starlight/opctest.rom");
    private readonly ResPath _uxnCatRom = new("/_Starlight/cat.com");

    [Test]
    public async Task TestCompilingOpcTest()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        var resourceManager = server.ResolveDependency<IResourceManager>();
        var uxnSystem = server.ResolveDependency<IEntitySystemManager>().GetEntitySystem<UxnSystem>();

        await server.WaitAssertion(() =>
        {
            var uxnTalSource = resourceManager.ContentFileReadAllText(_uxntalSourceFile);
            var result = uxnSystem.Compile(uxnTalSource, out var error, out var rom);

            //Make sure compile succeded.
            Assert.That(result, Is.EqualTo(true));
            //Make sure compile is the right size.
            Assert.That(rom.Count, Is.EqualTo(3323));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    [TestCase("/_Starlight/hello.rom")]
    [TestCase("/_Starlight/opctest.rom", null, null, 0x80)]
    [TestCase("/_Starlight/console.rom", "foobar", "baz qux", 0x80)]
    #nullable enable
    public async Task RunRomAsTest(string file, string? stdin = null, string? argv = null, byte? expected = 0x01)
    {
        var res = new ResPath(file);
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        var resourceManager = server.ResolveDependency<IResourceManager>();
        var uxnSystem = server.ResolveDependency<IEntitySystemManager>().GetEntitySystem<UxnSystem>();
        var sawmill = server.ResolveDependency<ILogManager>().GetSawmill("uxn.testrunner");

        await server.WaitAssertion(() =>
        {
            var uxnRunner = new UXNProcessor();
            var mem = uxnRunner.SystemMem;
            var writeHead = 0x100;
            var stream = resourceManager.ContentFileRead(res);
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

            var stdio = uxnRunner.AttachDevice(0x1, new FakeStdioDevice(stdin, argv, sawmill));
            uxnRunner.RunUnlimited(sawmill);

            sawmill.Info($"Ran {uxnRunner.InstructionCounter} instructions");
            sawmill.Info($"Program output:\n{new string(UxnSystem.Codepage437.GetChars([.. stdio.FakedOutput])).Trim()}");

            //Make sure program succeded.
            Assert.That(uxnRunner.SystemDevice.Status, Is.EqualTo(expected));
        });

        await pair.CleanReturnAsync();
    }
}
