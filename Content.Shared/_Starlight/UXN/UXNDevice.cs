using System.Runtime.CompilerServices;
using Content.Shared._Starlight.UXN.Devices;
using Content.Shared.Anomaly.Components;
using Robust.Shared.Toolshed.Commands.Generic;
namespace Content.Shared._Starlight.UXN;


public struct Byte256
{
    private readonly byte[] _inner = new byte[256];

    public Byte256() {}

    public byte this[int i]
    {
        get => _inner[i];
        set => _inner[i] = value;
    }
}

public sealed class UxnStack
{
    public byte StackPointer { get; private set; } = 0;
    public byte StackPointerReturn { get; private set; } = 0;
    public Byte256 Stack { get; private set; } = new();

    public void Warp()
    {
        StackPointer = StackPointerReturn;
        StackPointerReturn = StackPointer;
    }

    public byte PopByte(bool sim)
    {
        byte val = Stack[StackPointer];
        StackPointer += 1;
        if (!sim) StackPointerReturn = StackPointer;
        return val;
    }
    public ushort PopShort(bool sim)
    {
        var lsb = PopByte(sim);
        var msb = PopByte(sim);
        return (ushort)((msb << 8) | lsb);
    }

    public void PushByte(byte dat)
    {
        Warp();
        var stack = Stack;
        stack[StackPointer] = dat;
        StackPointer += 1;
        StackPointerReturn += 1;
    }
    public void PushShort(ushort dat)
    {
        PushByte((byte)(dat >> 8));
        PushByte((byte)(dat & 0xff));
    }

    public void SetPointer(byte ptr)
    {
        Warp();
        StackPointer = ptr;
        StackPointerReturn = ptr;
    }
}

public struct UxnMem
{
    private readonly byte[] _inner = new byte[65536];

    public UxnMem() {}

    public byte this[int i]
    {
        get => _inner[i];
        set => _inner[i] = value;
    }
}

public struct UxnDevices
{
    private readonly UXNDevice[] _inner = new UXNDevice[0xF];

    public UxnDevices() {}

    public UXNDevice this[int i]
    {
        get => _inner[i];
        set => _inner[i] = value;
    }
}

[Virtual]
public class UXNDevice
{
    /// <summary>
    /// Called *after* writing a value into a memory address so you can read it.
    /// </summary>
    /// <param name="memTarget">the index into devicemem that is written</param>
    /// <param name="deviceMem">the memory space of devices that the UXN just wrote to</param>
    /// <param name="proc">the UXN processor that is managing the device and memory</param>
    public virtual void WriteValue(byte memTarget, Byte256 deviceMem, UXNProcessor proc) { }

    /// <summary>
    /// Called *before* reading a value from a memory address so the device can modify it
    /// </summary>
    /// <param name="memTarget">the address the CPU is about to read from</param>
    /// <param name="deviceMem">the full adress space of the Device memory</param>
    /// <param name="proc">the UXN processor that is reading value</param>
    public virtual void ReadValue(byte memTarget, Byte256 deviceMem, UXNProcessor proc) { }

    /// <summary>
    /// Called after every instruction is executed. intended for enqueing new events
    /// </summary>
    /// <param name="proc"></param>
    public virtual void ProcessorStep(UXNProcessor proc) { }

    /// <summary>
    /// Called when this device is attached to a UXN processor.
    /// </summary>
    /// <param name="proc"></param>
    public virtual void OnAttach(UXNProcessor proc) { }
}

public abstract class UxnEvent
{
    /// <summary>
    /// Performs the UXN event. the impls should handle setting PC based on device information. and filling out relevant memory before running it.
    /// </summary>
    /// <param name="proc">the UXN processor that is handling this event</param>
    public abstract void PerformEvent(UXNProcessor proc);
}

public sealed class UXNProcessor
{
    public UXNProcessor()
    {
        SystemDevice = AttachDevice(0x00, new StandardSystemDevice());
    }

    public bool Running { get; private set; } = true;

    public StandardSystemDevice SystemDevice;

    public Byte256 DevMem { get; private set; } = new();

