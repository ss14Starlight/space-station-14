using Content.Server.Administration.Logs;
using Content.Server.Administration.Managers;
using Content.Server.EUI;
using Content.Server.GameTicking;
using Content.Server.Ghost.Roles.Components;
using Content.Server.Ghost.Roles.Events;
using Content.Server.Ghost.Roles.UI;
using Content.Server.Mind.Commands;
using Content.Server.Popups;
using Content.Server.RoundEnd;
using Content.Shared.Administration;
using Content.Shared.CCVar;
using Content.Shared.Database;
using Content.Shared.Follower;
using Content.Shared.GameTicking;
using Content.Shared.Ghost.Roles.Components;
using Content.Shared.Ghost.Roles.Raffles;
using Content.Shared.Ghost.Roles;
using Content.Shared.Ghost;
using Content.Shared.Mind.Components;
using Content.Shared.Mind;
using Content.Shared.Mobs;
using Content.Shared.Players;
using Content.Shared.Roles;
using Content.Shared.Starlight.GhostTheme;
using Content.Shared.Starlight;
using Content.Shared.Verbs;
using Content.Shared.Weapons.Ranged.Systems;
using Content.Shared._NullLink;
using JetBrains.Annotations;
using Robust.Server.GameObjects;
using Robust.Server.Player;
using Robust.Shared.Collections;
using Robust.Shared.Configuration;
using Robust.Shared.Console;
using Robust.Shared.Enums;
using Robust.Shared.GameObjects;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Robust.Shared.Utility;
using System.Linq;

namespace Content.Server.Ghost.Roles;

[UsedImplicitly]
public sealed class GhostThemeSystem : EntitySystem
{
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly EuiManager _euiManager = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly IPlayerRolesManager _playerRoles = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<GhostComponent, PlayerAttachedEvent>(OnPlayerAttached);
    }

    private readonly Dictionary<ICommonSession, GhostThemeEui> _openUis = [];
    public override void Shutdown()
    {
        base.Shutdown();
    }

    public void OpenEui(ICommonSession session)
    {
        if (session.AttachedEntity is not { Valid: true } attached ||
            !EntityManager.HasComponent<GhostComponent>(attached))
            return;

        if (_openUis.ContainsKey(session))
            CloseEui(session);

        HashSet<string> availableThemes = [];

        foreach (var ghostTheme in _prototypeManager.EnumeratePrototypes<GhostThemePrototype>())
            if(ghostTheme.Requirements.Count == 0 || ghostTheme.Requirements.All(x => x.Handle(session)))
                availableThemes.Add(ghostTheme.ID);

        var eui = _openUis[session] = new GhostThemeEui(availableThemes);

        _euiManager.OpenEui(eui, session);
        eui.StateDirty();
    }
    public void CloseEui(ICommonSession session)
    {
        if (!_openUis.ContainsKey(session))
            return;

        _openUis.Remove(session, out var eui);

        eui?.Close();
    }
    public void ChangeColor(ICommonSession session, Color color)
    {
        if (session.AttachedEntity is not { Valid: true } attached ||
            !EntityManager.TryGetComponent<GhostThemeComponent>(attached, out var themes))
            return;

        themes.GhostThemeColor = color;

        Dirty(attached, themes);

        var playerData = _playerRoles.GetPlayerData(attached);
        if (playerData != null)
        {
            playerData.GhostThemeColor = color;
        }

        _appearance.SetData(attached, GhostThemeVisualLayers.Color, color);
    }
    public void ChangeTheme(ICommonSession session, string theme)
    {
        if (session.AttachedEntity is not { Valid: true } attached ||
            !EntityManager.TryGetComponent<GhostThemeComponent>(attached, out var themes))
            return;

        if(!_prototypeManager.TryIndex<GhostThemePrototype>(theme, out var proto))
            return;

        if (proto.Requirements.Count != 0 && proto.Requirements.Any(x => !x.Handle(session)))
            return;

        themes.SelectedGhostTheme = theme;

        Dirty(attached, themes);

        if (_playerRoles.GetPlayerData(attached) is PlayerData playerData)
            playerData.GhostTheme = theme;

        _appearance.SetData(attached, GhostThemeVisualLayers.Base, theme);
    }
    public void UpdateAllEui()
    {
        foreach (var eui in _openUis.Values)
        {
            eui.StateDirty();
        }
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
    }
    private void OnPlayerAttached(EntityUid uid, GhostComponent component, PlayerAttachedEvent args)
    {
        var theme = EnsureComp<GhostThemeComponent>(uid);
        var playerData = _playerRoles.GetPlayerData(uid);
        if (playerData != null && playerData.GhostTheme != null)
        {
            if (!_prototypeManager.TryIndex<GhostThemePrototype>(playerData.GhostTheme, out var proto)
                || !_playerManager.TryGetSessionByEntity(uid, out var session))
                return;

            if (proto.Requirements.Count != 0 && proto.Requirements.Any(x => !x.Handle(session)))
                return;

            theme.SelectedGhostTheme = playerData.GhostTheme;
            theme.GhostThemeColor = playerData.GhostThemeColor;
            _appearance.SetData(uid, GhostThemeVisualLayers.Color, playerData.GhostThemeColor);

            Dirty(uid, theme);

            _appearance.SetData(uid, GhostThemeVisualLayers.Base, playerData.GhostTheme);
        }
    }
}

[AnyCommand]
public sealed class GhostTheme : IConsoleCommand
{
    [Dependency] private readonly IEntityManager _e = default!;

    public string Command => "ghostTheme";
    public string Description => "Opens ghost theme preferences window.";
    public string Help => $"{Command}";
    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (shell.Player != null)
            _e.System<GhostThemeSystem>().OpenEui(shell.Player);
        else
            shell.WriteLine("You can only open ghost theme UI on a client.");
    }
}
