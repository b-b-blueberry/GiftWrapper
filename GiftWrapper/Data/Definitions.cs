using Microsoft.Xna.Framework;
using Colour = Microsoft.Xna.Framework.Color;

namespace GiftWrapper.Data
{
	public record Definitions
	{
		public float AddedFriendship;
		public int GiftValue;
		public int WrapValue;
		public int[] HitCount;
		public int HitShake;
		public string HitSound;
		public string LastHitSound;
		public string OpenSound;
		public string RemoveSound;
		public string InvalidGiftStringPath;
		public string InvalidGiftSound;
		public int EventConditionId;
		public int CategoryNumber;
		public Colour CategoryTextColour;
		public Colour SecretTextColour;
		public Colour? DefaultTextColour;
		public string WrapItemTexture;
		public Rectangle WrapItemSource;
	}
}
