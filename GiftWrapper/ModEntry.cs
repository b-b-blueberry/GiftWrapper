using GiftWrapper.Data;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Graphics;
using SpaceCore.Events;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Locations;
using StardewValley.Menus;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using xTile.Dimensions;
using Object = StardewValley.Object;
using HarmonyLib; // el diavolo nuevo

namespace GiftWrapper
{
	public sealed class ModEntry : Mod
	{
		internal static ModEntry Instance;
		internal static Config Config;
		internal static ITranslationHelper I18n => ModEntry.Instance.Helper.Translation;
		internal static IJsonAssetsAPI JsonAssets;

		internal static Dictionary<string, Lazy<Texture2D>> GiftSprites { get; private set; }

		public const string AssetPrefix = "blueberry.GiftWrapper.";
		public const string ItemPrefix = "blueberry.gw.";
		public const string GiftWrapName = "giftwrap";
		public const string WrappedGiftName = "wrappedgift";

		internal static readonly string GameContentDataPath = Path.Combine("Mods", ModEntry.AssetPrefix + "Assets", "Data");
		internal static readonly string GameContentGiftTexturePath = Path.Combine("Mods", ModEntry.AssetPrefix + "Assets", "Gifts");
		internal static readonly string GameContentMenuTexturePath = Path.Combine("Mods", ModEntry.AssetPrefix + "Assets", "Menu");
		internal static readonly string GameContentCardTexturePath = Path.Combine("Mods", ModEntry.AssetPrefix + "Assets", "Card");

		internal static readonly string LocalDataPath = Path.Combine("assets", "data");
		internal static readonly string LocalGiftTexturePath = Path.Combine("assets", "gifts");
		internal static readonly string LocalMenuTexturePath = Path.Combine("assets", "menu-{0}");
		internal static readonly string LocalCardTexturePath = Path.Combine("assets", "card");

		internal static readonly string ContentPackPath = Path.Combine("assets", "ContentPack");
		internal static string LocalAudioPath => Path.Combine(ModEntry.Instance.Helper.DirectoryPath, "assets", "audio");


		internal enum GiftType
		{
			BedFurniture,
			Furniture,
			BigCraftable,
			MeleeWeapon,
			Hat,
			Boots,
			Clothing,
			Ring,
			Object
		}

		public override void Entry(IModHelper helper)
		{
			ModEntry.Instance = this;
			ModEntry.Config = this.Helper.ReadConfig<Config>();

			if (!Enum.IsDefined(typeof(Config.Themes), ModEntry.Config.Theme))
			{
				// Pick random theme if theme not picked or defined
				List<Config.Themes> themes = Enum.GetValues(typeof(Config.Themes)).Cast<Config.Themes>().ToList();
				ModEntry.Config.Theme = themes[Game1.random.Next(themes.Count)];
			}

			this.Helper.Events.GameLoop.GameLaunched += this.OnGameLaunched;
		}

		private bool TryLoadApis()
		{
			// SpaceCore setup
			try
			{
				ISpaceCoreAPI spacecoreApi = this.Helper.ModRegistry
					.GetApi<ISpaceCoreAPI>
					("spacechase0.SpaceCore");
				spacecoreApi.RegisterSerializerType(typeof(GiftItem));
			}
			catch (Exception e)
			{
				this.Monitor.Log($"Failed to register objects with SpaceCore.{Environment.NewLine}{e}", LogLevel.Error);
				return false;
			}

			// Add Json Assets items
			ModEntry.JsonAssets = this.Helper.ModRegistry.GetApi<IJsonAssetsAPI>("spacechase0.JsonAssets");
			if (ModEntry.JsonAssets is null)
			{
				this.Monitor.Log("Can't access the Json Assets API. Is the mod installed correctly?", LogLevel.Error);
				return false;
			}
			ModEntry.JsonAssets.LoadAssets(path: Path.Combine(this.Helper.DirectoryPath, ModEntry.ContentPackPath));

			// Add GMCM config page
			this.RegisterGenericModConfigMenuPage();

			return true;
		}

