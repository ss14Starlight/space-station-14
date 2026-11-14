using Content.Server._Starlight.Achievement;
using Content.Server._Starlight.Language;
using Content.Server._Starlight.Statistics;
using Content.Server.Antag;
using Content.Server.GameTicking.Rules.Components;
using Content.Shared.GameTicking.Components;
using Robust.Shared.Player;

namespace Content.Server.GameTicking.Rules;

public sealed partial class ZombieRuleSystem
{
    [Dependency] private LanguageSystem _language = default!;
    [Dependency] private AchievementSystem _achievement = default!;
    [Dependency] private RoundStatisticsSystem _roundStatistics = default!;

    private readonly HashSet<ICommonSession> _initialInfected = new();
    protected override void Ended(EntityUid uid, ZombieRuleComponent component, GameRuleComponent gameRule,
        GameRuleEndedEvent args)
    {
        base.Ended(uid, component, gameRule, args);
        _initialInfected.Clear();
    }
    /// <summary>
    /// Gives initial infected the ability to understand (but not speak) zombie language.
    /// </summary>
    private void OnInitialInfectedSelected(EntityUid uid, ZombieRuleComponent comp,
        ref AfterAntagEntitySelectedEvent args)
    {
        if (args.Session is { } session) _initialInfected.Add(session);
        _language.AddLanguage(args.EntityUid, "Zombie", addSpoken: false, addUnderstood: true);
    }
}
