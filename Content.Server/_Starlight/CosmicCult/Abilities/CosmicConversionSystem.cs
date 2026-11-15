using Content.Server.Bible.Components;
using Content.Server.Popups;
using Content.Shared._Starlight.CosmicCult.Components;
using Content.Shared._Starlight.CosmicCult;
using Content.Shared.Mindshield.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Stunnable;
using Content.Shared.Damage.Systems;
using Content.Shared.Roles;
using Content.Shared.Roles.Components;
using Content.Shared.Mind;
using Content.Shared.SSDIndicator;
using Robust.Server.Player;
using Robust.Shared.Random;
using Content.Shared._Starlight.Shadekin.Components;
using Content.Shared._Starlight.NullSpace.Components;
using Content.Shared._Starlight.Changeling;
using Content.Shared._Starlight.Devil;
using Content.Shared._Starlight.Antags.Vampires.Components;
using Content.Shared._Starlight.Roles;

namespace Content.Server._Starlight.CosmicCult.Abilities;

public sealed partial class CosmicConversionSystem : EntitySystem
{
    [Dependency] private CosmicCultRuleSystem _cultRule = default!;
    [Dependency] private CosmicGlyphSystem _cosmicGlyph = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private PopupSystem _popup = default!;
    [Dependency] private SharedCosmicCultSystem _cosmicCult = default!;
    [Dependency] private SharedStunSystem _stun = default!;
    [Dependency] private SharedRoleSystem _role = default!;
    [Dependency] private SharedMindSystem _mind = default!;
    [Dependency] private IPlayerManager _playerMan = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private IRobustRandom _rand = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CosmicGlyphConversionComponent, TryActivateGlyphEvent>(OnConversionGlyph);
    }

    private void OnConversionGlyph(Entity<CosmicGlyphConversionComponent> uid, ref TryActivateGlyphEvent args)
    {
        foreach (var entity in _lookup.GetEntitiesIntersecting(Transform(uid).Coordinates))
            if (HasComp<NullSpaceBlockerComponent>(entity))
            {
                _popup.PopupEntity(Loc.GetString("cosmicability-generic-fail"), uid, args.User);
                args.Cancel();
                return;
            }

        var possibleTargets = _cosmicGlyph.GetTargetsNearGlyph(uid, uid.Comp.ConversionRange, entity => _cosmicCult.EntityIsCultist(entity));
        if (possibleTargets.Count == 0)
        {
            _popup.PopupEntity(Loc.GetString("cult-glyph-conditions-not-met"), uid, args.User);
            args.Cancel();
            return;
        }

        TryConvertTarget(uid, possibleTargets, ref args);
    }

    private void TryConvertTarget(Entity<CosmicGlyphConversionComponent> uid, HashSet<EntityUid> possibleTargets, ref TryActivateGlyphEvent args)
    {
        var validTargets = new List<EntityUid>(possibleTargets.Count);
        string? commonReason = null;
        var allSame = true;
        foreach (var target in possibleTargets)
        {
            var blockReason = GetConversionBlockReason(uid, target);
            if (blockReason == null)
            {
                validTargets.Add(target);
                continue;
            }

            if (commonReason == null)
                commonReason = blockReason;
            else if (commonReason != blockReason)
                allSame = false;
        }

        if (validTargets.Count > 0)
        {
            var target = _rand.Pick(validTargets);
            _stun.TryAddStunDuration(target, TimeSpan.FromSeconds(4f));
            _damageable.TryChangeDamage(target, uid.Comp.ConversionHeal * -1);
            _cultRule.CosmicConversion(uid, target);
            return;
        }

        if (allSame && commonReason != null)
            _popup.PopupEntity(Loc.GetString(commonReason), uid, args.User);
        else
            _popup.PopupEntity(Loc.GetString("cult-glyph-conditions-not-met"), uid, args.User);
        args.Cancel();
    }

    private string? GetConversionBlockReason(Entity<CosmicGlyphConversionComponent> uid, EntityUid target)
    {
        if (_mobState.IsDead(target))
            return "cult-glyph-target-dead";

        if (!IsMindActive(target))
            return "cult-glyph-target-nomind";

        if (uid.Comp.NegateProtection)
            return null;

        if (HasComp<BibleUserComponent>(target))
            return "cult-glyph-target-chaplain";

        if (HasComp<ChangelingComponent>(target) || HasComp<VampireComponent>(target))
            return "cult-glyph-target-otherantag";

        if (HasComp<MindShieldComponent>(target))
            return "cult-glyph-target-mindshield";

        if (HasComp<BrighteyeComponent>(target))
            return "cult-glyph-target-brighteye";

        if (HasComp<DevilComponent>(target))
            return "cult-glyph-target-devil";

        if (_mind.TryGetMind(target, out var mind, out _) && (_role.MindHasRole<WizardRoleComponent>(mind) || _role.MindHasRole<WizardDuelistRoleComponent>(mind)))
            return "cult-glyph-target-wizard";

        return null;
    }

    /// <summary>
    /// The victim needs a live player mind. Mindless entities and SSD or departed bodies can't be converted.
    /// </summary>
    private bool IsMindActive(EntityUid target)
    {
        if (!_mind.TryGetMind(target, out _, out var mind) || mind is null)
            return false;

        if (!_playerMan.TryGetSessionById(mind.UserId, out _))
            return false;

        if (TryComp<SSDIndicatorComponent>(target, out var ssd) && ssd.IsSSD)
            return false;

        return true;
    }
}
