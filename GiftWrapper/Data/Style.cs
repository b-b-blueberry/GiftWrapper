﻿using Microsoft.Xna.Framework;

namespace GiftWrapper.Data
{
	public record Style
	{
		/// <summary>
		/// Asset key used to load texture from game content.
		/// </summary>
		public string Texture;
		/// <summary>
		/// Region of texture asset used when drawing object sprite.
		/// </summary>
		public Rectangle Area;
		/// <summary>
		/// Sound cue played on item hit, including last hit.
		/// </summary>
		public string HitSound;
		/// <summary>
		/// Sound cue played on item last hit, upon being removed.
		/// </summary>
		public string LastHitSound;
		/// <summary>
		/// Sound cue played on item removed, upon gift claimed.
		/// </summary>
		public string OpenSound;
		/// <summary>
		/// Sound cue played on item picked up, without being hit.
		/// </summary>
		public string PickUpSound;
	}
}