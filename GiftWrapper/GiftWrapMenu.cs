using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;
using System;
using System.Collections.Generic;
using System.Linq;
using Object = StardewValley.Object;

namespace GiftWrapper
{
	public class GiftWrapMenu : ItemGrabMenu
	{
		/// <summary> Custom sprite assets for menu elements </summary>
		public Texture2D Texture;
		/// <summary> Clickable container to display items placed by the user for turning into wrapped gifts </summary>
		public ClickableTextureComponent ItemSlot;
		/// <summary> Contextual clickable button to confirm the gift wrap action, revealed when there are items to be wrapped </summary>
		public ClickableTextureComponent WrapButton;
		/// <summary>
		/// Tile position of the placed gift wrap item used to open this menu at the current game location,
		/// passed to ModEntry.Instance.PackItem() to remove from the world objects list
		/// </summary>
		public Vector2 GiftWrapPosition;
		/// <summary> Item instance currently in the ItemSlot container to be wrapped </summary>
		public Item ItemToWrap;
		/// <summary> Whether to have the contextual clickable gift wrap confirm button be visible and interactible </summary>
		public bool ShowWrapButton { get => this.ItemToWrap is not null; }

		/// <summary> Current wrapped gift animation timer </summary>
		private int _animTimer;
		/// <summary> Current wrapped gift animation frame </summary>
		private int _animFrame;
		/// <summary> Time between animation frames </summary>
		private const int AnimFrameTime = 100;
		/// <summary> Number of frames in wrapped gift button animation </summary>
		private const int AnimFrames = 4;
		/// <summary> Value at which animTimer will reset to 0 </summary>
		private const int AnimTimerLimit = AnimFrameTime * AnimFrames;
		/// <summary> Reflects InventoryMenu item shake </summary>
		private readonly IReflectedField<Dictionary<int, double>> _iconShakeTimerField;

		private static readonly Rectangle BackgroundSource = new(0, 0, 128, 80);
		private static readonly Rectangle DecorationSource = new(0, BackgroundSource.Height, 96, 64);
		private static readonly Rectangle ItemSlotSource = new(BackgroundSource.X + BackgroundSource.Width - 18, BackgroundSource.Y + BackgroundSource.Height, 18, 18);
		private static readonly Rectangle WrapButtonSource = new(548, 262, 18, 20);
		private Rectangle _backgroundArea;
		private Rectangle _decorationArea;
		private new const int borderWidth = 4;
		private readonly int _borderScaled;
		private readonly int _defaultClickable = -1;
		private readonly int _inventoryExtraWidth = 4 * Game1.pixelZoom;

