using Content.Shared._Starlight.TutorialServer;

namespace Content.Client._Starlight.TutorialServer;

public sealed class TutorialSystem : EntitySystem
{
    public bool IsInTutorial { get; private set; }

    public override void Initialize()
    {
        base.Initialize();

        SubscribeNetworkEvent<TutorialStartedEvent>(OnTutorialStarted);
        SubscribeNetworkEvent<TutorialEndedEvent>(OnTutorialEnded);
    }

    private void OnTutorialStarted(TutorialStartedEvent args) => IsInTutorial = true;
    private void OnTutorialEnded(TutorialEndedEvent args) => IsInTutorial = false;
}
