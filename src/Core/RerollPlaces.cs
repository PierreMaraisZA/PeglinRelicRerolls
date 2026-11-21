namespace PeglinRelicRerolls.Core
{
    /// <summary>
    /// Where a reroll happened, as reported to reroll listeners. Plain strings so new places can be added later without
    /// changing the API's signature; listeners should ignore names they don't know.
    /// </summary>
    public static class RerollPlaces
    {
        /// <summary>A relic choice screen after a battle (including a floor boss's and other mods' relic screens).</summary>
        public const string RelicScreen = "relic_screen";

        /// <summary>A treasure chest's relic choice.</summary>
        public const string Chest = "chest";

        /// <summary>The relics on a shop's shelf.</summary>
        public const string Shop = "shop";
    }
}
