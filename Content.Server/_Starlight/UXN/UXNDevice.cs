using System.Collections;
using Content.Server._Starlight.UXN.Devices;

namespace Content.Server._Starlight.UXN;

public sealed class Byte256
{
    private readonly byte[] _inner = new byte[256];

    public Byte256() =>
        Array.Fill<byte>(_inner, 0x00);

    public byte this[int i]
    {
        get => _inner[i];
        set => _inner[i] = value;
    }

    public byte[] ToRaw() => (byte[])_inner.Clone();
}

public sealed class UxnStack
{
    public byte StackPointer { get; private set; } = 0;
    public byte StackPointerReturn { get; private set; } = 0;
    public Byte256 Stack { get; private set; } = new();

    public void Warp()
    {
        StackPointer += StackPointerReturn;
        StackPointerReturn = 0;
    }

    public byte PopByte(bool sim)
    {
        if (sim) StackPointerReturn++;
        StackPointer -= 1;
        return Stack[StackPointer];
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
        Stack[StackPointer] = dat;
        StackPointer += 1;
    }
    public void PushShort(ushort dat)
    {
        PushByte((byte)(dat >> 8));
        PushByte((byte)(dat & 0xff));
    }
    public void SetPointer(byte ptr)
    {
        StackPointer = ptr;
        StackPointerReturn = 0;
    }

    public (byte, byte[]) ToRaw() =>
        (StackPointer, Stack.ToRaw());
}

public sealed class UxnMem
{
    private readonly byte[] _inner = new byte[65536];

    public UxnMem() =>
        Array.Fill<byte>(_inner, 0x00);

    public byte this[int i]
    {
        get => _inner[i];
        set => _inner[i] = value;
    }

    public byte[] ToRaw() => (byte[])_inner.Clone();
}

public struct UxnDevices : IEnumerable<UXNDevice>
{
    private readonly UXNDevice[] _inner = new UXNDevice[0x10];

    public UxnDevices() =>
        Array.Fill(_inner, new UXNDevice());

    public UXNDevice this[int i]
    {
        get => _inner[i];
        set => _inner[i] = value;
    }

    public IEnumerator<UXNDevice> GetEnumerator() => ((IEnumerable<UXNDevice>)_inner).GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
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

[DataDefinition]
public partial struct UxnFrame
{
    public ushort PC;
    public (byte, byte[]) WS;
    public (byte, byte[]) RS;
    public byte[] Mem;

    public UxnFrame(ushort pc, (byte, byte[]) ws, (byte, byte[]) rs, byte[] mem)
    {
        PC = pc;
        WS = ws;
        RS = rs;
        Mem = mem;
    }

    public static UxnFrame FromProcessor(UXNProcessor proc) =>
        new(proc.PC, proc.WorkingStack.ToRaw(), proc.ReturnStack.ToRaw(), proc.SystemMem.ToRaw());
}

public sealed class UXNProcessor
{
    public static readonly string[] DISASM_TABLE = ["BRK", "INC", "POP", "NIP", "SWP", "ROT", "DUP", "OVR", "EQU", "NEQ", "GTH", "LTH", "JMP", "JCN", "JSR", "STH", "LDZ", "STZ", "LDR", "STR", "LDA", "STA", "DEI", "DEO", "ADD", "SUB", "MUL", "DIV", "AND", "ORA", "EOR", "SFT", "JCI", "INC2", "POP2", "NIP2", "SWP2", "ROT2", "DUP2", "OVR2", "EQU2", "NEQ2", "GTH2", "LTH2", "JMP2", "JCN2", "JSR2", "STH2", "LDZ2", "STZ2", "LDR2", "STR2", "LDA2", "STA2", "DEI2", "DEO2", "ADD2", "SUB2", "MUL2", "DIV2", "AND2", "ORA2", "EOR2", "SFT2", "JMI", "INCr", "POPr", "NIPr", "SWPr", "ROTr", "DUPr", "OVRr", "EQUr", "NEQr", "GTHr", "LTHr", "JMPr", "JCNr", "JSRr", "STHr", "LDZr", "STZr", "LDRr", "STRr", "LDAr", "STAr", "DEIr", "DEOr", "ADDr", "SUBr", "MULr", "DIVr", "ANDr", "ORAr", "EORr", "SFTr", "JSI", "INC2r", "POP2r", "NIP2r", "SWP2r", "ROT2r", "DUP2r", "OVR2r", "EQU2r", "NEQ2r", "GTH2r", "LTH2r", "JMP2r", "JCN2r", "JSR2r", "STH2r", "LDZ2r", "STZ2r", "LDR2r", "STR2r", "LDA2r", "STA2r", "DEI2r", "DEO2r", "ADD2r", "SUB2r", "MUL2r", "DIV2r", "AND2r", "ORA2r", "EOR2r", "SFT2r", "LIT", "INCk", "POPk", "NIPk", "SWPk", "ROTk", "DUPk", "OVRk", "EQUk", "NEQk", "GTHk", "LTHk", "JMPk", "JCNk", "JSRk", "STHk", "LDZk", "STZk", "LDRk", "STRk", "LDAk", "STAk", "DEIk", "DEOk", "ADDk", "SUBk", "MULk", "DIVk", "ANDk", "ORAk", "EORk", "SFTk", "LIT2", "INC2k", "POP2k", "NIP2k", "SWP2k", "ROT2k", "DUP2k", "OVR2k", "EQU2k", "NEQ2k", "GTH2k", "LTH2k", "JMP2k", "JCN2k", "JSR2k", "STH2k", "LDZ2k", "STZ2k", "LDR2k", "STR2k", "LDA2k", "STA2k", "DEI2k", "DEO2k", "ADD2k", "SUB2k", "MUL2k", "DIV2k", "AND2k", "ORA2k", "EOR2k", "SFT2k", "LITr", "INCkr", "POPkr", "NIPkr", "SWPkr", "ROTkr", "DUPkr", "OVRkr", "EQUkr", "NEQkr", "GTHkr", "LTHkr", "JMPkr", "JCNkr", "JSRkr", "STHkr", "LDZkr", "STZkr", "LDRkr", "STRkr", "LDAkr", "STAkr", "DEIkr", "DEOkr", "ADDkr", "SUBkr", "MULkr", "DIVkr", "ANDkr", "ORAkr", "EORkr", "SFTkr", "LIT2r", "INC2kr", "POP2kr", "NIP2kr", "SWP2kr", "ROT2kr", "DUP2kr", "OVR2kr", "EQU2kr", "NEQ2kr", "GTH2kr", "LTH2kr", "JMP2kr", "JCN2kr", "JSR2kr", "STH2kr", "LDZ2kr", "STZ2kr", "LDR2kr", "STR2kr", "LDA2kr", "STA2kr", "DEI2kr", "DEO2kr", "ADD2kr", "SUB2kr", "MUL2kr", "DIV2kr", "AND2kr", "ORA2kr", "EOR2kr", "SFT2kr"];
    public UXNProcessor() => Reset();

