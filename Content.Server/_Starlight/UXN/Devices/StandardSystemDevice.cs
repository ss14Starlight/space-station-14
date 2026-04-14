using System.Linq;

namespace Content.Server._Starlight.UXN.Devices;

[Virtual]
public class StandardSystemDevice : UXNDevice
{
    public StandardSystemDevice(int numBanks = 0)
    {
        var bankCount = Math.Clamp(numBanks, 0, ushort.MaxValue);
        ExtraPages = [.. Enumerable.Range(0, bankCount).Select(_ => new UxnMem())];
    }

    public Dictionary<string, UXNDevice> AttachableDevices = new();
    public Dictionary<string, byte> AttachedDevices = new();

    protected List<UxnMem> ExtraPages = [];

    public byte Status { get; private set; } = 0;
    public override void ReadValue(byte memTarget, Byte256 deviceMem, UXNProcessor proc)
    {
        var lsn = memTarget & 0x0F;
        switch ((StandardSystemDeviceMemory)lsn)
        {
            case StandardSystemDeviceMemory.WriteStackPointer: //wst
                deviceMem[memTarget] = proc.WorkingStack.StackPointer;
                break;
            case StandardSystemDeviceMemory.ReturnStackPointer: //rst
                deviceMem[memTarget] = proc.ReturnStack.StackPointer;
                break;
            case StandardSystemDeviceMemory.State: //state
                deviceMem[memTarget] = Status;
                break;
            default:
                break;
        }
    }

    public override void WriteValue(byte memTarget, Byte256 deviceMem, UXNProcessor proc)
    {
        var lsn = memTarget & 0x0F;
        switch ((StandardSystemDeviceMemory)lsn)
        {
            case StandardSystemDeviceMemory.Expansion: //expansion, Lower Half
                // get the address into system memory to read the expansion command from there
                var res = deviceMem.GetShort((byte)(memTarget - 0x01)); //since this is on the "least significant" half we gotta go one back to get the whole short.
                var mem = proc.SystemMem; //shorter name for readability :3c
                var cmd = mem[res];
                switch ((StandardSystemDeviceExpansionCommands)cmd)
                {
                    /*fill length* bank* start* value */ case StandardSystemDeviceExpansionCommands.Fill:
                        SystemFillCommand(res, mem);
                        break;
                    /*cpyl length* src_bank* src_addr* dest_bank* dest_addr* */ case StandardSystemDeviceExpansionCommands.CopyLeft:
                        SystemCopyLeftCommand(res, mem);
                        break;
                    /*cpyr length* src_bank* src_addr* dest_bank* dest_addr* */ case StandardSystemDeviceExpansionCommands.CopyRight:
                        SystemCopyRightCommand(res, mem);
                        break;
                    /*atch name* slot*/ case StandardSystemDeviceExpansionCommands.Attach:
                        SystemAttachCommand(res, mem, proc);
                        break;
                    /*dtch slot*/ case StandardSystemDeviceExpansionCommands.Detach:
                        SystemDetachCommand(res, mem, proc);
                        break;
                    default:
                        break; //Specified command does not exists.
                };
                break;
            case StandardSystemDeviceMemory.WriteStackPointer: //wst
                proc.WorkingStack.SetPointer(deviceMem[memTarget]);
                break;
            case StandardSystemDeviceMemory.ReturnStackPointer: //rst
                proc.ReturnStack.SetPointer(deviceMem[memTarget]);
                break;
            case StandardSystemDeviceMemory.Debug: //debug
                System.Diagnostics.Debugger.Break(); //BREAKPOINT!!... that doesn't work for some reason... ugh...
                break;
            case StandardSystemDeviceMemory.State: //state
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
        var name = ReadBuffered(mem, 0, nameptr).ToLowerInvariant();
        var slot = mem[(ushort)(baseAddr + 3)];

        if ((!AttachableDevices.TryGetValue(name, out var value)) || AttachedDevices.ContainsKey(name))
            return; //we dont have a device by that name or it's already attached

        var dev = proc.Devices[slot & 0x0F];
        if (dev.GetType() != typeof(UXNDevice))
            return; //device slot is taken

        proc.AttachDevice((byte)(slot & 0x0F), value);
        AttachedDevices[name] = (byte)(slot & 0x0F); //mark this slot as detachable so it can be detached later if needed.
    }

    private void SystemDetachCommand(ushort baseAddr, UxnMem mem, UXNProcessor proc)
    {
        var dtchSlot = (byte)(mem[(ushort)(baseAddr + 1)] & 0x0F);
        var entry = AttachedDevices.FirstOrDefault(p => p.Value == dtchSlot);
        if (entry.Key is null)
            return; //this slot was not attached via a command. as such it isn't safe to detach

        proc.AttachDevice((byte)(dtchSlot & 0x0F), new UXNDevice());
        AttachedDevices.Remove(entry.Key);
    }
}

public enum StandardSystemDeviceMemory : byte
{
    Expansion = 0x03,
    WriteStackPointer = 0x04,
    ReturnStackPointer = 0x05,
    Debug = 0x0E,
    State = 0x0F
}

public enum StandardSystemDeviceExpansionCommands : byte
{
    Fill,
    CopyLeft,
    CopyRight,
    Attach,
    Detach
}
