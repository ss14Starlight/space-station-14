namespace Content.Shared._Starlight.Economy
{
    [RegisterComponent]
    public sealed partial class ItemPriceComponent : Component
    {
        [DataField]
        public string PriceCategory = string.Empty;

        [DataField]
        public int FallbackPrice = 200;
    }
}
