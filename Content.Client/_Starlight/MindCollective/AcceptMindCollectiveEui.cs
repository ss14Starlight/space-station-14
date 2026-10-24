using System.Numerics;
using Content.Client.Eui;
using Content.Client.UserInterface.Controls;
using Content.Shared._Starlight.MindCollective;
using Content.Shared.Eui;
using JetBrains.Annotations;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Utility;

namespace Content.Client._Starlight.MindCollective;

[UsedImplicitly]
public sealed class AcceptMindCollectiveEui : BaseEui
{
    private readonly FancyWindow _window;
    private readonly RichTextLabel _prompt;
    private bool _answered;

    public AcceptMindCollectiveEui()
    {
        _window = new FancyWindow { Title = Loc.GetString("mind-collective-consent-title"), MinSize = new(340, 200) };
        _window.SetSize = Vector2.Max(_window.MinSize, Vector2.Min(new(460, 280),
            IoCManager.Resolve<IUserInterfaceManager>().WindowRoot.Size - new Vector2(24)));
        var content = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 12,
            HorizontalExpand = true,
            VerticalExpand = true,
        };
        _prompt = new RichTextLabel { HorizontalExpand = true, VerticalExpand = true };
        content.AddChild(_prompt);
        var buttons = new BoxContainer { SeparationOverride = 8 };
        var yes = new Button { Text = Loc.GetString("mind-collective-confirm-yes"), HorizontalExpand = true, MinHeight = 32 };
        var no = new Button { Text = Loc.GetString("mind-collective-confirm-no"), HorizontalExpand = true, MinHeight = 32 };
        yes.OnPressed += _ =>
        {
            Answer(true);
            _window.Close();
        };
        no.OnPressed += _ => _window.Close();
        _window.OnClose += () => Answer(false);
        buttons.AddChild(yes);
        buttons.AddChild(no);
        content.AddChild(buttons);
        _window.ContentsContainer.AddChild(content);
    }

    public override void HandleState(EuiStateBase state)
    {
        base.HandleState(state);
        if (state is not MindCollectiveConsentState consent)
            return;
        _window.Title = consent.Title;
        _prompt.SetMessage(FormattedMessage.FromUnformatted($"{consent.Name}\n\n{consent.Description}"));
    }

    private void Answer(bool accepted)
    {
        if (_answered)
            return;
        _answered = true;
        SendMessage(new MindCollectiveConsentMessage(accepted));
    }

    public override void Opened()
    {
        IoCManager.Resolve<IClyde>().RequestWindowAttention();
        _window.OpenCentered();
    }

    public override void Closed()
    {
        _answered = true;
        _window.Close();
        _window.Dispose();
    }
}
