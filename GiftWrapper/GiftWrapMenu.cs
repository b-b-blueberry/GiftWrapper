using GiftWrapper.Data;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;
using System;
using System.Collections.Generic;
using System.Linq;
using Colour = Microsoft.Xna.Framework.Color;

namespace GiftWrapper
{
	public class GiftWrapMenu : ItemGrabMenu
	{
		// Items

		/// <summary>
		/// Clickable container to display items placed by the user for turning into wrapped gifts.
		/// </summary>
		public readonly ClickableTextureComponent ItemSlot;
		/// <summary>
		/// Contextual clickable button to confirm the gift wrap action,
		/// revealed when there are items to be wrapped.
		/// </summary>
		public readonly ClickableTextureComponent WrapButton;
		/// <summary>
		/// Tile position of the placed gift wrap item used to open this menu at the current game location,
		/// passed to ModEntry.Instance.PackItem() to remove from the world objects list.
		/// </summary>
		public readonly Vector2 GiftWrapPosition;

		/// <summary>
		/// Item instance currently in the ItemSlot container to be wrapped.
		/// </summary>
		public Item ItemToWrap;

		/// <summary>
		/// Whether to have the contextual clickable gift wrap confirm button be visible and interactible.
		/// </summary>
		public bool IsWrapButtonAllowed { get => this.ItemToWrap is not null; }
		/// <summary>
		/// Whether to have the contextual close button be visible and interactible.
		/// </summary>
		public bool IsCloseButtonAllowed { get => !Game1.options.SnappyMenus; }

		// Animations

		/// <summary>
		/// Current wrapped gift animation timer.
		/// </summary>
		private int _animTimer;
		/// <summary>
		/// Current wrapped gift animation frame.
		/// </summary>
		private int _animFrame;
		/// <summary>
		/// Duration in milliseconds to continue shaking item slot.
		/// </summary>
		private double _itemSlotShakeTimer;
		/// <summary>
		/// Offset applied to item slot on tick when shaking.
		/// </summary>
		private Vector2 _itemSlotShakeVector;

		// Regions

		/// <summary>
		/// Region of all collected menu components.
		/// </summary>
		private Rectangle _displayArea;
		/// <summary>
		/// Region of coloured background sprite.
		/// </summary>
		private Rectangle _backgroundArea;
		/// <summary>
		/// Region of inventory card components.
		/// </summary>
		private Rectangle _inventoryArea;
		/// <summary>
		/// Region of info card components.
		/// </summary>
		private Rectangle _infoArea;

		// UI

		/// <summary>
		/// Translated text shown in info text area.
		/// </summary>
		private readonly string _infoText;
		/// <summary>
		/// Dimensions of border sprites after scaling is applied.
		/// </summary>
		private readonly Point _borderScaled;
		/// <summary>
		/// Value at which animTimer will reset to 0.
		/// </summary>
		private readonly int _animTimerLimit;
		/// <summary>
		/// Unique ID of clickable component selected by default with snappy navigation.
		/// </summary>
		private readonly int _defaultClickable = -1;
		/// <summary>
		/// Reflects InventoryMenu item shake.
		/// </summary>
		private readonly IReflectedField<Dictionary<int, double>> _iconShakeTimerField;
		/// <summary>
		/// Spritesheet for menu components.
		/// </summary>
		private readonly Texture2D _menuTexture;
		/// <summary>
		/// Spritesheet for paper card backdrop component.
		/// </summary>
		private readonly Texture2D _cardTexture;
		/// <summary>
		/// Spritesheet for wrap button clickable component.
		/// </summary>
		private readonly Texture2D _wrapButtonTexture;
		/// <summary>
		/// UI definitions.
		/// </summary>
		private readonly UI UI;