		private void RegisterGenericModConfigMenuPage()
		{
			IGenericModConfigMenuAPI api = this.Helper.ModRegistry.GetApi<IGenericModConfigMenuAPI>("spacechase0.GenericModConfigMenu");
			if (api is null)
				return;

			Dictionary<Config.Themes, Translation> themes = Enum
				.GetValues(typeof(Config.Themes))
				.Cast<Config.Themes>()
				.ToDictionary(
					value => value,
					value => ModEntry.I18n.Get("config.theme." + (int)value));

			api.Register(
				mod: this.ModManifest,
				reset: () => ModEntry.Config = new Config(),
				save: () => this.Helper.WriteConfig(ModEntry.Config));

			// Themes
			api.AddTextOption(
				mod: this.ModManifest,
				name: () => ModEntry.I18n.Get("config.theme.name"),
				tooltip: () => ModEntry.I18n.Get("config.theme.description"),
				getValue: () => themes[ModEntry.Config.Theme],
				setValue: (string theme) =>
				{
					ModEntry.Config.Theme = (Config.Themes)Enum.Parse(typeof(Config.Themes), theme);
					ModEntry.Instance.Helper.GameContent.InvalidateCache(ModEntry.GameContentMenuTexturePath);
				},
				allowedValues: themes.Keys.Select((Config.Themes key) => key.ToString()).ToArray(),
				formatAllowedValue: (string theme) => themes[(Config.Themes)Enum.Parse(typeof(Config.Themes), theme)]);

			// Availability
			api.AddBoolOption(
				mod: this.ModManifest,
				name: () => ModEntry.I18n.Get("config.availableallyear.name"),
				tooltip: () => ModEntry.I18n.Get("config.availableallyear.description"),
				getValue: () => ModEntry.Config.AlwaysAvailable,
				setValue: (bool value) => ModEntry.Config.AlwaysAvailable = value);

			// Tooltip enabled
			api.AddBoolOption(
				mod: this.ModManifest,
				name: () => ModEntry.I18n.Get("config.giftpreviewtileenabled.name"),
				tooltip: () => ModEntry.I18n.Get("config.giftpreviewtileenabled.description"),
				getValue: () => ModEntry.Config.GiftPreviewEnabled,
				setValue: (bool value) => ModEntry.Config.GiftPreviewEnabled = value);

			// Tooltip range
			api.AddNumberOption(
				mod: this.ModManifest,
				name: () => ModEntry.I18n.Get("config.giftpreviewtilerange.name"),
				tooltip: () => ModEntry.I18n.Get("config.giftpreviewtilerange.description"),
				getValue: () => ModEntry.Config.GiftPreviewTileRange,
				setValue: (int value) => ModEntry.Config.GiftPreviewTileRange = value);

			// Tooltip fade
			api.AddNumberOption(
				mod: this.ModManifest,
				name: () => ModEntry.I18n.Get("config.giftpreviewfadespeed.name"),
				tooltip: () => ModEntry.I18n.Get("config.giftpreviewfadespeed.description"),
				getValue: () => ModEntry.Config.GiftPreviewFadeSpeed,
				setValue: (int value) => ModEntry.Config.GiftPreviewFadeSpeed = value,
				min: 1,
				max: 20,
				interval: 1);
		}

		private void OnGameLaunched(object sender, GameLaunchedEventArgs e)
		{
			if (!this.TryLoadApis())
			{
				this.Monitor.Log("Failed to load required mods. Mod will not be loaded.", LogLevel.Error);
				return;
			}

			// Event handlers
			this.Helper.Events.Content.AssetRequested += AssetManager.OnAssetRequested;
			this.Helper.Events.Input.ButtonPressed += this.OnButtonPressed;
			this.Helper.Events.Display.MenuChanged += this.OnMenuChanged;
			SpaceEvents.BeforeGiftGiven += this.OnGiftGiven;

			// Gift data
			Data.Data data = this.Helper.GameContent.Load<Data.Data>(ModEntry.GameContentDataPath);
			GiftItem.ReloadDefinitions(data.Definitions);
			ModEntry.GiftSprites = data.Styles.Values
				.Select((Style style) => style.Texture ?? ModEntry.GameContentGiftTexturePath)
				.Distinct()
				.ToList()
				.ToDictionary(
					(string path) => path,
					(string path) => new Lazy<Texture2D>(() => ModEntry.Instance.Helper.GameContent.Load<Texture2D>(path)));

			// Audio
			foreach (string id in data.Audio.Keys)
			{
				SoundEffect[] sounds = data.Audio[id].Select((string path) =>
				{
					path = Path.Combine(ModEntry.LocalAudioPath, $"{path}.wav");
					using FileStream stream = new(path, FileMode.Open);
					return SoundEffect.FromStream(stream);
				}).ToArray();
				CueDefinition cue = new(
					cue_name: id,
					sound_effects: sounds,
					category_id: Game1.audioEngine.GetCategoryIndex("Sound"));
				Game1.soundBank.AddCue(cue);
			}
			
			// Patches
			Harmony harmony = new(id: this.ModManifest.UniqueID);
			harmony.Patch(
				original: AccessTools.Method(type: typeof(Event), name: nameof(Event.chooseSecretSantaGift)),
				prefix: new HarmonyMethod(methodType: typeof(ModEntry), methodName: nameof(ModEntry.TrySecretSantaGift)));
		}

