using System.Collections.Generic;

namespace GiftWrapper.Data
{
	/// <summary>
	/// Model for mod feature data.
	/// </summary>
	public class Data
	{
		/// <summary>
		/// Map of audio cue IDs to asset paths.
		/// </summary>
		public Dictionary<string, string[]> Audio;
		/// <summary>
		/// Model for UI data, used for theme definitions.
		/// </summary>
		public UI UI;
	}
}
