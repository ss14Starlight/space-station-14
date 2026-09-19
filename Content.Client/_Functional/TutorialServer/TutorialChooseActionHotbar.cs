using Robust.Shared.Prototypes;

namespace Content.Client.UserInterface.Systems.Actions;

/// <summary>
/// Tutorial Server helpers for the Choose-a-tutorial hotbar slot (client-only layout).
/// </summary>
public sealed partial class ActionUIController
{
    private static readonly EntProtoId TutorialChooseRoleActionProto = "ActionTutorialChooseRole";

    private bool IsTutorialChooseRoleAction(EntityUid actionId)
    {
        return EntityManager.GetComponentOrNull<MetaDataComponent>(actionId)?.EntityPrototype?.ID
            == TutorialChooseRoleActionProto.Id;
    }

    /// <summary>
    /// True when the Choose-a-tutorial action is assigned to a hotbar slot.
    /// </summary>
    public bool IsTutorialChooseRoleOnHotbar()
    {
        foreach (var id in _actions)
        {
            if (id is { } uid && IsTutorialChooseRoleAction(uid))
                return true;
        }

        return false;
    }
}
