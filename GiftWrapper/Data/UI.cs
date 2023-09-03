﻿
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Colour = Microsoft.Xna.Framework.Color;

namespace GiftWrapper.Data
{
	public class UI
	{
		/// <summary>
		/// Scale of UI components.
		/// </summary>
		public int Scale;

		// Cues

		/// <summary>
		/// Blank sound cue.
		/// </summary>
		public string NoSound;
		/// <summary>
		/// Sound cue played on menu opened.
		/// </summary>
		public string OpenSound;
		/// <summary>
		/// Sound cue played on gift wrapped.
		/// </summary>
		public string SuccessSound;
		/// <summary>
		/// Sound cue played on item conflicts.
		/// </summary>
		public string FailureSound;
		/// <summary>
		/// Sound cue played on item interactions.
		/// </summary>
		public string ItemSound;
		/// <summary>
		/// Mapping of item names to sound cues for item interactions.
		/// </summary>
		public Dictionary<string, string> ItemSounds;
		/// <summary>
		/// Mapping of sound cues to item categories for item interactions.
		/// </summary>
		public Dictionary<string, List<int>> CategorySounds;
		/// <summary>
		/// List of item categories with shake effects played on item interactions.
		/// </summary>
		public List<int> ShakeCategories;

		// Strings

		/// <summary>
		/// Path to translated string dictionary asset for text override.
		/// </summary>
		public string InfoTextPath;
		/// <summary>
		/// Key in translated string dictionary asset for text override.
		/// </summary>
		public string InfoTextKey;

		// Colours

		/// <summary>
		/// Colour set on items in inventory not allowed to be wrapped as gifts.
		/// </summary>
		public Colour InventoryInvalidGiftColour;

		// Animations

		/// <summary>
		/// Duration in milliseconds of shake animation on item slot.
		/// </summary>
		public int ShortShakeDuration;
		/// <summary>
		/// Duration in milliseconds of shake animation on item slot.
		/// </summary>
		public int LongShakeDuration;
		/// <summary>
		/// Time between animation frames on gift wrap button.
		/// </summary>
		public int WrapButtonFrameTime;
		/// <summary>
		/// Number of frames in gift wrap button animation.
		/// </summary>
		public int WrapButtonFrames;

		// Regions

		/// <summary>
		/// Region of texture asset used when drawing component sprite.
		/// </summary>
		public Rectangle SceneBackgroundSource;
		/// <summary>
		/// Region of texture asset used when drawing component sprite.
		/// </summary>
		public Rectangle InfoBackgroundSource;
		/// <summary>
		/// Region of texture asset used when drawing component sprite.
		/// </summary>
		public Rectangle InventorySlotSource;
		/// <summary>
		/// Region of texture asset used when drawing component sprite.
		/// </summary>
		public Rectangle InventoryLockedSlotSource;
		/// <summary>
		/// Region of texture asset used when drawing component sprite.
		/// </summary>
		public Rectangle ItemSlotSource;
		/// <summary>
		/// Region of texture asset used when drawing component sprite.
		/// </summary>
		public Rectangle WrapButtonSource;
		/// <summary>
		/// List of data models for UI sprite components.
		/// </summary>
		public List<Decoration> Decorations;

		// Offsets

		/// <summary>
		/// Offset from position when drawing sprite.
		/// </summary>
		public Point CardOffset;

		// Assets

		/// <summary>
		/// Asset key used to load texture from game content.
		/// </summary>
		public string MenuSpriteSheetPath;
		/// <summary>
		/// Asset key used to load texture from game content.
		/// </summary>
		public string CardSpriteSheetPath;
		/// <summary>
		/// Asset key used to load texture from game content.
		/// </summary>
		public string WrapButtonSpriteSheetPath;

		// Dimensions

		/// <summary>
		/// Coordinates in unscaled pixels of border sprites in source texture.
		/// </summary>
		public Point BorderSourceAt;
		/// <summary>
		/// Dimensions in unscaled pixels of border sprites,
		/// where sides and corners share the same axial dimension,
		/// and sides are assumed to be a 1px area between corners.
		/// </summary>
		public Point BorderSize;
		/// <summary>
		/// Dimensions in unscaled pixels of draw offsets for border sprites,
		/// where positive values will draw borders closer to the area centre,
		/// and negative values will draw further out.
		/// Adding an offset allows for drop shadows embedded in the sprite to
		/// be drawn over menu backgrounds while neither enlarging the background,
		/// nor having an empty space between the border and the background.
		/// </summary>
		public Point BorderOffset;
		/// <summary>
		/// Margins in unscaled pixels for info text area.
		/// </summary>
		public Point InfoTextMargin;
		/// <summary>
		/// Margins in unscaled pixels for inventory area.
		/// </summary>
		public Point InventoryMargin;

		// Sizes

		/// <summary>
		/// Width of area in menu dedicated to info text.
		/// </summary>
		public int InfoBackgroundWidth;
	}
}
