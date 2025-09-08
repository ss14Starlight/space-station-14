using System.IO;
using System.Text;
using Content.Shared._Starlight;
using Content.Shared._Starlight.UXN;
using Content.Shared._Starlight.UXN.Devices;
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
    public async Task TestOpcTestPasses()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        var resourceManager = server.ResolveDependency<IResourceManager>();
        var uxnSystem = server.ResolveDependency<IEntitySystemManager>().GetEntitySystem<UxnSystem>();
        var sawmill = server.ResolveDependency<ILogManager>().GetSawmill("uxn.testrunner");

        await server.WaitAssertion(() =>
        {
            var uxnTalSource = resourceManager.ContentFileReadAllText(_uxntalSourceFile);

            var uxnRunner = new UXNProcessor();
            var mem = uxnRunner.SystemMem;
            var writeHead = 0x100;
            var stream = resourceManager.ContentFileRead(_uxnopctestRom);
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

            sawmill.Info($"Ran {uxnRunner.InstructionCounter} instructions");
            sawmill.Info($"UXN Opcode test output:\n{new string(UxnSystem.Codepage437.GetChars([.. stdio.FakedOutput]))}");

            //Make sure compile succeded.
            Assert.That(uxnRunner.SystemDevice.Status, Is.EqualTo(0x00));
        });

        await pair.CleanReturnAsync();
    }
}
