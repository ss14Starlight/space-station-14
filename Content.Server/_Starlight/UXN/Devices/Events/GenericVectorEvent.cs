namespace Content.Server._Starlight.UXN.Devices.Events;

public sealed partial class GenericVectorEvent(ushort vector) : UxnEvent
{
    public override void PerformEvent(UXNProcessor proc) => proc.PC = vector;
}