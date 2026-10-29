using Content.Shared._Starlight.Computers.RemoteControl;
using Content.Shared._Starlight.Silicons;
using Content.Shared.Examine;
using Content.Shared.Verbs;
using Robust.Shared.Utility;

namespace Content.Shared._Starlight.Silicons;

public sealed class RemoteControlBrainExamineSystem : EntitySystem
{
    [Dependency] private ExamineSystemShared _examine = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RemoteControlBrainComponent, GetVerbsEvent<ExamineVerb>>(OnGetExamineVerbs);
    }

    private void OnGetExamineVerbs(EntityUid uid, RemoteControlBrainComponent component,
        GetVerbsEvent<ExamineVerb> args)
    {
        if (!args.CanInteract || !args.CanAccess || !RemoteControlChannel.IsValid(component.Channel))
            return;

        var message = new FormattedMessage();
        message.AddMarkupOrThrow(Loc.GetString("remote-control-brain-channel-examine",
            ("channel", RemoteControlChannel.Format(component.Channel))));

        _examine.AddDetailedExamineVerb(args, component, message,
            Loc.GetString("remote-control-brain-channel-verb"));
    }
}
