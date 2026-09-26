using Content.Shared._Starlight.IdentityManagement.Components;
using Content.Shared.Examine;
using Content.Shared.IdentityManagement;

namespace Content.Shared._Starlight.IdentityManagement;

/// <summary>
/// Mirrors HumanoidAppearanceSystem's own examine line ("She is a young human.") for
/// animal identity holders, minus the age clause ("She is a dog.").
/// </summary>
public sealed class AnimalIdentitySystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AnimalIdentityComponent, ExaminedEvent>(OnAnimalExamined);
    }

    private void OnAnimalExamined(EntityUid uid, AnimalIdentityComponent component, ExaminedEvent args)
    {
        var identity = Identity.Entity(uid, EntityManager);
        var noun = Loc.GetString(component.NounId);

        args.PushText(Loc.GetString("animal-identity-component-examine", ("user", identity), ("noun", noun)));
    }
}
