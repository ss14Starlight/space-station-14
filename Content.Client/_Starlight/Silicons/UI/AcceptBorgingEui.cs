using Content.Client.Eui;
using Content.Shared._Starlight.Silicons.Borgs;
using JetBrains.Annotations;
using Robust.Client.Graphics;

namespace Content.Client._Starlight.Silicons.UI;

[UsedImplicitly]
public sealed class AcceptBorgingEui : BaseEui
{
    private readonly AcceptBorgingWindow _window;

    public AcceptBorgingEui()
    {
        _window = new AcceptBorgingWindow();

        _window.OnDenyButtonPressed += () =>
        {
            SendMessage(new AcceptBorgingChoiceMessage(AcceptBorgingUiButton.Deny));
            _window.Close();
        };

        _window.OnClose += () => SendMessage(new AcceptBorgingChoiceMessage(AcceptBorgingUiButton.Deny));

        _window.OnAcceptButtonPressed += () =>
        {
            SendMessage(new AcceptBorgingChoiceMessage(AcceptBorgingUiButton.Accept));
            _window.Close();
        };
    }

    public override void Opened()
    {
        IoCManager.Resolve<IClyde>().RequestWindowAttention();
        _window.OpenCentered();
    }

    public override void Closed()
    {
        _window.Close();
    }
}
