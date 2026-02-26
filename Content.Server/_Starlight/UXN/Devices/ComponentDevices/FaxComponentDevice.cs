using System.Linq;
using System.Text;
using Content.Server._Starlight.Fax;
using Content.Server.DeviceNetwork.Systems;
using Content.Server.Fax;
using Content.Shared.DeviceNetwork;
using Content.Shared.Fax.Components;
using Content.Shared.Paper;

namespace Content.Server._Starlight.UXN.Devices.ComponentDevices;

/// <summary>
/// A UXNDevice for a FaxMachine. memory layout is as follows.
/// 0x00 - status
/// 0x01 - command
/// 0x02,0x03 - bank1len*
/// 0x04,0x05 - bank1ptr*
/// 0x06,0x07 - bank2len*
/// 0x08,0x09 - bank2ptr*
/// 0x0A-0x0D - unused
/// 0x0E,0x0F - vector*
/// Commands are as follows
/// 0x00 - Continue buffered write
/// 0x01 - Re-scan devices
/// 0x02 - Write scanned devices to buffer1 in the format of name[null]XXXX-XXXX[null]
/// 0x03 - Update target fax addr from buffer1
/// 0x04 - send fax. buffer 1 is destination fax **id**. buffer 2 is fax contents.
/// 0xf0 - puts the number of buffered faxes into Status
/// 0xf1 - starts reading content from the next buffered fax
/// 0xf2 - reads the name of the fax into buffer1
/// 0xf3 - reads the contents of the fax into buffer1
/// 0xf4 - reads the stamps of the fax into buffer1 as a null-seperated list.
/// 0xf5 - reads the sender of the fax (address not name. buffer should ideally be 9 bytes long "XXXX-XXXX")
/// Status Codes
/// 0x00 - Success.
/// 0x80 - Invalid address. the fax address is invalid.
/// 0x81 - nothing buffered
/// 0x82 - buffered fax has no stamps.
/// 0xFF - Information buffered. The device has more information to write then the provided buffer(s) can hold. you can call `bufread` to continue reading (bufread can also raise this meaning it has even MORE to read).
/// UXN "Header" for the fax device (assuming mount at f0)
/*
|f0 @Fax &status $1 &cmd $1 &bnk1len $2 &bnk1ptr $2 &bnk2len $2 &bnk2ptr $2 &unused $4 &vector $2

|00 @Faxcmd &bufread $1 &reload $1 &dumpnames $1 &settarget $1 &send $1
|f0 &bufcount $1 &readnext $1 &readname $1 &readcontent $1 &readstamps $1 &readsender $1
*/
/// </summary>
public sealed class FaxComponentDevice : ComponentUxnDevice<FaxMachineComponent>
{
    private FaxSystem _fax = null!;
    private DeviceNetworkSystem _deviceNetwork = null!;
    protected override void SetupCore(EntityUid entity, FaxMachineComponent component)
    {
        var _entMan = IoCManager.Resolve<IEntitySystemManager>();
        _fax = _entMan.GetEntitySystem<FaxSystem>();
        _deviceNetwork = _entMan.GetEntitySystem<DeviceNetworkSystem>();
    } // We dont need any extra setup/information from the ent. but we do need the systems

    public readonly Queue<MinimalFaxInfo> ReadQueue = new();
    public MinimalFaxInfo? Next = null;

