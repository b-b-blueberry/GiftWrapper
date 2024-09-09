using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using StardewValley.ItemTypeDefinitions;

namespace GiftWrapper
{
	public class WrapItemDataDefinition : BaseItemDataDefinition
	{
		public static string TypeDefinitionId => "(BB_GW)";

		public static string ItemName => ModEntry.ItemPrefix + ModEntry.WrapItemName;

		public override string Identifier => WrapItemDataDefinition.TypeDefinitionId;

		public override Item CreateItem(ParsedItemData data)
		{
			return new WrapItem();
		}

		public override bool Exists(string itemId)
		{
			return itemId == WrapItemDataDefinition.ItemName;
		}

		public override IEnumerable<string> GetAllIds()
		{
			return [WrapItemDataDefinition.ItemName];
		}

		public override ParsedItemData GetData(string itemId)
		{
			return new ParsedItemData(
				itemType: this,
				itemId: itemId,
				spriteIndex: 0,
				textureName: ModEntry.GameContentWrapTexturePath,
				internalName: WrapItemDataDefinition.ItemName,
				displayName: ModEntry.I18n.Get("item.giftwrap.name"),
				description: ModEntry.I18n.Get("item.giftwrap.description"),
				category: ModEntry.Definitions.CategoryNumber,
				objectType: null,
				rawData: null,
				isErrorItem: false,
				excludeFromRandomSale: false);
		}

		public override Rectangle GetSourceRect(ParsedItemData data, Texture2D texture, int spriteIndex)
		{
			return ModEntry.Definitions.WrapItemSource;
		}
	}
}
