using Content.Shared._Starlight.Holograms;
using Robust.Client.UserInterface;

namespace Content.Client._Starlight.Holograms.UI;

public sealed class HologramConsoleBoundUserInterface : BoundUserInterface
{
    [ViewVariables]
    private HologramConsoleWindow? _window;

    public HologramConsoleBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<HologramConsoleWindow>();
        _window.OnProjectHologram += OnProjectHologram;
        _window.OnRecallHologram += OnRecallHologram;
        _window.OnEjectDisk += OnEjectDisk;
        _window.OnToggleCarry += OnToggleCarry;
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is not HologramConsoleBoundUserInterfaceState castState)
            return;

        _window?.UpdateState(castState);
    }

    private void OnProjectHologram(NetEntity diskUid, NetEntity projectorUid) =>
        SendMessage(new HologramConsoleProjectHologramMessage(diskUid, projectorUid));

    private void OnRecallHologram() =>
        SendMessage(new HologramConsoleRecallMessage());

    private void OnEjectDisk(NetEntity diskUid) =>
        SendMessage(new HologramConsoleEjectDiskMessage(diskUid));

    private void OnToggleCarry(bool allowCarry) =>
        SendMessage(new HologramConsoleToggleCarryMessage(allowCarry));
}