    public override void WriteValue(byte memTarget, Byte256 deviceMem, UXNProcessor proc)
    {
        if ((memTarget & 0x0F) != 0x01)
            return; //the bank being written is NOT the "command" bank. so we can just treat it as normal memory IO.
        byte command = deviceMem[memTarget];
        GetPointers(memTarget, deviceMem, out var buf1size, out var buf1ptr, out var buf2size, out var buf2ptr);
        var component = Entity.Comp;
        switch (command)
        {
            case 0x00: //Continue buffered write op.
                var bank1bufstatus = ContinueBufferedWrite(proc.SystemMem, buf1size, buf1ptr, true);
                var bank2bufstatus = ContinueBufferedWrite(proc.SystemMem, buf2size, buf2ptr, false);
                deviceMem[memTarget & 0xF0] = (byte)(bank1bufstatus || bank2bufstatus ? 0xFF : 0x00);
                break;
            case 0x01: //Re-Scan for devices
                _fax.Refresh(Entity.Owner, Entity.Comp);
                deviceMem[memTarget & 0xF0] = 0x00; //success!
                break;
            case 0x02: //List known faxes
                DumpKnownFaxes(memTarget, deviceMem, proc.SystemMem);
                break;
            case 0x03: //Set Destination fax
                var addr = component.DestinationFaxAddress;
                component.DestinationFaxAddress = ReadBuffered(proc.SystemMem, buf1size, buf1ptr);
                if (component.DestinationFaxAddress == null)
                {
                    deviceMem[memTarget & 0xF0] = 0x80; //invalid address
                    component.DestinationFaxAddress = addr;
                    break;
                }
                if (!component.KnownFaxes.TryGetValue(component.DestinationFaxAddress, out var _))
                {
                    deviceMem[memTarget & 0xF0] = 0x80; //invalid address
                    component.DestinationFaxAddress = addr;
                    break;
                }
                deviceMem[memTarget & 0xF0] = 0x00;
                break;
            case 0x04: //Send a fax to destination.

                if (component.DestinationFaxAddress == null)
                {
                    deviceMem[memTarget & 0xF0] = 0x80; //invalid address
                    break;
                }
                if (!component.KnownFaxes.TryGetValue(component.DestinationFaxAddress, out var _))
                {
                    deviceMem[memTarget & 0xF0] = 0x80; //invalid address
                    break;
                }

                var name = ReadBuffered(proc.SystemMem, buf1size, buf1ptr).Trim();
                var contents = ReadBuffered(proc.SystemMem, buf2size, buf2ptr).Trim();
                var payload = new NetworkPayload
                {
                    [DeviceNetworkConstants.Command] = FaxConstants.FaxPrintCommand,
                    [FaxConstants.FaxPaperNameData] = name,
                    [FaxConstants.FaxPaperContentData] = contents,
                };
                _deviceNetwork.QueuePacket(Entity, component.DestinationFaxAddress, payload);
                deviceMem[memTarget & 0xF0] = 0x00;
                break;
            case 0xf0: //Read number of buffered faxes
                deviceMem[memTarget & 0xF0] = (byte)Math.Min(ReadQueue.Count, 0xFF);
                break;
            case 0xf1: //Read number of faxes in-buffer
                if (!(ReadQueue.Count > 0))
                {
                    deviceMem[memTarget & 0xF0] = 0x81;
                    break;
                }
                Next = ReadQueue.Dequeue();
                deviceMem[memTarget & 0xF0] = 0x00;
                break;
            case 0xf2: //Read the name of the fax.
                if (Next == null)
                {
                    deviceMem[memTarget & 0xF0] = 0x81;
                    break;
                }
                deviceMem[memTarget & 0xF0] = (byte)(WriteBuffered(proc.SystemMem, buf1size, buf1ptr, Encoding.ASCII.GetBytes(Next.Name), true) ? 0xFF : 0x00);
                break;
            case 0xf3: //Read the contents of the fax.
                if (Next == null)
                {
                    deviceMem[memTarget & 0xF0] = 0x81;
                    break;
                }
                deviceMem[memTarget & 0xF0] = (byte)(WriteBuffered(proc.SystemMem, buf1size, buf1ptr, Encoding.ASCII.GetBytes(Next.Content), true) ? 0xFF : 0x00);
                break;
            case 0xf4: //Read the stamps of the fax.
                if (Next == null)
                {
                    deviceMem[memTarget & 0xF0] = 0x81;
                    break;
                }
                if (Next.StampedBy == null)
                {
                    deviceMem[memTarget & 0xF0] = 0x82;
                    break;
                }
                List<byte> output = new();
                foreach (StampDisplayInfo item in Next.StampedBy)
                {
                    output.AddRange(Encoding.ASCII.GetBytes(item.StampedName));
                    output.Add(0x00);
                }
                deviceMem[memTarget & 0xF0] = (byte)(WriteBuffered(proc.SystemMem, buf1size, buf1ptr, [.. output], true) ? 0xFF : 0x00);
                break;
            case 0xf5: //Read the sender of the fax.
                if (Next == null)
                {
                    deviceMem[memTarget & 0xF0] = 0x81;
                    break;
                }
                deviceMem[memTarget & 0xF0] = (byte)(WriteBuffered(proc.SystemMem, buf1size, buf1ptr, Encoding.ASCII.GetBytes(Next.Sender), true) ? 0xFF : 0x00);
                break;
            default: //invalid device command
                break;
        }
    }