    public ushort PC = 0x100; //starts right at the END of zero-page
    public UxnMem SystemMem { get; private set; } = new();
    public UxnStack WorkingStack { get; private set; } = new();
    public UxnStack ReturnStack { get; private set; } = new();
    public UxnDevices Devices { get; private set; } = new();

    private Queue<UxnEvent> _events = [];

    /// <summary>
    /// Runs the UXN for a single step
    /// </summary>
    /// <returns>if the UXN encountered a break and can queue another vector</returns>
    public bool Step()
    {
        if (!Running) return true;

        var instr = SystemMem[PC];
        PC++;

        bool keep = (instr & 0x80) != 0x00;
        bool ret = (instr & 0x40) != 0x00;
        bool shrt = (instr & 0x20) != 0x00;
        bool imme = (instr & 0x1F) == 0x00;

        var stack = ret ? ReturnStack : WorkingStack;
        var otherStack = ret ? WorkingStack : ReturnStack;
        stack.Warp();
        otherStack.Warp();

        var masked = imme ? instr : instr & 0x1F;
        switch (masked)
        {
            case 0x00: return true; // NOP
            #region immediates
            case 0x20: // JCI
                {
                    var msb = SystemMem[PC];
                    var addr = (ushort)((msb << 8) | SystemMem[PC + 1]);
                    PC += 2;
                    if (stack.PopByte(false) != 0) PC += addr;
                }
                break;
            case 0x40: // JMI
                {
                    var msb = SystemMem[PC];
                    var addr = (ushort)((msb << 8) | SystemMem[PC + 1]);
                    PC += addr;
                    PC += 2;
                }
                break;
            case 0x60: // JSI
                {
                    var msb = SystemMem[PC];
                    var addr = (ushort)((msb << 8) | SystemMem[PC + 1]);
                    PC += 2;
                    ReturnStack.PushShort(PC);
                    PC += addr;
                }
                break;
            case 0x80: // LIT
                {
                    stack.PushByte(SystemMem[PC]);
                    PC++;
                }
                break;
            case 0xA0: // LIT2
                {
                    var msb = SystemMem[PC];
                    var res = (ushort)((msb << 8) | SystemMem[PC + 1]);
                    PC += 2;
                    stack.PushShort(res);
                }
                break;
            #endregion immediates
            #region basic stack
            case 0x01: //INC a -- a+1
                if (shrt)
                {
                    var a = stack.PopShort(keep);
                    stack.PushShort((ushort)(a + 1));
                }
                else
                {
                    var a = stack.PopByte(keep);
                    stack.PushByte((byte)(a + 1));
                }
                break;
            case 0x02: //POP a --
                if (shrt)
                {
                    stack.PopShort(keep);
                }
                else
                {
                    stack.PopByte(keep);
                }
                break;
            case 0x03: //NIP a b -- b
                if (shrt)
                {
                    var b = stack.PopShort(keep);
                    stack.PopShort(keep); // a
                    stack.PushShort(b);
                }
                else
                {
                    var b = stack.PopByte(keep);
                    stack.PopByte(keep);
                    stack.PushByte(b);
                }
                break;
            case 0x04: // SWP a b -- b a
                if (shrt)
                {
                    var b = stack.PopShort(keep);
                    var a = stack.PopShort(keep);
                    stack.PushShort(b);
                    stack.PushShort(a);
                }
                else
                {
                    var b = stack.PopByte(keep);
                    var a = stack.PopByte(keep);
                    stack.PushByte(b);
                    stack.PushByte(a);
                }
                break;
            case 0x05: // ROT a b c -- b c a
                if (shrt)
                {
                    var c = stack.PopShort(keep);
                    var b = stack.PopShort(keep);
                    var a = stack.PopShort(keep);
                    stack.PushShort(b);
                    stack.PushShort(c);
                    stack.PushShort(a);
                }
                else
                {
                    var c = stack.PopByte(keep);
                    var b = stack.PopByte(keep);
                    var a = stack.PopByte(keep);
                    stack.PushByte(b);
                    stack.PushByte(c);
                    stack.PushByte(a);
                }
                break;
            case 0x06: // DUP a -- a a
                if (shrt)
                {
                    var a = stack.PopShort(keep);
                    stack.PushShort(a);
                    stack.PushShort(a);
                }
                else
                {
                    var a = stack.PopByte(keep);
                    stack.PushByte(a);
                    stack.PushByte(a);
                }
                break;
            case 0x07: // OVR a b -- a b a
                if (shrt)
                {
                    var b = stack.PopShort(keep);
                    var a = stack.PopShort(keep);
                    stack.PushShort(a);
                    stack.PushShort(b);
                    stack.PushShort(a);
                }
                else
                {
                    var b = stack.PopByte(keep);
                    var a = stack.PopByte(keep);
                    stack.PushByte(a);
                    stack.PushByte(b);
                    stack.PushByte(a);
                }
                break;
            #endregion basic stack
            #region comparisons
            case 0x08: // EQU a b -- bool8
                if (shrt)
                {
                    var b = stack.PopShort(keep);
                    var a = stack.PopShort(keep);
                    stack.PushByte((byte)(a == b ? 1 : 0));
                }
                else
                {
                    var b = stack.PopByte(keep);
                    var a = stack.PopByte(keep);
                    stack.PushByte((byte)(a == b ? 1 : 0));
                }
                break;
            case 0x09: // NEQ a b -- bool8
                if (shrt)
                {
                    var b = stack.PopShort(keep);
                    var a = stack.PopShort(keep);
                    stack.PushByte((byte)(a == b ? 0 : 1));
                }
                else
                {
                    var b = stack.PopByte(keep);
                    var a = stack.PopByte(keep);
                    stack.PushByte((byte)(a == b ? 0 : 1));
                }
                break;
            case 0x0A: // GTH a b -- bool8
                if (shrt)
                {
                    var b = stack.PopShort(keep);
                    var a = stack.PopShort(keep);
                    stack.PushByte((byte)(a > b ? 1 : 0));
                }
                else
                {
                    var b = stack.PopByte(keep);
                    var a = stack.PopByte(keep);
                    stack.PushByte((byte)(a > b ? 1 : 0));
                }
                break;
            case 0x0B: // LTH a b -- bool8
                if (shrt)
                {
                    var b = stack.PopShort(keep);
                    var a = stack.PopShort(keep);
                    stack.PushByte((byte)(a < b ? 1 : 0));
                }
                else
                {
                    var b = stack.PopByte(keep);
                    var a = stack.PopByte(keep);
                    stack.PushByte((byte)(a < b ? 1 : 0));
                }
                break;
            #endregion comparisons
            #region JMPs
            case 0x0C: // JMP addr --
                PC = shrt ? stack.PopShort(keep) : (ushort)(PC + stack.PopByte(keep));
                break;
            case 0x0D: // JCN cond8 addr --
                {
                    var tgt = shrt ? stack.PopShort(keep) : (ushort)(PC + stack.PopByte(keep));
                    if (stack.PopByte(keep) != 0)
                        PC = tgt;
                }
                break;
            case 0x0E: // JSR addr --
                otherStack.PushShort(PC);
                PC = shrt ? stack.PopShort(keep) : (ushort)(PC + stack.PopByte(keep));
                break;
            case 0x0F: // STH a -- | a
                if (shrt)
                {
                    var a = stack.PopShort(keep);
                    otherStack.PushShort(a);
                }
                else
                {
                    var a = stack.PopByte(keep);
                    otherStack.PushByte(a);
                }
                break;
            #endregion JMPs
            #region memory manipulation
            case 0x10: // LDZ addr8 -- value
                {
                    var zp = stack.PopByte(keep);
                    if (shrt)
                    {
                        var msb = SystemMem[zp];
                        stack.PushShort((ushort)((msb << 8) | SystemMem[(zp + 1) & 0xff]));
                    }
                    else
                    {
                        stack.PushByte(SystemMem[zp]);
                    }
                }
                break;
            case 0x11: // STZ val addr8 --
                {
                    var addr = stack.PopByte(keep);
                    var mem = SystemMem;
                    if (shrt)
                    {
                        var val = stack.PopShort(keep);
                        mem[addr] = (byte)(val >> 8);
                        mem[(addr + 1) & 0xFF] = (byte)(val & 0xff);
                    }
                    else
                    {
                        mem[addr] = stack.PopByte(keep);
                    }
                }
                break;
            case 0x12: // LDR addr8 -- value
                {
                    var addr = (ushort)(stack.PopByte(keep) + PC);
                    if (shrt)
                    {
                        stack.PushShort(
                            (ushort)((SystemMem[addr] << 8) | SystemMem[addr + 1])
                        );
                    }
                    else
                    {
                        stack.PushByte(SystemMem[addr]);
                    }
                }
                break;
            case 0x13: // STR value addr8 --
                {
                    var addr = (stack.PopByte(keep) + PC) & 0xFFFF;
                    var mem = SystemMem;
                    if (shrt)
                    {
                        var val = stack.PopShort(keep);
                        mem[addr] = (byte)(val >> 8);
                        mem[(addr + 1) & 0xFFFF] = (byte)(val & 0xFF);
                    }
                    else
                    {
                        mem[addr] = stack.PopByte(keep);
                    }
                }
                break;
            case 0x14: // LDA addr16 -- value
                {
                    var addr = stack.PopShort(keep);
                    if (shrt)
                    {
                        stack.PushShort((ushort)((SystemMem[addr] << 8) | SystemMem[(addr + 1) & 0xFFFF]));
                    }
                    else
                    {
                        stack.PushByte(SystemMem[addr]);
                    }
                }
                break;
            case 0x15: // STA value addr16 --
                {
                    var addr = stack.PopShort(keep);
                    var mem = SystemMem;
                    if (shrt)
                    {
                        var val = stack.PopShort(keep);
                        mem[addr] = (byte)(val >> 8);
                        mem[(addr + 1) & 0xFFFF] = (byte)(val & 0xFF);
                    }
                    else
                    {
                        mem[addr] = stack.PopByte(keep);
                    }
                }
                break;
            case 0x16: // DEI device8 -- value
                {
                    var dev = stack.PopByte(keep);
                    var devInstance = Devices[dev >> 4];
                    if (shrt)
                    {
                        devInstance.ReadValue(dev, DevMem, this);
                        devInstance.ReadValue((byte)((dev + 1) & 0xFF), DevMem, this);
                        var msb = DevMem[dev];
                        stack.PushShort((ushort)((msb << 8) | DevMem[(dev + 1) & 0xFF]));
                    }
                    else
                    {
                        devInstance.ReadValue(dev, DevMem, this);
                        stack.PushByte(DevMem[dev]);
                    }
                }
                break;
            case 0x17: // DEO value, device8 --
                {
                    var dev = stack.PopByte(keep);
                    var devInstance = Devices[dev >> 4];
                    var mem = DevMem;
                    if (shrt)
                    {
                        var val = stack.PopShort(keep);
                        mem[dev] = (byte)(val >> 8);
                        mem[(dev + 1) & 0xFF] = (byte)(val & 0xFF);
                        devInstance.WriteValue(dev, DevMem, this);
                        devInstance.WriteValue((byte)((dev + 1) & 0xFF), DevMem, this);
                    }
                    else
                    {
                        mem[dev] = stack.PopByte(keep);
                        devInstance.WriteValue(dev, DevMem, this);
                    }
                }
                break;
            #endregion memory manipulation
            #region math
            case 0x18: // ADD a b -- a+b
                if (shrt)
                {
                    var b = stack.PopShort(keep);
                    var a = stack.PopShort(keep);
                    stack.PushShort((ushort)(a + b));
                }
                else
                {
                    var b = stack.PopShort(keep);
                    var a = stack.PopByte(keep);
                    stack.PushByte((byte)(a + b));
                }
                break;
            case 0x19: // SUB a b -- a-b
                if (shrt)
                {
                    var b = stack.PopShort(keep);
                    var a = stack.PopShort(keep);
                    stack.PushShort((ushort)(a - b));
                }
                else
                {
                    var b = stack.PopShort(keep);
                    var a = stack.PopByte(keep);
                    stack.PushByte((byte)(a - b));
                }
                break;
            case 0x1A: // MUL a b -- a*b
                if (shrt)
                {
                    var b = stack.PopShort(keep);
                    var a = stack.PopShort(keep);
                    stack.PushShort((ushort)(a * b));
                }
                else
                {
                    var b = stack.PopShort(keep);
                    var a = stack.PopByte(keep);
                    stack.PushByte((byte)(a * b));
                }
                break;
            case 0x1B: // DIV a b -- a/b
                if (shrt)
                {
                    var b = stack.PopShort(keep);
                    var a = stack.PopShort(keep);
                    stack.PushShort((ushort)(a / b));
                }
                else
                {
                    var b = stack.PopShort(keep);
                    var a = stack.PopByte(keep);
                    stack.PushByte((byte)(a / b));
                }
                break;
            case 0x1D: // OR a b -- a||b
                if (shrt)
                {
                    var b = stack.PopShort(keep);
                    var a = stack.PopShort(keep);
                    stack.PushShort((ushort)(a | b));
                }
                else
                {
                    var b = stack.PopShort(keep);
                    var a = stack.PopByte(keep);
                    stack.PushByte((byte)(a | b));
                }
                break;
            case 0x1E: // XOR a b -- a^b
                if (shrt)
                {
                    var b = stack.PopShort(keep);
                    var a = stack.PopShort(keep);
                    stack.PushShort((ushort)(a ^ b));
                }
                else
                {
                    var b = stack.PopShort(keep);
                    var a = stack.PopByte(keep);
                    stack.PushByte((byte)(a ^ b));
                }
                break;
            case 0x1F: // SFT a shift8 -- c
                {
                    var shift8 = stack.PopByte(keep);
                    var left = shift8 >> 4;
                    var right = shift8 & 0xF0;
                    if (shrt)
                    {
                        var a = stack.PopShort(keep);
                        stack.PushShort((ushort)((a >> right) << left));
                    }
                    else
                    {
                        var a = stack.PopByte(keep);
                        stack.PushByte((byte)((a >> right) << left));
                    }
                }
                break;
            #endregion
            default: throw new InvalidOperationException($"got {masked} for a opcode which shouldn't possible");
        }
        ;
        return true;
    }

