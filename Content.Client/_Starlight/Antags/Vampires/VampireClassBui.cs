using Content.Client.UserInterface.Controls;
using Content.Shared._Starlight.Antags.Vampires;
using Content.Shared._Starlight.Antags.Vampires.Prototypes;
using JetBrains.Annotations;
using Robust.Client.UserInterface;
using Robust.Shared.IoC;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;
using System.Linq;

namespace Content.Client._Starlight.Antags.Vampires;

[UsedImplicitly]
public sealed class VampireClassBui : BoundUserInterface
{
    private SimpleRadialMenu? _menu;
    private bool _choiceMade;
    private readonly IPrototypeManager _proto;

    public VampireClassBui(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
        _proto = IoCManager.Resolve<IPrototypeManager>();
    }

    protected override void Open()
    {
        base.Open();
        _menu = this.CreateWindow<SimpleRadialMenu>();
        _menu.Track(Owner);
        _choiceMade = false;

        _menu.OnClose += OnMenuClosed;

        var buttonModels = CreateClassButtons();
        _menu.SetButtons(buttonModels);

        _menu.OpenOverMouseScreenPosition();
    }

    private void OnMenuClosed()
    {
        if (_choiceMade 
            || !EntMan.EntityExists(Owner) || !EntMan.TryGetComponent<MetaDataComponent>(Owner, out _))
            return;

        SendMessage(new VampireClassClosedBuiMsg());
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            if (_menu != null)
                _menu.OnClose -= OnMenuClosed;
            _menu = null;
        }

        base.Dispose(disposing);
    }

    private IEnumerable<RadialMenuActionOption<string>> CreateClassButtons()
    {
        var protos = _proto.EnumeratePrototypes<VampireClassPrototype>();

        var buttons = new List<RadialMenuActionOption<string>>();
        foreach (var proto in protos)
        {
            buttons.Add(new RadialMenuActionOption<string>(HandleClassChoice, proto.ID)
            {
                IconSpecifier = RadialMenuIconSpecifier.With(proto.Icon),
                ToolTip = Loc.GetString(proto.Tooltip)
            });
        }

        return buttons;
    }

    private void HandleClassChoice(string classId)
    {
        _choiceMade = true;
        SendPredictedMessage(new VampireClassChosenBuiMsg { Choice = classId });
        Close();
    }
}
