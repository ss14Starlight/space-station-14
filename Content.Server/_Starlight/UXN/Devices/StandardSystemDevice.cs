using System.Linq;

namespace Content.Server._Starlight.UXN.Devices;

[Virtual]
public class StandardSystemDevice : UXNDevice
{
    public StandardSystemDevice(int numBanks = 0)
    => ExtraPages = [.. Enumerable.Repeat(new UxnMem(), Math.Min(numBanks,ushort.MaxValue))]; //clamped because any more and UXN cant access them.
    
    public Dictionary<string, UXNDevice> AttachableDevices = new();
    public Dictionary<string, byte> AttachedDevices = new();

    protected List<UxnMem> ExtraPages = [];

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
            case 0x03: //expansion, Lower Half
                // get the address into system memory to read the expansion command from there
                var res = deviceMem.GetShort((byte)(memTarget - 0x01)); //since this is on the "least significant" half we gotta go one back to get the whole short.
                var mem = proc.SystemMem; //shorter name for readability :3c
                var cmd = mem[res];
                switch (cmd)
                {
                    /*fill length* bank* start* value */ case 0x00:
                        SystemFillCommand(res, mem);
                        break;
                    /*cpyl length* src_bank* src_addr* dest_bank* dest_addr* */ case 0x01:
                        SystemCopyLeftCommand(res, mem);
                        break;
                    /*cpyr length* src_bank* src_addr* dest_bank* dest_addr* */ case 0x02:
                        SystemCopyRightCommand(res, mem);
                        break;
                    /*atch name* slot*/ case 0x03:
                        SystemAttachCommand(res, mem, proc);
                        break;
                    /*dtch slot*/ case 0x04:
                        SystemDetachCommand(res, mem, proc);
                        break;
                    default:
                        break; //Specified command does not exists.
                };
                break;
            case 0x04: //wst
                proc.WorkingStack.SetPointer(deviceMem[memTarget]);
                break;
            case 0x06: //rst
                proc.ReturnStack.SetPointer(deviceMem[memTarget]);
                break;
            case 0x0e: //debug
                System.Diagnostics.Debugger.Break(); //BREAKPOINT!!... that doesn't work for some reason... ugh...
                break;
            case 0x0f: //state
                Status = deviceMem[memTarget];
                break;
            default:
                break;
        }
    }

    private void SystemFillCommand(ushort baseAddr, UxnMem mem)
    {
        var length = mem.GetShort((ushort)(baseAddr + 0x01));
        var bank = mem.GetShort((ushort)(baseAddr + 0x03));
        var addres = mem.GetShort((ushort)(baseAddr + 0x05));
        var value = mem[(ushort)(baseAddr + 7)];
        if (bank > ExtraPages.Count)
            return;

        var target = (bank == 0) ? mem : ExtraPages[(ushort)(bank - 1)];
        for (int i = 0; i < length; i++)
        {
            target[(ushort)(addres + i)] = value;
        }
    }

    private void SystemCopyLeftCommand(ushort baseAddr, UxnMem mem)
    {
        var length = mem.GetShort((ushort)(baseAddr + 0x01));
        var sourceBank = mem.GetShort((ushort)(baseAddr + 0x03));
        var source = mem.GetShort((ushort)(baseAddr + 0x05));
        var destBank = mem.GetShort((ushort)(baseAddr + 0x07));
        var dest = mem.GetShort((ushort)(baseAddr + 0x09));

        if (sourceBank > ExtraPages.Count || destBank > ExtraPages.Count)
            return;

        var sourcePage = (sourceBank == 0) ? mem : ExtraPages[(ushort)(sourceBank - 1)];
        var destPage = (destBank == 0) ? mem : ExtraPages[(ushort)(destBank - 1)];
        for (int i = 0; i < length; i++)
        {
            destPage[(ushort)(dest + i)] = sourcePage[(ushort)(source + i)];
        }
    }

    private void SystemCopyRightCommand(ushort baseAddr, UxnMem mem)
    {
        var length = mem.GetShort((ushort)(baseAddr + 0x01));
        var sourceBank = mem.GetShort((ushort)(baseAddr + 0x03));
        var source = mem.GetShort((ushort)(baseAddr + 0x05));
        var destBank = mem.GetShort((ushort)(baseAddr + 0x07));
        var dest = mem.GetShort((ushort)(baseAddr + 0x09));

        if (sourceBank > ExtraPages.Count || destBank > ExtraPages.Count)
            return;

        var sourcePage = (sourceBank == 0) ? mem : ExtraPages[(ushort)(sourceBank - 1)];
        var destPage = (destBank == 0) ? mem : ExtraPages[(ushort)(destBank - 1)];
        for (int i = 0; i < length; i++)
        {
            // yes there is a magic -1 in here. no I dont know why it does not work without it. suffer as I have suffered.
            destPage[(ushort)(dest + length - i - 1)] = sourcePage[(ushort)(source + length - i - 1)];
        }
    }

    private void SystemAttachCommand(ushort baseAddr, UxnMem mem, UXNProcessor proc)
    {
        var nameptr = mem.GetShort((ushort)(baseAddr + 1));
        var name = ReadBuffered(mem, 0, nameptr).ToLower();
        var slot = mem[(ushort)(baseAddr + 3)];

        if (!AttachableDevices.ContainsKey(name))
            return; //we dont have a device by that name

        var dev = proc.Devices[slot & 0x0F];
        if (dev.GetType() != typeof(UXNDevice))
            return; //device slot is taken

        proc.AttachDevice((byte)(slot & 0x0F), AttachableDevices[name]);
        AttachedDevices[name] = ((byte)(slot & 0x0F)); //mark this slot as detachable so it can be detached later if needed.
    }

    private void SystemDetachCommand(ushort baseAddr, UxnMem mem, UXNProcessor proc)
    {
        var dtchSlot = (byte)(mem[(ushort)(baseAddr + 1)] & 0x0F);
        if (!AttachedDevices.ContainsValue(dtchSlot))
            return; //this slot was not attached via a command. as such it isn't safe to detach

        proc.Devices[dtchSlot & 0x0F].OnDetach(proc); //call on detach so the device can clean up if it needs to
        proc.AttachDevice((byte)(dtchSlot & 0x0F), new UXNDevice()); //detach by attaching a blank device

        if (AttachedDevices.FirstOrDefault(p => p.Value == dtchSlot)
            is { Key: var key })
        {
            AttachedDevices.Remove(key);
        }
    }
}