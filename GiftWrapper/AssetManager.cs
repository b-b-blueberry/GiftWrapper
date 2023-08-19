using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using System.Linq;

namespace GiftWrapper
{
	public static class AssetManager
	{
		public static void OnAssetRequested(object sender, AssetRequestedEventArgs e)
		{
			if (e.NameWithoutLocale.IsEquivalentTo(ModEntry.GameContentTexturePath))
			{
				e.LoadFromModFile<Texture2D>(relativePath: $"{ModEntry.LocalTexturePath}.png", priority: AssetLoadPriority.Medium);
			}
			else if (e.NameWithoutLocale.IsEquivalentTo(@"Data/ObjectInformation"))
			{
				if (ModEntry.JsonAssets is null || Game1.currentLocation is null)
					return;

				e.Edit(apply: AssetManager.EditData);

			}
			else if (e.NameWithoutLocale.IsEquivalentTo(@"Strings/UI"))
			{
				if (ModEntry.JsonAssets is null || Game1.currentLocation is null)
					return;

				e.Edit(apply: AssetManager.EditStrings);
			}
		}

		private static void EditData(IAssetData asset)
		{
			var data = asset.AsDictionary<int, string>().Data;

			// Add localised names and descriptions for new objects
			foreach (var pair in data.Where(pair => pair.Value.Split('/') is string[] split && split[0].StartsWith(ModEntry.AssetPrefix)).ToList())
			{
				string[] fields = pair.Value.Split('/');
				string name = fields[0].Split(separator: new[] { '.' }, count: 3)[2];
				fields[4] = ModEntry.I18n.Get($"item.{name}.name").ToString();
				fields[5] = ModEntry.I18n.Get($"item.{name}.description").ToString();
				data[pair.Key] = string.Join("/", fields);
			}

			asset.AsDictionary<int, string>().ReplaceWith(data);
		}

		private static void EditStrings(IAssetData asset)
		{
			var data = asset.AsDictionary<string, string>().Data;

			// Add global chat message for gifts opened
			// Format message tokens so that they can be later tokenised by the game in multiplayer.globalChatInfoMessage()
			foreach (string key in new [] { "message.giftopened", "message.giftopened.quantity" })
			{
				data.Add($"Chat_{ModEntry.AssetPrefix}{key}",
					ModEntry.I18n.Get(key, new
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
