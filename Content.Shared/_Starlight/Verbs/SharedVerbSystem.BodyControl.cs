using Content.Shared._Starlight.BodyControl;
using Content.Shared.Hands.Components;

// ReSharper disable CheckNamespace
namespace Content.Shared.Verbs;

public abstract partial class SharedVerbSystem
{
    private bool WithControlledVerb(EntityUid actor, Verb verb, Action command)
    {
        var context = EntityManager.System<BodyControlHandContextSystem>();
        return context.WithController(actor, verb is ExamineVerb ? null : "body.interact",
            verb is not ExamineVerb && HasComp<HandsComponent>(context.GetControlledBody(actor)), command);
    }
}
