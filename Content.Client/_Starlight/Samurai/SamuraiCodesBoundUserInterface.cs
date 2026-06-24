using Content.Shared._Starlight.Samurai;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client._Starlight.Samurai;

[UsedImplicitly]
public sealed partial class SamuraiCodesBoundUserInterface : BoundUserInterface
{
    [Dependency] private IEntityManager _entMan = default!;

    [ViewVariables]
    private SamuraiCodesMenu? _menu;

    public SamuraiCodesBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _menu = this.CreateWindow<SamuraiCodesMenu>();
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is not SamuraiCodesBuiState msg)
            return;

        if (!_entMan.TryGetComponent<SamuraiCodesComponent>(Owner, out var comp))
            return;

        _menu?.Update(comp, msg);
    }
}
