using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Content.Server._Daiquiri.AIHelp;
using Content.Server.Chat.Systems;
using Content.Server.NPC;
using Content.Server.NPC.HTN;
using Content.Server.NPC.Systems;
using Content.Shared._Daiquiri.CCVar;
using Content.Shared.Chat;
using Content.Shared.GameTicking;
using Content.Shared.Humanoid;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Power.Components;
using Content.Shared.Power.EntitySystems;
using Content.Shared.PowerCell;
using Content.Shared.Speech;
using Content.Shared.Storage.Components;
using Content.Shared.Storage.EntitySystems;
using Robust.Shared.Configuration;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Localization;
using Robust.Shared.Log;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server._Daiquiri.Robot;

public sealed partial class RobotCompanionSystem : EntitySystem
{
    [Dependency] private AIHelpSystem _aiHelp = default!;
    [Dependency] private ChatSystem _chat = default!;
    [Dependency] private IConfigurationManager _config = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private NPCSystem _npc = default!;
    [Dependency] private HTNSystem _htn = default!;
    [Dependency] private SharedHumanoidAppearanceSystem _humanoid = default!;
    [Dependency] private PowerCellSystem _powerCell = default!;
    [Dependency] private SharedBatterySystem _battery = default!;
    [Dependency] private SharedEntityStorageSystem _storage = default!;
    [Dependency] private SharedPowerReceiverSystem _powerReceiver = default!;
    [Dependency] private SharedTransformSystem _xforms = default!;

    private ISawmill _sawmill = default!;

    private const string FollowMarker = "[FOLLOW]";

    private const string StopMarker = "[STOP]";

    private const string StayMarker = "[STAY]";

    private const string FreeMarker = "[FREE]";

    private const string ChargeMarker = "[CHARGE]";

