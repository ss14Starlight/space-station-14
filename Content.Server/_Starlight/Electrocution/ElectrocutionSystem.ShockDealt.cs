using Content.Server._Starlight.Electrocution;

// ReSharper disable once CheckNamespace
namespace Content.Server.Electrocution;

public sealed partial class ElectrocutionSystem
{
    /// <summary>
    /// Called exactly once at each successful exit of <see cref="TryDoElectrifiedAct"/>, after the chain loop.
    /// The loop runs from the end of the target list down to index 0, which is the directly touched target,
    /// so <paramref name="shocked"/> (the loop's last result) tells whether that target was shocked.
    /// </summary>
    private void RaiseShockDealt(EntityUid source, EntityUid target, bool shocked)
    {
        if (!shocked)
            return;

        var ev = new ElectrifiedShockDealtEvent(target);
        RaiseLocalEvent(source, ref ev);
    }
}