		public GiftWrapMenu(Vector2 tile) : base(inventory: null, context: null)
		{
			// Definitions
			Data.Data data = ModEntry.Instance.Helper.GameContent.Load<Data.Data>(ModEntry.GameContentDataPath);
			this.UI = data.UI;

			// Custom fields
			this.GiftWrapPosition = tile;
			this._animTimerLimit = this.UI.WrapButtonFrameTime * this.UI.WrapButtonFrames;
			this._borderScaled = new(x: this.UI.BorderSize.X * this.UI.Scale, y: this.UI.BorderSize.Y * this.UI.Scale);
			this._menuTexture = ModEntry.Instance.Helper.GameContent.Load<Texture2D>(this.UI.MenuSpriteSheetPath);
			this._cardTexture = ModEntry.Instance.Helper.GameContent.Load<Texture2D>(this.UI.CardSpriteSheetPath);
			this._wrapButtonTexture = ModEntry.Instance.Helper.GameContent.Load<Texture2D>(this.UI.WrapButtonSpriteSheetPath);
			this._infoText = this.UI.InfoTextPath is null || this.UI.InfoTextKey is null || !ModEntry.Instance.Helper.GameContent.Load<Dictionary<string, string>>(this.UI.InfoTextPath).TryGetValue(this.UI.InfoTextKey, out string str) || str is null
				? ModEntry.I18n.Get("menu.infopanel.body")
				: str;

			// Base fields
			this.initializeUpperRightCloseButton();
			this.trashCan = null;
			this._iconShakeTimerField = ModEntry.Instance.Helper.Reflection.GetField<Dictionary<int, double>>(this.inventory, "_iconShakeTimer");

			// Clickables
			int ID = 1000;

			// Item slot clickable
			this.ItemSlot = new ClickableTextureComponent(
				bounds: Rectangle.Empty,
				texture: this._menuTexture,
				sourceRect: this.UI.ItemSlotSource,
				scale: this.UI.Scale,
				drawShadow: false)
			{
				myID = ++ID
			};

			// Wrap button clickable
			this.WrapButton = new ClickableTextureComponent(
				bounds: Rectangle.Empty,
				texture: this._wrapButtonTexture,
				sourceRect: this.UI.WrapButtonSource,
				scale: this.UI.Scale,
				drawShadow: false)
			{
				myID = ++ID
			};

			// Position components
			this.gameWindowSizeChanged(oldBounds: Rectangle.Empty, newBounds: Game1.uiViewport.ToXna());

			// Clickable navigation
			this._defaultClickable = this.ItemSlot.myID;
			this.populateClickableComponentList();

			// Setup
			Game1.playSound(this.UI.OpenSound);
			Game1.freezeControls = true;
			ModEntry.Instance.Helper.Events.GameLoop.UpdateTicked += this.OnGiftWrapMenuInitialised;
		}

		private void OnGiftWrapMenuInitialised(object sender, StardewModdingAPI.Events.UpdateTickedEventArgs e)
		{
			ModEntry.Instance.Helper.Events.GameLoop.UpdateTicked -= this.OnGiftWrapMenuInitialised;

			// Prevents having the click-down that opens the menu from also interacting with the menu on click-released
			Game1.freezeControls = false;
			if (Game1.options.SnappyMenus)
			{
				this.snapToDefaultClickableComponent();
			}
		}

		private string GetSoundFor(Item item)
		{
			string cue = this.UI.ItemSounds.TryGetValue(item.Name, out var key)
				? key
				: this.UI.CategorySounds.Keys.FirstOrDefault((string key) => this.UI.CategorySounds[key].Contains(item.Category));
			return cue ?? this.UI.ItemSound;
		}

		private void ShakeAndSoundFor(Item item)
		{
			string sound = this.GetSoundFor(item);
			Game1.playSound(sound);
			if (this.UI.ShakeCategories.Contains(item.Category))
			{
				this._itemSlotShakeTimer = item.Stack > 1 ? this.UI.LongShakeDuration : this.UI.ShortShakeDuration;
			}
		}

		protected override void cleanupBeforeExit()
		{
			if (this.ItemToWrap is not null)
			{
				// Return items in item slot to player when closing
				Game1.createItemDebris(
					item: this.ItemToWrap,
					origin: Game1.player.Position,
					direction: -1);
			}

			base.cleanupBeforeExit();
		}

		public override void emergencyShutDown()
		{
			this.exitFunction();
			base.emergencyShutDown();
		}
		
