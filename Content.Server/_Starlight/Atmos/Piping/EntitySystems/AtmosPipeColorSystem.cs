// ReSharper disable CheckNamespace
using Content.Server.Administration.Managers;
using Content.Server.Atmos.Piping.Components;
using Content.Shared.Administration;
using Content.Shared.Verbs;
using Robust.Shared.Console;
using Robust.Shared.Player;
using Robust.Shared.Utility;
using static Content.Server.Administration.Systems.AdminVerbSystem;

namespace Content.Server.Atmos.Piping.EntitySystems;

public sealed partial class AtmosPipeColorSystem
{
    [Dependency] private IAdminManager _adminManager = default!;
    [Dependency] private IConsoleHost _consoleHost = default!;

    private void SLInitialize()
    {
        SubscribeLocalEvent<AtmosPipeColorComponent, GetVerbsEvent<Verb>>(OnGetVerbs);
    }

    private void OnGetVerbs(Entity<AtmosPipeColorComponent> ent, ref GetVerbsEvent<Verb> args)
    {
        if (!TryComp(args.User, out ActorComponent? actor))
            return;

        var player = actor.PlayerSession;

        if (!_adminManager.HasAdminFlag(player, AdminFlags.Mapping))
            return;

        Verb flood = new()
        {
            Text = Loc.GetString("admin-trick-floodpipes"),
            Category = VerbCategory.Tricks,
            Icon = new SpriteSpecifier.Texture(new("/Textures/Interface/AdminActions/unbolt.png")),
            Act = () => {
                var cmd = $"colornetwork {ent.Owner} Pipe \"{ent.Comp.Color.ToHex()}\"";
                _consoleHost.ExecuteCommand(cmd);
                Log.Info(cmd);
            },
            Message = Loc.GetString("admin-trick-floodpipes-description"),
            Priority = (int)TricksVerbPriorities.AtmosColorFloodfill,
        };
        args.Verbs.Add(flood);

    }
}