		public GiftWrapMenu(Vector2 position) : base(inventory: null, context: null)
		{
			Game1.playSound("scissors");
			Game1.freezeControls = true;

			// Custom fields
			this.GiftWrapPosition = position;
			this.Texture = Game1.content.Load<Texture2D>(ModEntry.GameContentTexturePath);

			// Base fields
			this.initializeUpperRightCloseButton();
			this.trashCan = null;
			this._iconShakeTimerField = ModEntry.Instance.Helper.Reflection.GetField<Dictionary<int, double>>(this.inventory, "_iconShakeTimer");

			Point centre = Game1.graphics.GraphicsDevice.Viewport.Bounds.Center;
			if (Context.IsSplitScreen)
			{
				// Centre the menu in splitscreen
				centre.X = centre.X / 3 * 2;
			}

			this._borderScaled = borderWidth * Game1.pixelZoom;
			int yOffset;
			int ID = 1000;

			// Widen inventory to allow more space in the text area above
			this.inventory.width += this._inventoryExtraWidth;
			this.inventory.xPositionOnScreen -= this._inventoryExtraWidth / 2;

			// Background panel
			yOffset = -32 * Game1.pixelZoom;
			this._backgroundArea = new Rectangle(
				this.inventory.xPositionOnScreen - (this._borderScaled / 2),
				centre.Y + yOffset - (BackgroundSource.Height / 2 * Game1.pixelZoom),
				BackgroundSource.Width * Game1.pixelZoom,
				BackgroundSource.Height * Game1.pixelZoom);

			yOffset = -28 * Game1.pixelZoom;
			this._decorationArea = new Rectangle(
				this._backgroundArea.X + ((BackgroundSource.Width - DecorationSource.Width) / 2 * Game1.pixelZoom),
				centre.Y + yOffset - (DecorationSource.Height / 2 * Game1.pixelZoom),
				DecorationSource.Width * Game1.pixelZoom,
				DecorationSource.Height * Game1.pixelZoom);

			this.inventory.yPositionOnScreen = this._backgroundArea.Y + this._backgroundArea.Height + (borderWidth * 2 * Game1.pixelZoom);

			// Item slot clickable
			yOffset = -24 * Game1.pixelZoom;
			this.ItemSlot = new ClickableTextureComponent(
				bounds: new Rectangle(
					this._backgroundArea.X + ((BackgroundSource.Width - ItemSlotSource.Width) / 2 * Game1.pixelZoom),
					this._backgroundArea.Y + (this._backgroundArea.Height / 2) + yOffset,
					ItemSlotSource.Width * Game1.pixelZoom,
					ItemSlotSource.Height * Game1.pixelZoom),
				texture: this.Texture,
				sourceRect: ItemSlotSource,
				scale: Game1.pixelZoom, drawShadow: false)
			{
				myID = ++ID
			};

			// Wrap button clickable
			yOffset = 16 * Game1.pixelZoom;
			Texture2D junimoTexture = Game1.content.Load<Texture2D>(@"LooseSprites/JunimoNote");
			this.WrapButton = new ClickableTextureComponent(
				bounds: new Rectangle(
					this.ItemSlot.bounds.X,
					this._backgroundArea.Y + (this._backgroundArea.Height / 2) + yOffset,
					WrapButtonSource.Width * Game1.pixelZoom,
					WrapButtonSource.Height * Game1.pixelZoom),
				texture: junimoTexture,
				sourceRect: WrapButtonSource,
				scale: Game1.pixelZoom, drawShadow: false)
			{
				myID = ++ID
			};

			// Clickable navigation
			this._defaultClickable = this.ItemSlot.myID;
			this.populateClickableComponentList();

			ModEntry.Instance.Helper.Events.GameLoop.UpdateTicked += this.Event_UnfreezeControls;
		}

		/// <summary>
		/// Prevents having the click-down that opens the menu from also interacting with the menu on click-released
		/// </summary>
		private void Event_UnfreezeControls(object sender, StardewModdingAPI.Events.UpdateTickedEventArgs e)
		{
			ModEntry.Instance.Helper.Events.GameLoop.UpdateTicked -= this.Event_UnfreezeControls;
			Game1.freezeControls = false;
			if (Game1.options.SnappyMenus)
			{
				this.snapToDefaultClickableComponent();
			}
		}

		protected override void cleanupBeforeExit()
		{
			if (this.ItemToWrap is not null)
			{
				// Return items in item slot to player when closing
				Game1.createItemDebris(item: this.ItemToWrap, origin: Game1.player.Position, direction: -1);
			}
			base.cleanupBeforeExit();
		}

		public override void emergencyShutDown()
		{
			this.exitFunction();
			base.emergencyShutDown();
		}

