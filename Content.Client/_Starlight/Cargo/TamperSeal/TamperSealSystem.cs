using Content.Shared._Starlight.Cargo.TamperSeal;
using Content.Shared._Starlight.Cargo.TamperSeal.Components;
using Robust.Shared.GameObjects;

namespace Content.Client._Starlight.Cargo.TamperSeal;

/// <inheritdoc/>
public sealed class TamperSealSystem : SharedTamperSealSystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<TamperSealComponent, AfterAutoHandleStateEvent>(OnAfterHandleState);
    }

    /// <summary>
    /// Trigger a visuals update on state change. This makes VV writes trigger a visuals update.
    /// </summary>
    private void OnAfterHandleState(EntityUid uid, TamperSealComponent seal, ref AfterAutoHandleStateEvent args)
    {
        if (TryComp<AppearanceComponent>(uid, out var appearance))
            Appearance.QueueUpdate(uid, appearance);
    }
}
