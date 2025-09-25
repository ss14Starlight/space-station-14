using Content.Shared._Starlight.Devil;
using Robust.Shared.Audio;
using Content.Shared.Paper;

namespace Content.Server._Starlight.Devil;

public sealed partial class DevilSystem : EntitySystem
{
    [Dependency] private readonly PaperSystem _paper = default!;

    private readonly string InfernalContractPrototype = "InfernalContract";
    private SoundPathSpecifier ContractSummonSound = new("/Audio/Effects/thudswoosh.ogg");

    private void SubscribeContract()
    {

    }

    private EntityUid CreateContract(EntityUid author, DevilComponent devilComp)
    {
        var paper = Spawn(InfernalContractPrototype, Transform(author).Coordinates);
        var content = Loc.GetString("infernal-contract-base", ("truename", devilComp.TrueName));
        _paper.SetContent(paper, content);

        _audio.PlayPvs(ContractSummonSound, author);

        return paper;
    }
}