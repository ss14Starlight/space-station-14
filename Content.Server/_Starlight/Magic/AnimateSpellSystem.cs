using Content.Server.Destructible;
using Content.Server.Destructible.Thresholds;
using Content.Server.Destructible.Thresholds.Behaviors;
using Content.Shared.Actions.Components;
using Content.Shared.Destructible;
using Content.Shared.Destructible.Thresholds.Triggers;
using Content.Shared.Item;
using Content.Shared.Magic.Components;
using Content.Shared._Starlight.Magic.Components;
using Content.Shared.Magic.Events;
using Content.Shared.Magic.Systems;
using Robust.Shared.Audio;
using Robust.Shared.Random;

namespace Content.Server._Starlight.Magic;

/// <summary>
/// Server-side system for handling animated objects, specifically setting their HP based on size.
/// HP ranges are configurable per-staff via AnimatedObjectHPComponent.
/// </summary>
public sealed class AnimateSpellSystem : EntitySystem
{
    [Dependency] private readonly IRobustRandom _random = default!;

    private EntityUid? _lastActionUsed; // Track the last action used for animated objects

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<AnimateComponent, AnimateSpellEvent>(OnAnimateSpell);
        SubscribeLocalEvent<ChangeComponentsSpellEvent>(OnChangeComponentsSpell);
    }

    private void OnChangeComponentsSpell(ChangeComponentsSpellEvent ev)
    {
        // Store the action entity if this is an animate spell
        if (ev.ToAdd.ContainsKey("Animate"))
            _lastActionUsed = ev.Action.Owner;
    }

    private void OnAnimateSpell(EntityUid uid, AnimateComponent component, ref AnimateSpellEvent args) =>
        SetAnimatedObjectHP(uid, _lastActionUsed);

    private void SetAnimatedObjectHP(EntityUid uid, EntityUid? action)
    {
        // Try to get HP configuration from the staff (action container)
        AnimatedObjectHPComponent? hpConfig = null;
        if (action != null && TryComp<ActionComponent>(action.Value, out var actionComp) && actionComp.Container != null)
        {
            TryComp<AnimatedObjectHPComponent>(actionComp.Container.Value, out hpConfig);
        }

        // Use default values if no config found on staff
        hpConfig ??= new AnimatedObjectHPComponent();

        // Determine HP based on item size with random variance
        int hp = 0;

        // Check for stored original size (Item component may have been removed)
        if (TryComp<AnimatedObjectSizeComponent>(uid, out var sizeComp))
        {
            var sizeId = sizeComp.OriginalSize;

            // Get HP range based on size from component configuration
            if (!hpConfig.Ranges.TryGetValue(sizeId, out var range))
                return;

            hp = _random.Next(range.Min, range.Max + 1);
        }
        else
        {
            // Objects that were never items - furniture, structures, etc.
            if (hpConfig.Ranges.TryGetValue("NonItem", out var range))
            hp = _random.Next(range.Min, range.Max + 1);
        }

        if (hp == 0)
            return;

        // Add or update Destructible component with size-based HP
        var destructible = EnsureComp<DestructibleComponent>(uid);
        destructible.Thresholds = new()
        {
            new DamageThreshold
            {
                Trigger = new DamageTrigger
                {
                    Damage = hp
                },
                Behaviors = new()
                {
                    new DoActsBehavior
                    {
                        Acts = ThresholdActs.Destruction
                    },
                    new PlaySoundBehavior
                    {
                        Sound = new SoundCollectionSpecifier("MetalBreak")
                    }
                }
            }
        };
    }
}
