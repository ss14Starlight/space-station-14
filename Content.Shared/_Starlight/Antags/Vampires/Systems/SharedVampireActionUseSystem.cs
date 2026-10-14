namespace Content.Shared._Starlight.Antags.Vampires.Systems;

public sealed class SharedVampireActionUseSystem : EntitySystem
{
    public bool TryUse(EntityUid user, EntityUid? actionEntity = null, int bloodCost = 0, bool showPopup = true)
    {
        var ev = new VampireActionUseAttemptEvent(user, actionEntity, bloodCost, showPopup);
        RaiseLocalEvent(user, ref ev, true);
        return ev.Allowed;
    }
}
