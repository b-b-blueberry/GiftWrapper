namespace GiftWrapper
{
	public interface IJsonAssetsAPI
	{
		void LoadAssets(string path);
		int GetObjectId(string name);
	}
}