    /// <summary>
    /// Wipes *everything* from the UXN reseting it's stacks, memory and program counter
    /// </summary>
    public void Reset()
    {
        PC = 0x100;
        DevMem = new();
        SystemMem = new();
        ReturnStack = new();
        WorkingStack = new();
        Devices = new();
        _events = [];
        Running = true;
    }

    public void PushEvent(UxnEvent uevent)
    {
        _events.Enqueue(uevent);
        return;
    }

    public T AttachDevice<T>(byte slot, T device) where T : UXNDevice
    {
        var devices = Devices;
        devices[slot] = device;
        device.OnAttach(this);
        return device;
    }

    public bool RunLimited(int steps)
    {
        if (!Running)
        {
            if (_events.Count == 0)
                return false; // UXN is dead in the water. we stopped running and have no events to start it.
            _events.Dequeue().PerformEvent(this); //we have a event which should get us moving again
            Running = true;
        }

        for (int i = 0; i < steps; i++)
        {
            if (Step())
            {
                if (SystemDevice.Status != 0)
                {
                    Running = false;
                    _events.Clear();
                    return true;
                }
                if (_events.Count > 0)
                    _events.Dequeue().PerformEvent(this);
                else
                {
                    Running = false;
                    return false;
                }
            }
        }

        return false;
    }

    public bool RunUnlimited()
    {
        while (Running)
        {
            if (Step())
            {
                if (SystemDevice.Status != 0)
                {
                    Running = false;
                    _events.Clear();
                    return true;
                }
                if (_events.Count > 0)
                    _events.Dequeue().PerformEvent(this);
                else
                {
                    Running = false;
                    return false;
                }
            }
        }
        return false;
    }
}
