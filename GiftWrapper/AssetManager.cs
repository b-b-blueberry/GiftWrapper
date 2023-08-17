using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using System.Collections.Generic;
using System.Linq;

namespace GiftWrapper
{
	public static class AssetManager
	{
		private static ITranslationHelper i18n => ModEntry.Instance.i18n;

		public static void AssetRequested(object sender, AssetRequestedEventArgs e)
		{
			if (e.NameWithoutLocale.IsEquivalentTo(ModEntry.GameContentTexturePath))
			{
				e.LoadFromModFile<Texture2D>(relativePath: $"{ModEntry.LocalTexturePath}.png", priority: AssetLoadPriority.Medium);
			}
			else if (e.NameWithoutLocale.IsEquivalentTo(@"Data/ObjectInformation"))
			{
				if (ModEntry.JsonAssets == null || Game1.currentLocation == null)
					return;

				e.Edit(apply: AssetManager.EditData);

			}
			else if (e.NameWithoutLocale.IsEquivalentTo(@"Strings/UI"))
			{
				if (ModEntry.JsonAssets == null || Game1.currentLocation == null)
					return;

				e.Edit(apply: AssetManager.EditStrings);
			}
		}

		private static void EditData(IAssetData asset)
		{
			IDictionary<int, string> data = asset.AsDictionary<int, string>().Data;

			// Add localised names and descriptions for new objects
			foreach (var pair in data.Where(pair => pair.Value.Split('/') is string[] split && split[0].StartsWith(ModEntry.AssetPrefix)).ToList())
			{
				string[] itemData = pair.Value.Split('/');
				string itemName = itemData[0].Split(new[] { '.' }, 3)[2];
				itemData[4] = i18n.Get("item." + itemName + ".name").ToString();
				itemData[5] = i18n.Get("item." + itemName + ".description").ToString();
				data[pair.Key] = string.Join("/", itemData);
			}

			asset.AsDictionary<int, string>().ReplaceWith(data);
		}

		private static void EditStrings(IAssetData asset)
		{
			IDictionary<string, string> data = asset.AsDictionary<string, string>().Data;

			// Add global chat message for gifts opened
			// Format message tokens so that they can be later tokenised by the game in multiplayer.globalChatInfoMessage()
			foreach (string i18nKey in new [] { "message.giftopened", "message.giftopened.quantity" })
			{
				data.Add("Chat_" + ModEntry.AssetPrefix + i18nKey,
					i18n.Get(i18nKey, new
					{
						Recipient = "{0}",
						Sender = "{1}",
						ItemName = "{2}",
						ItemQuantity = "{3}"
					}));
			}

			asset.AsDictionary<string, string>().ReplaceWith(data);
		}
	}
}
