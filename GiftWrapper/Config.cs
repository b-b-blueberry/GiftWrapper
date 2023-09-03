namespace GiftWrapper
{
	public class Config
	{
		public enum Themes
		{
			Red = 1,
			Blue = 2,
			Green = 3
		}

		public Themes Theme { get; set; } = (Themes)(-1);
		public bool AlwaysAvailable { get; set; } = false;
		public bool GiftPreviewEnabled { get; set; } = true;
		public int GiftPreviewTileRange { get; set; } = 5;
		public int GiftPreviewFadeSpeed { get; set; } = 10;
	}
}