    private void DumpKnownFaxes(byte memTarget, Byte256 deviceMem, UxnMem uxnMem)
    {
        GetPointers(memTarget, deviceMem, out var buf1size, out var buf1ptr, out var buf2size, out var buf2ptr);
        List<byte> output = new();
        foreach (KeyValuePair<string, string> item in Entity.Comp.KnownFaxes)
        {
            output.AddRange(Encoding.ASCII.GetBytes(item.Value));
            output.Add(0x00);
            output.AddRange(Encoding.ASCII.GetBytes(item.Key));
            output.Add(0x00);
        }
        deviceMem[memTarget & 0xF0] = (byte)(WriteBuffered(uxnMem, buf1size, buf1ptr, [.. output], true) ? 0xFF : 0x00);
    }

    #region Utility
    /// <summary>
    /// Gets pointers into device memory from the 
    /// </summary>
    /// <param name="memTarget">the memory address of the device (gets & 0xF0'd so it can be  any value apart of the device)</param>
    /// <param name="deviceMem">the memory to read pointers from</param>
    /// <param name="buf1size">the size of buffer 1. 0x00 is "until null byte"</param>
    /// <param name="buf1ptr">the starting address of buffer 1</param>
    /// <param name="buf2size">the size of buffer 2. 0x00 is "until null byte"</param>
    /// <param name="buf2ptr">the starting address of buffer 2</param>
    private void GetPointers(byte memTarget, Byte256 deviceMem, out ushort buf1size, out ushort buf1ptr, out ushort buf2size, out ushort buf2ptr)
    {
        var baseAddr = memTarget & 0xF0;
        buf1size = deviceMem.GetShort((byte)(baseAddr + 0x02));
        buf1ptr = deviceMem.GetShort((byte)(baseAddr + 0x04));
        buf2size = deviceMem.GetShort((byte)(baseAddr + 0x06));
        buf2ptr = deviceMem.GetShort((byte)(baseAddr + 0x08));
    }
    #endregion

    private readonly Queue<byte> _buf1Queue = new();
    private readonly Queue<byte> _buf2Queue = new();

    /// <summary>
    /// Writes a string into UXN's memory. buffering it into relevant queue if it runs out of space.
    /// </summary>
    /// <param name="mem">The memory to write into</param>
    /// <param name="bufferLen">The size of the buffer</param>
    /// <param name="addr">The starting address of the buffer</param>
    /// <param name="toWrite">The String to write into the buffer</param>
    /// <param name="primary">wheter to write into <see cref="_buf1Queue"/>/<see cref="_buf2Queue"/> depending on true/false</param>
    /// <returns>if extra contents were stashed in _buf1/2Queue</returns>
    private bool WriteBuffered(UxnMem mem, ushort bufferLen, ushort addr, byte[] toWrite, bool primary)
    {
        Queue<byte> enqued = new Queue<byte>(toWrite);
        var bytesToWrite = Math.Min(bufferLen, enqued.Count);
        Queue<byte> activeBuffer = primary ? _buf1Queue : _buf2Queue;
        for (var i = 0; i < bytesToWrite; i++)
        {
            mem[(ushort)(addr + i)] = enqued.Dequeue();
        }
        activeBuffer.Clear();
        enqued.ToList().ForEach(activeBuffer.Enqueue);
        return activeBuffer.Count != 0;
    }

    /// <summary>
    /// Continues a buffered write started by <see cref="WriteBuffered(UxnMem, ushort, ushort, string, bool)"/>
    /// </summary>
    /// <param name="mem">The memory to write into</param>
    /// <param name="bufferLen">The size of the buffer</param>
    /// <param name="addr">The starting address of the buffer</param>
    /// <param name="primary">wheter to write into <see cref="_buf1Queue"/>/<see cref="_buf2Queue"/> depending on true/false</param>
    /// <returns>if there is still more buffered contents to read</returns>
    private bool ContinueBufferedWrite(UxnMem mem, ushort bufferLen, ushort addr, bool primary)
    {
        Queue<byte> activeBuffer = primary ? _buf1Queue : _buf2Queue;
        var bytesToWrite = Math.Min(bufferLen, activeBuffer.Count);
        for (var i = 0; i < bytesToWrite; i++)
        {
            mem[(ushort)(addr + i)] = activeBuffer.Dequeue();
        }
        return activeBuffer.Count != 0;
    }

    public void MakeEvent(UXNProcessor uxn, MinimalFaxInfo info)
    {
        ReadQueue.Enqueue(info);
        uxn.PushEvent(new FaxRecievedUxnEvent(
            uxn.DevMem.GetShort(
                (byte)((uxn.SystemDevice.AttachedDevices["faxmachine"] << 0x4) + 0x0E)
                )
            )
         );
    }
}

public sealed partial class FaxRecievedUxnEvent(ushort vector) : UxnEvent
{
    public override void PerformEvent(UXNProcessor proc) => proc.PC = vector;
}