    public bool Running { get; private set; } = true;

    public StandardSystemDevice SystemDevice = new(); //this gets overrwiten basically instantly but oh well.

    public Byte256 DevMem { get; private set; } = new();

    public ushort PC = 0x100; //starts right at the END of zero-page
    public UxnMem SystemMem { get; private set; } = new();
    public UxnStack WorkingStack { get; private set; } = new();
    public UxnStack ReturnStack { get; private set; } = new();
    public UxnDevices Devices { get; private set; } = new();

    /// <summary>
    /// Used by <see cref="RunLimited(int)"/> when determining the number of instructions ran. can be much higher then <see cref="RealInstructionCounter"/>.
    /// Can be incremented by <see cref="AddInstructionsToCounter(int)"/>
    /// </summary>
    public int InstructionCounter { get; private set; } = 0;
    /// <summary>
    /// The real number of instructions executed by the UXN. Good for knowing how long a program ACTUALLY ran. If you wanna know vaguely how much "effort" was put into something.
    /// Check <seealso cref="InstructionCounter"/>
    /// </summary>
    public int RealInstructionCounter { get; private set; } = 0;
    //public List<(ushort, string)> InstrLog { get; private set; } = new();
    //public List<UxnFrame> FrameLog { get; private set; } = new();

    private Queue<UxnEvent> _events = new();

    /// <summary>
    /// Used by devices mainly when they want their device to consume extra instructions as apart of their execution
    /// </summary>
    /// <param name="amount"></param>
    public void AddInstructionsToCounter(int amount) => InstructionCounter += amount;