		public override void gameWindowSizeChanged(Rectangle oldBounds, Rectangle newBounds)
		{
			newBounds.Offset(offsetX: -newBounds.X, offsetY: -newBounds.Y);

			this.xPositionOnScreen = newBounds.X;
			this.yPositionOnScreen = newBounds.Y;

			base.gameWindowSizeChanged(oldBounds: oldBounds, newBounds: newBounds);

			Point centre = newBounds.Center;
			if (Context.IsSplitScreen)
			{
				// Centre the menu in splitscreen
				centre.X = centre.X / 3 * 2;
			}

			int yOffset = -8 * this.UI.Scale;

			// Menu visible area
			Point displaySize = new(
				x: (this.UI.SceneBackgroundSource.Width + this.UI.InfoBackgroundWidth + this.UI.BorderSize.X) * this.UI.Scale,
				y: this.UI.SceneBackgroundSource.Height * this.UI.Scale + this.inventory.height);
			this._displayArea = new(
				x: centre.X - displaySize.X / 2,
				y: centre.Y - displaySize.Y / 2 + yOffset,
				width: displaySize.X,
				height: displaySize.Y
			);

			// Scene background
			this._backgroundArea = new Rectangle(
				x: this._displayArea.X,
				y: this._displayArea.Y,
				width: this.UI.SceneBackgroundSource.Width * this.UI.Scale,
				height: this.UI.SceneBackgroundSource.Height * this.UI.Scale);

			// Info area
			this._infoArea = new(
				x: this._displayArea.X + this._displayArea.Width - (this.UI.InfoBackgroundWidth + 1) * this.UI.Scale,
				y: this._backgroundArea.Y,
				width: this.UI.InfoBackgroundWidth * this.UI.Scale,
				height: this._backgroundArea.Height);

			// Inventory area
			this._inventoryArea = new(
				x: centre.X - this.inventory.width / 2,
				y: this._backgroundArea.Bottom + this._borderScaled.Y / 7 * 13,
				width: Game1.tileSize * 12,
				height: Game1.tileSize * this.inventory.rows);

			// Item slot
			this.ItemSlot.bounds = new Rectangle(
				x: this._backgroundArea.Left + this._backgroundArea.Width / 7 * 4,
				y: this._backgroundArea.Top + this._backgroundArea.Height / 3 - this.UI.ItemSlotSource.Width * this.UI.Scale / 2,
				width: this.UI.ItemSlotSource.Width * this.UI.Scale,
				height: this.UI.ItemSlotSource.Height * this.UI.Scale);

			// Wrap button
			this.WrapButton.bounds = new Rectangle(
				x: this.ItemSlot.bounds.Right + (this._backgroundArea.Right - this.ItemSlot.bounds.Right) / 2,
				y: this._backgroundArea.Center.Y,
				width: this.UI.WrapButtonSource.Width * this.UI.Scale,
				height: this.UI.WrapButtonSource.Height * this.UI.Scale);
			this.WrapButton.setPosition(this.WrapButton.bounds.Location.ToVector2() - this.WrapButton.bounds.Size.ToVector2() / 2);

			// Close button
			this.upperRightCloseButton.setPosition(
				x: this._infoArea.X + this._infoArea.Width - 2 * this.UI.Scale,
				y: this._infoArea.Y - this.UI.BorderSize.Y * this.UI.Scale);

			// Evidently this was good enough for the base game
			this.inventory = new(
				xPosition: this._inventoryArea.X,
				yPosition: this._inventoryArea.Y,
				playerInventory: false,
				actualInventory: null,
				highlightMethod: this.inventory.highlightMethod)
			{
				width = this._inventoryArea.Width,
				height = this._inventoryArea.Height
			};
		}

		public override void receiveLeftClick(int x, int y, bool playSound = true)
		{
			if (Game1.freezeControls)
				return;

			if (this.ItemSlot.containsPoint(x, y) && this.ItemToWrap is not null)
			{
				if (this.inventory.tryToAddItem(toPlace: this.ItemToWrap, sound: playSound ? this.GetSoundFor(this.ItemToWrap) : null) is null)
				{
					// Take all from wrapping
					this.ItemToWrap = null;
				}
				else
				{
					// Inventory couldn't take all
					Game1.playSound(this.UI.FailureSound);
				}
			}
			else if (this.WrapButton.containsPoint(x, y) && this.IsWrapButtonAllowed && this.ItemToWrap is not null)
			{
				Object wrappedGift = ModEntry.GetWrappedGift(modData: null);
				ModEntry.PackItem(ref wrappedGift, this.ItemToWrap, this.GiftWrapPosition, showMessage: true);
				if (wrappedGift != null)
				{
					// Convert wrapping to gift and close menu, giving the player the wrapped gift
					this.ItemToWrap = null;
					Game1.player.addItemToInventory(wrappedGift);
					Game1.playSound(this.UI.SuccessSound);
					this.exitThisMenuNoSound();
				}
				else
				{
					// Wrapping couldn't be gifted
					Game1.playSound(this.UI.FailureSound);
				}
			}
			else if (this.inventory.getInventoryPositionOfClick(x, y) is int index && this.inventory.actualInventory.ElementAtOrDefault(index) is Item item && ModEntry.IsItemAllowed(item))
			{
				if (this.ItemToWrap is not null && this.ItemToWrap.canStackWith(item))
				{
					// Try add all to wrapping
					int maximumToSend = Math.Min(this.inventory.actualInventory[index].Stack, this.ItemToWrap.maximumStackSize() - this.ItemToWrap.Stack);
					this.ItemToWrap.Stack += maximumToSend;
					this.inventory.actualInventory[index].Stack -= maximumToSend;
					if (this.inventory.actualInventory[index].Stack < 1)
						this.inventory.actualInventory[index] = null;
					this.ShakeAndSoundFor(item);
				}
				else
				{
					// Add all to wrapping
					(this.ItemToWrap, this.inventory.actualInventory[index]) = (this.inventory.actualInventory[index], this.ItemToWrap);
					this.ShakeAndSoundFor(item);
				}
			}
			else if (this.IsCloseButtonAllowed && this.upperRightCloseButton.containsPoint(x, y))
			{
				// Close menu
				this.exitThisMenu();
			}
		}
		
