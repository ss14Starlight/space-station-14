using Content.Shared._Starlight.Chemistry;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client._Starlight.Chemistry.UI;

[UsedImplicitly]
public sealed class ChameleonGlassBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    private ChameleonGlassMenu? _menu;

    protected override void Open()
    {
        base.Open();
        _menu = this.CreateWindow<ChameleonGlassMenu>();
        _menu.OnSelected += id => SendMessage(new ChameleonGlassSelectMessage(id));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        if (state is ChameleonGlassUiState glass)
            _menu?.UpdateState(glass.SelectedReagent);
    }
}
