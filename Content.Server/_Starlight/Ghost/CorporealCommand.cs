using System.Linq;
using Content.Server.Administration;
using Content.Shared.Administration;
using Content.Shared.Emoting;
using Content.Shared.Ghost;
using Content.Shared.Speech;
using Content.Shared.Speech.Components;
using Content.Shared.Starlight.TextToSpeech;
using Robust.Shared.Player;
using Robust.Shared.Toolshed;

namespace Content.Server._Starlight.Ghost;

/// <summary>
/// Allows you to force a ghost to become visible and be able to talk and emote n such. Admeme nonsense.
/// </summary>
[ToolshedCommand]
[AdminCommand(AdminFlags.Fun)]
public sealed class CorporealCommand : ToolshedCommand
{
    [CommandImplementation("on")]
    public EntityUid MakeCorporeal(IInvocationContext ctx, [PipedArgument] EntityUid uid)
    {
        if (!TryComp<GhostComponent>(uid, out var ghost))
        {
            ctx.WriteLine("Target must be a ghost.");
            return uid;
        }

        ghost.AlwaysVisible = true;
        ghost.BypassGhostChat = true;
        EnsureComp<SpeechComponent>(uid);
        EnsureComp<EmotingComponent>(uid);
        EnsureComp<VocalComponent>(uid);
        EnsureComp<TextToSpeechComponent>(uid);
        return uid;
    }

    [CommandImplementation("on")]
    public ICommonSession MakeCorporeal(IInvocationContext ctx, [PipedArgument] ICommonSession session)
    {
        MakeCorporeal(ctx, session.AttachedEntity ?? EntityUid.Invalid);
        return session;
    }

    [CommandImplementation("on")]
    public IEnumerable<EntityUid> MakeCorporeal(IInvocationContext ctx, [PipedArgument] IEnumerable<EntityUid> uid) =>
        uid.Select(x => MakeCorporeal(ctx, x));

    [CommandImplementation("on")]
    public IEnumerable<ICommonSession> MakeCorporeal(IInvocationContext ctx,
        [PipedArgument] IEnumerable<ICommonSession> session) =>
        session.Select(x => MakeCorporeal(ctx, x));

    [CommandImplementation("off")]
    public EntityUid MakeNonCorporeal(IInvocationContext ctx, [PipedArgument] EntityUid uid)
    {
        if (!TryComp<GhostComponent>(uid, out var ghost))
        {
            ctx.WriteLine("Target must be a ghost.");
            return uid;
        }

        ghost.AlwaysVisible = false;
        ghost.BypassGhostChat = false;
        RemComp<SpeechComponent>(uid);
        RemComp<EmotingComponent>(uid);
        RemComp<VocalComponent>(uid);
        return uid;
    }

    [CommandImplementation("off")]
    public ICommonSession MakeNonCorporeal(IInvocationContext ctx, [PipedArgument] ICommonSession session)
    {
        MakeNonCorporeal(ctx, session.AttachedEntity ?? EntityUid.Invalid);
        return session;
    }

    [CommandImplementation("off")]
    public IEnumerable<EntityUid> MakeNonCorporeal(IInvocationContext ctx, [PipedArgument] IEnumerable<EntityUid> uid) =>
        uid.Select(x => MakeNonCorporeal(ctx, x));

    [CommandImplementation("off")]
    public IEnumerable<ICommonSession> MakeNonCorporeal(IInvocationContext ctx,
        [PipedArgument] IEnumerable<ICommonSession> session) =>
        session.Select(x => MakeNonCorporeal(ctx, x));
}
