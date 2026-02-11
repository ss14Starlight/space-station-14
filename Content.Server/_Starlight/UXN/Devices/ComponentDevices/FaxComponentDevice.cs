using System.Text;
using Content.Server.Fax;
using Content.Shared.Fax.Components;

namespace Content.Server._Starlight.UXN.Devices.ComponentDevices;

public sealed class FaxComponentDevice : ComponentUxnDevice<FaxMachineComponent>
{
    private FaxSystem _fax;
    protected override void SetupCore(EntityUid entity, FaxMachineComponent component) {
        var _entMan = IoCManager.Resolve<EntitySystemManager>();
        _fax = _entMan.GetEntitySystem<FaxSystem>();
    } // We dont need any extra setup/information from the ent. but we do need the sytem

    public override void ReadValue(byte memTarget, Byte256 deviceMem, UXNProcessor proc) => base.ReadValue(memTarget, deviceMem, proc);
    public override void WriteValue(byte memTarget, Byte256 deviceMem, UXNProcessor proc) => base.WriteValue(memTarget, deviceMem, proc);

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
            short readAddr = addr;
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
                output.Append(mem[addr + i]);
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
    private bool WriteBuffered(UxnMem mem, ushort bufferLen, ushort addr, string toWrite, bool primary)
    {

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

    }
}