using Robust.Client.UserInterface;
using JetBrains.Annotations;
using Content.Shared._Starlight.Devil;

namespace Content.Client._Starlight.Devil.Ui;

[UsedImplicitly]
public sealed class DevilDamnationsBoundUserInterface : BoundUserInterface
{
    [ViewVariables]
    private DevilDamnationsMenu? _menu;
    private EntityUid _owner;

    public DevilDamnationsBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
        _owner = owner;
    }

    protected override void Open()
    {
        base.Open();

        _menu = this.CreateWindow<DevilDamnationsMenu>();
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is not DevilDamnationsBuiState msg) return;
        _menu?.Update(msg);
    }
}
