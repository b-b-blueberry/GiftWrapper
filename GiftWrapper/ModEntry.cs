using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SpaceCore.Events;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Locations;
using StardewValley.Menus;
using StardewValley.Objects;
using StardewValley.Tools;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using xTile.Dimensions;
using Object = StardewValley.Object;

namespace GiftWrapper
{
	public sealed class ModEntry : Mod
	{
		internal static ModEntry Instance;
		internal static Config Config;
		internal static ITranslationHelper I18n => ModEntry.Instance.Helper.Translation;
		internal static IJsonAssetsAPI JsonAssets;


		public const string AssetPrefix = "blueberry.GiftWrapper.";
		public const string ItemPrefix = "blueberry.gw.";
		public const string GiftWrapName = "giftwrap";
		public const string WrappedGiftName = "wrappedgift";

		internal static readonly string GameContentDataPath = Path.Combine("Mods", ModEntry.AssetPrefix + "Assets", "Data");
		internal static readonly string GameContentMenuTexturePath = Path.Combine("Mods", ModEntry.AssetPrefix + "Assets", "Menu");
		internal static readonly string GameContentCardTexturePath = Path.Combine("Mods", ModEntry.AssetPrefix + "Assets", "Card");

		internal static readonly string LocalDataPath = Path.Combine("assets", "data");
		internal static readonly string LocalMenuTexturePath = Path.Combine("assets", "menu-{0}");
		internal static readonly string LocalCardTexturePath = Path.Combine("assets", "card");

		internal static readonly string ContentPackPath = Path.Combine("assets", "ContentPack");

		internal const int GiftWrapFriendshipBoost = 25;

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
				getValue: () => ModEntry.Config.AvailableAllYear,
				setValue: (bool value) => ModEntry.Config.AvailableAllYear = value);