		/// <summary>
		/// Interactions for shop menus.
		/// </summary>
		private void OnMenuChanged(object sender, MenuChangedEventArgs e)
		{
			// Add items to shop stock
			if (e.NewMenu is ShopMenu menu && ModEntry.IsShopAllowed(menu: menu, location: Game1.currentLocation) is Shop shop)
			{
				ModEntry.AddToShop(menu: menu, shop: shop, item: ModEntry.GetWrapItem());
			}
		}

		/// <summary>
		/// Interactions for wrapping paper item.
		/// </summary>
		private void OnButtonPressed(object sender, ButtonPressedEventArgs e)
		{
			if (!Context.CanPlayerMove
				|| Game1.player.isRidingHorse()
				|| Game1.player.isInBed.Value
				|| Game1.player.temporarilyInvincible
				|| Game1.IsFading()
				|| Game1.activeClickableMenu is not null
				|| (new Location(x: (int)e.Cursor.ScreenPixels.X, y: (int)e.Cursor.ScreenPixels.Y)
						is Location point
					&& Game1.onScreenMenus.Any((IClickableMenu menu)
						=> menu.isWithinBounds(x: point.X, y: point.Y))
					&& Game1.currentLocation.checkAction(
						tileLocation: (Game1.viewport.Location + point) / Game1.tileSize,
						viewport: Game1.viewport,
						who: Game1.player)))
				return;

			// World interactions
			if (Game1.currentLocation.Objects.ContainsKey(e.Cursor.GrabTile)
				&& Game1.currentLocation.Objects[e.Cursor.GrabTile] is Object o)
			{
				if (ModEntry.IsGiftWrap(o))
				{
					// Open the gift wrap menu from placed gift wrap when left-clicking
					if (e.Button.IsActionButton())
					{
						Game1.activeClickableMenu = new GiftWrapMenu(tile: e.Cursor.GrabTile);
					}
					else if (e.Button.IsUseToolButton())
					{
						if (Game1.player.addItemToInventoryBool(o))
						{
							Game1.playSound("pickUpItem");
							Game1.currentLocation.Objects.Remove(e.Cursor.GrabTile);
						}
						else
						{
							Game1.playSound("cancel");
						}
					}
				}
			}
			else
			{
				// Place held gift wrap on the ground when left-clicking
				if (e.Button.IsUseToolButton()
					&& ModEntry.IsTileAllowed(Game1.currentLocation, e.Cursor.GrabTile)
					&& ModEntry.IsGiftWrap(Game1.player.ActiveObject))
				{
					if (!ModEntry.IsLocationAllowed(Game1.currentLocation))
					{
						Game1.showRedMessage(ModEntry.I18n.Get("error.location"));
						return;
					}
					const string placementSound = "throwDownITem"; // not a typo
					this.Helper.Input.Suppress(e.Button);
					Game1.playSound(placementSound); 
					Game1.currentLocation.Objects[e.Cursor.GrabTile] = Game1.player.ActiveObject.getOne() as Object;
					if (--Game1.player.ActiveObject.Stack < 1)
					{
						Game1.player.removeItemFromInventory(Game1.player.ActiveObject);
					}
				}
			}
		}
		
		/// <summary>
		/// Interactions for wrapped gifts.
		/// </summary>
		private void OnGiftGiven(object sender, EventArgsBeforeReceiveObject e)
		{
			// Ignore NPC gifts that aren't going to be accepted
			if (!ModEntry.IsNpcAllowed(player: Game1.player, npc: e.Npc, gift: e.Gift))
				return;

			if (e.Gift is GiftItem gift)
			{
				// Cancel the wrapped gift NPC gift
				e.Cancel = true;

				Definitions definitions = this.Helper.GameContent.Load<Data.Data>(ModEntry.GameContentDataPath).Definitions;

				Item item = gift.ItemInGift.Value;
				if (!ModEntry.IsNpcGiftAllowed(item))
				{
					// Ignore actual gifts that are invalid NPC gifts, eg. Tools
					// Ignore actual gifts wrapped as part of large stacks, as items are typically only able to be given as gifts one-at-a-time
					Game1.showRedMessage(message: Game1.content.LoadString(definitions.InvalidGiftStringPath));
					Game1.playSound(definitions.InvalidGiftSound);
					return;
				}

				// Redeliver the NPC gift as the actual gift
				e.Npc.receiveGift(
					o: item as Object,
					giver: Game1.player,
					updateGiftLimitInfo: true,
					friendshipChangeMultiplier: definitions.AddedFriendship,
					showResponse: true);

				// Remove wrapped gift from inventory
				Game1.player.removeItemFromInventory(e.Gift);
			}
		}

