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

    private readonly ResPath _compilerRom = new("/_Starlight/Uxn/Rom/drifloon.rom");

    private readonly UXNProcessor _compiler = new();

    public static Encoding Codepage437 = Encoding.GetEncoding("us-ascii");

    public override void Initialize()
    {
        //var encodings = Encoding.GetEncodings();

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
        if (!_resourceManager.ContentFileExists(_compilerRom))
        {
            error = $"Failed to load UXN assembler {_compilerRom}";
            rom = null;
            return false;
        }
        _compiler.Reset();
        var mem = _compiler.SystemMem;
        var writeHead = 0x100;
        var stream = _resourceManager.ContentFileRead(_compilerRom);
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

        var stdio = _compiler.AttachDevice(0x01, new FakeStdioDevice(uxnTal));

        _compiler.RunUnlimited(Log); //Basically this *should* run until it runs out of source code to process;

        Log.Info($"Assembled UXN program in {_compiler.InstructionCounter} instructions, FakedStdio provided {stdio.CharCount}/{uxnTal.Length} chars");

        var stdErr = stdio.FakedError;
        if (_compiler.SystemDevice.Status < 0x80)
        {
            error = new string(Codepage437.GetChars([.. stdErr]));
            Log.Error($"Failed to compile uxntal. drifloon error output:\n {error}");
            rom = null;
            return false;
        }

        rom = stdio.FakedOutput;
        error = null;
        return true;
    }
}