		public override void receiveRightClick(int x, int y, bool playSound = true)
		{
			if (Game1.freezeControls)
				return;

			if (this.ItemSlot.containsPoint(x, y) && this.ItemToWrap is not null)
			{
				if (this.inventory.tryToAddItem(toPlace: this.ItemToWrap.getOne(), sound: this.GetSoundFor(this.ItemToWrap)) is null)
				{
					// Take one from wrapping
					if (this.ItemToWrap.maximumStackSize() <= 1 || --this.ItemToWrap.Stack < 1)
						this.ItemToWrap = null;
				}
				else
				{
					// Inventory couldn't take one
					Game1.playSound(this.UI.FailureSound);
				}
			}
			else if (this.inventory.getInventoryPositionOfClick(x, y) is int index && this.inventory.actualInventory.ElementAtOrDefault(index) is Item item && ModEntry.IsItemAllowed(item))
			{
				bool movedOne = false;
				if (this.ItemToWrap is not null)
				{
					// Add one to wrapping
					if (this.ItemToWrap.canStackWith(item))
					{
						++this.ItemToWrap.Stack;
						movedOne = true;
					}
					// Take all of wrapping and add one to wrap
					else if (this.inventory.tryToAddItem(toPlace: this.ItemToWrap, sound: this.UI.NoSound) is null)
					{
						this.ItemToWrap = item.getOne();
						movedOne = true;
					}
				}
				else
				{
					// Add one to wrapping
					this.ItemToWrap = item.getOne();
					movedOne = true;
				}

				if (movedOne)
				{
					// Take one from inventory
					if (this.inventory.actualInventory[index].maximumStackSize() <= 1 || --this.inventory.actualInventory[index].Stack < 1)
						this.inventory.actualInventory[index] = null;
					this.ShakeAndSoundFor(item);
				}
				else
				{
					// None were moved
					Game1.playSound(this.UI.FailureSound);
				}
			}
		}

		public override void performHoverAction(int x, int y)
		{
			if (Game1.freezeControls)
				return;

			this.hoverText = "";
			this.hoveredItem = null;

			this.inventory.hover(x, y, null);

			const float scale = 0.25f;
			this.ItemSlot.tryHover(x, y, maxScaleIncrease: scale);
			this.WrapButton.tryHover(x, y, maxScaleIncrease: scale);

			if (this.IsCloseButtonAllowed)
			{
				// Hover close button
				this.upperRightCloseButton.tryHover(x, y, maxScaleIncrease: scale * 2);
			}
			if (this.ItemSlot.containsPoint(x, y) && this.ItemToWrap is not null) 
			{
				// Hover item slot
				this.hoveredItem = this.ItemToWrap;
			}
			else if (this.inventory.getInventoryPositionOfClick(x, y) is int index && this.inventory.actualInventory.ElementAtOrDefault(index) is Item item && ModEntry.IsItemAllowed(item))
			{
				// Hover inventory item
				this.hoveredItem = item;
			}
		}

		public override void receiveKeyPress(Keys key)
		{
			if (Game1.freezeControls)
				return;

			bool isExitKey = key == Keys.Escape
				|| Game1.options.doesInputListContain(Game1.options.menuButton, key)
				|| Game1.options.doesInputListContain(Game1.options.journalButton, key);
			if (isExitKey)
			{
				this.exitThisMenu();
			}
			else if (Game1.options.SnappyMenus)
			{
				// Gamepad navigation
				int inventoryWidth = this.GetColumnCount();
				int current = this.currentlySnappedComponent is not null ? this.currentlySnappedComponent.myID : -1;
				int snapTo = -1;
				if (Game1.options.doesInputListContain(Game1.options.moveLeftButton, key))
				{
					// Left
					if (current == this.WrapButton.myID)
					{
						// WrapButton => ItemSlot
						snapTo = this.ItemSlot.myID;
					}
					else if (current < this.inventory.inventory.Count && current % inventoryWidth == 0)
					{
						// Inventory =|
						snapTo = current;
					}
				}
				else if (Game1.options.doesInputListContain(Game1.options.moveRightButton, key))
				{
					// Right
					if (current == this.ItemSlot.myID && this.IsWrapButtonAllowed)
					{
						// ItemSlot => WrapButton
						snapTo = this.WrapButton.myID;
					}
					else if (current < this.inventory.inventory.Count && current % inventoryWidth == inventoryWidth - 1)
					{
						// Inventory =|
						snapTo = current;
					}
				}
				else if (Game1.options.doesInputListContain(Game1.options.moveUpButton, key))
				{
					// Up
					if (current >= 0 && current < this.inventory.inventory.Count)
					{
						if (current < inventoryWidth)
						{
							// Inventory => WrapButton/ItemSlot
							snapTo = this.IsWrapButtonAllowed ? this.WrapButton.myID : this.ItemSlot.myID;
						}
						else
						{
							// Inventory => Inventory
							snapTo = current - inventoryWidth;
						}
					}
				}
				else if (Game1.options.doesInputListContain(Game1.options.moveDownButton, key))
				{
					// Down
					if (current == this.ItemSlot.myID || current == this.WrapButton.myID)
					{
						// ItemSlot/WrapButton => Inventory
						snapTo = 0;
					}
				}
				if (snapTo != -1)
				{
					this.setCurrentlySnappedComponentTo(snapTo);
					return;
				}
			}

			base.receiveKeyPress(key);
		}

