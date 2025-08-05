using System.Text;
using Content.Shared._Starlight.UXN;

namespace Content.Shared._Starlight.UXN.Devices;

public sealed class FakeStdioDevice : UXNDevice
{
    private string FakedInput = "";
    public List<byte> FakedOutput = [];
    public List<byte> FakedError = [];

    public FakeStdioDevice(string fakeInput) => FakedInput = fakeInput;

    public override void ReadValue(byte memTarget, Byte256 deviceMem, UXNProcessor proc)
    {
        var lsn = memTarget & 0x0F;
        switch (lsn)
        {
            case 0x02:
                proc.PushEvent(MakeEvent()); //TODO: figure out how to UXN encode this.
                break;
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

    public override void OnAttach(UXNProcessor proc) => proc.PushEvent(MakeEvent());

    private StdioCharEvent MakeEvent()
    {
        var letter = FakedInput[0];
        var cp437 = Encoding.GetEncoding(437);
        var letterByte = cp437.GetBytes([letter])[0];
        FakedInput = FakedInput.Substring(1);
        return new StdioCharEvent(letterByte);

    }
}

public sealed class StdioCharEvent : UxnEvent
{
    public byte Letter = 0x00;

    public StdioCharEvent(byte letter) => Letter = letter;

    public override void PerformEvent(UXNProcessor proc)
    {
        var mem = proc.DevMem;
        proc.PC = (ushort)((mem[0x10] << 8) | mem[0x11]);
        mem[0x12] = Letter;
        mem[0x17] = 0x01; //stdin char spam!
    }
}