		/// <summary>
		/// Harmony prefix method.
		/// Allows gifted items to be gifted to the player's secret friend during the Winter Star event.
		/// </summary>
		/// <param name="i">Item chosen as a gift.</param>
		/// <param name="who">Player choosing the gift.</param>
		private static void TrySecretSantaGift(ref Item i, Farmer who)
		{
			if (i is GiftItem gift)
			{
				if (gift.IsItemInside && Utility.highlightSantaObjects(i: gift.ItemInGift.Value))
				{
					// Unwrap valid gifts before given to the player's secret friend
					i = gift.ItemInGift.Value;
				}
			}
		}

		/// <summary>
		/// Adds an item to a given shop menu with properties from a given shop entry. 
		/// </summary>
		/// <param name="menu">Shop menu to add to.</param>
		/// <param name="shop">Shop entry with sale data.</param>
		/// <param name="item">Item to add to shop.</param>
		public static void AddToShop(ShopMenu menu, Shop shop, ISalable item)
		{
			const int priceRounding = 5;
			float price = item.salePrice() * shop.PriceMultiplier;
			int priceRounded = (int)(price * (1f / priceRounding)) * priceRounding;
			int index = shop.AddAtItem?.FirstOrDefault((string name)
				=> menu.forSale.Any((ISalable i) => i.Name == name))
				is string name ? menu.forSale.FindIndex((ISalable i) => i.Name == name) + 1 : 0;

			menu.itemPriceAndStock.Add(item, new int[] { priceRounded, int.MaxValue });
			if (index >= 0)
				menu.forSale.Insert(index, item);
			else
				menu.forSale.Add(item);
		}

		public static string GetThemedTexturePath()
		{
			return string.Format(ModEntry.LocalMenuTexturePath, ModEntry.Config.Theme);
		}

		public static Texture2D GetStyleTexture(Style style)
		{
			return ModEntry.GiftSprites[style.Texture ?? ModEntry.GameContentGiftTexturePath].Value;
		}

		public static Object GetWrapItem(int stack = 1)
		{
			int id = ModEntry.JsonAssets.GetObjectId(ModEntry.ItemPrefix + ModEntry.GiftWrapName);
			return new(parentSheetIndex: id, initialStack: stack);
		}

		public static bool IsGiftWrap(Item item)
		{
			return item?.Name == ModEntry.ItemPrefix + ModEntry.GiftWrapName;
		}

		public static bool IsWrappedGift(Item item)
		{
			return item?.Name == ModEntry.ItemPrefix + ModEntry.WrappedGiftName;
		}

		public static bool IsItemAllowed(Item item)
		{
			return item is not (null or WrapItem or GiftItem) && item.canBeTrashed();
		}

		public static bool IsLocationAllowed(GameLocation location)
		{
			return location is not (null or Mine or MineShaft or VolcanoDungeon or BeachNightMarket or MermaidHouse or AbandonedJojaMart)
				&& !location.isTemp();
		}

		public static bool IsTileAllowed(GameLocation location, Vector2 tile)
		{
			return location.isTileLocationTotallyClearAndPlaceableIgnoreFloors(tile)
				&& !location.Objects.ContainsKey(tile)
				&& location.isCharacterAtTile(tile) is null
				&& location.isTileOccupiedByFarmer(tile) is null;
		}

		public static bool IsNpcAllowed(Farmer player, NPC npc, Item gift)
		{
			return npc.canReceiveThisItemAsGift(i: gift)
				&& player.friendshipData.TryGetValue(npc.Name, out Friendship data)
				&& data.GiftsThisWeek < 2
				&& data.GiftsToday == 0;
		}

		public static bool IsNpcGiftAllowed(Item item)
		{
			return item is Object o && o.canBeGivenAsGift() && o.Stack == 1;
		}

		public static Shop IsShopAllowed(ShopMenu menu, GameLocation location)
		{
			Data.Data data = ModEntry.Instance.Helper.GameContent.Load<Data.Data>(ModEntry.GameContentDataPath);
			int id = data.Definitions.EventConditionId;
			return data.Shops.FirstOrDefault((Shop shop)
				=> (shop.Context is null
					|| shop.Context == menu.storeContext
					|| shop.Context == menu.portraitPerson?.Name)
				&& (shop.Conditions is null
					|| shop.Conditions.Length == 0
					|| shop.Conditions.Any((string s) => location.checkEventPrecondition($"{id}/{s}") == id))
				&& shop.IfAlwaysAvailable == ModEntry.Config.AlwaysAvailable);
		}
	}
}
