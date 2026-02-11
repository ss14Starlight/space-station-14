using System.Linq;
using System.Text;
using Content.Server.Fax;
using Content.Shared.Fax.Components;

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
/// </summary>
public sealed class FaxComponentDevice : ComponentUxnDevice<FaxMachineComponent>
{
    private FaxSystem _fax = null!;
    protected override void SetupCore(EntityUid entity, FaxMachineComponent component) {
        var _entMan = IoCManager.Resolve<EntitySystemManager>();
        _fax = _entMan.GetEntitySystem<FaxSystem>();
    } // We dont need any extra setup/information from the ent. but we do need the sytem

    public override void WriteValue(byte memTarget, Byte256 deviceMem, UXNProcessor proc)
    {
        if ((memTarget & 0x0F) != 0x01)
            return; //the bank being written is NOT the "command" bank. so we can just treat it as normal memory IO.
        byte command = deviceMem[memTarget];
        switch(command)
        {
            case 0x00: //Scan Devices


            default: //invalid device command
                break;
        }
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

    /// <summary>
    /// Reads a string from a buffer.
    /// </summary>
    /// <param name="mem">the memory to read from</param>
    /// <param name="bufferLen">the size of the buffer. if 0x00 will attempt to read until it encounters null (basically a null-terminated string)</param>
    /// <param name="addr">the starting addr of the buffer</param>
    /// <returns></returns>
    private string ReadBuffered(UxnMem mem, ushort bufferLen, ushort addr)
    {
        StringBuilder output = new StringBuilder();
        if (bufferLen == 0)
        {
            byte read = 0xff;
            ushort readAddr = addr;
            ushort counter = 1;
            while (read != 0 && counter != 0)
            {
                read = mem[addr];
                addr++;
                counter++;
                output.Append(Encoding.ASCII.GetChars([read]));
            }
            output.Length--; //Delete the last character
        } else
        {
            for (short i = 0; i < bufferLen; i++)
            {
                output.Append(mem[(ushort)(addr + i)]);
            }
        }
        return output.ToString();
    }

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