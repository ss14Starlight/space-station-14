using Content.Server.Destructible;
using Content.Shared._Starlight.Cargo.TamperSeal;
using Content.Shared._Starlight.Cargo.TamperSeal.Components;
using Content.Shared._Starlight.Construction;
using Content.Shared.Damage.Systems;

namespace Content.Server._Starlight.Cargo.TamperSeal;

/// <inheritdoc/>
public sealed partial class TamperSealSystem : SharedTamperSealSystem
{
    [Dependency] private DestructibleSystem _destructible = default!;

    public override void Initialize()
    {
        base.Initialize();

        // Direct interactions with a tamper-sealed entity.
        SubscribeLocalEvent<TamperSealComponent, DamageChangedEvent>(OnDamageChanged);
        SubscribeLocalEvent<TamperSealComponent, ConstructionInteractAttemptEvent>(OnConstructionInteractAttempt);

        // Init and cleanup of appearance data.
        SubscribeLocalEvent<TamperSealComponent, ComponentStartup>(OnTamperSealStartup);
        SubscribeLocalEvent<TamperSealComponent, ComponentShutdown>(OnTamperSealShutdown);
    }

    /// <summary>
    /// Detect tamper-sealed entities being destroyed and trigger punishment if applicable.
    /// </summary>
    private void OnDamageChanged(EntityUid uid, TamperSealComponent seal, DamageChangedEvent args)
    {
        if (seal.Opened)
            return;
        if (!args.DamageIncreased)
            return;
        if (!_destructible.TryGetDestroyedAt((uid, null), out var destroyedAt))
            return;
        if (args.Damageable.TotalDamage < destroyedAt)
            return;

        // Trigger destroy behavior (shared code).
        DoDestroy(uid, seal, args.Origin, entityDestroyed: true, serverOnly: true);
    }

    /// <summary>
    /// Prevent any construction steps from occurring on sealed entities.
    /// </summary>
    private void OnConstructionInteractAttempt(EntityUid uid, TamperSealComponent seal,
        ref ConstructionInteractAttemptEvent args)
    {
        if (args.Canceled || seal.Opened)
            return;

        args.Canceled = true;
    }

    /// <summary>
    /// Initialize appearance data to default false when a tamper seal component starts.
    /// </summary>
    private void OnTamperSealStartup(EntityUid uid, TamperSealComponent seal, ComponentStartup args)
    {
        Appearance.SetData(uid, TamperSealVisuals.Opened, false);
        Appearance.SetData(uid, TamperSealVisuals.Destroyed, false);
    }

    /// <summary>
    /// Delete now-irrelevant appearance data on shutdown.
    /// </summary>
    private void OnTamperSealShutdown(EntityUid uid, TamperSealComponent seal, ComponentShutdown args)
    {
        Appearance.RemoveData(uid, TamperSealVisuals.Opened);
        Appearance.RemoveData(uid, TamperSealVisuals.Destroyed);
    }

}
