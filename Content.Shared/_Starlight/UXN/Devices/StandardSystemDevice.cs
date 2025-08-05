using Content.Shared._Starlight.UXN;

namespace Content.Shared._Starlight.UXN.Devices;

public sealed class StandardSystemDevice : UXNDevice
{
    public byte Status { get; private set; } = 0;
    public override void ReadValue(byte memTarget, Byte256 deviceMem, UXNProcessor proc)
    {
        var lsn = memTarget & 0x0F;
        switch (lsn)
        {
            case 0x04: //wst
                deviceMem[memTarget] = proc.WorkingStack.StackPointer;
                break;
            case 0x05: //rst
                deviceMem[memTarget] = proc.ReturnStack.StackPointer;
                break;
            case 0x0f: //state
                deviceMem[memTarget] = Status;
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
            case 0x04: //wst
                proc.WorkingStack.SetPointer(deviceMem[memTarget]);
                break;
            case 0x06: //rst
                proc.ReturnStack.SetPointer(deviceMem[memTarget]);
                break;
            case 0x0f: //state
                Status = deviceMem[memTarget];
                break;
            default:
                break;
        }
    }
}