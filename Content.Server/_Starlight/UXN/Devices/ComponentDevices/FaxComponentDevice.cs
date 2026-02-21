using System.Linq;
using System.Text;
using Content.Server.DeviceNetwork.Systems;
using Content.Server.Fax;
using Content.Shared.DeviceNetwork;
using Content.Shared.Fax.Components;
using Microsoft.CodeAnalysis;

namespace Content.Server._Starlight.UXN.Devices.ComponentDevices;

/// <summary>
/// A UXNDevice for a FaxMachine. memory layout is as follows.
/// 0x00 - status
/// 0x01 - command
/// 0x02,0x03 - bank1len*
/// 0x04,0x05 - bank1ptr*
/// 0x06,0x07 - bank2len*
/// 0x08,0x09 - bank2ptr*
/// 0x0A-0x0F - unused
/// Commands are as follows
/// 0x00 - Continue buffered write
/// 0x01 - Re-scan devices
/// 0x02 - Write scanned devices to buffer1 in the format of name[null]XXXX-XXXX[null]
/// 0x03 - Update target fax addr from buffer1
/// 0x04 - send fax. buffer 1 is destination fax **id**. buffer 2 is fax contents.
/// </summary>
public sealed class FaxComponentDevice : ComponentUxnDevice<FaxMachineComponent>
{
    private FaxSystem _fax = null!;
    private DeviceNetworkSystem _deviceNetwork = null!;
    protected override void SetupCore(EntityUid entity, FaxMachineComponent component) {
        var _entMan = IoCManager.Resolve<IEntitySystemManager>();
        _fax = _entMan.GetEntitySystem<FaxSystem>();
        _deviceNetwork = _entMan.GetEntitySystem<DeviceNetworkSystem>();
    } // We dont need any extra setup/information from the ent. but we do need the systems

    public override void WriteValue(byte memTarget, Byte256 deviceMem, UXNProcessor proc)
    {
        if ((memTarget & 0x0F) != 0x01)
            return; //the bank being written is NOT the "command" bank. so we can just treat it as normal memory IO.
        byte command = deviceMem[memTarget];
        ushort buf1size, buf1ptr, buf2size, buf2ptr;
        GetPointers(memTarget, deviceMem, out buf1size, out buf1ptr, out buf2size, out buf2ptr);
        var component = Entity.Comp;
        switch (command)
        {
            case 0x00: //Continue buffered write op.
                GetPointers(memTarget, deviceMem, out buf1size, out buf1ptr, out buf2size, out buf2ptr);
                var bank1bufstatus = ContinueBufferedWrite(proc.SystemMem, buf1size, buf1ptr, true);
                var bank2bufstatus = ContinueBufferedWrite(proc.SystemMem, buf2size, buf2ptr, false);
                deviceMem[memTarget & 0xF0] =  (byte)(bank1bufstatus || bank2bufstatus ? 0xFF : 0x00);
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
                break;
            default: //invalid device command
                break;
        }
    }

    private void DumpKnownFaxes(byte memTarget, Byte256 deviceMem, UxnMem uxnMem)
    {
        GetPointers(memTarget, deviceMem, out var buf1size, out var buf1ptr, out var buf2size, out var buf2ptr);
        List<byte> output = new();
        foreach (KeyValuePair<string,string> item in Entity.Comp.KnownFaxes)
        {
            output.AddRange(Encoding.ASCII.GetBytes(item.Value));
            output.Add(0x00);
            output.AddRange(Encoding.ASCII.GetBytes(item.Key));
            output.Add(0x00);
        }
        WriteBuffered(uxnMem, buf1size, buf1ptr, [.. output], true);
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
    /// <returns>if string contents were put into a buffer.</returns>
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
        activeBuffer.Concat(enqued);
        return activeBuffer.Count != 0;
    }

    /// <summary>
    /// Continues a buffered write started by <see cref="WriteBuffered(UxnMem, ushort, ushort, string, bool)"/>
    /// </summary>
    /// <param name="mem">The memory to write into</param>
    /// <param name="bufferLen">The size of the buffer</param>
    /// <param name="addr">The starting address of the buffer</param>
    /// <param name="primary">wheter to write into <see cref="_buf1Queue"/>/<see cref="_buf2Queue"/> depending on true/false</param>
    /// <returns>if there is still more buffered strong contents to read</returns>
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
}