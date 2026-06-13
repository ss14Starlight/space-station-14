using Content.Client._Starlight.MHelp;
using Content.Client.Administration.Managers;
using Content.Shared.Starlight.MHelp;
using Content.Shared.Starlight.CCVar;
using Content.Shared.Administration;
using JetBrains.Annotations;
using Robust.Client.Audio;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controllers;
using Robust.Shared.Configuration;
using Robust.Shared.Input.Binding;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Utility;
using Content.Shared._NullLink;
namespace Content.Client.UserInterface.Systems.Bwoink;

[UsedImplicitly]
public sealed class MHelpUIController : UIController, IOnSystemChanged<MentorSystem>
{
    [Dependency] private readonly INullLinkPlayerRolesManager _playerRoles = default!;
    [Dependency] private readonly ISharedNullLinkPlayerRolesReqManager _playerRolesReq = default!;
    [Dependency] private readonly IClientAdminManager _adminManager = default!;
    [Dependency] private readonly IConfigurationManager _config = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IClyde _clyde = default!;
    [Dependency] private readonly StaffHelpUIController _staffhelp = default!;
    [Dependency] private readonly AHelpUIController _aHelp = default!;
    [UISystemDependency] private readonly AudioSystem _audio = default!;

    private MentorSystem? _mentorSystem;
    public IMHelpUIHandler? UIHelper;
    public bool _hasUnreadMHelp;
    private string? _mHelpSound;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeNetworkEvent<MHelpTypingUpdated>(OnTypingUpdated);

        _playerRoles.PlayerRolesChanged += OnPlayerStatusUpdated;
        _config.OnValueChanged(StarlightCCVars.MHelpSound, v => _mHelpSound = v, true);
    }

    private void OnPlayerStatusUpdated()
    {
        if (UIHelper is not { IsOpen: true })
            return;
        EnsureUIHelper();
    }

    public void OnSystemLoaded(MentorSystem system)
    {
        _mentorSystem = system;
        _mentorSystem.OnMentoringTextMessageReceived += ReceivedMentoring;
    }

    public void OnSystemUnloaded(MentorSystem system)
    {
        CommandBinds.Unregister<MHelpUIController>();

        DebugTools.Assert(_mentorSystem != null);
        _mentorSystem!.OnMentoringTextMessageReceived -= ReceivedMentoring;
        _mentorSystem = null;
    }

    private void ReceivedMentoring(object? sender, SharedMentorSystem.MHelpTextMessage message)
    {
        var localPlayer = _playerManager.LocalSession;
        if (localPlayer == null)
            return;
        if (message.PlaySound && localPlayer.UserId != message.Sender && _config.GetCVar(StarlightCCVars.MHelpPing))
        {
            if (_mHelpSound != null)
                _audio.PlayGlobal(_mHelpSound, Filter.Local(), false);
            _clyde.RequestWindowAttention();
        }

        EnsureUIHelper();

        if (!UIHelper!.IsOpen)
        {
            UnreadMHelpReceived();
        }

        UIHelper!.Receive(message);
    }

    private void OnTypingUpdated(MHelpTypingUpdated args, EntitySessionEventArgs session)
        => UIHelper?.PeopleTypingUpdated(args);

    public void EnsureUIHelper()
    {

        var isMentor = _playerManager.LocalSession is { } local && _playerRolesReq.IsMentor(local);
        var isAdmin = _adminManager.HasFlag(AdminFlags.Adminhelp);

        if (UIHelper != null && UIHelper.IsMentor == (isMentor || isAdmin))
            return;

        UIHelper?.Dispose();
        var ownerUserId = _playerManager.LocalUser!.Value;
        UIHelper = isMentor || isAdmin ? new MentorMHelpUIHandler(ownerUserId) : new UserMHelpUIHandler(ownerUserId);

        UIHelper.OnMessageSend += (ticket, textMessage, playSound) => _mentorSystem?.Send(ticket,  textMessage, playSound);
        UIHelper.OnInputTextChanged += (ticket, text) => _mentorSystem?.SendInputTextUpdated(ticket,  text.Length > 0);
        UIHelper.OnTicketClosed += ticket => _mentorSystem?.SendCloseTicket(ticket);
        UIHelper.OnTptoPressed += ticket => _mentorSystem?.SentTpto(ticket);
    }

    public void Open()
    {
        var localUser = _playerManager.LocalUser;
        if (localUser == null)
            return;
        EnsureUIHelper();
        if (UIHelper!.IsOpen)
            return;
        UIHelper!.Open(localUser.Value);
    }

    public void Open(NetUserId userId)
    {
        EnsureUIHelper();
        if (!UIHelper!.IsMentor)
            return;
        UIHelper?.Open(userId);
    }

    public void ToggleWindow()
    {
        EnsureUIHelper();
        UIHelper?.ToggleWindow();
    }

    private void UnreadMHelpReceived()
    {
        _hasUnreadMHelp = true;
        _staffhelp.RefreshAhelpButton();
    }
}
