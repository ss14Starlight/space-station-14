using System;
using System.Collections.Generic;
using System.Text;
using Robust.Shared.Timing;

namespace Content.Server._Starlight.UXN.Devices.ComponentDevices;

/// <summary>
/// A UXNDevice for a delaying execution for a few seconds. memory layout is as follows.
/// 0x00,0x01 - Vector*
/// 0x02,0x03 - Time* the time (in-seconds) to delay execution from when this was executed.
/// 0x04-0x0F - unused
/// </summary>
public sealed partial class AttachedDevice : ComponentUxnDevice<UxnAttachedComponent>
{
    private IGameTiming _gameTiming = default!;

    protected override void SetupCore(EntityUid euid, UxnAttachedComponent comp) => _gameTiming = IoCManager.Resolve<IGameTiming>();

    public override void WriteValue(byte memTarget, Byte256 deviceMem, UXNProcessor proc)
    {
        if ((memTarget & 0x0F) == 0x02)
        {
            var badr = memTarget & 0xF0;
            var future = _gameTiming.CurTime + TimeSpan.FromSeconds(deviceMem.GetShort((byte)(badr + 0x02)));
            Entity.Comp.DelayExecution = future;
            proc.PushEvent(new ContinueFromPauseEvent(deviceMem.GetShort((byte)(badr)), Entity.Comp));
        }
    }
}

public sealed partial class ContinueFromPauseEvent : UxnEvent
{
    public ushort Vector = 0;
    public UxnAttachedComponent Comp = default!;
    private readonly IGameTiming _gameTiming = IoCManager.Resolve<IGameTiming>();
    public ContinueFromPauseEvent(ushort vector, UxnAttachedComponent comp)
    {
        Vector = vector;
        Comp = comp;
    }
    public override bool PreRun(UXNProcessor proc) => Comp.DelayExecution > _gameTiming.CurTime;
    public override void PerformEvent(UXNProcessor proc) => proc.PC = Vector;
}