		public override void receiveGamePadButton(Buttons b)
		{
			if (Game1.freezeControls)
				return;

			// Contextual navigation
			int current = this.currentlySnappedComponent is not null ? this.currentlySnappedComponent.myID : -1;
			int snapTo = -1;
			if (b is Buttons.LeftShoulder)
			{
				// Left
				if (current == this.ItemSlot.myID)
					// ItemSlot => Inventory
					snapTo = 0;
				else if (current == this.WrapButton.myID)
					// WrapButton => ItemSlot
					snapTo = this.ItemSlot.myID;
				else if (current < this.inventory.inventory.Count)
					// Inventory => WrapButton/ItemSlot
					snapTo = this.IsWrapButtonAllowed ? this.WrapButton.myID : this.ItemSlot.myID;
				else
					// ??? => Default
					snapTo = this._defaultClickable;
			}
			if (b is Buttons.RightShoulder)
			{
				// Right
				if (current == this.ItemSlot.myID)
					// ItemSlot => WrapButton/Inventory
					snapTo = this.IsWrapButtonAllowed ? this.WrapButton.myID : 0;
				else if (current == this.WrapButton.myID)
					// WrapButton => Inventory
					snapTo = 0;
				else if (current > this.inventory.inventory.Count)
					// Inventory => ItemSlot
					snapTo = this.ItemSlot.myID;
				else
					// ??? => Default
					snapTo = this._defaultClickable;
			}
			this.setCurrentlySnappedComponentTo(snapTo);
		}

		public override void snapToDefaultClickableComponent()
		{
			if (this._defaultClickable == -1)
				return;
			this.setCurrentlySnappedComponentTo(this._defaultClickable);
		}

		public override void setCurrentlySnappedComponentTo(int id)
		{
			if (id == -1 || this.getComponentWithID(id) is null)
				return;

			this.currentlySnappedComponent = this.getComponentWithID(id);
			this.snapCursorToCurrentSnappedComponent();
		}

		public override void update(GameTime time)
		{
			// ItemSlot shake
			this._itemSlotShakeVector = Vector2.Zero;
			if (this._itemSlotShakeTimer > 0.001d)
			{
				this._itemSlotShakeTimer -= time.ElapsedGameTime.Milliseconds;
				this._itemSlotShakeVector.X = Utility.RandomFloat(-0.4f, 0.4f) * this.UI.Scale;
				this._itemSlotShakeVector.Y = Utility.RandomFloat(-0.4f, 0.4f) * this.UI.Scale;
			}

			// WrapButton animation loop
			this._animTimer += time.ElapsedGameTime.Milliseconds;
			if (this._animTimer >= this._animTimerLimit)
				this._animTimer = 0;
			if (this._animFrame > 0 || this.WrapButton.scale > this.WrapButton.baseScale)
				this._animFrame = (int)((float)this._animTimer / this._animTimerLimit * this.UI.WrapButtonFrames);

			base.update(time);
		}

