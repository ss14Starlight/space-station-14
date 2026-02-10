using System.Text;

namespace Content.Server._Starlight.UXN.Devices;

public sealed class FakeStdioDevice : UXNDevice
{
    private string _fakedInput = "";
    public List<byte> FakedOutput = [];
    public List<byte> FakedError = [];

    public int CharCount = 0;
    private readonly ISawmill? _sawmill;
    public readonly string? Args;

    public FakeStdioDevice(string? fakeInput = null, string? argv = null, ISawmill? sawmill = null)
    {
        _fakedInput = fakeInput ?? "";
        _sawmill = sawmill;
        Args = argv;
    }

    public override void ReadValue(byte memTarget, Byte256 deviceMem, UXNProcessor proc)
    {
        var lsn = memTarget & 0x0F;
        switch (lsn)
        {
            default:
                break;
        }
    }

    public override void WriteValue(byte memTarget, Byte256 deviceMem, UXNProcessor proc)
    {
        var lsn = memTarget & 0x0F;
        switch (lsn)
        {
            case 0x08: //stdout
                FakedOutput.Add(deviceMem[memTarget]);
                break;
            case 0x09: //stderr
                FakedError.Add(deviceMem[memTarget]);
                break;
            default:
                break;
        }
    }

    public override void OnAttach(UXNProcessor proc)
    {
        if (Args != null)
        {
            List<ArgvCharEvent> events = [];
            var args = Args.Split(" ");
            foreach (var arg in args)
            {
                foreach (var letter in Encoding.ASCII.GetBytes(arg))
                {
                    events.Add(new ArgvCharEvent(letter, 0x02));
                }
                events.Add(new ArgvCharEvent(0x0a, 0x03)); //Split. according to the test rom it is split with a newline on argv.
            }
            events.RemoveAt(events.Count - 1); //remove the last split to replace with...
            events.Add(new ArgvCharEvent(0x0a, 0x04)); //End of Args. still a newline.
            foreach (var ev in events)
            {
                ev.Sawmill = _sawmill;
                proc.PushEvent(ev); //and now we push them all into the console
            }
            proc.DevMem[0x17] = (byte)args.Length; //we have args
        }
        MakeEvent(proc); //get the ball rolling
    }

    public void MakeEvent(UXNProcessor proc)
    {
        if (_fakedInput.Length <= 0)
            return;
        var letter = _fakedInput[0];
        var letterByte = Encoding.ASCII.GetBytes(letter.ToString())[0];
        _fakedInput = _fakedInput[1..];
        _sawmill?.Info($"FakedStdio: push char '{letter}'");
        CharCount++;
        proc.PushEvent(new StdioCharEvent(letterByte, this));

        if (_fakedInput.Length == 0)
            proc.PushEvent(new ArgvCharEvent(0x00, 0x04)); //end of stdin is null not a newline IG?
    }
}

public sealed class ArgvCharEvent(byte letter, byte type) : UxnEvent
{
    public byte Letter = letter;
    public byte Type = type;
    public ISawmill? Sawmill = null;
    public override void PerformEvent(UXNProcessor proc)
    {
        var mem = proc.DevMem;
        proc.PC = (ushort)((mem[0x10] << 8) | mem[0x11]);
        mem[0x12] = Letter;
        mem[0x17] = Type;
        Sawmill?.Info($"Popped ARGV char 0x{Letter:x2} type 0x{Type:x2}");
    }
}

public sealed class StdioCharEvent : UxnEvent
{
    public byte Letter = 0x00;
    private readonly FakeStdioDevice _dev;
    private readonly ISawmill? _sawmill;

    public StdioCharEvent(byte letter, FakeStdioDevice dev, ISawmill? sawmill = null)
    {
        Letter = letter;
        _dev = dev;
        _sawmill = sawmill;
    }
    public override void PerformEvent(UXNProcessor proc)
    {
        _sawmill?.Info($"Provided char {Letter}");
        var mem = proc.DevMem;
        proc.PC = (ushort)((mem[0x10] << 8) | mem[0x11]);
        mem[0x12] = Letter;
        mem[0x17] = 0x01; //stdin char spam!
        _dev.MakeEvent(proc);
    }
}