// ReSharper disable CheckNamespace
namespace Content.Shared._Starlight.Computers.RemoteControl;

public static class RemoteControlAudioHelpers
{
    public static EntityUid? GetPredictedAudioUser(this IEntityManager entityManager, EntityUid? user)
        => entityManager.System<SharedRemoteControlConsoleSystem>().GetPredictedAudioUser(user);
}