		public override void draw(SpriteBatch b)
		{
			Rectangle screen = Game1.uiViewport.ToXna();
			screen.Offset(
				offsetX: -screen.X,
				offsetY: -screen.Y);

			// Blackout
			b.Draw(
				texture: Game1.fadeToBlackRect,
				destinationRectangle: screen,
				color: Colour.Black * 0.5f);

			// Card
			b.Draw(
				texture: this._cardTexture,
				position: this._backgroundArea.Center.ToVector2() + this.UI.CardOffset.ToVector2() * this.UI.Scale,
				sourceRectangle: null,
				color: Colour.White,
				rotation: 0,
				scale: this.UI.Scale,
				origin: this._cardTexture.Bounds.Size.ToVector2() / 2,
				effects: SpriteEffects.None,
				layerDepth: 1);

			// Inventory
			this.DrawGiftWrapInventory(b);

			// Scene
			{
				// Scene background
				b.Draw(
					texture: this._menuTexture,
					destinationRectangle: this._backgroundArea,
					sourceRectangle: this.UI.SceneBackgroundSource,
					color: Colour.White,
					rotation: 0,
					origin: Vector2.Zero,
					effects: SpriteEffects.None,
					layerDepth: 1);

				// Scene border
				this.DrawBorder(
					b: b,
					area: this._backgroundArea,
					drawBorderOutside: true,
					drawFillColour: false);
			}

			// Info
			{
				// Info panel and border
				this.DrawBorder(
					b: b,
					area: this._infoArea,
					drawBorderOutside: true,
					drawFillColour: true);

				// Info text
				Vector2 margin = this.UI.InfoTextMargin.ToVector2() * this.UI.Scale;
				// Give a little extra leeway with non-English locales to fit them into the body text area
				string text = Game1.parseText(
					text: this._infoText,
					whichFont: Game1.smallFont,
					width: this._infoArea.Width - (LocalizedContentManager.CurrentLanguageCode is LocalizedContentManager.LanguageCode.en ? (int)margin.X : 0));
				if (Game1.smallFont.MeasureString(text).Y > this._infoArea.Height)
				{
					// Remove the last line if body text overflows
					text = text[..text.LastIndexOf('.')];
				}
				Utility.drawTextWithShadow(
					b: b,
					text: text,
					font: Game1.smallFont,
					position: new Vector2(x: this._infoArea.X + (int)margin.X, y: this._infoArea.Y + (int)margin.Y),
					color: Game1.textColor);
			}

			// Clickables
			{
				// Item slot
				b.Draw(
					texture: this.ItemSlot.texture,
					position: this.ItemSlot.bounds.Location.ToVector2() + this.ItemSlot.bounds.Size.ToVector2() / 2 + this._itemSlotShakeVector,
					sourceRectangle: new(
						x: this.ItemSlot.sourceRect.X,
						y: this.ItemSlot.sourceRect.Y,
						width: this.ItemSlot.sourceRect.Width,
						height: this.ItemSlot.sourceRect.Height),
					color: Colour.White,
					rotation: 0,
					scale: this.ItemSlot.scale,
					origin: this.ItemSlot.sourceRect.Size.ToVector2() / 2,
					effects: SpriteEffects.None,
					layerDepth: 1);

				// Item in slot
				this.ItemToWrap?.drawInMenu(
					spriteBatch: b,
					location: new Vector2(x: this.ItemSlot.bounds.X, y: this.ItemSlot.bounds.Y)
						+ (this.ItemSlot.bounds.Size.ToVector2() - new Vector2(Game1.tileSize)) / 2
						+ this._itemSlotShakeVector,
					scaleSize: this.ItemSlot.scale / this.UI.Scale);

				// Wrap button
				if (this.IsWrapButtonAllowed)
				{
					b.Draw(
						texture: this.WrapButton.texture,
						position: this.WrapButton.bounds.Location.ToVector2() + this.WrapButton.bounds.Size.ToVector2() / 2,
						sourceRectangle: new(
							x: this.WrapButton.sourceRect.X + (this._animFrame * this.WrapButton.sourceRect.Width),
							y: this.WrapButton.sourceRect.Y,
							width: this.WrapButton.sourceRect.Width,
							height: this.WrapButton.sourceRect.Height),
						color: Colour.White,
						rotation: 0,
						scale: this.WrapButton.scale,
						origin: this.WrapButton.sourceRect.Size.ToVector2() / 2,
						effects: SpriteEffects.None,
						layerDepth: 1);
				}
			}

			// Decorations
			foreach (Decoration decor in this.UI.Decorations) {
				b.Draw(
					texture: this._menuTexture,
					position: this._displayArea.Location.ToVector2() + decor.Position.ToVector2() * this.UI.Scale,
					sourceRectangle: decor.Source,
					color: Colour.White,
					rotation: 0,
					origin: decor.Source.Size.ToVector2() / 2,
					scale: this.UI.Scale,
					effects: SpriteEffects.None,
					layerDepth: 1);
			}

			// User
			{
				// Close button
				if (this.IsCloseButtonAllowed)
				{
					this.upperRightCloseButton.draw(b);
				}

				// Tooltips
				if (this.hoveredItem is not null)
				{
					IClickableMenu.drawToolTip(
						b: b,
						hoverText: this.hoveredItem.getDescription(),
						hoverTitle: this.hoveredItem.DisplayName,
						hoveredItem: this.hoveredItem,
						heldItem: this.heldItem is not null);
				}

				// Cursors
				Game1.mouseCursorTransparency = 1;
				this.drawMouse(b);
			}
		}

