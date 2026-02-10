using System.Linq;

namespace Content.Server._Starlight.UXN.Devices;

[Virtual]
public class StandardSystemDevice : UXNDevice
{
    public StandardSystemDevice(int numBanks = 0)
    => ExtraPages = [.. Enumerable.Repeat(new UxnMem(), Math.Min(numBanks,ushort.MaxValue))]; //clamped because any more and UXN cant access them.
    

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
                // get the address into system memory to read the expansion command from
                var msb = deviceMem[(byte)(memTarget-1)];
                var res = (ushort)((msb << 8) | (deviceMem[memTarget]));
                var cmd = proc.SystemMem[res];
                ushort lenmsb, length, srcbmsb, sourceBank, srcmsb, source, dstbmsb, destBank, dstmsb, dest;
                UxnMem sourcePage, destPage;
                switch (cmd)
                {
                    /*fill length* bank* start* value */ case 0x00: //fill
                        lenmsb = proc.SystemMem[(ushort)(res + 1)];
                        length = (ushort)((lenmsb << 8) | (proc.SystemMem[(ushort)(res+2)]));
                        var bnkmsb = proc.SystemMem[(ushort)(res+3)];
                        var bank = (ushort)((bnkmsb << 8) | (proc.SystemMem[(ushort)(res + 4)]));
                        var adrmsb = proc.SystemMem[(ushort)(res+5)];
                        var addres = (ushort)((adrmsb << 8) | (proc.SystemMem[(ushort)(res + 6)]));
                        var value = proc.SystemMem[(ushort)(res + 7)];
                        if (bank > ExtraPages.Count)
                            break;
                        

                        var target = (bank == 0) ? proc.SystemMem : ExtraPages[(ushort)(bank - 1)];
                        for (int i = 0; i < length; i++)
                        {
                            target[(ushort)(addres + i)] = value;
                        }
                        break;
                    /*cpyl length* src_bank* src_addr* dest_bank* dest_addr* */ case 0x01: //cpyl
                        lenmsb = proc.SystemMem[(ushort)(res + 1)];
                        length = (ushort)((lenmsb << 8) | (proc.SystemMem[(ushort)(res + 2)]));
                        srcbmsb = proc.SystemMem[(ushort)(res + 3)];
                        sourceBank = (ushort)((srcbmsb << 8) | (proc.SystemMem[(ushort)(res + 4)]));
                        srcmsb = proc.SystemMem[(ushort)(res + 5)];
                        source = (ushort)((srcmsb << 8) | (proc.SystemMem[(ushort)(res + 6)]));
                        dstbmsb = proc.SystemMem[(ushort)(res + 7)];
                        destBank = (ushort)((dstbmsb << 8) | (proc.SystemMem[(ushort)(res + 8)]));
                        dstmsb = proc.SystemMem[(ushort)(res + 9)];
                        dest = (ushort)((dstmsb << 8) | (proc.SystemMem[(ushort)(res + 10)]));

                        if (sourceBank > ExtraPages.Count || destBank > ExtraPages.Count)
                            break;

                        sourcePage = (sourceBank == 0) ? proc.SystemMem : ExtraPages[(ushort)(sourceBank - 1)];
                        destPage = (destBank == 0) ? proc.SystemMem : ExtraPages[(ushort)(destBank - 1)];
                        for (int i = 0; i < length; i++)
                        {
                            destPage[(ushort)(dest + i)] = sourcePage[(ushort)(source + i)];
                        }
                        break;
                    /*cpyr length* src_bank* src_addr* dest_bank* dest_addr* */ case 0x02:
                        lenmsb = proc.SystemMem[(ushort)(res + 1)];
                        length = (ushort)((lenmsb << 8) | (proc.SystemMem[(ushort)(res + 2)]));
                        srcbmsb = proc.SystemMem[(ushort)(res + 3)];
                        sourceBank = (ushort)((srcbmsb << 8) | (proc.SystemMem[(ushort)(res + 4)]));
                        srcmsb = proc.SystemMem[(ushort)(res + 5)];
                        source = (ushort)((srcmsb << 8) | (proc.SystemMem[(ushort)(res + 6)]));
                        dstbmsb = proc.SystemMem[(ushort)(res + 7)];
                        destBank = (ushort)((dstbmsb << 8) | (proc.SystemMem[(ushort)(res + 8)]));
                        dstmsb = proc.SystemMem[(ushort)(res + 9)];
                        dest = (ushort)((dstmsb << 8) | (proc.SystemMem[(ushort)(res + 10)]));

                        if (sourceBank > ExtraPages.Count || destBank > ExtraPages.Count)
                            break;

                        sourcePage = (sourceBank == 0) ? proc.SystemMem : ExtraPages[(ushort)(sourceBank - 1)];
                        destPage = (destBank == 0) ? proc.SystemMem : ExtraPages[(ushort)(destBank - 1)];
                        for (int i = 0; i < length; i++)
                        {
                            // yes there is a magic -1 in here. no I dont know why it does not work without it. suffer as I have suffered.
                            destPage[(ushort)(dest + length - i - 1)] = sourcePage[(ushort)(source + length - i - 1)];
                        }
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
            case 0x0f: //state
                Status = deviceMem[memTarget];
                break;
            default:
                break;
        }
    }
}