			// Mouse buttons
			api.AddBoolOption(
				mod: this.ModManifest,
				name: () => ModEntry.I18n.Get("config.invertmousebuttons.name"),
				tooltip: () => ModEntry.I18n.Get("config.invertmousebuttons.description"),
				getValue: () => ModEntry.Config.InteractUsingToolButton,
				setValue: (bool value) => ModEntry.Config.InteractUsingToolButton = value);
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
		}

		private void OnMenuChanged(object sender, MenuChangedEventArgs e)
		{
			bool isWinterStarPeriod = Game1.currentSeason == "winter" && Game1.dayOfMonth >= 18;
			if (e.NewMenu is ShopMenu shop && shop.portraitPerson?.Name == "Pierre" && (ModEntry.Config.AvailableAllYear || isWinterStarPeriod))
			{
				// Gift wrap is purchaseable from Pierre throughout the Secret Friend gifting event
				int id = ModEntry.JsonAssets.GetObjectId(ModEntry.ItemPrefix + ModEntry.GiftWrapName);
				Object o = new(parentSheetIndex: id, initialStack: 1);
				float price = o.Price * Game1.MasterPlayer.difficultyModifier;
				if (Game1.dayOfMonth > 25)
				{
					// Sell gift wrap at clearance prices between the end of the event and the end of the year
					// For an 80g base price, clearance price is 30g
					price *= 0.4f;
				}
				shop.itemPriceAndStock.Add(o, new int[] { (int)(price * 0.2) * 5, int.MaxValue });
				if (isWinterStarPeriod)
				{
					// Gift wrap appears at the top of the shop stock over the Winter Star festival
					shop.forSale.Insert(0, o);
				}
				else
				{
					// If using the available-all-year config option, place gift wrap further down the list at other times of year
					ISalable item = shop.forSale.FirstOrDefault((ISalable i) => i.Name == "Bouquet") ?? shop.forSale.Last((ISalable i) => i.Name.EndsWith("Sapling"));
					if (item is not null)
					{
						int index = shop.forSale.IndexOf(item) + 1;
						shop.forSale.Insert(index, o);
					}
					else
					{
						shop.forSale.Add(o);
					}
				}
			}
		}

		private void OnButtonPressed(object sender, ButtonPressedEventArgs e)
		{
			if (!Context.CanPlayerMove)
				return;

			// Menu interactions
			if (Game1.activeClickableMenu is GameMenu gameMenu
				&& gameMenu.GetCurrentPage() is InventoryPage inventoryPage
				&& e.Button.IsActionButton())
			{
				Item hoverItem = inventoryPage.inventory.hover(
					x: Game1.getOldMouseX(),
					y: Game1.getOldMouseY(),
					heldItem: Game1.player.CursorSlotItem);
				if (ModEntry.IsWrappedGift(Game1.player.CursorSlotItem)
					&& Game1.player.CursorSlotItem.Category == -22
					&& hoverItem is not null
					&& hoverItem is FishingRod)
				{
					// Prevent tackle-method wrapped gifts from being attached to fishing rods with a tackle attachment slot
					this.Helper.Input.Suppress(e.Button);
					Game1.playSound("cancel");
					return;
				}
			}

			if (Game1.player.isRidingHorse()
				|| Game1.player.isInBed.Value
				|| Game1.player.temporarilyInvincible
				|| Game1.IsFading())
				return;

			Vector2 screenPos = new(x: e.Cursor.ScreenPixels.X, y: e.Cursor.ScreenPixels.Y);
			Location tilePos = new(x: (int)e.Cursor.ScreenPixels.X, y: (int)e.Cursor.ScreenPixels.Y);
			if (Game1.activeClickableMenu is not null // No active menus or onscreen menus
				|| Game1.onScreenMenus.Any((IClickableMenu menu) => menu.isWithinBounds(x: (int)screenPos.X, y: (int)screenPos.Y))
				|| Game1.currentLocation.checkAction(tileLocation: tilePos, viewport: Game1.viewport, who: Game1.player))
			{
				return;
			}

			// World interactions
			if (Game1.currentLocation.Objects.ContainsKey(e.Cursor.GrabTile)
				&& Game1.currentLocation.Objects[e.Cursor.GrabTile] is Object o)
			{
				if (ModEntry.IsGiftWrap(o))
				{
					// Open the gift wrap menu from placed gift wrap when left-clicking
					if (ModEntry.IsInteractButton(e.Button))
					{
						Game1.activeClickableMenu = new GiftWrapMenu(tile: e.Cursor.GrabTile);
					}
					else if (ModEntry.IsPlacementButton(e.Button))
					{
						if (Game1.player.couldInventoryAcceptThisItem(o))
						{
							Game1.player.addItemToInventory(o);
							Game1.playSound("pickUpItem");
							Game1.currentLocation.Objects.Remove(e.Cursor.GrabTile);
						}
						else
						{
							Game1.playSound("cancel");
						}
					}
				}
				else if (ModEntry.IsWrappedGift(o))
				{
					// Unwrap the placed gift and pop out the actual gift when left-clicking
					if (ModEntry.IsInteractButton(e.Button))
					{
						// Add actual gift to inventory and remove wrapped gift object
						Item actualGift = ModEntry.UnpackItem(modData: o.modData, recipientName: Game1.player.Name);
						if (actualGift is null || Game1.createItemDebris(item: actualGift, origin: Game1.player.Position, direction: -1) is not null)
						{
							Game1.currentLocation.playSound("getNewSpecialItem");
							Game1.currentLocation.Objects.Remove(e.Cursor.GrabTile);
						}
						else
						{
							Game1.playSound("cancel");
							this.Monitor.Log($"Couldn't open the {o.Name} at {e.Cursor.GrabTile} (gift is a {actualGift.Name})", LogLevel.Debug);
						}
					}
					else if (ModEntry.IsPlacementButton(e.Button))
					{
						Object wrappedGift = ModEntry.GetWrappedGift(o.modData);
						if (Game1.player.addItemToInventory(item: wrappedGift) is null)
						{
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
				// Place held gift wrap and wrapped gifts on the ground when left-clicking
				bool isPlaceableTile = Game1.currentLocation.isTileLocationTotallyClearAndPlaceableIgnoreFloors(e.Cursor.GrabTile)
					&& !Game1.currentLocation.Objects.ContainsKey(e.Cursor.GrabTile);
				bool isPlaceableLocation =
					!(Game1.currentLocation is Mine
						|| Game1.currentLocation.Name.StartsWith("UndergroundMine")
						|| Game1.currentLocation.isTemp());
				if (ModEntry.IsPlacementButton(e.Button)
					&& isPlaceableTile
					&& (ModEntry.IsGiftWrap(Game1.player.ActiveObject) || ModEntry.IsWrappedGift(Game1.player.ActiveObject)))
				{
					if (!isPlaceableLocation)
					{
						Game1.showRedMessage(ModEntry.I18n.Get("error.location"));
						return;
					}
					const string placementSound = "throwDownITem"; // not a typo
					if (ModEntry.IsGiftWrap(Game1.player.ActiveObject))
					{
						this.Helper.Input.Suppress(e.Button);
						Game1.playSound(placementSound); 
						Game1.currentLocation.Objects[e.Cursor.GrabTile] = Game1.player.ActiveObject.getOne() as Object;
						--Game1.player.ActiveObject.Stack;
						if (Game1.player.ActiveObject.Stack < 1)
						{
							Game1.player.removeItemFromInventory(Game1.player.ActiveObject);
						}
					}
					else if (ModEntry.IsWrappedGift(Game1.player.ActiveObject))
					{
						this.Helper.Input.Suppress(e.Button);
						ModEntry.PlaceWrappedGift(
							wrappedGift: Game1.player.CurrentItem,
							location: Game1.currentLocation,
							tilePosition: e.Cursor.GrabTile,
							sound: placementSound);
						return;
					}
				}
			}
		}
		
		private void OnGiftGiven(object sender, EventArgsBeforeReceiveObject e)
		{
			// Ignore NPC gifts that aren't going to be accepted
			if (!e.Npc.canReceiveThisItemAsGift(e.Gift)
				|| !Game1.player.friendshipData.ContainsKey(e.Npc.Name)
				|| Game1.player.friendshipData[e.Npc.Name].GiftsThisWeek > 1
				|| Game1.player.friendshipData[e.Npc.Name].GiftsToday > 0)
			{
				return;
			}

			if (ModEntry.IsWrappedGift(e.Gift))
			{
				// Cancel the wrapped gift NPC gift
				e.Cancel = true;

				Item actualGift = ModEntry.UnpackItem(modData: e.Gift.modData, recipientName: null);
				if (actualGift is not Object o || o.bigCraftable.Value || !o.canBeGivenAsGift() || actualGift.Stack > 1)
				{
					// Ignore actual gifts that are invalid NPC gifts, eg. Tools
					// Ignore actual gifts wrapped as part of large stacks, as items are typically only able to be given as gifts one-at-a-time
					Game1.showRedMessage(message: Game1.content.LoadString("Strings\\StringsFromCSFiles:Event.cs.1803"));
					Game1.playSound("cancel");
					return;
				}

				// Redeliver the NPC gift as the actual gift
				e.Npc.receiveGift(
					o: actualGift as Object,
					giver: Game1.player,
					updateGiftLimitInfo: true,
					friendshipChangeMultiplier: 1,
					showResponse: true);

				// Add bonus friendship for having given them a wrapped gift
				Game1.player.changeFriendship(amount: ModEntry.GiftWrapFriendshipBoost, n: e.Npc);

				// Remove wrapped gift from inventory
				Game1.player.removeItemFromInventory(e.Gift);
			}
		}

		internal static string GetThemedTexturePath()
		{
			return string.Format(ModEntry.LocalMenuTexturePath, ModEntry.Config.Theme);
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
		
		public static bool IsInteractButton(SButton button)
		{
			return (button.IsActionButton() && !ModEntry.Config.InteractUsingToolButton)
				|| (button.IsUseToolButton() && ModEntry.Config.InteractUsingToolButton);
		}

		public static bool IsPlacementButton(SButton button)
		{
			return (button.IsUseToolButton() && !ModEntry.Config.InteractUsingToolButton)
				|| (button.IsActionButton() && ModEntry.Config.InteractUsingToolButton);
		}

		public static Object GetWrappedGift(ModDataDictionary modData)
		{
			// Object-based solution for wrapped gifts:
			Object wrappedGift = new(parentSheetIndex: ModEntry.JsonAssets.GetObjectId(ModEntry.ItemPrefix + ModEntry.WrappedGiftName), initialStack: 1)
			{
				// Tackle category: Cannot be stacked higher than 1, which solves the issue of modData folding.
				// The unfortunate side-effect is that they can, however, be attached to rods. We'll sort this out in ButtonPressed.
				Category = -22
			};
			if (modData is not null)
			{
				wrappedGift.modData = modData;
			}
			return wrappedGift;
		}

		public static bool PlaceWrappedGift(Item wrappedGift, GameLocation location, Vector2 tilePosition, string sound)
		{
			if (!string.IsNullOrEmpty(sound))
			{
				Game1.playSound(sound);
			}

			if (wrappedGift is null || location is null || (location.Objects.ContainsKey(tilePosition) && location.Objects[tilePosition] is not null))
			{
				return false;
			}

			Object placedGift = new(parentSheetIndex: ModEntry.JsonAssets.GetObjectId(ModEntry.ItemPrefix + ModEntry.WrappedGiftName), initialStack: 1);
			if (wrappedGift.modData is not null)
			{
				placedGift.modData = wrappedGift.modData;
			}

			location.Objects[tilePosition] = placedGift;

			if (Game1.player.Items.Contains(wrappedGift))
			{
				Game1.player.removeItemFromInventory(wrappedGift);
			}

			return true;
		}

		public static void PackItem(ref Object wrappedGift, Item giftToWrap, Vector2 placedGiftWrapPosition, bool showMessage)
		{
			if (Game1.player.couldInventoryAcceptThisItem(wrappedGift))
			{
				bool isDefined = Enum.IsDefined(typeof(GiftType), giftToWrap.GetType().Name);
				bool isBigCraftable = giftToWrap is Object bc && bc.bigCraftable.Value;
				if (!isDefined)
				{
					// Avoid adding items with undefined behaviour
					Game1.showRedMessage(ModEntry.I18n.Get("error.wrapping", new { ItemName = wrappedGift.DisplayName }));
					wrappedGift = null;
					return;
				}

				// Define all the data to be serialised into the wrapped gift's modData
				long giftSender = Game1.player.UniqueMultiplayerID;
				string giftName = giftToWrap.Name;
				int giftId = giftToWrap is Hat hat
					? hat.which.Value
					: giftToWrap is MeleeWeapon weapon
						? Game1.content.Load<Dictionary<int, string>>(@"Data/weapons").First(pair => pair.Value.Split('/')[0] == weapon.Name).Key
						: giftToWrap is Boots boots
							? boots.indexInTileSheet.Value
							: giftToWrap.ParentSheetIndex;
				int giftParentId = giftToWrap is Object o
					? o.preservedParentSheetIndex.Value
					: -1;
				int giftType = isBigCraftable
					? (int)GiftType.BigCraftable
					: isDefined
						? (int)Enum.Parse(typeof(GiftType), giftToWrap.GetType().Name)
						: -1;
				int giftStack = giftToWrap is Object
					? giftToWrap.Stack
					: -1;
				int giftQuality = giftToWrap is Object o1
					? o1.Quality
					: giftToWrap is Boots boots1
						? boots1.appliedBootSheetIndex.Value
						: -1;
				int giftPreserve = giftToWrap is Object o2
					? o2.preserve.Value.HasValue ? (int)o2.preserve.Value : -1
					: -1;
				int giftHoney = giftToWrap is Object o3 // We use 0 for honeyType as HoneyType.Wild == -1.
					? o3.honeyType.Value.HasValue ? (int)o3.honeyType.Value.Value : 0
					: 0;
				string giftColour = giftToWrap is Clothing c
					? string.Join("/", new [] { c.clothesColor.Value.R, c.clothesColor.Value.G, c.clothesColor.Value.B, c.clothesColor.Value.A })
					: giftToWrap is Boots boots2
						? boots2.indexInColorSheet.ToString()
						: "-1";

				// Convert the gift item's modData into a serialisable form to be added to the wrapped gift's modData
				Dictionary<string, string> giftDataRaw = new();
				foreach (var pair in giftToWrap.modData.FieldDict)
					giftDataRaw.Add(pair.Key, pair.Value.Value);
				string giftDataSerialised = JsonConvert.SerializeObject(giftDataRaw);

				if (Game1.currentLocation.Objects.Remove(placedGiftWrapPosition))
				{
					// Add all fields into wrapped gift's modData
					wrappedGift.modData[ModEntry.ItemPrefix + "giftsender"] = giftSender.ToString();
					wrappedGift.modData[ModEntry.ItemPrefix + "giftname"] = giftName;
					wrappedGift.modData[ModEntry.ItemPrefix + "giftid"] = giftId.ToString();
					wrappedGift.modData[ModEntry.ItemPrefix + "giftparentid"] = giftParentId.ToString();
					wrappedGift.modData[ModEntry.ItemPrefix + "gifttype"] = giftType.ToString();
					wrappedGift.modData[ModEntry.ItemPrefix + "giftstack"] = giftStack.ToString();
					wrappedGift.modData[ModEntry.ItemPrefix + "giftquality"] = giftQuality.ToString();
					wrappedGift.modData[ModEntry.ItemPrefix + "giftpreserve"] = giftPreserve.ToString();
					wrappedGift.modData[ModEntry.ItemPrefix + "gifthoney"] = giftHoney.ToString();
					wrappedGift.modData[ModEntry.ItemPrefix + "giftcolour"] = giftColour;
					wrappedGift.modData[ModEntry.ItemPrefix + "giftdata"] = giftDataSerialised;

					if (showMessage)
					{
						string message = ModEntry.I18n.Get("message.giftwrapped", new
							{
								WrappedGiftName = wrappedGift.DisplayName,
								ItemName = giftToWrap.DisplayName
							});
						Game1.addHUDMessage(new HUDMessage(type: "", number: 1, add: true, color: Game1.textColor, messageSubject: wrappedGift));
					}
				}
			}
		}

		public static Item UnpackItem(ModDataDictionary modData, string recipientName)
		{
			string[] fields = new[] { 
				"giftsender", "giftname", "giftid", 
				"giftparentid", "gifttype", "giftstack", 
				"giftquality", "giftpreserve", "gifthoney",
				"giftcolour", "giftdata" };
			if (fields.Any((string field) => !modData.ContainsKey(ModEntry.ItemPrefix + field)))
			{
				string msg = fields.Where((string field) => !modData.ContainsKey(field))
					.Aggregate("This gift is missing data:", (string str, string field) => $"{str}\n{field}")
					+ "\nIf this gift was placed before updating, please revert to the previous version and collect the gift!"
					+ "\nOtherwise, leave a report on the mod page for Gift Wrapper with your log file (https://smapi.io/log).";
				ModEntry.Instance.Monitor.Log(msg, LogLevel.Warn);
				return null;
			}

			// Parse the wrapped gift's serialised modData fields to use in rebuilding its gift item
			long giftSender = long.Parse(modData[ModEntry.ItemPrefix + fields[0]]);
			string giftName = modData[ModEntry.ItemPrefix + fields[1]];
			int giftId = int.Parse(modData[ModEntry.ItemPrefix + fields[2]]);
			int giftParentId = int.Parse(modData[ModEntry.ItemPrefix + fields[3]]);
			int giftType = int.Parse(modData[ModEntry.ItemPrefix + fields[4]]);
			int giftStack = int.Parse(modData[ModEntry.ItemPrefix + fields[5]]);
			int giftQuality = int.Parse(modData[ModEntry.ItemPrefix + fields[6]]);
			int giftPreserve = int.Parse(modData[ModEntry.ItemPrefix + fields[7]]);
			int giftHoney = int.Parse(modData[ModEntry.ItemPrefix + fields[8]]);
			string giftColour = modData[ModEntry.ItemPrefix + fields[9]];
			string giftData = modData[ModEntry.ItemPrefix + fields[10]];
			Item actualGift = null;
			switch (giftType)
			{
				case (int)GiftType.BedFurniture:
					actualGift = new BedFurniture(which: giftId, tile: Vector2.Zero);
					break;
				case (int)GiftType.Furniture:
					actualGift = new Furniture(which: giftId, tile: Vector2.Zero);
					break;
				case (int)GiftType.BigCraftable:
					actualGift = new Object(tileLocation: Vector2.Zero, parentSheetIndex: giftId, isRecipe: false);
					break;
				case (int)GiftType.MeleeWeapon:
					actualGift = new MeleeWeapon(spriteIndex: giftId);
					break;
				case (int)GiftType.Hat:
					actualGift = new Hat(which: giftId);
					break;
				case (int)GiftType.Boots:
					actualGift = new Boots(which: giftId); // todo: test boots colour
					((Boots)actualGift).appliedBootSheetIndex.Set(giftQuality);
					((Boots)actualGift).indexInColorSheet.Set(int.Parse(giftColour));
					break;
				case (int)GiftType.Clothing:
					int[] colourSplit = giftColour.Split('/').ToList().ConvertAll(int.Parse).ToArray();
					Color colour = new(r: colourSplit[0], g: colourSplit[1], b: colourSplit[2], alpha: colourSplit[3]);
					actualGift = new Clothing(item_index: giftId);
					((Clothing)actualGift).clothesColor.Set(colour);
					break;
				case (int)GiftType.Ring:
					actualGift = new Ring(which: giftId);
					break;
				case (int)GiftType.Object:
					actualGift = new Object(parentSheetIndex: giftId, initialStack: giftStack)
					{
						Quality = giftQuality,
						Name = giftName
					};
					if (giftParentId != -1)
						((Object)actualGift).preservedParentSheetIndex.Value = giftParentId;
					if (giftPreserve != -1)
						((Object)actualGift).preserve.Value = (Object.PreserveType)giftPreserve;
					if (giftHoney != 0)
						((Object)actualGift).honeyType.Value = (Object.HoneyType)giftHoney;
					break;
			}

			if (actualGift is null)
			{
				return null;
			}

			var giftDataDeserialised = ((JObject)JsonConvert.DeserializeObject(giftData)).ToObject<Dictionary<string, string>>();
			if (giftDataDeserialised is not null)
			{
				// Apply serialised mod data back to the gifted item
				actualGift.modData.Set(giftDataDeserialised);
			}

			if (recipientName is not null && Game1.player.UniqueMultiplayerID != giftSender)
			{
				// Show a message to all players to celebrate this wonderful event
				Multiplayer multiplayer = ModEntry.Instance.Helper.Reflection.GetField<Multiplayer>(typeof(Game1), "multiplayer").GetValue();
				multiplayer.globalChatInfoMessage(ModEntry.ItemPrefix + (giftStack > 1 ? "message.giftopened_quantity" : "message.giftopened"),
					recipientName, // Recipient's name
					Game1.getFarmer(giftSender).Name, // Sender's name
					actualGift.DisplayName, // Gift name
					giftStack.ToString());	// Gift quantity
			}

			return actualGift;
		}
	}
}
