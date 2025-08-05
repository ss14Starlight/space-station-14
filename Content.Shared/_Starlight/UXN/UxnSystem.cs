using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Text;
using Content.Shared._Starlight.UXN;
using Content.Shared._Starlight.UXN.Devices;
using Robust.Shared.ContentPack;
using Robust.Shared.Utility;

namespace Content.Shared._Starlight;

public sealed partial class UxnSystem : EntitySystem
{
    [Dependency] private readonly IResourceManager _resourceManager = default!;

    private readonly ResPath _compilerRom = new ResPath("_Starlight/drifloon.rom");

    private readonly UXNProcessor _compiler = new();

    public static Encoding Codepage437 = Encoding.GetEncoding(437);

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<UxnAttachedComponent, MapInitEvent>(OnMapInit);
    }

    private void OnMapInit(EntityUid euid, UxnAttachedComponent uxn, MapInitEvent map)
    {
        var reader = _resourceManager.ContentFileReadText(uxn.UxntalSourceFile);
        if (Compile(reader.ReadToEnd(), out var error, out var rom))
        {

        }
    }


    public bool Compile(string uxnTal, [NotNullWhen(false)] out string? error, [NotNullWhen(true)] out List<byte>? rom)
    {
        _compiler.Reset();
        var mem = _compiler.SystemMem;
        var writeHead = 0x100;
        var stream = _resourceManager.ContentFileRead(_compilerRom);
        Span<byte> span = new byte[32];
        while (stream.CanRead)
        {
            var amnt = stream.Read(span);
            for (int i = 0; i < amnt; i++)
            {
                mem[writeHead] = span[i];
                writeHead++;
            }
        }

        var stdio = _compiler.AttachDevice(0x01, new FakeStdioDevice(uxnTal));

        _compiler.RunUnlimited(); //Basically this *should* run until it runs out of source code to process;

        var stdErr = stdio.FakedError;
        if (stdErr.Count > 0)
        {
            error = Codepage437.GetChars(stdErr.ToArray()).ToString() ?? "Failed to decode faked stderr to string";
            Log.Error($"Failed to compile uxntal \n```\n{uxnTal}\n```\n drifloon error output:\n {error}");
            rom = null;
            return false;
        }

        rom = stdio.FakedOutput;
        error = null;
        return true;
    }
}