		public override void receiveLeftClick(int x, int y, bool playSound = true)
		{
			if (this.ItemSlot.containsPoint(x, y) && this.ItemToWrap is not null)
			{
				if (this.inventory.tryToAddItem(toPlace: this.ItemToWrap, sound: playSound ? "coin" : null) is null)
				{
					// Take all from wrapping
					this.ItemToWrap = null;
				}
				else
				{
					// Inventory couldn't take all
					Game1.playSound("cancel");
				}
			}
			else if (this.WrapButton.containsPoint(x, y) && this.ShowWrapButton && this.ItemToWrap is not null)
			{
				Object wrappedGift = ModEntry.GetWrappedGift(modData: null);
				ModEntry.PackItem(ref wrappedGift, this.ItemToWrap, this.GiftWrapPosition, showMessage: true);
				if (wrappedGift != null)
				{
					// Convert wrapping to gift and close menu, giving the player the wrapped gift
					this.ItemToWrap = null;
					Game1.player.addItemToInventory(wrappedGift);
					Game1.playSound("discoverMineral");
					this.exitThisMenuNoSound();
				}
				else
				{
					// Wrapping couldn't be gifted
					Game1.playSound("cancel");
				}
			}
			else if (this.inventory.getInventoryPositionOfClick(x, y) is int index && index >= 0 && this.inventory.actualInventory[index] is not null)
			{
				if (this.ItemToWrap is not null && this.ItemToWrap.canStackWith(this.inventory.actualInventory[index]))
				{
					// Try add all to wrapping
					int maximumToSend = Math.Min(this.inventory.actualInventory[index].Stack, this.ItemToWrap.maximumStackSize() - this.ItemToWrap.Stack);
					this.ItemToWrap.Stack += maximumToSend;
					this.inventory.actualInventory[index].Stack -= maximumToSend;
					if (this.inventory.actualInventory[index].Stack < 1)
						this.inventory.actualInventory[index] = null;
					Game1.playSound("coin");
				}
				else
				{
					// Add all to wrapping
					(this.ItemToWrap, this.inventory.actualInventory[index]) = (this.inventory.actualInventory[index], this.ItemToWrap);
					Game1.playSound("coin");
				}
			}
			else
			{
				// Close menu
				if (this.upperRightCloseButton.containsPoint(x, y))
					this.exitThisMenu();
			}
		}
		
		public override void receiveRightClick(int x, int y, bool playSound = true)
		{
			if (this.ItemSlot.containsPoint(x, y) && this.ItemToWrap is not null)
			{
				if (this.inventory.tryToAddItem(toPlace: this.ItemToWrap.getOne()) is null)
				{
					// Take one from wrapping
					if (this.ItemToWrap.maximumStackSize() <= 1 || --this.ItemToWrap.Stack < 1)
						this.ItemToWrap = null;
				}
				else
				{
					// Inventory couldn't take one
					Game1.playSound("cancel");
				}
			}
			else if (this.inventory.getInventoryPositionOfClick(x, y) is int index && index >= 0 && this.inventory.actualInventory[index] is not null)
			{
				bool movedOne = false;
				if (this.ItemToWrap is not null)
				{
					// Add one to wrapping
					if (this.ItemToWrap.canStackWith(this.inventory.actualInventory[index]))
					{
						++this.ItemToWrap.Stack;
						movedOne = true;
					}
					// Take all of wrapping and add one to wrap
					else if (this.inventory.tryToAddItem(toPlace: this.ItemToWrap) is null)
					{
						this.ItemToWrap = this.inventory.actualInventory[index].getOne();
						movedOne = true;
					}
				}
				else
				{
					// Add one to wrapping
					this.ItemToWrap = this.inventory.actualInventory[index].getOne();
					movedOne = true;
				}

				if (movedOne)
				{
					// Take one from inventory
					if (this.inventory.actualInventory[index].maximumStackSize() <= 1 || --this.inventory.actualInventory[index].Stack < 1)
						this.inventory.actualInventory[index] = null;
					Game1.playSound("coin");
				}
				else
				{
					// None were moved
					Game1.playSound("cancel");
				}
			}
		}

		public override void performHoverAction(int x, int y)
		{
			this.hoverText = "";
			this.hoveredItem = null;
			if (this.ItemSlot.containsPoint(x, y) && this.ItemToWrap is not null) 
			{
				// Hover item slot
				this.hoveredItem = this.ItemToWrap;
			}
			else if (this.inventory.getInventoryPositionOfClick(x, y) is int index && index >= 0 && this.inventory.actualInventory[index] is Item item && item is not null)
			{
				// Hover inventory item
				this.hoveredItem = item;
			}
		}

