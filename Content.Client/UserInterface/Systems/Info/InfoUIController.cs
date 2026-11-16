using System;
using Content.Client.Administration.Managers;
using Content.Client.Gameplay;
using Content.Client.Info;
using Content.Shared.Administration;
using Content.Shared.Guidebook;
using Content.Shared.Info;
using Robust.Client.Console;
using Robust.Client.UserInterface.Controllers;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client.UserInterface.Systems.Info;

public sealed partial class InfoUIController : UIController, IOnStateExited<GameplayState>
{
    [Dependency] private IClientConsoleHost _consoleHost = default!;
    [Dependency] private INetManager _netManager = default!;
    [Dependency] private IPrototypeManager _prototype = default!;
    [Dependency] private IClientAdminManager _adminManager = default!;

    private RulesPopup? _rulesPopup;
    private RulesAndInfoWindow? _infoWindow;

    private bool _newcomerOwnsRules;
    private bool _rulesPending;
    private float _rulesPendingTime;

    private static readonly ProtoId<GuideEntryPrototype> DefaultRuleset = "DefaultRuleset";

    public ProtoId<GuideEntryPrototype> RulesEntryId = DefaultRuleset;

    public bool RulesInfoReceived { get; private set; }

    public bool RulesRequired { get; private set; }

    public float RulesWaitTime { get; private set; }

    protected override string SawmillName => "rules";

    public override void Initialize()
    {
        base.Initialize();

        _netManager.RegisterNetMessage<RulesAcceptedMessage>();
        _netManager.RegisterNetMessage<SendRulesInformationMessage>(OnRulesInformationMessage);

        _consoleHost.RegisterCommand("fuckrules",
            "",
            "",
            (shell, _, _) =>
        {
            if (!_adminManager.HasFlag(AdminFlags.Admin))
            {
                shell.WriteError("Insufficient perms for command: fuckrules");
                return;
            }

            OnAcceptPressed(true);
        });
    }

    private void OnRulesInformationMessage(SendRulesInformationMessage message)
    {
        RulesEntryId = message.CoreRules;
        RulesInfoReceived = true;
        RulesRequired = message.ShouldShowRules;
        RulesWaitTime = message.PopupTime;

        if (!message.ShouldShowRules)
            return;

        _rulesPending = true;
        _rulesPendingTime = message.PopupTime;

        Timer.Spawn(TimeSpan.FromSeconds(1), () =>
        {
            if (!_rulesPending)
                return;

            _rulesPending = false;
            ShowRules(_rulesPendingTime);
        });
    }

    public void ClaimRulesForNewcomer()
    {
        _newcomerOwnsRules = true;
        _rulesPending = false;

        if (_rulesPopup == null)
            return;

        _rulesPopup.OnQuitPressed -= OnQuitPressed;
        _rulesPopup.OnAcceptPressed -= OnAcceptPressed;
        _rulesPopup.Orphan();
        _rulesPopup = null;
    }

    public void OnStateExited(GameplayState state)
    {
        _newcomerOwnsRules = false;
        _rulesPending = false;

        if (_infoWindow == null)
            return;

        _infoWindow.Dispose();
        _infoWindow = null;
    }

    private void ShowRules(float time)
    {
        if (_rulesPopup != null || _newcomerOwnsRules)
            return;

        _rulesPopup = new RulesPopup
        {
            Timer = time
        };

        _rulesPopup.OnQuitPressed += OnQuitPressed;
        _rulesPopup.OnAcceptPressed += OnAcceptPressed;
        UIManager.WindowRoot.AddChild(_rulesPopup);
        LayoutContainer.SetAnchorPreset(_rulesPopup, LayoutContainer.LayoutPreset.Wide);
    }

    private void OnQuitPressed()
    {
        _consoleHost.ExecuteCommand("quit");
    }

    private void OnAcceptPressed(bool fuckRules)
    {
        var message = new RulesAcceptedMessage() { FuckRules = fuckRules };
        _netManager.ClientSendMessage(message);

        _rulesPending = false;
        _rulesPopup?.Orphan();
        _rulesPopup = null;
    }

    public void AcceptRules()
    {
        OnAcceptPressed(false);
    }

    public GuideEntryPrototype GetCoreRuleEntry()
    {
        if (!_prototype.TryIndex(RulesEntryId, out var guideEntryPrototype))
        {
            guideEntryPrototype = _prototype.Index(DefaultRuleset);
            Log.Error($"Couldn't find the following prototype: {RulesEntryId}. Falling back to {DefaultRuleset}, please check that the server has the rules set up correctly");
            return guideEntryPrototype;
        }

        return guideEntryPrototype;
    }

    public void OpenWindow()
    {
        if (_infoWindow == null || _infoWindow.Disposed)
            _infoWindow = UIManager.CreateWindow<RulesAndInfoWindow>();

        _infoWindow?.OpenCentered();
    }
}
