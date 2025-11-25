using Content.Shared.Access.Components;
using Content.Shared.Examine;

namespace Content.Shared.Access.Systems;

public sealed class VisitorIdCardSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        
        SubscribeLocalEvent<VisitorIdCardComponent, ExaminedEvent>(OnExamined);
    }
    
    private void OnExamined(EntityUid uid, VisitorIdCardComponent component, ExaminedEvent args) =>
        args.PushMarkup(!component.AccessSet
            ? Loc.GetString("visitor-id-card-access-out-of-range")
            : Loc.GetString("visitor-id-card-access-out-of-sector"));
}