		public override void receiveKeyPress(Keys key)
		{
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
					if (current < this.inventory.inventory.Count && current % inventoryWidth == 0)
					{
						// Inventory =|
						snapTo = current;
					}
				}
				else if (Game1.options.doesInputListContain(Game1.options.moveRightButton, key))
				{
					// Right
					if (current < this.inventory.inventory.Count && current % inventoryWidth == inventoryWidth - 1)
					{
						// Inventory =|
						snapTo = current;
					}
				}
				else if (Game1.options.doesInputListContain(Game1.options.moveUpButton, key))
				{
					// Up
					if (current == this.WrapButton.myID)
					{
						// WrapButton => ItemSlot
						snapTo = this.ItemSlot.myID;
					}
					else if (current >= 0 && current < this.inventory.inventory.Count)
					{
						if (current < inventoryWidth)
						{
							// Inventory => WrapButton/ItemSlot
							snapTo = this.ShowWrapButton ? this.WrapButton.myID : this.ItemSlot.myID;
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
					if (current == this.ItemSlot.myID)
					{
						// ItemSlot => WrapButton/Inventory
						snapTo = this.ShowWrapButton ? this.WrapButton.myID : 0;
					}
					else if (current == this.WrapButton.myID)
					{
						// WrapButton => Inventory
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
					snapTo = this.ShowWrapButton ? this.WrapButton.myID : this.ItemSlot.myID;
				else
					// ??? => Default
					snapTo = this._defaultClickable;
			}
			if (b is Buttons.RightShoulder)
			{
				// Right
				if (current == this.ItemSlot.myID)
					// ItemSlot => WrapButton/Inventory
					snapTo = this.ShowWrapButton ? this.WrapButton.myID : 0;
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
			// WrapButton animation loop
			this._animTimer += time.ElapsedGameTime.Milliseconds;
			if (this._animTimer >= AnimTimerLimit)
				this._animTimer = 0;
			this._animFrame = (int)((float)this._animTimer / AnimTimerLimit * AnimFrames);

			base.update(time);
		}

		public override void draw(SpriteBatch b)
		{
			// Blackout
			b.Draw(
				texture: Game1.fadeToBlackRect,
				destinationRectangle: Game1.graphics.GraphicsDevice.Viewport.GetTitleSafeArea(),
				sourceRectangle: null,
				color: Color.Black * 0.3f);

			// Inventory panel
			this.DrawInventory(b);

			// Background panel
			b.Draw(
				texture: this.Texture,
				destinationRectangle: this._backgroundArea,
				sourceRectangle: BackgroundSource,
				color: Color.White,
				rotation: 0,
				origin: Vector2.Zero,
				effects: SpriteEffects.None,
				layerDepth: 1);
			b.Draw(
				texture: this.Texture,
				destinationRectangle: this._decorationArea,
				sourceRectangle: DecorationSource,
				color: Color.White,
				rotation: 0,
				origin: Vector2.Zero,
				effects: SpriteEffects.None,
				layerDepth: 1);
			this.DrawPinkBorder(
				b: b,
				area: this._backgroundArea,
				drawBorderOutside: true,
				drawFillColour: false);

			// Info panel
			Rectangle textPanelArea = new(
				x: this._backgroundArea.X + this._backgroundArea.Width + this._borderScaled,
				y: this._backgroundArea.Y,
				width: this.inventory.width - this._backgroundArea.Width + (1 * Game1.pixelZoom),
				height: this._backgroundArea.Height);
			this.DrawPinkBorder(
				b: b,
				area: textPanelArea,
				drawBorderOutside: true,
				drawFillColour: true);

			// Body text
			Vector2 margin = new Vector2(2, 4) * Game1.pixelZoom;
			string text = ModEntry.I18n.Get("menu.infopanel.body");

			// Give a little extra leeway with non-English locales to fit them into the body text area
			text = Game1.parseText(
				text: text,
				whichFont: Game1.smallFont,
				width: textPanelArea.Width - (LocalizedContentManager.CurrentLanguageCode is LocalizedContentManager.LanguageCode.en ? (int)margin.X : 0));

			if (Game1.smallFont.MeasureString(text).Y > textPanelArea.Height)
			{
				// Remove the last line if body text overflows
				text = text[..text.LastIndexOf('.')];
			}
			Utility.drawTextWithShadow(
				b: b,
				text: text,
				font: Game1.smallFont,
				position: new Vector2(x: textPanelArea.X + (int)margin.X, y: textPanelArea.Y + (int)margin.Y),
				color: Game1.textColor);

			// Item clickables:
			// Item slot
			this.ItemSlot.draw(b);
			// Item inside the slot
			this.ItemToWrap?.drawInMenu(
				spriteBatch: b,
				location: new Vector2(x: this.ItemSlot.bounds.X, y: this.ItemSlot.bounds.Y),
				scaleSize: 1);
			if (this.ShowWrapButton)
			{
				// Wrap button
				b.Draw(
					texture: this.WrapButton.texture,
					destinationRectangle: this.WrapButton.bounds,
					sourceRectangle: new(
						x: this.WrapButton.sourceRect.X + (this._animFrame * this.WrapButton.sourceRect.Width),
						y: this.WrapButton.sourceRect.Y,
						width: this.WrapButton.sourceRect.Width,
						height: this.WrapButton.sourceRect.Height),
					color: Color.White,
					rotation: 0,
					origin: Vector2.Zero,
					effects: SpriteEffects.None,
					layerDepth: 1);
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

		/// <summary>
		/// Mostly a copy of InventoryMenu.draw(SpriteBatch b, int red, int blue, int green),
		/// though items considered unable to be cooked will be greyed out.
		/// </summary>
		private void DrawInventory(SpriteBatch b)
		{
			// Background card
			Vector4 margin = new Vector4(2, 4, 5, 4) * Game1.pixelZoom;
			Rectangle area = new(
				x: this.inventory.xPositionOnScreen - (int)margin.X,
				y: this.inventory.yPositionOnScreen - (int)margin.Y,
				width: this.inventory.width + (int)margin.Z,
				height: this.inventory.height + (int)margin.W);
			this.DrawPinkBorder(
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

			// Actual inventory
			for (int i = 0; i < this.inventory.capacity; ++i)
			{
				Vector2 position = new(
					x: this.inventory.xPositionOnScreen
						+ (this._inventoryExtraWidth / 2)
						+ (i % (this.inventory.capacity / this.inventory.rows) * 64)
						+ (this.inventory.horizontalGap * (i % (this.inventory.capacity / this.inventory.rows))),
					y: this.inventory.yPositionOnScreen
						+ (i / (this.inventory.capacity / this.inventory.rows) * (64 + this.inventory.verticalGap))
						+ (((i / (this.inventory.capacity / this.inventory.rows)) - 1) * 4)
						- (i >= this.inventory.capacity / this.inventory.rows
							|| !this.inventory.playerInventory || this.inventory.verticalGap != 0 ? 0 : 12));

				// Item slot frames
				b.Draw(
					texture: Game1.menuTexture,
					position,
					sourceRectangle: Game1.getSourceRectForStandardTileSheet(tileSheet: Game1.menuTexture, tilePosition: 10),
					color: Color.PeachPuff,
					rotation: 0,
					origin: Vector2.Zero,
					scale: 1,
					effects: SpriteEffects.None,
					layerDepth: 0.5f);
				b.Draw(
					texture: Game1.menuTexture,
					position,
					sourceRectangle: Game1.getSourceRectForStandardTileSheet(tileSheet: Game1.menuTexture, tilePosition: 10),
					color: Color.Orchid * 0.75f,
					rotation: 0,
					origin: Vector2.Zero,
					scale: 1,
					effects: SpriteEffects.None,
					layerDepth: 0.5f);

				// Greyed-out item slots
				if ((this.inventory.playerInventory || this.inventory.showGrayedOutSlots) && i >= Game1.player.maxItems.Value)
				{
					b.Draw(
						texture: Game1.menuTexture,
						position,
						sourceRectangle: Game1.getSourceRectForStandardTileSheet(tileSheet: Game1.menuTexture, tilePosition: 57),
						color: Color.White * 0.5f,
						rotation: 0,
						origin: Vector2.Zero,
						scale: 1,
						effects: SpriteEffects.None,
						layerDepth: 0.5f);
				}

				if (i >= 12 || !this.inventory.playerInventory)
					continue;
				string text = i switch
				{
					9 => "0",
					10 => "-",
					11 => "=",
					_ => string.Concat(i + 1),
				};
				Vector2 textOffset = Game1.tinyFont.MeasureString(text);
				b.DrawString(
					spriteFont: Game1.tinyFont,
					text: text,
					position: position + new Vector2(32f - (textOffset.X / 2f), -textOffset.Y),
					color: i == Game1.player.CurrentToolIndex ? Color.Red : Color.DimGray);
			}
			for (int i = 0; i < this.inventory.capacity; ++i)
			{
				// Items
				if (this.inventory.actualInventory.Count <= i || this.inventory.actualInventory.ElementAt(i) is null)
					continue;

				Vector2 location = new(
					x: this.inventory.xPositionOnScreen
					 + (i % (this.inventory.capacity / this.inventory.rows) * 64)
					 + (this.inventory.horizontalGap * (i % (this.inventory.capacity / this.inventory.rows))),
					y: this.inventory.yPositionOnScreen
						+ (i / (this.inventory.capacity / this.inventory.rows) * (64 + this.inventory.verticalGap))
						+ (((i / (this.inventory.capacity / this.inventory.rows)) - 1) * 4)
						- (i >= this.inventory.capacity / this.inventory.rows
						   || !this.inventory.playerInventory || this.inventory.verticalGap != 0 ? 0 : 12));

				bool drawShadow = this.inventory.highlightMethod(this.inventory.actualInventory[i]);
				if (iconShakeTimer.ContainsKey(i))
					location += 1 * new Vector2(Game1.random.Next(-1, 2), Game1.random.Next(-1, 2));
				this.inventory.actualInventory[i].drawInMenu(b,
					location: location,
					scaleSize: this.inventory.inventory.Count > i ? this.inventory.inventory[i].scale : 1,
					transparency: !this.inventory.highlightMethod(this.inventory.actualInventory[i]) ? 0.25f : 1,
					layerDepth: 0.865f,
					drawStackNumber: StackDrawType.Draw,
					color: Color.White,
					drawShadow: drawShadow);
			}
		}

		private void DrawPinkBorder(SpriteBatch b, Rectangle area, bool drawBorderOutside, bool drawFillColour)
		{
			Point point = new(x: 100, y: 80);
			Point cornerSize = new(x: 5, y: 5);

			Color colour = Color.White;
			float rotation = 0;
			Vector2 origin = Vector2.Zero;
			float scale = Game1.pixelZoom;
			SpriteEffects effects = SpriteEffects.None;
			float layerDepth = 1;

			Rectangle source;
			Rectangle scaled;

			if (drawBorderOutside)
			{
				area.X -= this._borderScaled;
				area.Y -= this._borderScaled;
				area.Width += this._borderScaled * 2;
				area.Height += this._borderScaled * 2;
			}

			if (drawFillColour)
			{
				// Fill colour
				Rectangle fillArea = new(area.X + this._borderScaled, area.Y + this._borderScaled, area.Width - (this._borderScaled * 2), area.Height - (this._borderScaled * 2));
				source = new(x: 380, y: 437, width: 1, height: 8); // Sample the date field background from the HUD clock in cursors
				b.Draw(
					texture: Game1.mouseCursors,
					destinationRectangle: fillArea,
					sourceRectangle: source,
					color: Color.White,
					rotation: rotation,
					origin: origin,
					effects: effects,
					layerDepth: layerDepth);
				b.Draw(
					texture: Game1.mouseCursors,
					destinationRectangle: fillArea,
					sourceRectangle: source,
					color: Color.Plum * 0.65f,
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
						texture: this.Texture,
						destinationRectangle: target,
						sourceRectangle: source,
						color: colour,
						rotation: rotation,
						origin: origin,
						effects: effects,
						layerDepth: layerDepth);
				}

				// Top
				source = new Rectangle(point.X + borderWidth + 1, point.Y, 1, borderWidth + 1);
				scaled = new Rectangle(0, 0, source.Width * Game1.pixelZoom, source.Height * Game1.pixelZoom);
				target = new Rectangle(area.X + (cornerSize.Y * Game1.pixelZoom), area.Y, area.Width - (cornerSize.X * Game1.pixelZoom * 2), scaled.Height);
				draw(target: target, source: source);
				// Bottom
				source = new Rectangle(point.X + borderWidth + 1, point.Y, 1, borderWidth);
				scaled = new Rectangle(0, 0, source.Width * Game1.pixelZoom, source.Height * Game1.pixelZoom);
				target = new Rectangle(area.X + (cornerSize.Y * Game1.pixelZoom), area.Y + area.Height - scaled.Height, area.Width - (cornerSize.X * Game1.pixelZoom * 2), scaled.Height);
				draw(target: target, source: source);
				// Left
				source = new Rectangle(point.X, point.Y + borderWidth, borderWidth, 1);
				scaled = new Rectangle(0, 0, source.Width * Game1.pixelZoom, source.Height * Game1.pixelZoom);
				target = new Rectangle(area.X, area.Y + (cornerSize.Y * Game1.pixelZoom), scaled.Width, area.Height - (cornerSize.Y * Game1.pixelZoom * 2));
				draw(target: target, source: source);
				// Right
				source = new Rectangle(point.X + source.Width + 1, point.Y + borderWidth, borderWidth + 1, 1);
				scaled = new Rectangle(0, 0, source.Width * Game1.pixelZoom, source.Height * Game1.pixelZoom);
				target = new Rectangle(area.X + area.Width - scaled.Width, area.Y + (cornerSize.Y * Game1.pixelZoom), scaled.Width, area.Height - (cornerSize.Y * Game1.pixelZoom * 2));
				draw(target: target, source: source);
			}

			// Corners:
			{
				Vector2 target;
				Rectangle corner;
				void draw(Vector2 target, Rectangle source)
				{
					b.Draw(
						texture: this.Texture,
						position: target,
						sourceRectangle: source,
						color: colour,
						rotation: rotation,
						origin: origin,
						scale: scale,
						effects: effects,
						layerDepth: layerDepth);
				}

				source = new Rectangle(point.X, point.Y, cornerSize.X, cornerSize.Y);
				scaled = new Rectangle(0, 0, source.Width * Game1.pixelZoom, source.Height * Game1.pixelZoom);

				// Top-left
				target = new Vector2(area.X, area.Y);
				corner = source;
				draw(target: target, source: corner);
				// Bottom-left
				target = new Vector2(area.X, area.Y + area.Height - scaled.Height);
				corner = new Rectangle(source.X, source.Y + source.Height, source.Width, source.Height);
				draw(target: target, source: corner);
				// Top-right
				target = new Vector2(area.X + area.Width - scaled.Width, area.Y);
				corner = new Rectangle(source.X + source.Width, source.Y, source.Width, source.Height);
				draw(target: target, source: corner);
				// Bottom-right
				target = new Vector2(area.X + area.Width - scaled.Width, area.Y + area.Height - scaled.Height);
				corner = new Rectangle(source.X + source.Width, source.Y + source.Height, source.Width, source.Height);
				draw(target: target, source: corner);
			}
		}
	}
}
