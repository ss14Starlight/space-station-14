using Content.Server.Administration.Managers;
using Content.Server.Chat.Managers;
using Content.Shared._Starlight.Admeme.NarsieCult;
using Content.Shared._Starlight.Admeme.Roles.Components;
using Content.Shared.Administration;
using Content.Shared.Database;
using Content.Shared.IdentityManagement;
using Content.Shared.Interaction;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.NPC.Systems;
using Content.Shared.Popups;
using Content.Shared.Roles;
using Content.Shared.Verbs;
using Robust.Server.Player;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Content.Shared.NPC.Prototypes;

namespace Content.Server._Starlight.Admeme.NarsieCult;

/// <summary>
///     Converts minds into Nar'Sie cultists: gives them the <c>MindRoleNarsieCultist</c> mind role,
///     sends a briefing and plays the custom conversion sound to them.
///     Two entry points: the <see cref="NarsieConverterComponent"/> item and an admin verb.
/// </summary>
public sealed partial class NarsieConversionSystem : EntitySystem
{
    [Dependency] private IAdminManager _admin = default!;
    [Dependency] private IChatManager _chat = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedMindSystem _mind = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedRoleSystem _role = default!;
    [Dependency] private NpcFactionSystem _npcFaction = default!;

    /// <summary>The mind role entity prototype granted on conversion.</summary>

    public static readonly EntProtoId CultistMindRole = "MindRoleNarsieCultist";

    private static readonly ProtoId<NpcFactionPrototype> CultistFaction = "MoffNarsianDemon";

    private static readonly SoundSpecifier DefaultSound =
        new SoundPathSpecifier(NarsieConverterComponent.DefaultSoundPath);

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<NarsieConverterComponent, AfterInteractEvent>(OnAfterInteract);
        SubscribeLocalEvent<MindContainerComponent, GetVerbsEvent<Verb>>(OnGetVerbs);
    }

    // ---- Item entry point -------------------------------------------------------------------

    private void OnAfterInteract(EntityUid uid, NarsieConverterComponent comp, AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target is not { } target)
            return;

        // Only react to things that can have a mind; let other interactions through.
        if (!TryComp<MindContainerComponent>(target, out var mindContainer))
            return;

        args.Handled = true;

        if (comp.RequireAlive && !_mobState.IsAlive(target))
        {
            _popup.PopupEntity(
                Loc.GetString("narsie-cult-convert-dead", ("target", Identity.Entity(target, EntityManager))),
                target,
                args.User,
                PopupType.SmallCaution);
            return;
        }

        TryConvert((target, mindContainer), args.User, comp.ConversionSound);
    }

    // ---- Admin verb entry point -------------------------------------------------------------

    private void OnGetVerbs(EntityUid uid, MindContainerComponent comp, GetVerbsEvent<Verb> args)
    {
        if (!TryComp<ActorComponent>(args.User, out var actor) ||
            !_admin.HasAdminFlag(actor.PlayerSession, AdminFlags.Fun))
        {
            return;
        }

        var target = args.Target;
        var user = args.User;

        args.Verbs.Add(new Verb
        {
            Text = Loc.GetString("narsie-cult-convert-verb"),
            Message = Loc.GetString("narsie-cult-convert-verb-desc"),
            Category = VerbCategory.Antag,
            Impact = LogImpact.High, // verbs with an Impact are written to the admin log automatically
            Act = () => TryConvert((target, null), user),
        });
    }

    // ---- Core -------------------------------------------------------------------------------

    /// <summary>
    ///     Converts the mind of <paramref name="target"/> into a Nar'Sie cultist.
    /// </summary>
    /// <param name="target">The mob to convert (must resolve a mind container).</param>
    /// <param name="user">Who is doing the converting (for popups). May be null.</param>
    /// <param name="sound">Conversion sound. Defaults to the Admeme Nar'Sie sound.</param>
    /// <returns>True if the role was newly granted.</returns>
    public bool TryConvert(Entity<MindContainerComponent?> target, EntityUid? user = null, SoundSpecifier? sound = null)
    {
        if (!Resolve(target, ref target.Comp, logMissing: false))
            return false;

        var name = Identity.Entity(target, EntityManager);

        if (!_mind.TryGetMind(target, out var mindId, out var mind))
        {
            if (user is { } u)
                _popup.PopupEntity(Loc.GetString("narsie-cult-convert-no-mind", ("target", name)), target, u, PopupType.SmallCaution);
            return false;
        }

        if (_role.MindHasRole<NarsieCultistRoleComponent>(mindId))
        {
            if (user is { } u)
                _popup.PopupEntity(Loc.GetString("narsie-cult-convert-already", ("target", name)), target, u, PopupType.SmallCaution);
            return false;
        }

        _role.MindAddRole(mindId, CultistMindRole, mind);

        _npcFaction.AddFaction((target.Owner, null), CultistFaction);

        if (mind.UserId is { } userId && _player.TryGetSessionById(userId, out var session))
        {
            _chat.DispatchServerMessage(session, Loc.GetString("narsie-cult-converted-briefing"));
            _audio.PlayGlobal(sound ?? DefaultSound, session);
        }

        _popup.PopupEntity(Loc.GetString("narsie-cult-convert-success", ("target", name)), target, PopupType.LargeCaution);
        return true;
    }
}
