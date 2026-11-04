using Content.Shared.Verbs;
using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.UserInterface.Setup;

/// <summary>
/// This handles marking an entity to be compatible with the setup system.
/// </summary>
public abstract class SharedSetupSystem : EntitySystem
{
    /// <inheritdoc/>
    public override void Initialize() => SubscribeLocalEvent<SetupableComponent, GetVerbsEvent<AlternativeVerb>>(OnGetVerbs);

    private void OnGetVerbs(EntityUid uid, SetupableComponent component, GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanInteract || !args.CanComplexInteract) return;

        if (component.NameSet) return;

        AlternativeVerb verb = new() { Text = Loc.GetString("setup-verb-text"), Act = () => OpenSetupInterface(uid, args.User, component) };
        args.Verbs.Add(verb);
    }

    protected virtual void OpenSetupInterface(EntityUid entOwner, EntityUid argsUser, SetupableComponent entComp) {}
}

[Serializable, NetSerializable]
public sealed class SetupBoundUiState(
    string name,
    bool nameDisabled
) : BoundUserInterfaceState
{
    public string Name { get; } = name;
    public bool NameDisabled { get; } = nameDisabled;
}

[Serializable, NetSerializable]
public sealed class SetupSetName(string name) : BoundUserInterfaceMessage
{
    public string Name { get; } = name;
}

[Serializable, NetSerializable]
public enum SetupUiKey : byte
{
    Key
}
