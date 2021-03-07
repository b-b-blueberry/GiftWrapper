using StardewModdingAPI;
using System;

namespace GiftWrapper
{
	public interface IGenericModConfigMenuAPI
	{
		void RegisterModConfig(IManifest mod, Action revertToDefault, Action saveToFile);
		void RegisterSimpleOption(IManifest mod, string optionName, string optionDesc, Func<bool> optionGet, Action<bool> optionSet);
		void RegisterLabel(IManifest mod, string labelName, string labelDesc);
	}
}
