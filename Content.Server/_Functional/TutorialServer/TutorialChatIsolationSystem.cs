using Content.Server.Chat.Managers;
using Content.Server.Chat.Systems;
using Content.Server.GameTicking;
using Content.Server.Radio;
using Content.Shared._Functional.TutorialServer;
using Content.Shared.CCVar;
using Content.Shared.Chat;
using Content.Shared.GameTicking.Components;
using Robust.Shared.Configuration;

namespace Content.Server._Functional.TutorialServer;

/// <summary>
/// Chat isolation while TutorialServer is active: no radio, and players cannot use OOC channels
/// (CVars also disable OOC/LOOC/dead; this hard-cancels in-game OOC attempts as a backstop).
/// </summary>
public sealed partial class TutorialChatIsolationSystem : EntitySystem
{
    [Dependency] private IChatManager _chat = default!;
    [Dependency] private IConfigurationManager _cfg = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RadioReceiveAttemptEvent>(OnRadioReceiveAttempt);
        SubscribeLocalEvent<RadioSendAttemptEvent>(OnRadioSendAttempt);
        // SubscribeLocalEvent<InGameOocMessageAttemptEvent>(OnInGameOocAttempt); // Starlight
        // After ChatSystem, which otherwise turns ooc.enabled back on at PostRound / lobby.
        SubscribeLocalEvent<GameRunLevelChangedEvent>(OnRunLevelChanged, after: [typeof(ChatSystem)]);
    }

    private bool TutorialActive()
    {
        var query = EntityQueryEnumerator<TutorialServerRuleComponent, ActiveGameRuleComponent, GameRuleComponent>();
        return query.MoveNext(out _, out _, out _, out _);
    }

    private void OnRadioReceiveAttempt(ref RadioReceiveAttemptEvent args)
    {
        if (TutorialActive())
            args.Cancelled = true;
    }

    private void OnRadioSendAttempt(ref RadioSendAttemptEvent args)
    {
        if (TutorialActive())
            args.Cancelled = true;
    }

    private void OnInGameOocAttempt(ref InGameOocMessageAttemptEvent args)
    {
        if (args.Cancelled || !TutorialActive())
            return;

        args.Cancelled = true;
        var loc = args.Type == InGameOOCChatType.Dead
            ? "tutorial-server-dead-chat-disabled"
            : "tutorial-server-looc-disabled";
        _chat.DispatchServerMessage(args.Session, Loc.GetString(loc), suppressLog: true);
    }

    private void OnRunLevelChanged(GameRunLevelChangedEvent ev)
    {
        if (!TutorialActive())
            return;

        _cfg.SetCVar(CCVars.OocEnabled, false);
        _cfg.SetCVar(CCVars.LoocEnabled, false);
        // _cfg.SetCVar(CCVars.DeadChatEnabled, false); // Starlight
        _cfg.SetCVar(CCVars.OocEnableDuringRound, true);
    }
}
