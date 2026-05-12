using Content.Client.Guidebook;
using Content.Shared._Starlight.Silicons.Borgs;
using Content.Shared.Guidebook;
using Robust.Shared.Prototypes;

namespace Content.Client._Starlight.Silicons.Borgs;

public sealed class SecurityBorgActionsSystem : EntitySystem
{
    [Dependency] private readonly GuidebookSystem _guidebook = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SecurityBorgComponent, BorgLawbookActionEvent>(OnLawbook);
    }

    /// <summary>
    /// Opens the guidebook to the space law page when the borg lawbook action is used.
    /// </summary>
    private void OnLawbook(EntityUid uid, SecurityBorgComponent _, BorgLawbookActionEvent args)
    {
        if (args.Handled)
            return;

        _guidebook.OpenHelp(new List<ProtoId<GuideEntryPrototype>> { "SpaceLaw" });
        args.Handled = true;
    }
}
