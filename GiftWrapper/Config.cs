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
		public bool InteractUsingToolButton { get; set; } = false;
		public bool AlwaysAvailable { get; set; } = false;
	}
}
