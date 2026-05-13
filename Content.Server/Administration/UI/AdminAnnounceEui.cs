using Content.Server._Starlight.Administration.Systems;
using Content.Server.Administration.Managers;
using Content.Server.Chat;
using Content.Server.Chat.Managers;
using Content.Server.Chat.Systems;
using Content.Server.EUI;
using Content.Shared.Administration;
using Content.Shared.Eui;

namespace Content.Server.Administration.UI
{
    public sealed class AdminAnnounceEui : BaseEui
    {
        [Dependency] private readonly IAdminManager _adminManager = default!;
        [Dependency] private readonly IChatManager _chatManager = default!;
        private readonly ChatSystem _chatSystem;
        private readonly AutoDiscordLogSystem _autoLog; //Starlight

        public AdminAnnounceEui()
        {
            IoCManager.InjectDependencies(this);
            var entSysMan = IoCManager.Resolve<IEntitySystemManager>(); //Starlight
            _chatSystem = entSysMan.GetEntitySystem<ChatSystem>(); //Starlight
            _autoLog = entSysMan.GetEntitySystem<AutoDiscordLogSystem>(); //Starlight
        }

        public override void Opened()
        {
            StateDirty();
        }

        public override EuiStateBase GetNewState()
        {
            return new AdminAnnounceEuiState();
        }

        public override void HandleMessage(EuiMessageBase msg)
        {
            base.HandleMessage(msg);

            switch (msg)
            {
                case AdminAnnounceEuiMsg.DoAnnounce doAnnounce:
                    if (!_adminManager.HasAdminFlag(Player, AdminFlags.Admin))
                    {
                        Close();
                        break;
                    }

                    switch (doAnnounce.AnnounceType)
                    {
                        case AdminAnnounceType.Server:
                            _chatManager.DispatchServerAnnouncement(doAnnounce.Announcement);
                            break;
                        // TODO: Per-station announcement support
                        case AdminAnnounceType.Station:
                            _chatSystem.DispatchGlobalAnnouncement(doAnnounce.Announcement, doAnnounce.Announcer, colorOverride: Color.Gold);
                            break;
                    }
                    var admin = Player?.Name ?? "Unknown"; //Starlight
                    _autoLog.LogToDiscord(Loc.GetString("autolog-announce", ("sender", doAnnounce.Announcer), ("message", doAnnounce.Announcement), ("admin", admin)), admin); //Starlight
                    StateDirty();

                    if (doAnnounce.CloseAfter)
                        Close();

                    break;
            }
        }
    }
}
