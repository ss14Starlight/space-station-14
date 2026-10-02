using System.Numerics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;

namespace Content.Client._NullLink;

public sealed class DiscordLinkWindow : DefaultWindow
{
    private readonly IUriOpener _uriOpener;
    private string _url = "";

    public DiscordLinkWindow()
    {
        _uriOpener = IoCManager.Resolve<IUriOpener>();

        Title = Loc.GetString("nulllink-discord-link-title");
        MinSize = new Vector2(440, 0);

        var box = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            Margin = new Thickness(12),
        };

        var text = new RichTextLabel { HorizontalExpand = true };
        text.SetMessage(Loc.GetString("nulllink-discord-link-text"));
        box.AddChild(text);

        var linkButton = new Button
        {
            Text = Loc.GetString("nulllink-discord-link-button"),
            HorizontalAlignment = Control.HAlignment.Center,
            Margin = new Thickness(0, 12, 0, 0),
        };
        linkButton.OnPressed += _ =>
        {
            if (!string.IsNullOrEmpty(_url))
                _uriOpener.OpenUri(_url);
            Close();
        };
        box.AddChild(linkButton);

        Contents.AddChild(box);
    }

    public void SetUrl(string url) => _url = url;
}
