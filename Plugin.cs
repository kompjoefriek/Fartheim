using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace Fartheim
{
	/*
	 * Todo:
	 * 
	 * what about farts catching/spreading fire?
	 * 
	 */

	[BepInPlugin("kompjoefriek.fartheim", Plugin.ModName, Plugin.Version)]
	public class Plugin : BaseUnityPlugin
	{
		public const string Version = "1.3.0";
		public const string ModName = "Fartheim";
		Harmony _harmony;
		public static ManualLogSource Log;

		AssetBundle _assetBundle;
		public static List<AudioClip> DryFarts = new List<AudioClip>();
		public static List<AudioClip> WetFarts = new List<AudioClip>();
		public static GameObject FartSystemPrefab;

		public static ConfigEntry<float> MaxFartTime;
		public static ConfigEntry<float> FartNoiseRange;
		public static ConfigEntry<bool> BunsOfSteel;
		public static ConfigEntry<bool> ShouldBirdsFart;
		public static ConfigEntry<bool> OnlyPlayers;

		private void Awake()
		{
#if DEBUG
			Log = Logger;
#else
			Log = new ManualLogSource(null);
#endif // DEBUG

			_assetBundle = AssetHelper.LoadAssetBundle("resources.fartheim");
			var farts = _assetBundle.LoadAllAssets<AudioClip>();
			foreach (var f in farts)
			{
				if (f.name.Contains("water")) WetFarts.Add(f);
				else DryFarts.Add(f);
			}
			if (DryFarts.Count > 0) DryFarts = DryFarts.OrderBy(f => f.length).ToList();
			if (WetFarts.Count > 0) WetFarts = WetFarts.OrderBy(f => f.length).ToList();

			FartSystemPrefab = _assetBundle.LoadAsset<GameObject>("Assets\\FartSystem.prefab");
			AssetHelper.RegisterPrefab(FartSystemPrefab);

			MaxFartTime = Config.Bind("Farts", "Maximum Fart Time", 30f);
			FartNoiseRange = Config.Bind("Farts", "Fart Noise Range", 32f);
			BunsOfSteel = Config.Bind("Farts", "Players Only Fart When Crouching", true);
			ShouldBirdsFart = Config.Bind("Farts", "Should Birds Fart", true);
			OnlyPlayers = Config.Bind("Farts", "Only Players Fart (unrealistic)", false);

			_harmony = Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly(), null);
		}

		private void OnDestroy()
		{
			_harmony?.UnpatchSelf();
		}
	}
}
