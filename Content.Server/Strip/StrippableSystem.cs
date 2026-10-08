using Content.Shared.Strip;
using Content.Server.RoundEnd;

namespace Content.Server.Strip;

public sealed class StrippableSystem : SharedStrippableSystem
{
  public override void Initialize() // 🌟Starlight🌟
  {
      base.Initialize();
      SubscribeLocalEvent<RoundEndSystemChangedEvent>((_) => ClearActiveStripDoAfters());
  }
}
