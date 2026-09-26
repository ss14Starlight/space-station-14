using Content.Shared.GameTicking;

namespace Content.Server._Starlight.Economy
{
    public sealed partial class PriceRandomizationSystem : EntitySystem
    {
        [Dependency] private ItemPriceManager _priceManager = default!;

        public override void Initialize()
        {
            base.Initialize();
            SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);
        }

        private void OnRoundRestart(RoundRestartCleanupEvent args) => _priceManager.ResetForNewRound();
    }
}
