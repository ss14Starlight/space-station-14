using Content.Shared._Starlight.Achievement;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.Abstract.Conditions;

public sealed partial class AchievementRequirement : BaseRequirement
{
    [Dependency] public IPrototypeManager _prototypes = default!;

    [DataField(required: true)]
    public List<ProtoId<AchievementPrototype>> Achievements { get; private set; } = [];

    public override string GetRequirementDescription()
    {
        base.GetRequirementDescription();

        var names = new List<string>(Achievements.Count);
        foreach (var required in Achievements)
        {
            var requiredprototype = _prototypes.Index<AchievementPrototype>(required);
            if (requiredprototype.Hidden)
                names.Add(Loc.GetString("achievment-hidden"));
            else
                names.Add(Loc.GetString(requiredprototype.Name));
        }

        return Loc.GetString("achievment-required", ("achivements", string.Join(", ", names)));
    }

    public override bool Handle(ICommonSession user)
    {
        base.Handle(user);
        var achievement = Ent.System<SharedAchievementSystem>();

        foreach (var required in Achievements)
            if (!achievement.HasAchievementUnlockedAsync(user, required.Id).GetAwaiter().GetResult())
                return false;

        return true;
    }
}
