using Content.Shared._Starlight.UserInterface.Setup;
using Robust.Server.GameObjects;

namespace Content.Server._Starlight.UserInterface.Setup;

/// <inheritdoc/>
public sealed partial class SetupSystem : SharedSetupSystem
{
    [Dependency] private MetaDataSystem _metadata = default!;
    [Dependency] private UserInterfaceSystem _userInterface = default!;
    public const int SetupNameLimit = 32;
    /// <inheritdoc/>
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SetupableComponent, SetupSetName>(OnSetName);
        SubscribeLocalEvent<SetupableComponent, ComponentInit>(OnInit);
    }

    private void OnInit(Entity<SetupableComponent> ent, ref ComponentInit args)
    {
        var meta = MetaData(ent);
        if (meta.EntityPrototype != null && meta.EntityName != meta.EntityPrototype.Name)
        {
            ent.Comp.NameSet = true;
            Dirty(ent, ent.Comp);
            UpdateSetupInterface(ent, ent.Comp);
        }
    }

    private void OnSetName(Entity<SetupableComponent> ent, ref SetupSetName args)
    {
        if (args.UiKey is not SetupUiKey || string.IsNullOrEmpty(args.Name) ||
            args.Name.Length > SetupNameLimit) return;
        ent.Comp.NameSet = true;
        var meta = MetaData(ent);
        _metadata.SetEntityName(ent.Owner, $"{meta.EntityPrototype?.Name} ({args.Name})");
        Dirty(ent, ent.Comp);
        UpdateSetupInterface(ent, ent.Comp);
    }

    protected override void OpenSetupInterface(EntityUid uid, EntityUid user, SetupableComponent? comp)
    {
        if (!Resolve(uid, ref comp)) return;

        if (!_userInterface.TryOpenUi(uid, SetupUiKey.Key, user)) return;

        UpdateSetupInterface(uid, comp);
    }
    private void UpdateSetupInterface(EntityUid uid, SetupableComponent? comp)
    {
        if (!Resolve(uid, ref comp)) return;

        if (comp.NameSet)
        {
            _userInterface.CloseUi(uid, SetupUiKey.Key);
            return;
        }

        var state = new SetupBoundUiState(MetaData(uid).EntityName, comp.NameSet);
        _userInterface.SetUiState(uid, SetupUiKey.Key, state);
    }
}
