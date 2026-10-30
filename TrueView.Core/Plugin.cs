using BepInEx;
using TrueView.Configuration;
using TrueView.Patches;
using TrueView.Systems;
using TrueView.Utilities;
using UnityEngine;

namespace TrueView.Core;

[BepInPlugin("com.trueview", "TrueView", "2.1.1")]
public class Plugin : BaseUnityPlugin
{
	public const string GUID = "com.trueview";

	public const string Name = "TrueView";

	public const string Version = "2.1.1";

	private void Awake()
	{
		Log.Init(base.Logger);
		HandlingConfig.Init(base.Config);
		PatchRegistry.EnableAll();
		Log.Info("TrueView 2.0.0 loaded");
	}
}
