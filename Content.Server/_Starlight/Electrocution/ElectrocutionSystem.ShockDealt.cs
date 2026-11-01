using Content.Server._Starlight.Electrocution;

// ReSharper disable once CheckNamespace
namespace Content.Server.Electrocution;

public sealed partial class ElectrocutionSystem
{
    private void RaiseShockDealt(EntityUid source, EntityUid target, bool shocked)
    {
        if (!shocked)
            return;

        var ev = new ElectrifiedShockDealtEvent(target);
        RaiseLocalEvent(source, ref ev);
    }
}