		/// <summary>
		/// Mostly a copy of InventoryMenu.draw(SpriteBatch b, int red, int blue, int green),
		/// though items considered unable to be cooked will be greyed out.
		/// </summary>
		private void DrawGiftWrapInventory(SpriteBatch b)
		{
			// Background card
			Point margin = (new Vector2(x: this.UI.InventoryMargin.X, y: this.UI.InventoryMargin.Y) * this.UI.Scale).ToPoint();
			Rectangle area = new(
				x: this.inventory.xPositionOnScreen - margin.X,
				y: this.inventory.yPositionOnScreen - margin.Y,
				width: this.inventory.width + margin.X * 2,
				height: this.inventory.height + margin.Y * 2);
			this.DrawBorder(
				b: b,
				area: area,
				drawBorderOutside: true,
				drawFillColour: true);

			// Inventory item shakes
			Dictionary<int, double> iconShakeTimer = this._iconShakeTimerField.GetValue();
			for (int key = 0; key < this.inventory.inventory.Count; ++key)
			{
				if (iconShakeTimer.ContainsKey(key) && Game1.currentGameTime.TotalGameTime.TotalSeconds >= iconShakeTimer[key])
					iconShakeTimer.Remove(key);
			}

			Point point = new(x: this.inventory.xPositionOnScreen, y: this.inventory.yPositionOnScreen);

			// Actual inventory
			for (int i = 0; i < this.inventory.capacity; ++i)
			{
				// Item slot frames
				Vector2 position = new(
					x: point.X
						+ (i % (this.inventory.capacity / this.inventory.rows) * Game1.tileSize)
						+ (this.inventory.horizontalGap * (i % (this.inventory.capacity / this.inventory.rows))),
					y: point.Y
						+ (i / (this.inventory.capacity / this.inventory.rows) * (Game1.tileSize + this.inventory.verticalGap))
						+ (((i / (this.inventory.capacity / this.inventory.rows)) - 1) * this.UI.Scale)
						- (i >= this.inventory.capacity / this.inventory.rows
							|| !this.inventory.playerInventory || this.inventory.verticalGap != 0 ? 0 : 12));

				b.Draw(
					texture: this._menuTexture,
					position: position,
					sourceRectangle: i < Game1.player.maxItems.Value ? this.UI.InventorySlotSource : this.UI.InventoryLockedSlotSource,
					color: Colour.White,
					rotation: 0,
					origin: Vector2.Zero,
					scale: this.UI.Scale,
					effects: SpriteEffects.None,
					layerDepth: 0.5f);
			}
			for (int i = 0; i < this.inventory.capacity; ++i)
			{
				Vector2 position = new(
					x: point.X
						+ (i % (this.inventory.capacity / this.inventory.rows) * Game1.tileSize)
						+ (this.inventory.horizontalGap * (i % (this.inventory.capacity / this.inventory.rows))),
					y: point.Y
						+ (i / (this.inventory.capacity / this.inventory.rows) * (Game1.tileSize + this.inventory.verticalGap))
						+ (((i / (this.inventory.capacity / this.inventory.rows)) - 1) * this.UI.Scale)
						- (i >= this.inventory.capacity / this.inventory.rows
						   || !this.inventory.playerInventory || this.inventory.verticalGap != 0 ? 0 : 12));

				// Item icons
				if (this.inventory.actualInventory.ElementAtOrDefault(i) is Item item)
				{
					Colour colour = ModEntry.IsItemAllowed(item) ? Colour.White : this.UI.InventoryInvalidGiftColour;
					bool drawShadow = this.inventory.highlightMethod(item);
					if (iconShakeTimer.ContainsKey(i))
						position += 1 * new Vector2(Game1.random.Next(-1, 2), Game1.random.Next(-1, 2));
					item.drawInMenu(
						spriteBatch: b,
						location: position,
						scaleSize: this.inventory.inventory.Count > i ? this.inventory.inventory[i].scale : 1,
						transparency: !this.inventory.highlightMethod(item) ? 0.25f : 1,
						layerDepth: 0.865f,
						drawStackNumber: StackDrawType.Draw,
						color: colour,
						drawShadow: drawShadow);
				}
			}
		}

