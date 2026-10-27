using Content.Client._Starlight.BodyControl;

// ReSharper disable CheckNamespace
namespace Content.Client.CombatMode;

public sealed partial class CombatModeSystem
{
    private EntityUid? CombatUiEntity => EntityManager.System<BodyControlViewSystem>().Entity;

    private bool TryGetControlledCombatMode(out bool combatMode)
    {
        combatMode = false;
        if (!EntityManager.System<BodyControlViewSystem>().TryGetSource(out _, out var source))
            return false;
        combatMode = IsInCombatMode(source.Body);
        return true;
    }

    [SubscribeLocalEvent]
    private void OnControlViewChanged(BodyControlViewChangedEvent args)
        => LocalPlayerCombatModeUpdated?.Invoke(IsInCombatMode());
}
