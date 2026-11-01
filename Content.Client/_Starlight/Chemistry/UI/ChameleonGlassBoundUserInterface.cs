using Content.Client.Clothing.UI;
using Content.Shared._Starlight.Chemistry;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client._Starlight.Chemistry.UI;

[UsedImplicitly]
public sealed class ChameleonGlassBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    private ChameleonMenu? _menu;

    protected override void Open()
    {
        base.Open();
        _menu = this.CreateWindow<ChameleonMenu>();
        _menu.UpdateReagentState(null);
        _menu.OnIdSelected += id => SendMessage(new ChameleonGlassSelectMessage(id));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        if (state is ChameleonGlassUiState glass)
            _menu?.UpdateReagentState(glass.SelectedReagent);
    }
}