		/// <summary>
		/// Draw a 9-slice sprite quadrangle with an optional sprite fill.
		/// </summary>
		/// <param name="b"></param>
		/// <param name="area"></param>
		/// <param name="drawBorderOutside"></param>
		/// <param name="drawFillColour"></param>
		private void DrawBorder(SpriteBatch b, Rectangle area, bool drawBorderOutside, bool drawFillColour)
		{
			Colour colour = Colour.White;
			float rotation = 0;
			Vector2 origin = Vector2.Zero;
			float scale = this.UI.Scale;
			SpriteEffects effects = SpriteEffects.None;
			float layerDepth = 1;

			Rectangle source;
			Point scaled;

			Point offset = new(
				x: this.UI.BorderOffset.X * this.UI.Scale,
				y: this.UI.BorderOffset.Y * this.UI.Scale);

			if (drawBorderOutside)
			{
				area.X -= this._borderScaled.X;
				area.Y -= this._borderScaled.Y;
				area.Width += this._borderScaled.X * 2;
				area.Height += this._borderScaled.Y * 2;
			}

			if (drawFillColour)
			{
				// Fill colour
				Rectangle fillArea = new(
					x: area.X + this._borderScaled.X,
					y: area.Y + this._borderScaled.Y,
					width: area.Width - (this._borderScaled.X * 2),
					height: area.Height - (this._borderScaled.Y * 2));
				b.Draw(
					texture: Game1.fadeToBlackRect,
					destinationRectangle: fillArea,
					sourceRectangle: null,
					color: Colour.White,
					rotation: rotation,
					origin: origin,
					effects: effects,
					layerDepth: layerDepth);
				b.Draw(
					texture: this._menuTexture,
					destinationRectangle: fillArea,
					sourceRectangle: this.UI.InfoBackgroundSource,
					color: Colour.White,
					rotation: rotation,
					origin: origin,
					effects: effects,
					layerDepth: layerDepth);
			}

			// Sides:
			{
				Rectangle target;
				void draw(Rectangle target, Rectangle source)
				{
					b.Draw(
						texture: this._menuTexture,
						destinationRectangle: target,
						sourceRectangle: source,
						color: colour,
						rotation: rotation,
						origin: origin,
						effects: effects,
						layerDepth: layerDepth);
				}

				// Top
				source = new Rectangle(
					x: this.UI.BorderSourceAt.X + this.UI.BorderSize.X,
					y: this.UI.BorderSourceAt.Y,
					width: 1,
					height: this.UI.BorderSize.Y);
				scaled = new Point(
					x: source.Width * this.UI.Scale,
					y: source.Height * this.UI.Scale);
				target = new Rectangle(
					x: area.X + (this.UI.BorderSize.X * this.UI.Scale) + offset.X,
					y: area.Y + offset.Y,
					width: area.Width - (this.UI.BorderSize.X * this.UI.Scale * 2) - offset.X * 2,
					height: scaled.Y);
				draw(target: target, source: source);

				// Bottom
				source = new Rectangle(
					x: this.UI.BorderSourceAt.X + this.UI.BorderSize.X,
					y: this.UI.BorderSourceAt.Y,
					width: 1,
					height: this.UI.BorderSize.Y);
				scaled = new Point(
					x: source.Width * this.UI.Scale,
					y: source.Height * this.UI.Scale);
				target = new Rectangle(
					x: area.X + (this.UI.BorderSize.X * this.UI.Scale) + offset.X,
					y: area.Y + area.Height - scaled.Y - offset.Y,
					width: area.Width - (this.UI.BorderSize.X * this.UI.Scale * 2) - offset.X * 2,
					height: scaled.Y);
				draw(target: target, source: source);

				// Left
				source = new Rectangle(
					x: this.UI.BorderSourceAt.X,
					y: this.UI.BorderSourceAt.Y + this.UI.BorderSize.Y,
					width: this.UI.BorderSize.X,
					height: 1);
				scaled = new Point(
					x: source.Width * this.UI.Scale,
					y: source.Height * this.UI.Scale);
				target = new Rectangle(
					x: area.X + offset.X,
					y: area.Y + (this.UI.BorderSize.Y * this.UI.Scale) + offset.Y,
					width: scaled.X,
					height: area.Height - (this.UI.BorderSize.Y * this.UI.Scale * 2) - offset.Y);
				draw(target: target, source: source);

				// Right
				source = new Rectangle(
					x: this.UI.BorderSourceAt.X + this.UI.BorderSize.X + 1,
					y: this.UI.BorderSourceAt.Y + this.UI.BorderSize.Y,
					width: this.UI.BorderSize.X,
					height: 1);
				scaled = new Point(
					x: source.Width * this.UI.Scale,
					y: source.Height * this.UI.Scale);
				target = new Rectangle(
					x: area.X + area.Width - scaled.X - offset.X,
					y: area.Y + (this.UI.BorderSize.Y * this.UI.Scale) + offset.Y,
					width: scaled.X,
					height: area.Height - (this.UI.BorderSize.Y * this.UI.Scale * 2) - offset.Y);
				draw(target: target, source: source);
			}

			// Corners:
			{
				void draw(Vector2 target, Rectangle source)
				{
					b.Draw(
						texture: this._menuTexture,
						position: target,
						sourceRectangle: source,
						color: colour,
						rotation: rotation,
						origin: origin,
						scale: scale,
						effects: effects,
						layerDepth: layerDepth);
				}

				source = new Rectangle(
					x: this.UI.BorderSourceAt.X,
					y: this.UI.BorderSourceAt.Y,
					width: this.UI.BorderSize.X,
					height: this.UI.BorderSize.Y);
				scaled = new Point(
					x: source.Width * this.UI.Scale,
					y: source.Height * this.UI.Scale);

				var corners = new (Vector2 target, Rectangle source)[]
				{
					// Top-left
					(
						target: new(
							x: area.X + offset.X,
							y: area.Y + offset.Y),
						source: source
					),
					// Bottom-left
					(
						target: new(
							x: area.X + offset.X,
							y: area.Y + area.Height - scaled.Y - offset.Y),
						source: new(
							x: source.X,
							y: source.Y + source.Height,
							width: source.Width,
							height: source.Height)
					),
					// Top-right
					(
						target: new(
							x: area.X + area.Width - scaled.X - offset.X,
							y: area.Y + offset.Y),
						source: new(
							x: source.X + 1 + source.Width,
							y: source.Y,
							width: source.Width,
							height: source.Height)
					),
					// Bottom-right
					(
						target: new(
							x: area.X + area.Width - scaled.X - offset.X,
							y: area.Y + area.Height - scaled.Y),
						source: new(
							x: source.X + 1 + source.Width,
							y: source.Y + 1 + source.Height,
							width: source.Width,
							height: source.Height)
					)
				};

				foreach ((Vector2 target, Rectangle source) corner in corners)
				{
					draw(target: corner.target, source: corner.source);
				}
			}
		}
	}
}