    /// <summary>
    /// Runs the UXN for a single step
    /// </summary>
    /// <returns>if the UXN encountered a break and can queue another vector</returns>
    public bool Step()
    {
        if (!Running) return true;

        var instr = SystemMem[PC];

        //InstrLog.Add((PC, DISASM_TABLE[instr]));
        //FrameLog.Add(UxnFrame.FromProcessor(this));
        PC++;

        InstructionCounter++;
        RealInstructionCounter++;

        bool keep = (instr & 0x80) != 0x00;
        bool ret = (instr & 0x40) != 0x00;
        bool shrt = (instr & 0x20) != 0x00;
        bool imme = (instr & 0x1F) == 0x00;

        var stack = ret ? ReturnStack : WorkingStack;
        var otherStack = ret ? WorkingStack : ReturnStack;
        
        var masked = imme ? instr : instr & 0x1F;
        switch (masked)
        {
            case 0x00: return true; // BRK
            #region immediates
            case 0x20: // JCI
                {
                    var msb = SystemMem[PC];
                    var addr = (ushort)((msb << 8) | SystemMem[(ushort)(PC + 1)]);
                    PC += 2;
                    if (stack.PopByte(false) != 0) PC += addr;
                }
                break;
            case 0x40: // JMI
                {
                    var msb = SystemMem[PC];
                    var addr = (ushort)((msb << 8) | SystemMem[(ushort)(PC + 1)]);
                    PC += addr;
                    PC += 2;
                }
                break;
            case 0x60: // JSI
                {
                    var msb = SystemMem[PC];
                    var addr = (ushort)((msb << 8) | SystemMem[(ushort)(PC + 1)]);
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
                    var res = (ushort)((msb << 8) | SystemMem[(ushort)(PC + 1)]);
                    PC += 2;
                    stack.PushShort(res);
                }
                break;
            case 0xC0: // LITr
                {
                    stack.PushByte(SystemMem[PC]);
                    PC++;
                }
                break;
            case 0xE0: // LIT2r
                {
                    var msb = SystemMem[PC];
                    var res = (ushort)((msb << 8) | SystemMem[(ushort)(PC + 1)]);
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
            case 0x02: // POP a --
                if (shrt)
                {
                    stack.PopShort(keep);
                }
                else
                {
                    stack.PopByte(keep);
                }
                break;
            case 0x03: // NIP a b -- b
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
                PC = shrt ? stack.PopShort(keep) : (ushort)(PC + (sbyte)stack.PopByte(keep));
                break;
            case 0x0D: // JCN cond8 addr --
                {
                    var tgt = shrt ? stack.PopShort(keep) : (ushort)(PC + (sbyte)stack.PopByte(keep));
                    if (stack.PopByte(keep) != 0)
                        PC = tgt;
                }
                break;
            case 0x0E: // JSR addr --
                otherStack.PushShort(PC);
                PC = shrt ? stack.PopShort(keep) : (ushort)(PC + (sbyte)stack.PopByte(keep));
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
                    var addr = (ushort)(PC + (sbyte)stack.PopByte(keep));
                    if (shrt)
                    {
                        stack.PushShort(
                            (ushort)((SystemMem[addr] << 8) | SystemMem[(ushort)(addr + 1)])
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
                    var addr = (PC + (sbyte)stack.PopByte(keep)) & 0xFFFF;
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
                        var possibleDev = Devices[((dev + 1) & 0xFF) >> 4];
                        devInstance.ReadValue(dev, DevMem, this);
                        possibleDev.ReadValue((byte)((dev + 1) & 0xFF), DevMem, this);
                        var msb = DevMem[dev];
                        stack.PushShort((ushort)((msb << 8) | (DevMem[(dev + 1) & 0xFF])));
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
                        var possibleDev = Devices[((dev + 1) & 0xFF) >> 4];
                        var val = stack.PopShort(keep);
                        mem[dev] = (byte)(val >> 8);
                        mem[(dev + 1) & 0xFF] = (byte)(val & 0xFF);
                        devInstance.WriteValue(dev, DevMem, this);
                        possibleDev.WriteValue((byte)((dev + 1) & 0xFF), DevMem, this);
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
                    var b = stack.PopByte(keep);
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
                    var b = stack.PopByte(keep);
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
                    var b = stack.PopByte(keep);
                    var a = stack.PopByte(keep);
                    stack.PushByte((byte)(a * b));
                }
                break;
            case 0x1B: // DIV a b -- a/b
                if (shrt)
                {
                    var b = stack.PopShort(keep);
                    var a = stack.PopShort(keep);
                    if (b != 0) { stack.PushShort((ushort)(a / b)); }
                    else { stack.PushShort(0x0000); }
                }
                else
                {
                    var b = stack.PopByte(keep);
                    var a = stack.PopByte(keep);
                    if (b != 0) { stack.PushByte((byte)(a / b)); }
                    else { stack.PushByte(0x00); }
                }
                break;
            case 0x1C: // AND a b -- a&b
                if (shrt)
                {
                    var b = stack.PopShort(keep);
                    var a = stack.PopShort(keep);
                    stack.PushShort((ushort)(a & b));
                }
                else
                {
                    var b = stack.PopByte(keep);
                    var a = stack.PopByte(keep);
                    stack.PushByte((byte)(a & b));
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
                    var b = stack.PopByte(keep);
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
                    var b = stack.PopByte(keep);
                    var a = stack.PopByte(keep);
                    stack.PushByte((byte)(a ^ b));
                }
                break;
            case 0x1F: // SFT a shift8 -- c
                {
                    var shift8 = stack.PopByte(keep);
                    var left = shift8 >> 4;
                    var right = shift8 & 0xF;
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

        stack.Warp();
        otherStack.Warp();

        foreach (UXNDevice dev in Devices)
            dev.ProcessorStep(this);

        return false;
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
        SystemDevice = AttachDevice(0x00, new StandardSystemDevice());
        _events = [];
        Running = true;
        InstructionCounter = 0;
        RealInstructionCounter = 0;
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

        var instrs = InstructionCounter + instrs;
        while (InstructionCounter < instrs)
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
                {
                    _events.Dequeue().PerformEvent(this);
                }
                else
                {
                    Running = false;
                }
            }
        }
        return false;
    }
}