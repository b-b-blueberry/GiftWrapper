namespace GiftWrapper.Data
{
	public record Shop
	{
		/// <summary>
		/// Name of shop IDs to match any <see cref="StardewValley.Menus.ShopMenu.ShopId"/>.
		/// </summary>
		public string[] ShopIds = null;
		/// <summary>
		/// <see cref="StardewValley.GameStateQuery"/> conditions to validate this entry.
		/// Valid if null, or if condition passes.
		/// </summary>
		public string Conditions = null;
		/// <summary>
		/// List of names of items used to index items added by this shop entry.
		/// Prioritised in descending order.
		/// </summary>
		public string[] AddAtItem = null;
		/// <summary>
		/// Multiplier applied to sale price after all usual multipliers.
		/// Based on <see cref="StardewValley.Item.salePrice"/>.
		/// </summary>
		public float PriceMultiplier = 1;
		/// <summary>
		/// Valid if null, or field equals value of <see cref="Config.AlwaysAvailable"/>.
		/// </summary>
		public bool? IfAlwaysAvailable = null;
	}
}
