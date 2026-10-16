using Content.Client.Administration.Managers;
using Content.Client.Changelog;
using Content.Client.UserInterface.Systems.EscapeMenu;
using Content.Shared.CCVar;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Collections;
using Robust.Shared.Configuration;

namespace Content.Client.Info
{
    public sealed class LinkBanner : BoxContainer
    {
        private readonly IConfigurationManager _cfg;
        private readonly INullLinkPlayerRolesManager _playerRoles;// NullLink

        private ValueList<(CVarDef<string> cVar, Button button)> _infoLinks;

        public LinkBanner()
        {
            var buttons = new BoxContainer
            {
                Orientation = LayoutOrientation.Horizontal
            };
            AddChild(buttons);

            var uriOpener = IoCManager.Resolve<IUriOpener>();
            _cfg = IoCManager.Resolve<IConfigurationManager>();
            _playerRoles = IoCManager.Resolve<INullLinkPlayerRolesManager>(); // NullLink
            // Starlight-edit: rules and the guidebook are buttons on the lobby top bar, the changelog goes first.
            var changelogButton = new ChangelogButton();
            changelogButton.OnPressed += args => UserInterfaceManager.GetUIController<ChangelogUIController>().ToggleWindow();
            buttons.AddChild(changelogButton);

            AddInfoButton("server-info-discord-button", CCVars.InfoLinksDiscord);
            AddInfoButton("server-info-website-button", CCVars.InfoLinksWebsite);
            AddInfoButton("server-info-wiki-button", CCVars.InfoLinksWiki);
            AddInfoButton("server-info-forum-button", CCVars.InfoLinksForum);
            AddInfoButton("server-info-telegram-button", CCVars.InfoLinksTelegram);

            // NullLink start
            var button = new Button { Text = Loc.GetString("server-info-connect-discord-button") };
            button.OnPressed += _ => {
                var link = _playerRoles.GetDiscordLink();
                if(link != null)
                    uriOpener.OpenUri(link);
            };
            buttons.AddChild(button);

            var steamButton = new Button { Text = Loc.GetString("server-info-connect-steam-button") };
            steamButton.OnPressed += _ => {
                var link = _playerRoles.GetSteamLink();
                if (link != null)
                    uriOpener.OpenUri(link);
            };
            buttons.AddChild(steamButton);
            // NullLink end

            void AddInfoButton(string loc, CVarDef<string> cVar)
            {
                var button = new Button { Text = Loc.GetString(loc) };
                button.OnPressed += _ => uriOpener.OpenUri(_cfg.GetCVar(cVar));
                buttons.AddChild(button);
                _infoLinks.Add((cVar, button));
            }
        }

        protected override void EnteredTree()
        {
            // LinkBanner is constructed before the client even connects to the server due to UI refactor stuff.
            // We need to update these buttons when the UI is shown.

            base.EnteredTree();

            foreach (var (cVar, link) in _infoLinks)
            {
                link.Visible = _cfg.GetCVar(cVar) != "";
            }
        }
    }
}