    private static readonly Regex MoveMarker =
        new(@"\[MOVE\s+(-?\d+(?:[.,]\d+)?)\s+(-?\d+(?:[.,]\d+)?)\]", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex TurnMarker =
        new(@"\[TURN\s+(-?\d+(?:[.,]\d+)?)\]", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex AnyMarker = new(
        @"\[(?:FOLLOW|STOP|STAY|FREE|CHARGE|MOVE\s+-?\d+(?:[.,]\d+)?\s+-?\d+(?:[.,]\d+)?|TURN\s+-?\d+(?:[.,]\d+)?)\]",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private const string StandStillKey = "StandStill";

    private const string StandStillTimeKey = "StandStillTime";

    private const string ChargeTargetKey = "ChargeTarget";

    private const string ChargeRangeKey = "ChargeRange";

    private const string StepTargetKey = "StepTarget";

    private const string StepRangeKey = "StepRange";

    private const float StandStillSeconds = 86400f;

    private const float ChargeEnterSlack = 0.5f;

    private static readonly TimeSpan StepTimeout = TimeSpan.FromSeconds(10);

    private readonly Dictionary<EntityUid, RobotState> _states = new();

    public override void Initialize()
    {
        base.Initialize();

        _sawmill = IoCManager.Resolve<ILogManager>().GetSawmill("ROBOT");

        SubscribeLocalEvent<RobotCompanionComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<RobotCompanionComponent, ListenEvent>(OnListen);
        SubscribeLocalEvent<RobotCompanionComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(_ => _states.Clear());
    }

    private void OnMapInit(Entity<RobotCompanionComponent> ent, ref MapInitEvent args)
    {
        if (!ent.Comp.AlwaysFemale)
            return;

        Robust.Shared.Timing.Timer.Spawn(TimeSpan.Zero, () =>
        {
            if (!TerminatingOrDeleted(ent.Owner))
                _humanoid.SetSex(ent.Owner, Sex.Female);
        });
    }

    public override void Update(float frameTime)
    {
        var query = EntityQueryEnumerator<RobotCompanionComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (!_states.TryGetValue(uid, out var state))
                continue;

            if (state.ChargeTarget is { } charger)
            {
                UpdateCharging(uid, comp, state, charger, frameTime);
                continue;
            }

            UpdateStep(uid, state);

            if (state.FollowTarget is not { } target)
                continue;

            if (TerminatingOrDeleted(target))
            {
                StopFollow(uid, state);
                continue;
            }

            var followerXform = Transform(uid);
            var targetXform = Transform(target);
            if (targetXform.MapID != followerXform.MapID ||
                (_xforms.GetWorldPosition(targetXform) - _xforms.GetWorldPosition(followerXform)).Length() > comp.MaxFollowDistance)
            {
                StopFollow(uid, state);
            }
        }
    }

    private void OnShutdown(Entity<RobotCompanionComponent> ent, ref ComponentShutdown args)
        => _states.Remove(ent.Owner);

    private RobotState GetState(EntityUid robot)
    {
        if (!_states.TryGetValue(robot, out var state))
            _states[robot] = state = new RobotState();

        return state;
    }

    private void OnListen(Entity<RobotCompanionComponent> ent, ref ListenEvent args)
    {
        var robot = ent.Owner;

        if (args.Source == robot || TerminatingOrDeleted(args.Source))
            return;

        if (HasComp<RobotCompanionComponent>(args.Source))
            return;

        if (TryComp<MobStateComponent>(robot, out var mobState) && _mobState.IsDead(robot, mobState))
            return;

        var heard = args.Message.Trim();

        if (!heard.Any(char.IsLetterOrDigit))
            return;

        var robotXform = Transform(robot);
        var sourceXform = Transform(args.Source);
        if (sourceXform.MapID != robotXform.MapID)
            return;
        if ((_xforms.GetWorldPosition(sourceXform) - _xforms.GetWorldPosition(robotXform)).Length() > ent.Comp.HearingRange)
            return;

        var state = GetState(robot);

        if (!ent.Comp.RespondToSpeech || state.Busy)
            return;

        if (_timing.CurTime < state.NextReply)
            return;

        state.NextReply = _timing.CurTime +
                          TimeSpan.FromSeconds(Math.Max(0f, _config.GetCVar(DaiquiriCCVars.AIHelpRobotReplyCooldown)));
        state.Busy = true;

        _ = RespondAsync(robot, args.Source, Name(args.Source), heard, ent.Comp, state);
    }

    private void StartFollow(EntityUid robot, EntityUid target, RobotState state)
    {
        if (!TryComp<HTNComponent>(robot, out var htn))
            return;

        state.FollowTarget = target;
        ReleaseStandStill(htn);
        htn.Blackboard.Remove<EntityCoordinates>(ChargeTargetKey);
        _npc.SetBlackboard(robot, NPCBlackboard.FollowTarget, new EntityCoordinates(target, Vector2.Zero), htn);
        _npc.WakeNPC(robot, htn);
        Replan(htn);
        _sawmill.Info($"[ROBOT] {ToPrettyString(robot)}: идёт за {Name(target)}.");
    }

    private void StopFollow(EntityUid robot, RobotState state)
    {
        state.FollowTarget = null;

        if (!TryComp<HTNComponent>(robot, out var htn))
            return;

        htn.Blackboard.Remove<EntityCoordinates>(NPCBlackboard.FollowTarget);
        Replan(htn);
    }

    private void StartStandStill(EntityUid robot, RobotState state)
    {
        state.FollowTarget = null;

        if (!TryComp<HTNComponent>(robot, out var htn))
            return;

        htn.Blackboard.Remove<EntityCoordinates>(NPCBlackboard.FollowTarget);
        htn.Blackboard.Remove<EntityCoordinates>(ChargeTargetKey);
        htn.Blackboard.Remove<EntityCoordinates>(StepTargetKey);
        _npc.SetBlackboard(robot, StandStillKey, true, htn);
        _npc.SetBlackboard(robot, StandStillTimeKey, StandStillSeconds, htn);
        _npc.WakeNPC(robot, htn);
        Replan(htn);
        _sawmill.Info($"[ROBOT] {ToPrettyString(robot)}: стоит на месте.");
    }

    private void ReleaseOrders(EntityUid robot, RobotState state)
    {
        state.FollowTarget = null;

        if (!TryComp<HTNComponent>(robot, out var htn))
            return;

        htn.Blackboard.Remove<EntityCoordinates>(NPCBlackboard.FollowTarget);
        ReleaseStandStill(htn);
        Replan(htn);
    }

    private void ReleaseStandStill(HTNComponent htn)
    {
        if (!htn.Blackboard.ContainsKey(StandStillKey) && !htn.Blackboard.ContainsKey(StandStillTimeKey))
            return;

        htn.Blackboard.Remove<bool>(StandStillKey);
        htn.Blackboard.Remove<float>(StandStillTimeKey);
    }

    private void Replan(HTNComponent htn)
    {
        if (htn.Plan != null)
            _htn.ShutdownPlan(htn);

        _htn.Replan(htn);
    }

    private void StepBy(EntityUid robot, RobotState state, Vector2 offset, RobotCompanionComponent comp)
    {
        if (!TryComp<HTNComponent>(robot, out var htn))
            return;

        var xform = Transform(robot);
        if (xform.MapUid is not { } mapUid)
            return;

        var forward = _xforms.GetWorldRotation(xform).ToVec();
        var right = new Vector2(forward.Y, -forward.X);
        var distance = Math.Clamp(offset.Length(), 0f, comp.MaxStepTiles);
        if (distance <= 0.01f)
            return;

        var direction = Vector2.Normalize(offset);
        var target = _xforms.GetWorldPosition(xform) + (forward * direction.X + right * direction.Y) * distance;

        state.StepTarget = new EntityCoordinates(mapUid, target);
        state.StepDeadline = _timing.CurTime + StepTimeout;

        state.RestoreStandStill = htn.Blackboard.ContainsKey(StandStillKey);

        ReleaseStandStill(htn);
        _npc.SetBlackboard(robot, StepTargetKey, state.StepTarget.Value, htn);
        _npc.SetBlackboard(robot, StepRangeKey, 0.4f, htn);
        _npc.WakeNPC(robot, htn);
        Replan(htn);
    }

    private void TurnBy(EntityUid robot, float degrees)
    {
        var xform = Transform(robot);
        var angle = Math.Clamp(degrees, -360f, 360f);
        _xforms.SetWorldRotation(robot, _xforms.GetWorldRotation(xform) + Angle.FromDegrees(angle));
    }

    private void UpdateStep(EntityUid robot, RobotState state)
    {
        if (state.StepTarget is not { } step)
            return;

        if (state.ChargeTarget != null)
            return;

        var arrived = false;
        if (TerminatingOrDeleted(robot) || _timing.CurTime > state.StepDeadline)
        {
            arrived = true;
        }
        else
        {
            var position = _xforms.GetWorldPosition(Transform(robot));
            var target = _xforms.ToWorldPosition(step);
            arrived = (target - position).Length() <= 0.6f;
        }

        if (!arrived)
            return;

        state.StepTarget = null;

        if (!state.RestoreStandStill)
            return;

        state.RestoreStandStill = false;
        StartStandStill(robot, state);
    }

    private void StartCharging(EntityUid robot, RobotCompanionComponent comp, RobotState state)
    {
        if (state.Charging || state.ChargeTarget != null)
            return;

        if (FindCharger(robot, comp.ChargeSearchRange) is not { } charger)
        {
            Say(robot, Loc.GetString("robot-companion-no-charger"));
            return;
        }

        state.ChargeTarget = charger;
        state.FollowTarget = null;
        state.StepTarget = null;

        if (!TryComp<HTNComponent>(robot, out var htn))
            return;

        ReleaseStandStill(htn);
        htn.Blackboard.Remove<EntityCoordinates>(NPCBlackboard.FollowTarget);

        _npc.SetBlackboard(robot, ChargeTargetKey, new EntityCoordinates(charger, Vector2.Zero), htn);
        _npc.SetBlackboard(robot, ChargeRangeKey, comp.ChargeEnterDistance + ChargeEnterSlack, htn);
        _npc.WakeNPC(robot, htn);
        Replan(htn);
        _sawmill.Info($"[ROBOT] {ToPrettyString(robot)}: идёт заряжаться к {ToPrettyString(charger)}.");
    }

    private void UpdateCharging(EntityUid robot, RobotCompanionComponent comp, RobotState state, EntityUid charger, float frameTime)
    {
        if (TerminatingOrDeleted(charger) || !TryComp<EntityStorageComponent>(charger, out var storage))
        {
            AbortCharging(robot, state);
            return;
        }

        var robotXform = Transform(robot);
        var chargerXform = Transform(charger);

        if (!state.Charging)
        {
            if (robotXform.MapID != chargerXform.MapID)
            {
                AbortCharging(robot, state);
                return;
            }

            var distance = (_xforms.GetWorldPosition(chargerXform) - _xforms.GetWorldPosition(robotXform)).Length();
            if (distance > comp.ChargeEnterDistance + ChargeEnterSlack)
                return;

            EnterCharger(robot, charger, storage, state);
            return;
        }

        ChargeTick(robot, charger, frameTime);

        if (GetBatteryLevel(robot) < 1f)
            return;

        _storage.OpenStorage(charger, storage);

        state.Charging = false;
        state.ChargeTarget = null;
        StartStandStill(robot, state);
        Say(robot, Loc.GetString("robot-companion-charged"));
        _sawmill.Info($"[ROBOT] {ToPrettyString(robot)}: зарядился.");
    }

    private void EnterCharger(EntityUid robot, EntityUid charger, EntityStorageComponent storage, RobotState state)
    {
        _storage.OpenStorage(charger, storage);
        _storage.CloseStorage(charger, storage);

        if (!_storage.Insert(robot, charger, storage))
        {
            _sawmill.Warning($"[ROBOT] {ToPrettyString(robot)}: не смог залезть в {ToPrettyString(charger)}.");
            AbortCharging(robot, state);
            return;
        }

        state.Charging = true;

        if (!TryComp<HTNComponent>(robot, out var htn))
            return;

        htn.Blackboard.Remove<EntityCoordinates>(ChargeTargetKey);
        _npc.SetBlackboard(robot, StandStillKey, true, htn);
        _npc.SetBlackboard(robot, StandStillTimeKey, StandStillSeconds, htn);
        Replan(htn);
    }

    private void AbortCharging(EntityUid robot, RobotState state)
    {
        if (state.ChargeTarget is { } charger &&
            TryComp<EntityStorageComponent>(charger, out var storage) &&
            TryComp<InsideEntityStorageComponent>(robot, out var inside) &&
            inside.Storage == charger)
        {
            _storage.Remove(robot, charger, storage);
        }

        state.Charging = false;
        state.ChargeTarget = null;

        if (!TryComp<HTNComponent>(robot, out var htn))
            return;

        htn.Blackboard.Remove<EntityCoordinates>(ChargeTargetKey);
    }

    private void ChargeTick(EntityUid robot, EntityUid charger, float frameTime)
    {
        if (!TryComp<ChargerComponent>(charger, out var chargerComp))
            return;

        var chargerXform = Transform(charger);
        if (!chargerComp.Portable && !chargerXform.Anchored)
            return;

        if (!chargerComp.Portable && !_powerReceiver.IsPowered(charger))
            return;

        if (!_powerCell.TryGetBatteryFromEntityOrSlot(robot, out var battery))
            return;

        var charge = _battery.GetCharge(battery.Value.AsNullable()) + chargerComp.ChargeRate * frameTime;
        _battery.SetCharge(battery.Value.AsNullable(), charge);
    }

    private EntityUid? FindCharger(EntityUid robot, float range)
    {
        var robotXform = Transform(robot);
        var origin = _xforms.GetWorldPosition(robotXform);

        EntityUid? best = null;
        var bestDistance = float.MaxValue;

        var query = EntityQueryEnumerator<ChargerComponent, EntityStorageComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out _, out var xform))
        {
            if (xform.MapID != robotXform.MapID)
                continue;

            if (MetaData(uid).EntityPrototype?.ID is not { } proto ||
                !proto.Contains("BorgCharger", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var distance = (_xforms.GetWorldPosition(xform) - origin).Length();
            if (distance > range || distance >= bestDistance)
                continue;

            best = uid;
            bestDistance = distance;
        }

        return best;
    }

    private float GetBatteryLevel(EntityUid robot)
    {
        if (!_powerCell.TryGetBatteryFromEntityOrSlot(robot, out var battery))
            return 1f;

        return Math.Clamp(_battery.GetChargeLevel(battery.Value.AsNullable()), 0f, 1f);
    }

    private async Task RespondAsync(
        EntityUid robot,
        EntityUid speaker,
        string speakerName,
        string heard,
        RobotCompanionComponent comp,
        RobotState state)
    {
        try
        {
            if (TerminatingOrDeleted(robot))
                return;

            var timeout = _config.GetCVar(DaiquiriCCVars.AIHelpTimeoutSeconds);
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(Math.Max(1, timeout)));

            state.History.Add(new AIHelpChatMessage { Role = "user", Content = $"{speakerName}: {heard}" });
            TrimHistory(state);

            SendThinking(robot);

            var result = await _aiHelp.CompleteAsync(
                state.History,
                BuildSystemPrompt(robot, comp),
                $"робот {ToPrettyString(robot)}",
                cts.Token,
                temperature: _config.GetCVar(DaiquiriCCVars.AIHelpRobotTemperature));

            if (!result.Success)
            {
                if (state.History.Count > 0 && state.History[^1].Role == "user")
                    state.History.RemoveAt(state.History.Count - 1);

                _sawmill.Warning($"[ROBOT] {ToPrettyString(robot)}: нет ответа от ИИ ({result.Status}).");
                return;
            }

            state.History.Add(new AIHelpChatMessage { Role = "assistant", Content = result.Text });
            TrimHistory(state);

            if (TerminatingOrDeleted(robot))
                return;

            var intent = ParseIntent(result.Text);

            if (comp.AllowFollowOrders && HasComp<ActorComponent>(speaker))
            {
                if (intent.Action != RobotAction.None || intent.Move != Vector2.Zero || intent.Turn != 0f)
                    AbortCharging(robot, state);

                switch (intent.Action)
                {
                    case RobotAction.Follow:
                        StartFollow(robot, speaker, state);
                        break;
                    case RobotAction.Stop:
                        StopFollow(robot, state);
                        break;
                    case RobotAction.Stay:
                        StartStandStill(robot, state);
                        break;
                    case RobotAction.Free:
                        ReleaseOrders(robot, state);
                        break;
                    case RobotAction.Charge:
                        StartCharging(robot, comp, state);
                        break;
                }

                if (intent.Move != Vector2.Zero)
                    StepBy(robot, state, intent.Move, comp);

                if (intent.Turn != 0f)
                    TurnBy(robot, intent.Turn);
            }

            var reply = PrepareReply(intent.Spoken, _config.GetCVar(DaiquiriCCVars.AIHelpRobotMaxReplyLength));
            if (string.IsNullOrWhiteSpace(reply))
                return;

            Say(robot, reply);
        }
        catch (Exception e)
        {
            _sawmill.Error($"[ROBOT] {ToPrettyString(robot)}: ошибка ответа: {e}");
        }
        finally
        {
            state.Busy = false;
        }
    }

    private void SendThinking(EntityUid robot)
    {
        _chat.TrySendInGameICMessage(
            robot,
            Loc.GetString("robot-companion-thinking"),
            InGameICChatType.Emote,
            ChatTransmitRange.Normal,
            hideLog: true,
            checkRadioPrefix: false,
            ignoreActionBlocker: true);
    }

    private void Say(EntityUid robot, string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return;

        _chat.TrySendInGameICMessage(
            robot,
            message,
            InGameICChatType.Speak,
            ChatTransmitRange.Normal,
            hideLog: true,
            checkRadioPrefix: false);
    }

    private string BuildSystemPrompt(EntityUid robot, RobotCompanionComponent comp)
    {
        var prompt = string.IsNullOrWhiteSpace(comp.SystemPrompt)
            ? _config.GetCVar(DaiquiriCCVars.AIHelpRobotSystemPrompt)
            : comp.SystemPrompt;

        if (!string.IsNullOrWhiteSpace(comp.PersonaPrompt))
            prompt = $"{prompt}\n{comp.PersonaPrompt}";

        if (GetBatteryNote(robot, comp) is { } note)
            prompt = $"{prompt}\n{note}";

        return prompt;
    }

    private string? GetBatteryNote(EntityUid robot, RobotCompanionComponent comp)
    {
        if (!comp.ReportBattery)
            return null;

        if (!_powerCell.TryGetBatteryFromEntityOrSlot(robot, out var battery))
            return null;

        var percent = (int) MathF.Round(GetBatteryLevel(robot) * 100f);

        var hint = percent switch
        {
            <= 15 => " Это критически мало — скоро отключишься, ищи зарядку или энергоячейку.",
            <= 40 => " Заряд ниже половины, стоит подзарядиться.",
            _ => string.Empty,
        };

        return $"Текущее состояние: заряд твоей батареи {percent}%.{hint}";
    }

    private static RobotIntent ParseIntent(string text)
    {
        var intent = new RobotIntent();

        var lastIndex = -1;

        void Consider(string marker, RobotAction candidate)
        {
            var index = text.LastIndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (index < 0 || index < lastIndex)
                return;

            lastIndex = index;
            intent.Action = candidate;
        }

        Consider(FollowMarker, RobotAction.Follow);
        Consider(StopMarker, RobotAction.Stop);
        Consider(StayMarker, RobotAction.Stay);
        Consider(FreeMarker, RobotAction.Free);
        Consider(ChargeMarker, RobotAction.Charge);

        if (MoveMarker.Matches(text) is { Count: > 0 } moves)
        {
            var last = moves[^1];
            if (TryParseFloat(last.Groups[1].Value, out var forward) && TryParseFloat(last.Groups[2].Value, out var right))
                intent.Move = new Vector2(Math.Clamp(forward, -5f, 5f), Math.Clamp(right, -5f, 5f));
        }

        if (TurnMarker.Matches(text) is { Count: > 0 } turns &&
            TryParseFloat(turns[^1].Groups[1].Value, out var degrees))
        {
            intent.Turn = Math.Clamp(degrees, -360f, 360f);
        }

        intent.Spoken = AnyMarker.Replace(text, string.Empty);
        return intent;
    }

    private static bool TryParseFloat(string value, out float result)
        => float.TryParse(value.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out result);

    private void TrimHistory(RobotState state)
    {
        var exchanges = Math.Max(0, _config.GetCVar(DaiquiriCCVars.AIHelpHistoryLength));
        var max = exchanges * 2;
        if (max <= 0)
        {
            state.History.Clear();
            return;
        }

        if (state.History.Count > max)
            state.History.RemoveRange(0, state.History.Count - max);
    }

    private static string PrepareReply(string text, int maxLength)
    {
        var clean = text
            .Replace('\n', ' ')
            .Replace('\r', ' ')
            .Replace("*", string.Empty)
            .Replace("`", string.Empty);

        clean = StripEmoji(clean);

        while (clean.Contains("  "))
            clean = clean.Replace("  ", " ");

        clean = clean.Trim().Trim('"', '\'', '«', '»', '“', '”').Trim();

        if (maxLength > 0 && clean.Length > maxLength)
        {
            var cut = clean.LastIndexOf(' ', Math.Min(maxLength, clean.Length - 1));
            clean = (cut > 0 ? clean[..cut] : clean[..maxLength]).TrimEnd() + "...";
        }

        return clean;
    }

    private static string StripEmoji(string text)
    {
        var builder = new StringBuilder(text.Length);

        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];

            if (char.IsSurrogate(c))
            {
                i++;
                continue;
            }

            var skip = c switch
            {
                (char) 0x200D => true,
                >= (char) 0xFE00 and <= (char) 0xFE0F => true,
                (char) 0x20E3 => true,
                >= (char) 0x2190 and <= (char) 0x2BFF => true,
                >= (char) 0x2E00 and <= (char) 0x2E7F => true,
                >= (char) 0x3000 and <= (char) 0x303F => true,
                >= (char) 0x3200 and <= (char) 0x33FF => true,
                >= (char) 0xFF00 and <= (char) 0xFFEF => true,
                _ => false,
            };

            if (!skip)
                builder.Append(c);
        }

        return builder.ToString();
    }

    private enum RobotAction : byte
    {
        None,
        Follow,
        Stop,
        Stay,
        Free,
        Charge,
    }

    private sealed class RobotIntent
    {
        public RobotAction Action;
        public Vector2 Move;
        public float Turn;
        public string Spoken = string.Empty;
    }

    private sealed class RobotState
    {
        public readonly List<AIHelpChatMessage> History = new();
        public bool Busy;
        public TimeSpan NextReply;
        public EntityUid? FollowTarget;
        public EntityUid? ChargeTarget;
        public bool Charging;
        public EntityCoordinates? StepTarget;
        public TimeSpan StepDeadline;
        public bool RestoreStandStill;
    }
}
