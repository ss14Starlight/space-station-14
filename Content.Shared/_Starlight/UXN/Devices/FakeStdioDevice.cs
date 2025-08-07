using System.Text;
using Content.Shared._Starlight.UXN;

namespace Content.Shared._Starlight.UXN.Devices;

public sealed class FakeStdioDevice : UXNDevice
{
    private string _fakedInput = "";
    public List<byte> FakedOutput = [];
    public List<byte> FakedError = [];

    public FakeStdioDevice(string fakeInput) => _fakedInput = fakeInput;

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

    public override void OnAttach(UXNProcessor proc) => MakeEvent(proc); //get the ball rolling

    public void MakeEvent(UXNProcessor proc)
    {
        if (_fakedInput.Length <= 0)
            return;
        var letter = _fakedInput[0];
        var letterByte = UxnSystem.Codepage437.GetBytes([letter])[0];
        _fakedInput = _fakedInput.Substring(1);
        proc.PushEvent(new StdioCharEvent(letterByte, this));
    }
}

public sealed class StdioCharEvent : UxnEvent
{
    public byte Letter = 0x00;
    private readonly FakeStdioDevice _dev;

    public StdioCharEvent(byte letter, FakeStdioDevice dev)
    {
        Letter = letter;
        _dev = dev;
    }
    public override void PerformEvent(UXNProcessor proc)
    {
        var mem = proc.DevMem;
        proc.PC = (ushort)((mem[0x10] << 8) | mem[0x11]);
        mem[0x12] = Letter;
        mem[0x17] = 0x01; //stdin char spam!
        _dev.MakeEvent(proc);
    }
}